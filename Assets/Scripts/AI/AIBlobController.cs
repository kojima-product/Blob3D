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
    /// Types: Wanderer / Hunter / Coward / Boss
    /// </summary>
    public class AIBlobController : BlobBase
    {
        // ---------- AI type & state enums ----------
        public enum AIType { Wanderer, Hunter, Coward, Boss }
        private enum AIState { Idle, Wander, Chase, Flee, Eat }

        [Header("AI Settings")]
        [SerializeField] private AIType aiType = AIType.Wanderer;
        [SerializeField] private float detectionRange = 40f;
        [SerializeField] private float stateUpdateInterval = 0.3f;
        // Wander direction changes are driven by Perlin noise, not timer

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

        public AIType Type => aiType;

        // ---------- Initialization ----------

        /// <summary>Setup AI (called from Spawner or Manager)</summary>
        public void Initialize(AIType type, float size)
        {
            aiType = type;
            SetSize(size);

            // Boss has wider detection range
            if (type == AIType.Boss)
            {
                detectionRange *= 1.5f;
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

            // Random initial facing direction
            float angle = Random.Range(0f, Mathf.PI * 2f);
            currentDirection = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
            desiredDirection = currentDirection;

            CreateNameLabel();
        }

        /// <summary>Create a WorldSpace Canvas with a name label above the AI blob.</summary>
        private void CreateNameLabel()
        {
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

        protected override void Start()
        {
            base.Start();
            // Stagger state evaluation so not all AI update on the same frame
            stateTimer = Random.Range(0f, stateUpdateInterval);
        }

        // ---------- Update loop ----------

        private void Update()
        {
            if (!IsAlive || GameManager.Instance == null ||
                GameManager.Instance.CurrentState != GameManager.GameState.Playing)
                return;

            // Evaluate state at intervals (performance optimization)
            stateTimer -= Time.deltaTime;
            if (stateTimer <= 0f)
            {
                stateTimer = stateUpdateInterval;
                EvaluateState();
            }

            // Advance Perlin time for wander noise
            perlinTime += Time.deltaTime * perlinSpeed;

            // Decay panic speed boost
            if (panicSpeedBoost > 0f)
            {
                panicSpeedBoost -= PanicDecayRate * Time.deltaTime;
                if (panicSpeedBoost < 0f) panicSpeedBoost = 0f;
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

            // State transition hesitation
            if (newState != currentState)
            {
                previousState = currentState;
                currentState = newState;

                // Brief pause when switching states for natural feel
                // Skip pause for urgent flee transitions
                if (newState != AIState.Flee)
                {
                    transitionPauseTimer = TransitionPauseDuration;
                }
            }
        }

        /// <summary>Wanderer: wander with flocking, flee from threats, opportunistically eat smaller blobs</summary>
        private AIState EvaluateWanderer()
        {
            BlobBase threat = FindNearestThreat();
            if (threat != null)
            {
                fleeTarget = threat;
                TriggerPanic();
                return AIState.Flee;
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

        /// <summary>Hunter: aggressively pursue smaller targets, flee only from close threats</summary>
        private AIState EvaluateHunter()
        {
            // Only flee from very close threats
            BlobBase threat = FindNearestThreat();
            if (threat != null && DistanceTo(threat) < detectionRange * 0.4f)
            {
                fleeTarget = threat;
                TriggerPanic();
                return AIState.Flee;
            }

            // Hunt prey with prediction
            BlobBase prey = FindNearestPrey();
            if (prey != null)
            {
                chaseTarget = prey;
                return AIState.Chase;
            }

            return AIState.Wander;
        }

        /// <summary>Coward: extremely cautious, flees early, only eats when very safe</summary>
        private AIState EvaluateCoward()
        {
            // Cowards have extended threat detection
            BlobBase threat = FindNearestThreat(detectionRange * 1.3f);
            if (threat != null)
            {
                fleeTarget = threat;
                TriggerPanic();
                return AIState.Flee;
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

        /// <summary>Boss: patrol center area, relentlessly chase anything in range</summary>
        private AIState EvaluateBoss()
        {
            BlobBase prey = FindNearestPrey();
            if (prey != null && DistanceTo(prey) < detectionRange)
            {
                chaseTarget = prey;
                return AIState.Chase;
            }

            // Boss patrols instead of just standing still
            return AIState.Wander;
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
                case AIState.Idle:
                    rb.velocity = Vector3.Lerp(rb.velocity, Vector3.zero, Time.fixedDeltaTime * 3f);
                    break;
            }
        }

        // ---------- Wander with Perlin noise + flocking ----------

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
            bossPatrolAngle += Time.fixedDeltaTime * bossPatrolSpeed;
            if (bossPatrolAngle > Mathf.PI * 2f) bossPatrolAngle -= Mathf.PI * 2f;

            // Target point on circle around center
            Vector3 patrolTarget = new Vector3(
                Mathf.Cos(bossPatrolAngle) * bossPatrolRadius,
                0f,
                Mathf.Sin(bossPatrolAngle) * bossPatrolRadius
            );

            Vector3 toTarget = (patrolTarget - transform.position);
            toTarget.y = 0f;

            desiredDirection = toTarget.sqrMagnitude > 0.1f ? toTarget.normalized : currentDirection;

            float speed = GetCurrentSpeed() * speedMultiplier * 0.45f;
            ApplyMovement(currentDirection, speed);
        }

        // ---------- Chase with prediction ----------

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
                // Predict where prey is heading and intercept
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
                targetPos = currentPreyPos + preyVelocityEstimate * predictionTime;
            }

            Vector3 toTarget = targetPos - transform.position;
            toTarget.y = 0f;

            if (toTarget.sqrMagnitude > 0.1f)
            {
                desiredDirection = toTarget.normalized;
            }

            // Boss chases at steady, relentless pace; others sprint
            float chaseSpeed;
            if (aiType == AIType.Boss)
            {
                chaseSpeed = GetCurrentSpeed() * speedMultiplier * 0.85f;
            }
            else
            {
                chaseSpeed = GetCurrentSpeed() * speedMultiplier * 1.1f;
            }

            ApplyMovement(currentDirection, chaseSpeed);
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

        private BlobBase FindNearestPrey()
        {
            BlobBase nearest = null;
            float nearestDist = detectionRange;

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

                    var effect = otherBlob.gameObject.AddComponent<AbsorptionEffect>();
                    effect.Initialize(transform, otherBlob.CurrentSize);
                    effect.SetBlobPair(this, otherBlob);
                    otherBlob.GetAbsorbed();
                    AddSize(otherBlob.CurrentSize * 0.8f);
                }
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
