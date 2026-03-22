using UnityEngine;
using System.Collections.Generic;
using TMPro;
using Blob3D.Core;
using Blob3D.Gameplay;
using Blob3D.Player;

namespace Blob3D.AI
{
    /// <summary>
    /// AI-controlled blob with organic, lively behavior.
    /// Uses Perlin noise wandering, Boids-like flocking, predictive chasing,
    /// panic flee responses, and smooth state transitions.
    /// Features: chase prediction, group hunting, self-preservation, territorial
    /// behavior, reaction delay variation, size-awareness, obstacle avoidance,
    /// confusion state, and boss multi-phase AI.
    /// Types: Wanderer / Hunter / Coward / Boss
    /// </summary>
    public class AIBlobController : BlobBase
    {
        // ---------- AI type & state enums ----------
        public enum AIType { Wanderer, Hunter, Coward, Boss }
        private enum AIState { Idle, Wander, Chase, Flee, Eat, Confused }

        // ---------- Boss phase enum ----------
        private enum BossPhase { Patrol, Aggressive, Berserk }

        [Header("AI Settings")]
        [SerializeField] private AIType aiType = AIType.Wanderer;
        [SerializeField] private float detectionRange = 40f;
        [SerializeField] private float stateUpdateInterval = 0.3f;
        // Wander direction changes are driven by Perlin noise, not timer

        // Fix: store base detection range to prevent multiplicative stacking on pool reuse
        private float baseDetectionRange;

        // ---------- State machine ----------
        private AIState currentState = AIState.Idle;
        private AIState previousState = AIState.Idle;
        private BlobBase chaseTarget;
        private BlobBase fleeTarget;
        private float stateTimer;

        // ---------- Smooth steering ----------
        private Vector3 currentDirection = Vector3.forward;
        private Vector3 desiredDirection = Vector3.forward;
        private const float SteerSmoothing = 3.5f;

        // ---------- Perlin noise wander ----------
        private float perlinOffsetX;
        private float perlinOffsetZ;
        private float perlinSpeed = 0.4f;
        private float perlinTime;

        // ---------- Flocking (Wanderers) ----------
        private const float FlockNeighborRange = 18f;
        private const float FlockSeparationRange = 5f;
        private const float FlockCohesionWeight = 0.3f;
        private const float FlockSeparationWeight = 0.8f;
        private const float FlockAlignmentWeight = 0.15f;

        // ---------- Chase prediction ----------
        private Vector3 lastPreyPosition;
        private Vector3 preyVelocityEstimate;

        // ---------- Fear / panic ----------
        private float panicSpeedBoost;
        private const float PanicInitialBoost = 1.8f;
        private const float PanicDecayRate = 1.2f;
        private float panicJitterPhase;

        // ---------- Boss patrol ----------
        private float bossPatrolAngle;
        private float bossPatrolRadius = 30f;
        private float bossPatrolSpeed = 0.15f;

        // ---------- Boss multi-phase ----------
        private BossPhase bossPhase = BossPhase.Patrol;
        private float bossInitialSize;
        private const float BossAggressivePlayerRange = 50f;
        private const float BossBerserkSizeRatio = 0.5f; // Berserk when size drops below 50% of initial

        // ---------- Reaction delay variation ----------
        private float reactionDelay;
        private float reactionTimer;
        private bool reactionReady = true;

        // ---------- Confusion state ----------
        private float confusionTimer;
        private const float ConfusionDurationMin = 0.5f;
        private const float ConfusionDurationMax = 1.5f;
        private Vector3 confusionWanderDir;

        // ---------- Territorial behavior ----------
        private Vector3 territoryCenter;
        private float territoryRadius;
        private bool hasTerritoryAssigned;

        // ---------- Self-preservation ----------
        private const float SelfPreservationCheckRange = 25f;
        private const float AbsorptionWitnessRange = 30f;
        private float witnessedAbsorptionFleeTimer;

        // ---------- Group hunting ----------
        private const float GroupHuntRange = 30f;
        private const int GroupHuntMinHunters = 2;

        // ---------- Size-awareness ----------
        private const float SmallBlobSizeRatio = 0.6f; // Considered "small" relative to threat
        private const float SizeAwarenessMultiplier = 1.5f; // Flee range multiplier for small blobs

        // ---------- Obstacle avoidance ----------
        private const float ObstacleDetectRange = 8f;
        private const float ObstacleAvoidWeight = 1.5f;
        private int obstacleLayerMask = -1;

        // ---------- Name label ----------
        private static readonly string[] NameList = {
            "Slimo", "Gooey", "Blobby", "Squish", "Jello", "Puddi", "Wobble", "Glorp",
            "Mochi", "Neru", "Puni", "Puyo", "Chomp", "Gulp", "Plop", "Bloop"
        };
        private Transform nameLabelTransform;
        private TextMeshProUGUI nameLabelText;

        // ---------- Breathing / idle pulse ----------
        private float breathPhase;
        private float breathSpeed;
        private float breathAmplitude;

        // ---------- Speed variation ----------
        private float speedMultiplier = 1f;

        // ---------- State transition hesitation ----------
        private float transitionPauseTimer;
        private const float TransitionPauseDuration = 0.2f;

        // ---------- Tracking prey dodge for confusion ----------
        private Vector3 lastChaseTargetPos;
        private float chaseDurationWithoutClosing;
        private const float DodgeDetectionTime = 1.5f;

        public AIType Type => aiType;

        // ---------- Initialization ----------

        /// <summary>Setup AI (called from Spawner or Manager)</summary>
        public void Initialize(AIType type, float size)
        {
            aiType = type;
            SetSize(size);

            // Fix: reset state machine for pool reuse
            currentState = AIState.Idle;
            previousState = AIState.Idle;
            chaseTarget = null;
            fleeTarget = null;
            panicSpeedBoost = 0f;
            transitionPauseTimer = 0f;
            confusionTimer = 0f;
            witnessedAbsorptionFleeTimer = 0f;
            chaseDurationWithoutClosing = 0f;
            reactionReady = true;
            reactionTimer = 0f;

            // Fix: use base detection range to prevent multiplicative stacking on pool reuse
            if (baseDetectionRange <= 0f) baseDetectionRange = detectionRange;
            detectionRange = baseDetectionRange;

            // Boss has wider detection range and stores initial size for phase tracking
            if (type == AIType.Boss)
            {
                detectionRange *= 1.5f;
                bossInitialSize = size;
                bossPhase = BossPhase.Patrol;
            }

            // Per-instance randomization for variety
            speedMultiplier = Random.Range(0.85f, 1.15f);
            perlinOffsetX = Random.Range(0f, 1000f);
            perlinOffsetZ = Random.Range(0f, 1000f);
            perlinSpeed = Random.Range(0.3f, 0.6f);
            breathPhase = Random.Range(0f, Mathf.PI * 2f);
            breathSpeed = Random.Range(1.5f, 2.5f);
            breathAmplitude = Random.Range(0.02f, 0.05f);
            bossPatrolAngle = Random.Range(0f, Mathf.PI * 2f);
            bossPatrolRadius = Random.Range(25f, 45f);

            // Reaction delay varies by AI type for personality
            AssignReactionDelay();

            // Assign territorial center for Hunters (territory = spawn location)
            AssignTerritory();

            // Random initial facing direction
            float angle = Random.Range(0f, Mathf.PI * 2f);
            currentDirection = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
            desiredDirection = currentDirection;

            CreateNameLabel();
        }

        /// <summary>
        /// Assign reaction delay based on AI type.
        /// Hunters react fast, Wanderers average, Cowards very fast (alert),
        /// Boss is deliberately slow but relentless.
        /// </summary>
        private void AssignReactionDelay()
        {
            switch (aiType)
            {
                case AIType.Hunter:
                    reactionDelay = Random.Range(0.05f, 0.15f);
                    break;
                case AIType.Coward:
                    reactionDelay = Random.Range(0.02f, 0.08f);
                    break;
                case AIType.Boss:
                    reactionDelay = Random.Range(0.2f, 0.4f);
                    break;
                default: // Wanderer
                    reactionDelay = Random.Range(0.1f, 0.3f);
                    break;
            }
        }

        /// <summary>
        /// Assign territory for AI types that exhibit territorial behavior.
        /// Hunters defend their spawn area; Boss defends center.
        /// </summary>
        private void AssignTerritory()
        {
            hasTerritoryAssigned = false;

            if (aiType == AIType.Hunter)
            {
                territoryCenter = transform.position;
                territoryRadius = Random.Range(20f, 35f);
                hasTerritoryAssigned = true;
            }
            else if (aiType == AIType.Boss)
            {
                territoryCenter = Vector3.zero; // Boss patrols center
                territoryRadius = bossPatrolRadius * 1.5f;
                hasTerritoryAssigned = true;
            }
        }

        /// <summary>Create a WorldSpace Canvas with a name label above the AI blob.</summary>
        private void CreateNameLabel()
        {
            // Fix: if name label already exists (pool reuse), just update the name text
            if (nameLabelTransform != null)
            {
                if (nameLabelText != null)
                    nameLabelText.text = NameList[Random.Range(0, NameList.Length)];
                return;
            }

            // Create canvas GameObject as child
            GameObject canvasObj = new GameObject("NameLabelCanvas");
            canvasObj.transform.SetParent(transform, false);

            Canvas labelCanvas = canvasObj.AddComponent<Canvas>();
            labelCanvas.renderMode = RenderMode.WorldSpace;
            labelCanvas.sortingOrder = 10;

            // Scale canvas to world-appropriate size
            RectTransform canvasRect = canvasObj.GetComponent<RectTransform>();
            canvasRect.sizeDelta = new Vector2(200f, 50f);
            canvasRect.localScale = Vector3.one * 0.01f;

            // Add TextMeshProUGUI
            GameObject textObj = new GameObject("NameText");
            textObj.transform.SetParent(canvasObj.transform, false);

            nameLabelText = textObj.AddComponent<TextMeshProUGUI>();
            nameLabelText.text = NameList[Random.Range(0, NameList.Length)];
            nameLabelText.fontSize = 36;
            nameLabelText.alignment = TextAlignmentOptions.Center;
            nameLabelText.color = Color.white;
            nameLabelText.fontStyle = FontStyles.Bold;
            nameLabelText.raycastTarget = false;

            RectTransform textRect = textObj.GetComponent<RectTransform>();
            textRect.sizeDelta = new Vector2(200f, 50f);
            textRect.anchoredPosition = Vector2.zero;

            nameLabelTransform = canvasObj.transform;
            UpdateNameLabelPosition();
        }

        /// <summary>Position label above blob and scale inversely with blob size for readability.</summary>
        private void UpdateNameLabelPosition()
        {
            if (nameLabelTransform == null) return;

            float blobRadius = CurrentSize * 0.15f * 0.5f;
            nameLabelTransform.localPosition = new Vector3(0f, blobRadius + 0.8f, 0f);

            // Scale inversely with blob size so text stays readable
            float labelScale = 0.01f * Mathf.Max(1f, CurrentSize * 0.3f);
            nameLabelTransform.localScale = Vector3.one * labelScale;

            // Billboard: always face main camera
            Camera mainCam = Camera.main;
            if (mainCam != null)
            {
                nameLabelTransform.rotation = Quaternion.LookRotation(
                    nameLabelTransform.position - mainCam.transform.position);
            }
        }

        /// <summary>
        /// Update name label color and font size based on threat level relative to the player.
        /// Red = dangerous (bigger), Yellow = similar size, Green = prey (smaller), White = no player.
        /// </summary>
        private void UpdateNameLabelColor()
        {
            if (nameLabelText == null) return;

            if (BlobController.Instance == null || !BlobController.Instance.IsAlive)
            {
                nameLabelText.color = Color.white;
                nameLabelText.fontSize = 36;
                return;
            }

            float playerSize = BlobController.Instance.CurrentSize;
            float ratio = CurrentSize / playerSize;

            // Color by threat level
            if (ratio > 1.1f)
                nameLabelText.color = new Color(1f, 0.3f, 0.3f); // Red - danger
            else if (ratio > 0.9f)
                nameLabelText.color = new Color(1f, 1f, 0.3f); // Yellow - similar
            else
                nameLabelText.color = new Color(0.3f, 1f, 0.4f); // Green - prey

            // Scale font size by relative size for visual hierarchy
            float fontSize = Mathf.Clamp(36f * ratio, 24f, 56f);
            nameLabelText.fontSize = fontSize;
        }

        protected override void Start()
        {
            base.Start();
            // Stagger state evaluation so not all AI update on the same frame
            stateTimer = Random.Range(0f, stateUpdateInterval);

            // Cache obstacle layer mask (everything except Player/AI blobs layer)
            // Use default physics layers: obstacles are on Default layer
            obstacleLayerMask = LayerMask.GetMask("Default");
            if (obstacleLayerMask == 0) obstacleLayerMask = 1; // Fallback to layer 0
        }

        // ---------- Update loop ----------

        private void Update()
        {
            if (!IsAlive || GameManager.Instance == null ||
                GameManager.Instance.CurrentState != GameManager.GameState.Playing)
                return;

            // Update reaction delay timer
            if (!reactionReady)
            {
                reactionTimer -= Time.deltaTime;
                if (reactionTimer <= 0f)
                {
                    reactionReady = true;
                }
            }

            // Evaluate state at intervals (performance optimization)
            stateTimer -= Time.deltaTime;
            if (stateTimer <= 0f)
            {
                stateTimer = stateUpdateInterval;
                if (reactionReady)
                {
                    EvaluateState();
                }
            }

            // Advance Perlin time for wander noise
            perlinTime += Time.deltaTime * perlinSpeed;

            // Decay panic speed boost
            if (panicSpeedBoost > 0f)
            {
                panicSpeedBoost -= PanicDecayRate * Time.deltaTime;
                if (panicSpeedBoost < 0f) panicSpeedBoost = 0f;
            }

            // Decay confusion timer
            if (confusionTimer > 0f)
            {
                confusionTimer -= Time.deltaTime;
            }

            // Decay witnessed absorption flee timer
            if (witnessedAbsorptionFleeTimer > 0f)
            {
                witnessedAbsorptionFleeTimer -= Time.deltaTime;
            }

            // Breathing idle pulse
            UpdateBreathingPulse();

            // Keep name label positioned above blob
            UpdateNameLabelPosition();
        }

        private void FixedUpdate()
        {
            if (!IsAlive || GameManager.Instance == null ||
                GameManager.Instance.CurrentState != GameManager.GameState.Playing)
                return;

            // Brief hesitation during state transitions
            if (transitionPauseTimer > 0f)
            {
                transitionPauseTimer -= Time.fixedDeltaTime;
                rb.velocity = Vector3.Lerp(rb.velocity, Vector3.zero, Time.fixedDeltaTime * 5f);
                ApplySquashAndStretch(rb.velocity);
                transform.position = GameManager.Instance.ClampToField(transform.position);
                ClampYPosition();
                return;
            }

            // Smooth steering: interpolate current direction toward desired
            currentDirection = Vector3.Slerp(
                currentDirection,
                desiredDirection,
                Time.fixedDeltaTime * SteerSmoothing
            ).normalized;

            ExecuteState();
            ApplySquashAndStretch(rb.velocity);

            // Clamp to field boundary
            transform.position = GameManager.Instance.ClampToField(transform.position);

            ClampYPosition();
        }

        // ---------- Breathing pulse (idle animation) ----------

        private void UpdateBreathingPulse()
        {
            breathPhase += Time.deltaTime * breathSpeed;

            // Breathing is disabled: ApplySquashAndStretch in FixedUpdate handles
            // all localScale changes (including idle wobble via the spring-damper).
            // Setting localScale here would fight with jiggle physics every frame.
        }

        // ---------- State machine evaluation ----------

        private void EvaluateState()
        {
            // If currently confused, stay confused until timer expires
            if (currentState == AIState.Confused && confusionTimer > 0f)
            {
                return;
            }

            AIState newState;

            switch (aiType)
            {
                case AIType.Wanderer:
                    newState = EvaluateWanderer();
                    break;
                case AIType.Hunter:
                    newState = EvaluateHunter();
                    break;
                case AIType.Coward:
                    newState = EvaluateCoward();
                    break;
                case AIType.Boss:
                    newState = EvaluateBoss();
                    break;
                default:
                    newState = AIState.Wander;
                    break;
            }

            // Update name label threat color
            UpdateNameLabelColor();

            // State transition hesitation
            if (newState != currentState)
            {
                previousState = currentState;
                currentState = newState;

                // Start reaction delay for non-urgent transitions
                if (newState != AIState.Flee && newState != AIState.Confused)
                {
                    reactionReady = false;
                    reactionTimer = reactionDelay;
                }

                // Brief pause when switching states for natural feel
                // Skip pause for urgent flee transitions and confusion
                if (newState != AIState.Flee && newState != AIState.Confused)
                {
                    transitionPauseTimer = TransitionPauseDuration;
                }
            }
        }

        /// <summary>Wanderer: wander with flocking, flee from threats, opportunistically eat smaller blobs</summary>
        private AIState EvaluateWanderer()
        {
            // Self-preservation: flee if nearby blob was just absorbed
            if (witnessedAbsorptionFleeTimer > 0f)
            {
                BlobBase nearestBigBlob = FindNearestThreat();
                if (nearestBigBlob != null)
                {
                    fleeTarget = nearestBigBlob;
                    TriggerPanic();
                    return AIState.Flee;
                }
            }

            // Size-awareness: small blobs detect threats from further away
            float effectiveRange = GetSizeAwareDetectionRange();
            BlobBase threat = FindNearestThreat(effectiveRange);
            if (threat != null)
            {
                fleeTarget = threat;
                TriggerPanic();
                return AIState.Flee;
            }

            // Self-preservation: flee when outnumbered by nearby threats
            if (IsOutnumbered())
            {
                BlobBase closestThreat = FindNearestThreat();
                if (closestThreat != null)
                {
                    fleeTarget = closestThreat;
                    TriggerPanic();
                    return AIState.Flee;
                }
            }

            // Opportunistically chase much smaller blobs nearby
            BlobBase prey = FindNearestPrey();
            if (prey != null && DistanceTo(prey) < detectionRange * 0.4f)
            {
                chaseTarget = prey;
                return AIState.Chase;
            }

            return AIState.Wander;
        }

        /// <summary>Hunter: aggressively pursue smaller targets, flee only from close threats, coordinate hunts</summary>
        private AIState EvaluateHunter()
        {
            // Self-preservation: flee if witnessed nearby absorption
            if (witnessedAbsorptionFleeTimer > 0f)
            {
                BlobBase nearestBigBlob = FindNearestThreat();
                if (nearestBigBlob != null && DistanceTo(nearestBigBlob) < detectionRange * 0.6f)
                {
                    fleeTarget = nearestBigBlob;
                    TriggerPanic();
                    return AIState.Flee;
                }
            }

            // Only flee from very close threats (size-aware range)
            float effectiveFleeRange = detectionRange * 0.4f;
            if (IsSmallRelativeTo(FindNearestThreat()))
            {
                effectiveFleeRange = detectionRange * 0.6f;
            }
            BlobBase threat = FindNearestThreat();
            if (threat != null && DistanceTo(threat) < effectiveFleeRange)
            {
                // Self-preservation: also check if outnumbered
                if (IsOutnumbered() || DistanceTo(threat) < detectionRange * 0.3f)
                {
                    fleeTarget = threat;
                    TriggerPanic();
                    return AIState.Flee;
                }
            }

            // Hunt prey with prediction and group coordination
            BlobBase prey = FindNearestPrey();
            if (prey != null)
            {
                chaseTarget = prey;
                return AIState.Chase;
            }

            // Territorial: return to territory if strayed too far
            if (hasTerritoryAssigned)
            {
                float distFromTerritory = Vector3.Distance(
                    new Vector3(transform.position.x, 0f, transform.position.z),
                    new Vector3(territoryCenter.x, 0f, territoryCenter.z));
                if (distFromTerritory > territoryRadius * 1.5f)
                {
                    // Head back toward territory (handled in DoWander via territory pull)
                    return AIState.Wander;
                }
            }

            return AIState.Wander;
        }

        /// <summary>Coward: extremely cautious, flees early, only eats when very safe</summary>
        private AIState EvaluateCoward()
        {
            // Size-awareness: small cowards are extra paranoid
            float effectiveRange = GetSizeAwareDetectionRange() * 1.3f;

            // Self-preservation: flee if nearby blob was absorbed
            if (witnessedAbsorptionFleeTimer > 0f)
            {
                BlobBase nearestBigBlob = FindNearestThreat(effectiveRange);
                if (nearestBigBlob != null)
                {
                    fleeTarget = nearestBigBlob;
                    TriggerPanic();
                    return AIState.Flee;
                }
            }

            // Cowards have extended threat detection
            BlobBase threat = FindNearestThreat(effectiveRange);
            if (threat != null)
            {
                fleeTarget = threat;
                TriggerPanic();
                return AIState.Flee;
            }

            // Self-preservation: flee when outnumbered
            if (IsOutnumbered())
            {
                BlobBase closestThreat = FindNearestThreat();
                if (closestThreat != null)
                {
                    fleeTarget = closestThreat;
                    TriggerPanic();
                    return AIState.Flee;
                }
            }

            // Only chase if prey is very close and no threats nearby
            BlobBase prey = FindNearestPrey();
            if (prey != null && DistanceTo(prey) < detectionRange * 0.25f)
            {
                chaseTarget = prey;
                return AIState.Chase;
            }

            return AIState.Wander;
        }

        /// <summary>
        /// Boss: multi-phase behavior.
        /// Phase 1 (Patrol): lazy circles near center, ignores distant prey.
        /// Phase 2 (Aggressive): when player gets close, actively hunts.
        /// Phase 3 (Berserk): at low health (size), faster and more aggressive.
        /// </summary>
        private AIState EvaluateBoss()
        {
            // Update boss phase
            UpdateBossPhase();

            switch (bossPhase)
            {
                case BossPhase.Patrol:
                    // Only chase prey that wanders very close
                    BlobBase nearbyPrey = FindNearestPrey();
                    if (nearbyPrey != null && DistanceTo(nearbyPrey) < detectionRange * 0.4f)
                    {
                        chaseTarget = nearbyPrey;
                        return AIState.Chase;
                    }
                    return AIState.Wander;

                case BossPhase.Aggressive:
                    BlobBase aggroPrey = FindNearestPrey();
                    if (aggroPrey != null && DistanceTo(aggroPrey) < detectionRange)
                    {
                        chaseTarget = aggroPrey;
                        return AIState.Chase;
                    }
                    return AIState.Wander;

                case BossPhase.Berserk:
                    // Berserk: chase anything in extended range, prioritize player
                    BlobBase berserkTarget = FindNearestPrey(detectionRange * 1.3f);
                    if (berserkTarget != null)
                    {
                        chaseTarget = berserkTarget;
                        return AIState.Chase;
                    }
                    return AIState.Wander;

                default:
                    return AIState.Wander;
            }
        }

        /// <summary>
        /// Determine boss phase based on player proximity and current size relative to initial.
        /// Transitions: Patrol -> Aggressive (player close) -> Berserk (low size).
        /// </summary>
        private void UpdateBossPhase()
        {
            // Berserk check: size dropped below threshold
            if (bossInitialSize > 0f && CurrentSize < bossInitialSize * BossBerserkSizeRatio)
            {
                bossPhase = BossPhase.Berserk;
                return;
            }

            // Aggressive check: player is nearby
            if (BlobController.Instance != null && BlobController.Instance.IsAlive)
            {
                float playerDist = DistanceTo(BlobController.Instance);
                if (playerDist < BossAggressivePlayerRange)
                {
                    if (bossPhase == BossPhase.Patrol)
                    {
                        bossPhase = BossPhase.Aggressive;
                    }
                    return;
                }
            }

            // If player is far and not berserk, can return to patrol
            if (bossPhase == BossPhase.Aggressive)
            {
                // Stay aggressive for a while — only de-escalate if player is very far
                if (BlobController.Instance != null && BlobController.Instance.IsAlive)
                {
                    float playerDist = DistanceTo(BlobController.Instance);
                    if (playerDist > BossAggressivePlayerRange * 1.5f)
                    {
                        bossPhase = BossPhase.Patrol;
                    }
                }
            }
        }

        // ---------- State execution ----------

        private void ExecuteState()
        {
            switch (currentState)
            {
                case AIState.Wander:
                    DoWander();
                    break;
                case AIState.Chase:
                    DoChase();
                    break;
                case AIState.Flee:
                    DoFlee();
                    break;
                case AIState.Confused:
                    DoConfused();
                    break;
                case AIState.Idle:
                    rb.velocity = Vector3.Lerp(rb.velocity, Vector3.zero, Time.fixedDeltaTime * 3f);
                    break;
            }
        }

        // ---------- Wander with Perlin noise + flocking + obstacle avoidance ----------

        private void DoWander()
        {
            float speed = GetCurrentSpeed() * speedMultiplier * 0.6f;

            if (aiType == AIType.Boss)
            {
                DoBossPatrol();
                return;
            }

            // Perlin noise-based organic wandering
            float noiseX = Mathf.PerlinNoise(perlinOffsetX + perlinTime, 0f) * 2f - 1f;
            float noiseZ = Mathf.PerlinNoise(0f, perlinOffsetZ + perlinTime) * 2f - 1f;
            Vector3 noiseDir = new Vector3(noiseX, 0f, noiseZ).normalized;

            // Blend current direction with noise for smooth curved paths
            Vector3 wanderDir = (currentDirection * 0.6f + noiseDir * 0.4f).normalized;

            // Apply flocking for Wanderers near other Wanderers
            if (aiType == AIType.Wanderer)
            {
                Vector3 flockForce = CalculateFlockingForce();
                wanderDir = (wanderDir + flockForce).normalized;
            }

            // Obstacle avoidance: steer around rocks, crystals, etc.
            Vector3 obstacleForce = CalculateObstacleAvoidance();
            if (obstacleForce.sqrMagnitude > 0.01f)
            {
                wanderDir = (wanderDir + obstacleForce * ObstacleAvoidWeight).normalized;
            }

            // Territorial pull: Hunters drift back toward their territory
            if (hasTerritoryAssigned && aiType == AIType.Hunter)
            {
                Vector3 toTerritory = territoryCenter - transform.position;
                toTerritory.y = 0f;
                float distFromTerritory = toTerritory.magnitude;
                if (distFromTerritory > territoryRadius * 0.7f)
                {
                    float pullStrength = Mathf.Clamp01(
                        (distFromTerritory - territoryRadius * 0.7f) / territoryRadius);
                    wanderDir = (wanderDir + toTerritory.normalized * pullStrength * 0.5f).normalized;
                }
            }

            // Steer away from field boundary
            Vector3 boundaryForce = CalculateBoundaryAvoidance();
            if (boundaryForce.sqrMagnitude > 0.01f)
            {
                wanderDir = (wanderDir + boundaryForce * 2f).normalized;
            }

            desiredDirection = wanderDir;
            ApplyMovement(currentDirection, speed);
        }

        // ---------- Boss patrol: large lazy circles near center ----------

        private void DoBossPatrol()
        {
            float patrolSpeedMult = 0.45f;

            // Boss speed and behavior varies by phase
            switch (bossPhase)
            {
                case BossPhase.Patrol:
                    patrolSpeedMult = 0.45f;
                    break;
                case BossPhase.Aggressive:
                    patrolSpeedMult = 0.6f;
                    bossPatrolSpeed = 0.25f; // Faster patrol when aggressive
                    break;
                case BossPhase.Berserk:
                    patrolSpeedMult = 0.75f;
                    bossPatrolSpeed = 0.35f; // Erratic patrol when berserk
                    break;
            }

            bossPatrolAngle += Time.fixedDeltaTime * bossPatrolSpeed;
            bossPatrolAngle %= (Mathf.PI * 2f);

            // Target point on circle around center
            Vector3 patrolTarget = new Vector3(
                Mathf.Cos(bossPatrolAngle) * bossPatrolRadius,
                0f,
                Mathf.Sin(bossPatrolAngle) * bossPatrolRadius
            );

            Vector3 toTarget = (patrolTarget - transform.position);
            toTarget.y = 0f;

            desiredDirection = toTarget.sqrMagnitude > 0.1f ? toTarget.normalized : currentDirection;

            float speed = GetCurrentSpeed() * speedMultiplier * patrolSpeedMult;
            ApplyMovement(currentDirection, speed);
        }

        // ---------- Chase with prediction + group hunting ----------

        private void DoChase()
        {
            if (chaseTarget == null || !chaseTarget.IsAlive || !CanAbsorb(chaseTarget))
            {
                currentState = AIState.Wander;
                return;
            }

            Vector3 targetPos = chaseTarget.transform.position;

            if (aiType == AIType.Hunter || aiType == AIType.Boss)
            {
                // Predict where prey is heading and intercept (lead the target)
                Vector3 currentPreyPos = chaseTarget.transform.position;
                preyVelocityEstimate = Vector3.Lerp(
                    preyVelocityEstimate,
                    (currentPreyPos - lastPreyPosition) / Mathf.Max(Time.fixedDeltaTime, 0.001f),
                    Time.fixedDeltaTime * 5f
                );
                lastPreyPosition = currentPreyPos;

                // Predict future position based on distance (further = more prediction)
                float dist = DistanceTo(chaseTarget);
                float predictionTime = Mathf.Clamp(dist / (GetCurrentSpeed() * speedMultiplier + 0.1f), 0f, 1.5f);

                // Berserk boss predicts further ahead
                if (aiType == AIType.Boss && bossPhase == BossPhase.Berserk)
                {
                    predictionTime *= 1.5f;
                }

                targetPos = currentPreyPos + preyVelocityEstimate * predictionTime;

                // Group hunting: if other hunters are nearby chasing same target,
                // offset approach angle to surround the prey
                if (aiType == AIType.Hunter)
                {
                    Vector3 surroundOffset = CalculateGroupHuntOffset();
                    if (surroundOffset.sqrMagnitude > 0.01f)
                    {
                        targetPos += surroundOffset;
                    }
                }

                // Dodge detection: if prey has been evading for a while, enter confusion
                DetectPreyDodge(currentPreyPos, dist);
            }

            Vector3 toTarget = targetPos - transform.position;
            toTarget.y = 0f;

            if (toTarget.sqrMagnitude > 0.1f)
            {
                desiredDirection = toTarget.normalized;
            }

            // Chase speed varies by type and boss phase
            float chaseSpeed;
            if (aiType == AIType.Boss)
            {
                switch (bossPhase)
                {
                    case BossPhase.Berserk:
                        chaseSpeed = GetCurrentSpeed() * speedMultiplier * 1.1f; // Fast and dangerous
                        break;
                    case BossPhase.Aggressive:
                        chaseSpeed = GetCurrentSpeed() * speedMultiplier * 0.95f;
                        break;
                    default:
                        chaseSpeed = GetCurrentSpeed() * speedMultiplier * 0.85f;
                        break;
                }
            }
            else
            {
                chaseSpeed = GetCurrentSpeed() * speedMultiplier * 1.1f;
            }

            ApplyMovement(currentDirection, chaseSpeed);
        }

        /// <summary>
        /// Calculate offset for group hunting coordination.
        /// Nearby hunters targeting the same prey will fan out to surround it.
        /// </summary>
        private Vector3 CalculateGroupHuntOffset()
        {
            if (chaseTarget == null) return Vector3.zero;

            List<AIBlobController> aiList = GetActiveAIList();
            if (aiList == null) return Vector3.zero;

            List<AIBlobController> nearbyHunters = new List<AIBlobController>();
            int myIndex = 0;

            for (int i = 0, count = aiList.Count; i < count; i++)
            {
                AIBlobController other = aiList[i];
                if (other == null || other == this || !other.IsAlive) continue;
                if (other.aiType != AIType.Hunter) continue;
                if (other.currentState != AIState.Chase) continue;
                if (other.chaseTarget != chaseTarget) continue;

                float dist = DistanceTo(other);
                if (dist < GroupHuntRange)
                {
                    if (other.GetInstanceID() < GetInstanceID())
                    {
                        myIndex++;
                    }
                    nearbyHunters.Add(other);
                }
            }

            // Need at least one other hunter for coordination
            if (nearbyHunters.Count < GroupHuntMinHunters - 1) return Vector3.zero;

            // Calculate angular offset to spread hunters around the prey
            int totalHunters = nearbyHunters.Count + 1;
            float angleStep = 360f / totalHunters;
            float myAngle = myIndex * angleStep * Mathf.Deg2Rad;

            // Offset perpendicular to the prey direction
            Vector3 toPrey = (chaseTarget.transform.position - transform.position).normalized;
            Vector3 perpendicular = new Vector3(-toPrey.z, 0f, toPrey.x);

            float offsetDist = Mathf.Min(DistanceTo(chaseTarget) * 0.3f, 10f);
            Vector3 offset = perpendicular * Mathf.Sin(myAngle) * offsetDist;
            offset.y = 0f;

            return offset;
        }

        /// <summary>
        /// Detect when prey has been successfully dodging — enter confusion state briefly.
        /// If the hunter has been chasing without getting closer for a threshold time,
        /// prey has dodged and hunter becomes momentarily confused.
        /// </summary>
        private void DetectPreyDodge(Vector3 currentPreyPos, float currentDist)
        {
            float previousDist = Vector3.Distance(transform.position, lastChaseTargetPos);

            // If not getting closer to prey, increment dodge counter
            if (currentDist >= previousDist - 0.5f)
            {
                chaseDurationWithoutClosing += Time.fixedDeltaTime;
            }
            else
            {
                chaseDurationWithoutClosing = 0f;
            }

            lastChaseTargetPos = currentPreyPos;

            // Prey has been dodging successfully — enter confusion
            if (chaseDurationWithoutClosing > DodgeDetectionTime)
            {
                EnterConfusion();
                chaseDurationWithoutClosing = 0f;
            }
        }

        /// <summary>Enter brief confusion state after prey dodges successfully</summary>
        private void EnterConfusion()
        {
            currentState = AIState.Confused;
            confusionTimer = Random.Range(ConfusionDurationMin, ConfusionDurationMax);
            // Random wander direction while confused
            float angle = Random.Range(0f, Mathf.PI * 2f);
            confusionWanderDir = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
            chaseTarget = null;
        }

        /// <summary>Execute confusion state: slow, erratic movement</summary>
        private void DoConfused()
        {
            if (confusionTimer <= 0f)
            {
                currentState = AIState.Wander;
                return;
            }

            // Slow, wobbly movement — the blob "lost track" of its prey
            float speed = GetCurrentSpeed() * speedMultiplier * 0.3f;

            // Add some wobble to the confusion direction
            float wobble = Mathf.Sin(Time.time * 8f) * 0.3f;
            Vector3 wobbledDir = new Vector3(
                confusionWanderDir.x + wobble,
                0f,
                confusionWanderDir.z - wobble
            ).normalized;

            desiredDirection = wobbledDir;
            ApplyMovement(currentDirection, speed);
        }

        // ---------- Flee with panic burst + jitter ----------

        private void DoFlee()
        {
            if (fleeTarget == null || !fleeTarget.IsAlive || DistanceTo(fleeTarget) > detectionRange * 1.5f)
            {
                currentState = AIState.Wander;
                panicSpeedBoost = 0f;
                return;
            }

            // Direction away from threat
            Vector3 fleeDir = (transform.position - fleeTarget.transform.position);
            fleeDir.y = 0f;
            fleeDir = fleeDir.normalized;

            // Panic jitter: slight random deviation for erratic fleeing
            panicJitterPhase += Time.fixedDeltaTime * 12f;
            float jitterAngle = Mathf.Sin(panicJitterPhase) * 25f * Mathf.Deg2Rad;
            Vector3 jitteredDir = new Vector3(
                fleeDir.x * Mathf.Cos(jitterAngle) - fleeDir.z * Mathf.Sin(jitterAngle),
                0f,
                fleeDir.x * Mathf.Sin(jitterAngle) + fleeDir.z * Mathf.Cos(jitterAngle)
            );

            // Obstacle avoidance while fleeing
            Vector3 obstacleForce = CalculateObstacleAvoidance();
            if (obstacleForce.sqrMagnitude > 0.01f)
            {
                jitteredDir = (jitteredDir + obstacleForce * 1.2f).normalized;
            }

            // Steer away from boundary while fleeing
            Vector3 boundaryForce = CalculateBoundaryAvoidance();
            if (boundaryForce.sqrMagnitude > 0.01f)
            {
                jitteredDir = (jitteredDir + boundaryForce * 1.5f).normalized;
            }

            desiredDirection = jitteredDir.normalized;

            // Panic burst speed that decays over time
            float fleeSpeed = GetCurrentSpeed() * speedMultiplier * (1.2f + panicSpeedBoost);
            ApplyMovement(currentDirection, fleeSpeed);
        }

        // ---------- Panic trigger ----------

        private void TriggerPanic()
        {
            // Only reset panic boost if not already panicking
            if (panicSpeedBoost < 0.1f)
            {
                panicSpeedBoost = PanicInitialBoost;
                panicJitterPhase = Random.Range(0f, Mathf.PI * 2f);
            }
        }

        // ---------- Flocking forces (Boids-like) ----------

        private Vector3 CalculateFlockingForce()
        {
            Vector3 cohesion = Vector3.zero;
            Vector3 separation = Vector3.zero;
            Vector3 alignment = Vector3.zero;
            int neighborCount = 0;

            List<AIBlobController> aiList = GetActiveAIList();
            if (aiList == null) return Vector3.zero;

            for (int i = 0, count = aiList.Count; i < count; i++)
            {
                AIBlobController other = aiList[i];
                if (other == null || other == this || !other.IsAlive) continue;

                // Only flock with same type (Wanderers with Wanderers)
                if (other.aiType != AIType.Wanderer) continue;

                float dist = DistanceTo(other);
                if (dist > FlockNeighborRange) continue;

                neighborCount++;

                // Cohesion: move toward center of neighbors
                cohesion += other.transform.position;

                // Alignment: match neighbor heading
                alignment += other.currentDirection;

                // Separation: push away from too-close neighbors
                if (dist < FlockSeparationRange && dist > 0.01f)
                {
                    Vector3 away = (transform.position - other.transform.position) / dist;
                    separation += away;
                }
            }

            if (neighborCount == 0) return Vector3.zero;

            // Average cohesion target and convert to direction
            cohesion = (cohesion / neighborCount - transform.position).normalized * FlockCohesionWeight;
            alignment = (alignment / neighborCount).normalized * FlockAlignmentWeight;
            separation = separation.normalized * FlockSeparationWeight;

            Vector3 flockForce = cohesion + separation + alignment;
            flockForce.y = 0f;
            return flockForce;
        }

        // ---------- Obstacle avoidance (rocks, crystals, barriers) ----------

        /// <summary>
        /// Detect nearby obstacles via SphereCast and return a steering force to avoid them.
        /// Uses physics raycasts in the forward direction and to the sides.
        /// </summary>
        private Vector3 CalculateObstacleAvoidance()
        {
            Vector3 avoidance = Vector3.zero;
            float blobRadius = CurrentSize * 0.15f * 0.5f;
            float detectDist = ObstacleDetectRange + blobRadius;

            // Cast rays in forward, left-forward, and right-forward directions
            Vector3 forward = currentDirection.sqrMagnitude > 0.01f ? currentDirection : transform.forward;
            Vector3 right = new Vector3(forward.z, 0f, -forward.x);
            forward.y = 0f;
            right.y = 0f;

            Vector3 origin = transform.position;
            origin.y = Mathf.Max(origin.y, 1f); // Ensure ray starts above ground

            // Three feeler rays
            Vector3[] directions = new Vector3[]
            {
                forward.normalized,
                (forward + right * 0.5f).normalized,
                (forward - right * 0.5f).normalized
            };

            for (int i = 0; i < directions.Length; i++)
            {
                RaycastHit hit;
                if (Physics.SphereCast(origin, blobRadius * 0.5f, directions[i], out hit,
                    detectDist, obstacleLayerMask, QueryTriggerInteraction.Ignore))
                {
                    // Don't avoid other blobs, only static obstacles
                    if (hit.collider.GetComponent<BlobBase>() != null) continue;
                    if (hit.collider.GetComponent<Feed>() != null) continue;

                    // Strength inversely proportional to distance
                    float strength = 1f - (hit.distance / detectDist);
                    Vector3 awayFromObstacle = (transform.position - hit.point);
                    awayFromObstacle.y = 0f;

                    if (awayFromObstacle.sqrMagnitude < 0.01f)
                    {
                        awayFromObstacle = -directions[i];
                    }

                    avoidance += awayFromObstacle.normalized * strength;
                }
            }

            avoidance.y = 0f;
            return avoidance;
        }

        // ---------- Boundary avoidance ----------

        private Vector3 CalculateBoundaryAvoidance()
        {
            if (GameManager.Instance == null) return Vector3.zero;

            float fieldRadius = GameManager.Instance.FieldRadius;
            Vector3 flatPos = new Vector3(transform.position.x, 0f, transform.position.z);
            float distFromCenter = flatPos.magnitude;

            // Start steering away when within 20% of boundary
            float avoidThreshold = fieldRadius * 0.8f;
            if (distFromCenter < avoidThreshold) return Vector3.zero;

            // Strength increases as we get closer to edge
            float urgency = (distFromCenter - avoidThreshold) / (fieldRadius - avoidThreshold);
            urgency = Mathf.Clamp01(urgency);

            Vector3 toCenter = -flatPos.normalized;
            return toCenter * urgency;
        }

        // ---------- Movement helper with smooth steering ----------

        private void ApplyMovement(Vector3 direction, float speed)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.01f) return;

            Vector3 targetVelocity = direction.normalized * speed;
            rb.velocity = Vector3.Lerp(rb.velocity, targetVelocity, Time.fixedDeltaTime * 6f);
        }

        // ---------- Detection: threats and prey (includes other AI) ----------

        private BlobBase FindNearestThreat(float range = -1f)
        {
            if (range < 0f) range = detectionRange;

            BlobBase nearest = null;
            float nearestDist = range;

            // Check player
            if (BlobController.Instance != null && BlobController.Instance.IsAlive)
            {
                if (BlobController.Instance.CanAbsorb(this))
                {
                    float d = DistanceTo(BlobController.Instance);
                    if (d < nearestDist)
                    {
                        nearest = BlobController.Instance;
                        nearestDist = d;
                    }
                }
            }

            // Check other AI blobs
            List<AIBlobController> aiList = GetActiveAIList();
            if (aiList != null)
            {
                for (int i = 0, count = aiList.Count; i < count; i++)
                {
                    AIBlobController other = aiList[i];
                    if (other == null || other == this || !other.IsAlive) continue;
                    if (!other.CanAbsorb(this)) continue;

                    float d = DistanceTo(other);
                    if (d < nearestDist)
                    {
                        nearest = other;
                        nearestDist = d;
                    }
                }
            }

            return nearest;
        }

        private BlobBase FindNearestPrey(float range = -1f)
        {
            if (range < 0f) range = detectionRange;

            BlobBase nearest = null;
            float nearestDist = range;

            // Check player
            if (BlobController.Instance != null && BlobController.Instance.IsAlive)
            {
                if (CanAbsorb(BlobController.Instance))
                {
                    float d = DistanceTo(BlobController.Instance);
                    if (d < nearestDist)
                    {
                        nearest = BlobController.Instance;
                        nearestDist = d;
                    }
                }
            }

            // Check other AI blobs
            List<AIBlobController> aiList = GetActiveAIList();
            if (aiList != null)
            {
                for (int i = 0, count = aiList.Count; i < count; i++)
                {
                    AIBlobController other = aiList[i];
                    if (other == null || other == this || !other.IsAlive) continue;
                    if (!CanAbsorb(other)) continue;

                    float d = DistanceTo(other);
                    if (d < nearestDist)
                    {
                        nearest = other;
                        nearestDist = d;
                    }
                }
            }

            return nearest;
        }

        // ---------- Self-preservation helpers ----------

        /// <summary>
        /// Check if this blob is outnumbered by nearby threats.
        /// Returns true if there are 2+ blobs nearby that can absorb this one.
        /// </summary>
        private bool IsOutnumbered()
        {
            int threatCount = 0;
            float checkRange = SelfPreservationCheckRange;

            if (BlobController.Instance != null && BlobController.Instance.IsAlive)
            {
                if (BlobController.Instance.CanAbsorb(this) && DistanceTo(BlobController.Instance) < checkRange)
                {
                    threatCount++;
                }
            }

            List<AIBlobController> aiList = GetActiveAIList();
            if (aiList != null)
            {
                for (int i = 0, count = aiList.Count; i < count; i++)
                {
                    AIBlobController other = aiList[i];
                    if (other == null || other == this || !other.IsAlive) continue;
                    if (!other.CanAbsorb(this)) continue;
                    if (DistanceTo(other) < checkRange)
                    {
                        threatCount++;
                        if (threatCount >= 2) return true;
                    }
                }
            }

            return threatCount >= 2;
        }

        /// <summary>
        /// Check if this blob is relatively small compared to a specific threat.
        /// Small blobs should be more cautious and flee from further away.
        /// </summary>
        private bool IsSmallRelativeTo(BlobBase threat)
        {
            if (threat == null) return false;
            return CurrentSize < threat.CurrentSize * SmallBlobSizeRatio;
        }

        /// <summary>
        /// Get detection range adjusted for size awareness.
        /// Smaller blobs detect threats from further away as a survival mechanism.
        /// </summary>
        private float GetSizeAwareDetectionRange()
        {
            // Find the nearest threat to compare size against
            BlobBase threat = FindNearestThreat();
            if (threat != null && IsSmallRelativeTo(threat))
            {
                return detectionRange * SizeAwarenessMultiplier;
            }
            return detectionRange;
        }

        /// <summary>
        /// Called by external systems when a nearby absorption event occurs.
        /// Triggers self-preservation flight response in witnessing blobs.
        /// </summary>
        public void WitnessAbsorption(Vector3 absorptionPoint)
        {
            float dist = Vector3.Distance(transform.position, absorptionPoint);
            if (dist < AbsorptionWitnessRange)
            {
                // Closer events cause longer flee response
                float responseStrength = 1f - (dist / AbsorptionWitnessRange);
                witnessedAbsorptionFleeTimer = Mathf.Max(witnessedAbsorptionFleeTimer,
                    responseStrength * 3f);
            }
        }

        // ---------- Utility helpers ----------

        private float DistanceTo(BlobBase other)
        {
            return Vector3.Distance(transform.position, other.transform.position);
        }

        /// <summary>Get the active AI list from AISpawner (cached reference)</summary>
        private List<AIBlobController> GetActiveAIList()
        {
            return AISpawner.Instance?.ActiveAIs;
        }

        // ---------- Collision handling ----------

        private void OnTriggerEnter(Collider other)
        {
            if (!IsAlive) return;

            // Feed absorption
            Feed feed = other.GetComponent<Feed>();
            if (feed != null && feed.IsActive)
            {
                feed.Consume();
                AddSize(feed.NutritionValue);
                return;
            }

            // Blob collision (uses TryAbsorb lock to prevent race conditions)
            BlobBase otherBlob = other.GetComponent<BlobBase>();
            if (otherBlob != null && otherBlob.IsAlive && otherBlob != this)
            {
                if (otherBlob is BlobController playerBlob && playerBlob.IsGhostActive) return;

                if (BlobBase.TryAbsorb(this, otherBlob))
                {
                    VFXManager.Instance?.PlayBlobSplash(otherBlob.transform.position, Color.white, otherBlob.CurrentSize);
                    AudioManager.Instance?.PlayBlobAbsorb();

                    // Notify nearby AI blobs of the absorption (self-preservation)
                    NotifyNearbyAbsorption(otherBlob.transform.position);

                    var effect = otherBlob.gameObject.AddComponent<AbsorptionEffect>();
                    effect.Initialize(transform, otherBlob.CurrentSize);
                    effect.SetBlobPair(this, otherBlob);
                    otherBlob.GetAbsorbed();
                    AddSize(otherBlob.CurrentSize * 0.8f);
                }
            }
        }

        /// <summary>
        /// Notify nearby AI blobs that an absorption just happened.
        /// This triggers self-preservation flee responses in witnesses.
        /// </summary>
        private void NotifyNearbyAbsorption(Vector3 absorptionPoint)
        {
            List<AIBlobController> aiList = GetActiveAIList();
            if (aiList == null) return;

            for (int i = 0, count = aiList.Count; i < count; i++)
            {
                AIBlobController other = aiList[i];
                if (other == null || other == this || !other.IsAlive) continue;
                other.WitnessAbsorption(absorptionPoint);
            }
        }

        public override void GetAbsorbed()
        {
            base.GetAbsorbed();
            // Visual deactivation handled by AbsorptionEffect

            // Notify AISpawner to respawn
            AISpawner.Instance?.OnAIDied(this);
        }
    }
}
