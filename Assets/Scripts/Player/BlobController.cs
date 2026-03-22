using UnityEngine;
using System;
using Blob3D.Core;
using Blob3D.Gameplay;
using Blob3D.Utils;

namespace Blob3D.Player
{
    /// <summary>
    /// Player blob controller.
    /// Handles virtual joystick input, movement with momentum, dash, split,
    /// collision response, and near-death feedback.
    /// Fires screen shake events on absorb for UI feedback.
    /// </summary>
    public class BlobController : BlobBase
    {
        // ---------- Settings ----------
        [Header("Player Settings")]
        [SerializeField] private float dashSpeedMultiplier = 2f;
        [SerializeField] private float dashDuration = 0.3f;
        [SerializeField] private float dashSizeCost = 0.5f;
        [SerializeField] private float dashCooldown = 1.5f;
        [SerializeField] private float splitForce = 25f;

        [Header("Momentum / Inertia")]
        [SerializeField] private float baseAcceleration = 18f;
        [SerializeField] private float baseDeceleration = 8f;
        [SerializeField] private float massFactor = 0.06f; // How much size slows accel/decel

        [Header("Dash Feel")]
        [SerializeField] private float dashSquatDuration = 0.1f;
        [SerializeField] private float dashSquatAmount = 0.15f; // Y-scale compression before dash

        [Header("Collision Bounce")]
        [SerializeField] private float bounceDeformAmount = 0.25f;
        [SerializeField] private float bounceDeformDuration = 0.3f;
        [SerializeField] private float bounceForceMultiplier = 0.6f;

        [Header("Near-Death Feedback")]
        [SerializeField] private float dangerDetectRadius = 8f;
        [SerializeField] private float dangerPullStrength = 0.8f;
        [SerializeField] private float dangerHapticInterval = 0.3f;

        [Header("Input Dead Zone")]
        [SerializeField] private float inputDeadZone = 0.12f;

        [Header("Screen Shake")]
        [SerializeField] private float feedShakeIntensity = 2f;
        [SerializeField] private float feedShakeDuration = 0.1f;
        [SerializeField] private float blobShakeIntensity = 6f;
        [SerializeField] private float blobShakeDuration = 0.25f;

        // ---------- State ----------
        private Vector2 inputDirection;
        private bool isDashing;
        private float dashTimer;
        private float dashCooldownTimer;
        private bool hasShield;
        private ParticleSystem dashTrailPS;

        // Momentum state
        private Vector3 currentVelocity;

        // Dash anticipation state
        private bool isDashSquatting;
        private float dashSquatTimer;
        private bool dashSquatPending; // Fire dash after squat completes

        // Collision bounce deformation state
        private float bounceDeformTimer;
        private Vector3 bounceNormal; // Direction of bounce impact

        // Near-death detection state
        private float dangerHapticTimer;
        private float currentDangerIntensity; // 0 = safe, 1 = max danger

        // ---------- Dash Cooldown ----------
        public float DashCooldownRatio => dashCooldown > 0 ? Mathf.Clamp01(dashCooldownTimer / dashCooldown) : 0f;

        // ---------- Power-up State ----------
        public float SpeedMultiplier { get; set; } = 1f;
        public bool IsMagnetActive { get; set; }
        public bool IsGhostActive { get; set; }

        public bool HasShield
        {
            get => hasShield;
            set
            {
                hasShield = value;
                if (hasShield)
                {
                    PowerUpEffectManager.Instance?.ShowShieldEffect(transform);
                }
                else
                {
                    PowerUpEffectManager.Instance?.HideShieldEffect();
                }
            }
        }

        // ---------- Events ----------

        /// <summary>
        /// Static event for screen shake requests.
        /// Parameters: (intensity, duration).
        /// UIManager subscribes to this to trigger canvas-based screen shake.
        /// </summary>
        public static event Action<float, float> OnScreenShakeRequested;

        /// <summary>
        /// Fired when this player absorbs another blob.
        /// Parameters: (victim name, score gained).
        /// </summary>
        public static event Action<string, int> OnBlobAbsorbed;

        /// <summary>
        /// Fired when this player is absorbed by another blob.
        /// Parameter: killer name.
        /// </summary>
        public static event Action<string> OnPlayerDied;

        /// <summary>
        /// Fired when dash afterimage trail should start.
        /// External VFX systems can subscribe to this.
        /// </summary>
        public static event Action<Transform> OnDashAfterimageRequested;

        // ---------- Singleton ----------
        public static BlobController Instance { get; private set; }

        // Trail renderer for player movement visual
        private TrailRenderer trailRenderer;

        protected override void Awake()
        {
            base.Awake();
            Instance = this;
            SetupTrailRenderer();
        }

        private void OnDestroy()
        {
            OnSizeChanged -= UpdateTrailWidth;

            // Fix: clear static singleton and events to prevent stale references after scene reload
            if (Instance == this)
            {
                Instance = null;
                OnScreenShakeRequested = null;
                OnBlobAbsorbed = null;
                OnPlayerDied = null;
                OnDashAfterimageRequested = null;
            }
        }

        /// <summary>Setup a short trail renderer behind the player blob for movement visual</summary>
        private void SetupTrailRenderer()
        {
            trailRenderer = gameObject.AddComponent<TrailRenderer>();
            trailRenderer.time = 0.3f;
            trailRenderer.startWidth = CurrentSize * 0.15f * 0.5f;
            trailRenderer.endWidth = 0f;
            trailRenderer.material = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
            trailRenderer.material.color = new Color(1f, 1f, 1f, 0.15f);
            trailRenderer.minVertexDistance = 0.1f;
            trailRenderer.autodestruct = false;
            trailRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            trailRenderer.receiveShadows = false;

            // Update trail width when size changes
            OnSizeChanged += UpdateTrailWidth;
        }

        /// <summary>Callback to keep trail width in sync with blob size</summary>
        private void UpdateTrailWidth(float newSize)
        {
            if (trailRenderer != null)
            {
                trailRenderer.startWidth = newSize * 0.15f * 0.5f;
            }
        }

        private void Update()
        {
            if (!IsAlive || GameManager.Instance == null || GameManager.Instance.CurrentState != GameManager.GameState.Playing)
                return;

            UpdateDash();
            UpdateDashSquat();
            UpdateNearDeathFeedback();
        }

        private void FixedUpdate()
        {
            if (!IsAlive || GameManager.Instance == null || GameManager.Instance.CurrentState != GameManager.GameState.Playing)
                return;

            Move();
            ApplyNearDeathPull();
            ApplySquashAndStretch(rb.velocity);
            ApplyBounceDeform();

            // Field boundary clamp
            transform.position = GameManager.Instance.ClampToField(transform.position);

            ClampYPosition();
        }

        // ---------- Input ----------

        /// <summary>Called from UI joystick. Applies dead zone filtering.</summary>
        public void SetInput(Vector2 direction)
        {
            // Apply radial dead zone to prevent micro-jitter
            if (direction.magnitude < inputDeadZone)
            {
                inputDirection = Vector2.zero;
                return;
            }

            // Remap input from dead zone edge to full range for smooth response
            float magnitude = direction.magnitude;
            float remapped = (magnitude - inputDeadZone) / (1f - inputDeadZone);
            inputDirection = direction.normalized * Mathf.Clamp01(remapped);
        }

        /// <summary>Called from dash button. Initiates squat anticipation before dash.</summary>
        public void TriggerDash()
        {
            if (dashCooldownTimer > 0f || isDashing || isDashSquatting) return;
            if (CurrentSize < dashSizeCost * 2f) return; // Not enough size

            // Start squat anticipation before actual dash
            isDashSquatting = true;
            dashSquatTimer = dashSquatDuration;
            dashSquatPending = true;

            AudioManager.Instance?.PlayDash();
            HapticHelper.LightImpact();
        }

        /// <summary>Execute the actual dash after squat anticipation completes</summary>
        private void ExecuteDash()
        {
            isDashing = true;
            dashTimer = dashDuration;
            dashCooldownTimer = dashCooldown;
            ReduceSize(dashSizeCost);

            // Dash trail VFX
            var renderer = GetComponent<Renderer>();
            Color trailColor = renderer != null ? renderer.material.color : Color.cyan;
            dashTrailPS = VFXManager.Instance?.PlayDashTrail(transform, trailColor);

            // Request afterimage trail effect
            OnDashAfterimageRequested?.Invoke(transform);
        }

        /// <summary>Split mechanic (double tap): eject a temporary sphere blob</summary>
        public void TriggerSplit()
        {
            if (CurrentSize < 2f) return; // Minimum size check

            float halfSize = CurrentSize * 0.5f;

            // Capture parent velocity before size change for inheritance
            Vector3 parentVelocity = rb.velocity;

            SetSize(halfSize);

            // Spawn position in front of the player
            Vector3 spawnPos = transform.position + transform.forward * 2f;
            spawnPos.y = halfSize * 0.15f * 0.5f;

            // Create a temporary sphere primitive (not from pool)
            GameObject splitObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            splitObj.name = "SplitBlob";
            splitObj.layer = gameObject.layer;
            splitObj.transform.position = spawnPos;

            // Set scale to match half size
            float scale = halfSize * 0.1f;
            splitObj.transform.localScale = Vector3.one * scale;

            // Apply a slightly darker version of the player's color
            Renderer playerRend = GetComponent<Renderer>();
            Renderer splitRend = splitObj.GetComponent<Renderer>();
            if (splitRend != null)
            {
                Color baseColor = playerRend != null ? playerRend.material.color : Color.cyan;
                Color darkened = baseColor * 0.7f;
                darkened.a = 1f;
                splitRend.material.color = darkened;
            }

            // Configure SphereCollider as trigger
            SphereCollider col = splitObj.GetComponent<SphereCollider>();
            if (col == null) col = splitObj.AddComponent<SphereCollider>();
            col.isTrigger = true;

            // Configure Rigidbody for physics impulse
            Rigidbody splitRb = splitObj.AddComponent<Rigidbody>();
            splitRb.useGravity = false;
            splitRb.constraints = RigidbodyConstraints.FreezePositionY | RigidbodyConstraints.FreezeRotation;

            // Inherit parent velocity for natural momentum transfer
            splitRb.velocity = parentVelocity;

            // Determine shoot direction from input or forward
            Vector3 shootDir = transform.forward;
            if (inputDirection.sqrMagnitude > 0.01f)
            {
                shootDir = GetCameraRelativeDirection(inputDirection);
            }

            // Add slight curve perpendicular to shoot direction for visual interest
            Vector3 curveOffset = Vector3.Cross(shootDir, Vector3.up).normalized * 0.15f;
            Vector3 curvedShootDir = (shootDir + curveOffset).normalized;

            splitRb.AddForce(curvedShootDir * splitForce, ForceMode.Impulse);

            // Auto-destroy after 5 seconds
            Destroy(splitObj, 5f);
        }

        // ---------- Movement ----------

        private void Move()
        {
            // Calculate mass-dependent responsiveness (larger blobs = more sluggish)
            float massInfluence = 1f / (1f + CurrentSize * massFactor);
            float acceleration = baseAcceleration * massInfluence;
            float deceleration = baseDeceleration * massInfluence;

            if (inputDirection.sqrMagnitude < 0.001f)
            {
                // Decelerate with inertia — larger blobs coast longer
                currentVelocity = Vector3.Lerp(currentVelocity, Vector3.zero, Time.fixedDeltaTime * deceleration);

                // Snap to zero when close enough to prevent drift
                if (currentVelocity.sqrMagnitude < 0.01f)
                {
                    currentVelocity = Vector3.zero;
                }

                rb.velocity = currentVelocity;
                return;
            }

            // Convert 2D input to camera-relative 3D direction
            Vector3 moveDir = GetCameraRelativeDirection(inputDirection);
            float speed = GetCurrentSpeed() * SpeedMultiplier;

            if (isDashing)
            {
                speed *= dashSpeedMultiplier;
                // During dash, use higher acceleration for snappy burst
                acceleration *= 2.5f;
            }

            // Accelerate toward target velocity with mass-based inertia
            Vector3 targetVelocity = moveDir * speed;
            currentVelocity = Vector3.Lerp(currentVelocity, targetVelocity, Time.fixedDeltaTime * acceleration);
            rb.velocity = currentVelocity;
        }

        /// <summary>
        /// Transform 2D input direction into world-space direction relative to the camera.
        /// W/up always moves away from camera, A/left always moves to camera's left, etc.
        /// </summary>
        private Vector3 GetCameraRelativeDirection(Vector2 input)
        {
            Camera mainCam = Camera.main;
            if (mainCam == null)
            {
                // Fallback: use raw input if no camera
                return new Vector3(input.x, 0f, input.y).normalized;
            }

            // Get camera forward and right vectors, flattened to XZ plane
            Vector3 camForward = mainCam.transform.forward;
            camForward.y = 0f;
            camForward.Normalize();

            Vector3 camRight = mainCam.transform.right;
            camRight.y = 0f;
            camRight.Normalize();

            // Transform input relative to camera orientation
            Vector3 worldDir = camForward * input.y + camRight * input.x;
            return worldDir.normalized;
        }

        // ---------- Dash State Machine ----------

        private void UpdateDash()
        {
            if (isDashing)
            {
                dashTimer -= Time.deltaTime;
                if (dashTimer <= 0f)
                {
                    isDashing = false;

                    // Stop dash trail VFX
                    if (dashTrailPS != null)
                    {
                        VFXManager.Instance?.StopDashTrail(dashTrailPS);
                        dashTrailPS = null;
                    }
                }
            }

            if (dashCooldownTimer > 0f)
            {
                dashCooldownTimer -= Time.deltaTime;
            }
        }

        /// <summary>Handle the squat anticipation phase before dash fires</summary>
        private void UpdateDashSquat()
        {
            if (!isDashSquatting) return;

            dashSquatTimer -= Time.deltaTime;
            if (dashSquatTimer <= 0f)
            {
                isDashSquatting = false;
                if (dashSquatPending)
                {
                    dashSquatPending = false;
                    ExecuteDash();
                }
            }
        }

        // ---------- Bounce Deformation on Obstacle Collision ----------

        /// <summary>Apply temporary deformation when bouncing off obstacles</summary>
        private void ApplyBounceDeform()
        {
            if (bounceDeformTimer <= 0f) return;

            bounceDeformTimer -= Time.fixedDeltaTime;
            float t = Mathf.Clamp01(bounceDeformTimer / bounceDeformDuration);

            // Damped oscillation for natural bounce recovery
            float deform = bounceDeformAmount * t * Mathf.Cos(t * Mathf.PI * 2f);
            float baseScale = CurrentSize * 0.15f; // sizeToScaleRatio

            // Squash along bounce normal, expand perpendicular
            Vector3 scale = transform.localScale;
            float normalDot = Mathf.Abs(Vector3.Dot(bounceNormal, Vector3.right));
            float lateralDot = Mathf.Abs(Vector3.Dot(bounceNormal, Vector3.forward));

            // Apply deformation proportional to impact direction
            scale.x += baseScale * deform * normalDot;
            scale.z += baseScale * deform * lateralDot;
            scale.y -= baseScale * deform * 0.3f; // Slight vertical squash

            transform.localScale = scale;
        }

        /// <summary>Also apply squat deformation during dash anticipation</summary>
        private void LateUpdate()
        {
            if (!isDashSquatting) return;

            // Squat: compress Y, expand XZ
            float squatProgress = 1f - Mathf.Clamp01(dashSquatTimer / dashSquatDuration);
            float squatCurve = Mathf.Sin(squatProgress * Mathf.PI * 0.5f); // Ease-in
            float squatScale = 1f - (dashSquatAmount * squatCurve);
            float expandScale = 1f + (dashSquatAmount * squatCurve * 0.5f);

            Vector3 scale = transform.localScale;
            scale.y *= squatScale;
            scale.x *= expandScale;
            scale.z *= expandScale;
            transform.localScale = scale;
        }

        // ---------- Near-Death Feedback ----------

        /// <summary>Detect nearby larger blobs and ramp up danger feedback</summary>
        private void UpdateNearDeathFeedback()
        {
            float closestDangerDist = float.MaxValue;
            float dangerSizeRatio = 0f;

            // Scan for nearby threats using overlap sphere
            Collider[] nearby = Physics.OverlapSphere(transform.position, dangerDetectRadius + CurrentSize * 0.15f);
            for (int i = 0; i < nearby.Length; i++)
            {
                BlobBase otherBlob = nearby[i].GetComponent<BlobBase>();
                if (otherBlob == null || otherBlob == this || !otherBlob.IsAlive) continue;

                // Only threats that are large enough to absorb us
                if (otherBlob.CurrentSize <= CurrentSize * 1.1f) continue;

                float dist = Vector3.Distance(transform.position, otherBlob.transform.position);
                if (dist < closestDangerDist)
                {
                    closestDangerDist = dist;
                    dangerSizeRatio = otherBlob.CurrentSize / CurrentSize;
                }
            }

            // Calculate danger intensity based on proximity and size ratio
            if (closestDangerDist < dangerDetectRadius)
            {
                float proximityFactor = 1f - Mathf.Clamp01(closestDangerDist / dangerDetectRadius);
                float sizeFactor = Mathf.Clamp01((dangerSizeRatio - 1.1f) / 2f); // Scale from 1.1x to 3.1x
                currentDangerIntensity = Mathf.Lerp(currentDangerIntensity,
                    proximityFactor * (0.5f + sizeFactor * 0.5f), Time.deltaTime * 5f);
            }
            else
            {
                currentDangerIntensity = Mathf.Lerp(currentDangerIntensity, 0f, Time.deltaTime * 3f);
            }

            // Haptic vibration ramp based on danger intensity
            if (currentDangerIntensity > 0.2f)
            {
                dangerHapticTimer -= Time.deltaTime;
                if (dangerHapticTimer <= 0f)
                {
                    // Vibration interval decreases with danger (more urgent when closer)
                    float interval = Mathf.Lerp(dangerHapticInterval, 0.08f, currentDangerIntensity);
                    dangerHapticTimer = interval;

                    if (currentDangerIntensity > 0.7f)
                        HapticHelper.MediumImpact();
                    else
                        HapticHelper.LightImpact();
                }
            }
        }

        /// <summary>Apply subtle gravitational pull toward nearby danger sources</summary>
        private void ApplyNearDeathPull()
        {
            if (currentDangerIntensity < 0.1f) return;

            // Find nearest threat for pull direction
            Collider[] nearby = Physics.OverlapSphere(transform.position, dangerDetectRadius + CurrentSize * 0.15f);
            Vector3 pullDirection = Vector3.zero;

            for (int i = 0; i < nearby.Length; i++)
            {
                BlobBase otherBlob = nearby[i].GetComponent<BlobBase>();
                if (otherBlob == null || otherBlob == this || !otherBlob.IsAlive) continue;
                if (otherBlob.CurrentSize <= CurrentSize * 1.1f) continue;

                Vector3 toThreat = (otherBlob.transform.position - transform.position).normalized;
                float dist = Vector3.Distance(transform.position, otherBlob.transform.position);
                float proximity = 1f - Mathf.Clamp01(dist / dangerDetectRadius);
                pullDirection += toThreat * proximity;
            }

            if (pullDirection.sqrMagnitude > 0.001f)
            {
                // Subtle pull — should be noticeable but not override player control
                Vector3 pull = pullDirection.normalized * dangerPullStrength * currentDangerIntensity;
                rb.AddForce(pull, ForceMode.Acceleration);
            }
        }

        // ---------- Screen Shake Helpers ----------

        /// <summary>
        /// Request a small screen shake (e.g., when absorbing feed).
        /// </summary>
        private void RequestFeedShake()
        {
            OnScreenShakeRequested?.Invoke(feedShakeIntensity, feedShakeDuration);
        }

        /// <summary>
        /// Request a larger screen shake (e.g., when absorbing another blob).
        /// </summary>
        private void RequestBlobShake()
        {
            OnScreenShakeRequested?.Invoke(blobShakeIntensity, blobShakeDuration);
        }

        // ---------- Collision ----------

        private void OnTriggerEnter(Collider other)
        {
            if (!IsAlive) return;

            // Feed absorption
            Feed feed = other.GetComponent<Feed>();
            if (feed != null && feed.IsActive)
            {
                // Capture color before consuming (renderer may be deactivated)
                Color feedColor = Color.white;
                Renderer feedRend = feed.GetComponent<Renderer>();
                if (feedRend != null && feedRend.material != null)
                {
                    feedColor = feedRend.material.color;
                }

                feed.Consume(false); // Effects handled here for player-specific feedback
                AddSize(feed.NutritionValue);
                ScoreManager.Instance?.AddFeedScore(feed.NutritionValue);
                AudioManager.Instance?.PlayFeedPop(feed.NutritionValue);
                VFXManager.Instance?.PlayFeedBurst(feed.transform.position, feedColor);
                RequestFeedShake();
                HapticHelper.LightImpact();
                return;
            }

            // Power-up pickup
            PowerUpItem powerUp = other.GetComponent<PowerUpItem>();
            if (powerUp != null && powerUp.IsActive)
            {
                powerUp.Collect(this);
                return;
            }

            // Blob collision (uses TryAbsorb lock to prevent race conditions)
            BlobBase otherBlob = other.GetComponent<BlobBase>();
            if (otherBlob != null && otherBlob.IsAlive)
            {
                if (IsGhostActive) return; // Ghost mode: pass through

                if (BlobBase.TryAbsorb(this, otherBlob))
                {
                    // We absorb the other blob
                    var effect = otherBlob.gameObject.AddComponent<AbsorptionEffect>();
                    effect.Initialize(transform, otherBlob.CurrentSize);
                    effect.SetBlobPair(this, otherBlob);
                    otherBlob.GetAbsorbed();
                    AddSize(otherBlob.CurrentSize * 0.8f);
                    ScoreManager.Instance?.AddBlobScore(otherBlob.CurrentSize);
                    AudioManager.Instance?.PlayBlobAbsorb();
                    VFXManager.Instance?.PlayBlobSplash(otherBlob.transform.position, Color.white, otherBlob.CurrentSize);
                    RequestBlobShake();
                    HapticHelper.MediumImpact();

                    // Notify HUD of absorption
                    string victimName = otherBlob.gameObject.name;
                    int scoreGain = Mathf.RoundToInt(otherBlob.CurrentSize * 50f);
                    OnBlobAbsorbed?.Invoke(victimName, scoreGain);
                }
                else if (BlobBase.TryAbsorb(otherBlob, this))
                {
                    // Shield check
                    if (hasShield)
                    {
                        // Fix: use property setter to trigger visual cleanup
                        HasShield = false;
                        BlobBase.ClearAbsorbPair(otherBlob, this);
                        return;
                    }

                    // We get absorbed -> game over
                    string killerName = otherBlob.gameObject.name;
                    OnPlayerDied?.Invoke(killerName);

                    var effect = gameObject.AddComponent<AbsorptionEffect>();
                    effect.Initialize(otherBlob.transform, CurrentSize);
                    effect.SetBlobPair(otherBlob, this);
                    GetAbsorbed();
                    GameManager.Instance.PlayerDied();
                }
                else
                {
                    // Equal-ish size: bounce off each other with momentum transfer
                    TriggerBounceResponse(other.transform.position);
                }
            }
        }

        /// <summary>Handle physical collision with obstacles for bounce deformation</summary>
        private void OnCollisionEnter(Collision collision)
        {
            if (!IsAlive) return;

            // Trigger bounce deformation on obstacle impact
            if (collision.contactCount > 0)
            {
                Vector3 contactNormal = collision.contacts[0].normal;
                float impactSpeed = collision.relativeVelocity.magnitude;

                if (impactSpeed > 2f)
                {
                    TriggerBounceResponse(collision.contacts[0].point);

                    // Reflect velocity with energy loss for realistic bounce
                    Vector3 reflected = Vector3.Reflect(currentVelocity, contactNormal);
                    currentVelocity = reflected * bounceForceMultiplier;
                    rb.velocity = currentVelocity;
                }
            }
        }

        /// <summary>Trigger visible bounce deformation and momentum feedback</summary>
        private void TriggerBounceResponse(Vector3 contactPoint)
        {
            bounceNormal = (transform.position - contactPoint).normalized;
            bounceDeformTimer = bounceDeformDuration;
            HapticHelper.LightImpact();
        }

        public override void GetAbsorbed()
        {
            base.GetAbsorbed();
            HapticHelper.HeavyImpact();
            AudioManager.Instance?.PlayGameOver();
            // Visual deactivation handled by AbsorptionEffect
        }
    }
}
