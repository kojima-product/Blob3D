using UnityEngine;
using System;
using System.Collections.Generic;

namespace Blob3D.Core
{
    /// <summary>
    /// プレイヤーとAI共通のblob基底クラス。
    /// サイズ管理、吸収判定、ビジュアルスケーリングを担当。
    /// </summary>
    [RequireComponent(typeof(SphereCollider))]
    [RequireComponent(typeof(Rigidbody))]
    public abstract class BlobBase : MonoBehaviour
    {
        // ---------- Absorption lock (prevents race conditions) ----------
        private static readonly HashSet<long> absorbingPairs = new HashSet<long>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            absorbingPairs.Clear();
        }

        private bool isBeingAbsorbed;

        /// <summary>
        /// Thread-safe absorption attempt. Returns true only if this is the first
        /// valid processing of this collision pair and the absorber is bigger.
        /// Prevents both OnTriggerEnter callbacks from processing the same collision.
        /// </summary>
        public static bool TryAbsorb(BlobBase absorber, BlobBase victim)
        {
            if (absorber == null || victim == null) return false;
            if (!absorber.IsAlive || !victim.IsAlive) return false;
            if (victim.isBeingAbsorbed) return false;

            // Only the bigger blob can absorb (10% size advantage required)
            if (absorber.CurrentSize <= victim.CurrentSize * 1.1f) return false;

            // Build a unique pair key from instance IDs (order-independent)
            int idA = absorber.GetInstanceID();
            int idB = victim.GetInstanceID();
            long pairKey = idA < idB
                ? ((long)idA << 32) | (long)(uint)idB
                : ((long)idB << 32) | (long)(uint)idA;

            // Only the first caller wins
            if (!absorbingPairs.Add(pairKey)) return false;

            victim.isBeingAbsorbed = true;
            return true;
        }

        /// <summary>Remove pair key after absorption completes (cleanup)</summary>
        public static void ClearAbsorbPair(BlobBase a, BlobBase b)
        {
            // Fix: null check — objects may be destroyed before cleanup runs
            if (a == null || b == null) return;
            int idA = a.GetInstanceID();
            int idB = b.GetInstanceID();
            long pairKey = idA < idB
                ? ((long)idA << 32) | (long)(uint)idB
                : ((long)idB << 32) | (long)(uint)idA;
            absorbingPairs.Remove(pairKey);
        }

        // ---------- 設定 ----------
        [Header("Blob Settings")]
        [SerializeField] protected float baseSpeed = 5.5f;    // Calm, controllable speed
        [SerializeField] protected float minSpeed = 3f;
        [SerializeField] private float initialSize = 2f;
        [SerializeField] private float sizeToScaleRatio = 0.15f; // size → localScale conversion

        [Header("Jiggle Physics")]
        [SerializeField] private float jiggleStiffness = 25f;    // Spring constant (higher = snappier return)
        [SerializeField] private float jiggleDamping = 4f;       // Damping (higher = less oscillation)
        [SerializeField] private float jiggleInertia = 0.12f;    // How much acceleration affects jiggle
        [SerializeField] private float maxJiggle = 0.35f;        // Max deformation ratio
        [SerializeField] private float absorbPulseMagnitude = 0.3f;  // Scale bump on absorb (gulp effect)
        [SerializeField] private float absorbPulseDuration = 0.5f;   // Duration of absorb pulse

        [Header("Secondary Oscillation")]
        [SerializeField] private float secondaryStiffness = 40f;  // Higher freq secondary spring
        [SerializeField] private float secondaryDamping = 6f;
        [SerializeField] private float secondaryInertia = 0.05f;  // Lighter response
        [SerializeField] private float maxSecondaryJiggle = 0.15f;

        [Header("Landing Squash")]
        [SerializeField] private float landingSquashThreshold = 8f;   // Velocity change threshold to trigger squash
        [SerializeField] private float landingSquashMagnitude = 0.25f; // Max squash deformation
        [SerializeField] private float landingSquashDuration = 0.35f;  // Duration of squash recovery

        // ---------- 状態 ----------
        public float CurrentSize { get; protected set; }
        public bool IsAlive { get; protected set; } = true;

        // Jiggle physics state (spring-damper system)
        private Vector3 jiggleDisplacement;  // Current spring displacement
        private Vector3 jiggleVelocity;      // Current spring velocity
        private Vector3 previousVelocity;    // For computing acceleration

        // Secondary oscillation state (higher freq detail wobble)
        private Vector3 secondaryDisplacement;
        private Vector3 secondaryVelocity;

        // Landing squash state
        private float landingSquashTimer;
        private float landingSquashAmount;
        private float previousYVelocity; // Track Y velocity for landing detection

        // Surface wave propagation state (triggered by absorption)
        private float waveEnergy;
        private float wavePhase;
        private const float WaveDecayRate = 5f;
        private const float WaveFrequency = 12f;
        private const float WaveAmplitudeScale = 0.08f;

        // Absorb pulse state
        private float absorbPulseTimer;
        private float absorbPulseScale;

        // ---------- コンポーネント ----------
        protected Rigidbody rb;
        protected SphereCollider sphereCollider;

        // ---------- イベント ----------
        public event Action<float> OnSizeChanged;   // 新サイズ
        public event Action OnAbsorbed;              // 吸収された

        // ---------- ライフサイクル ----------
        protected virtual void Awake()
        {
            rb = GetComponent<Rigidbody>();
            sphereCollider = GetComponent<SphereCollider>();

            rb.useGravity = false;
            rb.constraints = RigidbodyConstraints.FreezeRotation | RigidbodyConstraints.FreezePositionY;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            // Ensure trigger collider exists for absorption detection
            sphereCollider.isTrigger = true;

            // Add a second non-trigger collider for physical collision with obstacles/ground
            EnsurePhysicsCollider();
        }

        protected virtual void Start()
        {
            SetSize(initialSize);
        }

        /// <summary>Add a child object with a non-trigger SphereCollider for obstacle collision</summary>
        private void EnsurePhysicsCollider()
        {
            Transform existing = transform.Find("PhysicsCollider");
            if (existing != null) return;

            GameObject colliderChild = new GameObject("PhysicsCollider");
            colliderChild.transform.SetParent(transform, false);
            colliderChild.transform.localPosition = Vector3.zero;
            colliderChild.layer = gameObject.layer;

            SphereCollider physCol = colliderChild.AddComponent<SphereCollider>();
            physCol.isTrigger = false;
            physCol.radius = 0.5f; // Matches unit sphere
        }

        // ---------- サイズ管理 ----------

        /// <summary>サイズを直接設定する</summary>
        public void SetSize(float newSize)
        {
            CurrentSize = Mathf.Max(newSize, 0.1f);
            float scale = CurrentSize * sizeToScaleRatio;
            transform.localScale = Vector3.one * scale;

            // Lower center of mass: visual center accounts for gravity squish (85% of normal height)
            Vector3 pos = transform.position;
            pos.y = scale * 0.5f * 0.85f;
            transform.position = pos;

            OnSizeChanged?.Invoke(CurrentSize);
        }

        /// <summary>Add size when absorbing feed or another blob</summary>
        public void AddSize(float amount)
        {
            SetSize(CurrentSize + amount);
            TriggerAbsorbPulse();
            TriggerSurfaceWave(amount);
        }

        /// <summary>Trigger a visual scale pulse when absorbing something</summary>
        private void TriggerAbsorbPulse()
        {
            absorbPulseTimer = absorbPulseDuration;
            absorbPulseScale = absorbPulseMagnitude;
        }

        /// <summary>Trigger surface wave propagation after absorption</summary>
        private void TriggerSurfaceWave(float amount)
        {
            // Wave energy proportional to absorbed amount, capped for visual stability
            waveEnergy = Mathf.Min(waveEnergy + amount * WaveAmplitudeScale, WaveAmplitudeScale * 3f);
            wavePhase = 0f;
        }

        /// <summary>ダッシュ等によるサイズ減少</summary>
        public void ReduceSize(float amount)
        {
            SetSize(CurrentSize - amount);
        }

        /// <summary>サイズに応じた移動速度を算出</summary>
        public float GetCurrentSpeed()
        {
            // サイズが大きくなるほど遅くなる（対数的に減衰）
            float speedFactor = 1f / (1f + Mathf.Log(1f + CurrentSize * 0.1f));
            return Mathf.Max(baseSpeed * speedFactor, minSpeed);
        }

        /// <summary>Keep blob above ground level. Call in FixedUpdate.</summary>
        protected void ClampYPosition()
        {
            // Blob sits ON ground — Y position accounts for gravity-squished shape
            float visualHeight = CurrentSize * sizeToScaleRatio * (1f - 0.15f); // Account for gravity squish
            float radius = visualHeight * 0.5f;
            Vector3 pos = transform.position;
            if (pos.y < radius)
            {
                pos.y = radius;
                transform.position = pos;
            }
        }

        // ---------- 吸収判定 ----------

        /// <summary>相手を吸収できるか判定</summary>
        public bool CanAbsorb(BlobBase other)
        {
            if (other == null || !other.IsAlive || !IsAlive) return false;
            return CurrentSize > other.CurrentSize * 1.1f; // 10% larger to absorb (lower threshold for more action)
        }

        /// <summary>吸収された時の処理 (visual deactivation is handled by AbsorptionEffect)</summary>
        public virtual void GetAbsorbed()
        {
            IsAlive = false;
            isBeingAbsorbed = true;
            OnAbsorbed?.Invoke();
        }

        /// <summary>Reset absorption state and physics for pool reuse</summary>
        public virtual void ResetForReuse()
        {
            IsAlive = true;
            isBeingAbsorbed = false;

            // Reset all physics state for clean reuse
            jiggleDisplacement = Vector3.zero;
            jiggleVelocity = Vector3.zero;
            previousVelocity = Vector3.zero;
            secondaryDisplacement = Vector3.zero;
            secondaryVelocity = Vector3.zero;
            landingSquashTimer = 0f;
            landingSquashAmount = 0f;
            previousYVelocity = 0f;
            waveEnergy = 0f;
            wavePhase = 0f;
            absorbPulseTimer = 0f;
            absorbPulseScale = 0f;
        }

        // ---------- Jiggle Physics (Multi-Layer Spring-Damper Deformation) ----------

        /// <summary>
        /// Multi-layer physics-based jiggle deformation simulating a soft-body slime.
        ///
        /// Layer 1 (Primary): Spring-damper driven by movement acceleration.
        ///   Low frequency, high amplitude — the main squash/stretch from inertia.
        /// Layer 2 (Secondary): Damped harmonic wobble driven by primary jiggle velocity.
        ///   Higher frequency, lower amplitude — residual oscillation that makes the
        ///   blob feel alive and gelatinous.
        /// Layer 3 (Impact): Vertical squash on landing or collision with spring recovery.
        ///   Triggered by sudden velocity deceleration, bounces back naturally.
        /// Layer 4 (Surface wave): Radial ripple after absorption events.
        ///   Decaying sinusoidal wave that propagates across the surface.
        ///
        /// All layers are volume-preserving: squashing one axis expands the others.
        /// </summary>
        protected void ApplySquashAndStretch(Vector3 velocity)
        {
            float dt = Time.deltaTime;
            if (dt < 0.0001f) return;

            float baseScale = CurrentSize * sizeToScaleRatio;

            // === Absorb pulse (damped sine for natural gulp) ===
            if (absorbPulseTimer > 0f)
            {
                absorbPulseTimer -= dt;
                float t = 1f - Mathf.Clamp01(absorbPulseTimer / absorbPulseDuration);
                absorbPulseScale = absorbPulseMagnitude * Mathf.Sin(t * Mathf.PI) * Mathf.Exp(-t * 2f);
            }
            else
            {
                absorbPulseScale = Mathf.Lerp(absorbPulseScale, 0f, dt * 10f);
            }

            // === Compute acceleration (change in velocity) ===
            Vector3 acceleration = (velocity - previousVelocity) / dt;
            previousVelocity = velocity;
            acceleration = Vector3.ClampMagnitude(acceleration, 100f);

            // === Layer 1: Primary spring-damper ===
            // External force: acceleration pushes the jiggle mass opposite to movement (inertia)
            Vector3 primaryForce = -acceleration * jiggleInertia;
            Vector3 primarySpring = -jiggleStiffness * jiggleDisplacement;
            Vector3 primaryDamp = -jiggleDamping * jiggleVelocity;

            // Semi-implicit Euler integration
            jiggleVelocity += (primarySpring + primaryDamp + primaryForce) * dt;
            jiggleDisplacement += jiggleVelocity * dt;
            jiggleDisplacement = Vector3.ClampMagnitude(jiggleDisplacement, maxJiggle);

            // === Layer 2: Secondary wobble (damped harmonic oscillation) ===
            // Driven by primary jiggle velocity — creates residual high-frequency wobble
            Vector3 secForce = -jiggleVelocity * secondaryInertia;
            Vector3 secSpring = -secondaryStiffness * secondaryDisplacement;
            Vector3 secDamp = -secondaryDamping * secondaryVelocity;

            secondaryVelocity += (secSpring + secDamp + secForce) * dt;
            secondaryDisplacement += secondaryVelocity * dt;
            secondaryDisplacement = Vector3.ClampMagnitude(secondaryDisplacement, maxSecondaryJiggle);

            // === Layer 3: Impact deformation ===
            // Detect ground impact from Y velocity change
            float currentYVel = velocity.y;
            float yDecel = previousYVelocity - currentYVel;
            previousYVelocity = currentYVel;

            // Trigger impact squash on sudden vertical deceleration (landing)
            if (yDecel > landingSquashThreshold)
            {
                float impactStrength = Mathf.Clamp01(yDecel / 20f);
                landingSquashAmount = Mathf.Max(landingSquashAmount, impactStrength * landingSquashMagnitude);
                landingSquashTimer = landingSquashDuration;
            }

            // Detect horizontal collision impact from sudden deceleration
            float horizontalSpeed = new Vector2(velocity.x, velocity.z).magnitude;
            float prevHorizontalSpeed = new Vector2(previousVelocity.x, previousVelocity.z).magnitude;
            float horizontalDecel = prevHorizontalSpeed - horizontalSpeed;
            if (horizontalDecel > landingSquashThreshold * 0.5f)
            {
                float hImpact = Mathf.Clamp01(horizontalDecel / 15f);
                landingSquashAmount = Mathf.Max(landingSquashAmount, hImpact * landingSquashMagnitude * 0.6f);
                landingSquashTimer = landingSquashDuration;
            }

            // Impact recovery: spring-based bounce-back with energy decay per bounce
            // Each oscillation retains ~70% of previous energy for natural damping
            if (landingSquashTimer > 0f)
            {
                landingSquashTimer -= dt;
                float t = 1f - Mathf.Clamp01(landingSquashTimer / landingSquashDuration);
                // Damped oscillation: decaying cosine for natural bounce
                float bouncePhase = t * Mathf.PI * 3f; // ~1.5 full oscillations during recovery
                // Energy decay: 0.7^(bounceCount) — each half-cycle loses 30% energy
                float bounceCount = bouncePhase / Mathf.PI;
                float energyDecay = Mathf.Pow(0.7f, bounceCount);
                float dampedBounce = Mathf.Cos(bouncePhase) * energyDecay;
                landingSquashAmount *= Mathf.Abs(dampedBounce);
            }
            else
            {
                landingSquashAmount = Mathf.Lerp(landingSquashAmount, 0f, dt * 8f);
            }

            // === Layer 4: Surface wave propagation ===
            float waveContribution = 0f;
            if (waveEnergy > 0.001f)
            {
                wavePhase += dt * WaveFrequency;
                waveContribution = waveEnergy * Mathf.Sin(wavePhase);
                // Exponential decay of wave energy
                waveEnergy *= Mathf.Exp(-WaveDecayRate * dt);
            }

            // === Combine all layers into scale deformation ===
            // Total jiggle: primary + attenuated secondary
            Vector3 totalJiggle = jiggleDisplacement + secondaryDisplacement * 0.5f;

            // Decompose into movement-aligned and perpendicular components
            Vector3 moveDir = velocity.sqrMagnitude > 0.01f ? velocity.normalized : transform.forward;
            float axialJiggle = Vector3.Dot(totalJiggle, moveDir);
            Vector3 lateralJiggle = totalJiggle - axialJiggle * moveDir;
            float lateralMag = lateralJiggle.magnitude;

            // Movement-direction stretch (volume-preserving)
            float stretchX = 1f - axialJiggle;
            float squashYZ = 1f / Mathf.Sqrt(Mathf.Max(stretchX, 0.3f));

            // Lateral wobble contribution to Y/Z axes
            float wobbleY = squashYZ + lateralMag * 0.5f;
            float wobbleZ = squashYZ - lateralMag * 0.3f;

            // Impact deformation (squash Y, expand XZ for volume preservation)
            float impactY = 1f - landingSquashAmount;
            float impactXZ = 1f / Mathf.Sqrt(Mathf.Max(impactY, 0.4f));

            // Surface wave (uniform oscillation)
            float waveFactor = 1f + waveContribution;

            // Absorb pulse (uniform expansion with damped sine)
            float pulse = 1f + absorbPulseScale;

            // Final scale with all effects combined
            Vector3 targetScale = new Vector3(
                baseScale * stretchX * impactXZ * pulse * waveFactor,
                baseScale * wobbleY * impactY * pulse * waveFactor,
                baseScale * wobbleZ * impactXZ * pulse * waveFactor
            );

            // Ensure no axis goes below minimum
            float minScale = baseScale * 0.4f;
            targetScale.x = Mathf.Max(targetScale.x, minScale);
            targetScale.y = Mathf.Max(targetScale.y, minScale);
            targetScale.z = Mathf.Max(targetScale.z, minScale);

            // Gravity squish: slimes are wider at base, flatter on top
            // Larger blobs squish more under their own weight
            float sizeSquishFactor = Mathf.Clamp01(CurrentSize / 30f);
            float gravitySquish = Mathf.Lerp(0.08f, 0.25f, sizeSquishFactor); // 8%-25% flattening
            float yScaleVal = targetScale.y * (1f - gravitySquish);
            float xzExpand = 1f + gravitySquish * 0.5f; // Volume preservation
            targetScale.x *= xzExpand;
            targetScale.y = yScaleVal;
            targetScale.z *= xzExpand;

            // Apply scale (spring system provides natural smoothing — no extra lerp needed)
            transform.localScale = targetScale;

            // === Rotation: align stretch axis with movement direction ===
            if (velocity.sqrMagnitude > 0.1f)
            {
                Quaternion targetRot = Quaternion.LookRotation(moveDir, Vector3.up);
                float rotLerp = 1f - Mathf.Exp(-8f * dt);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, rotLerp);
            }
        }
    }
}
