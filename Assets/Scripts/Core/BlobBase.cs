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

        // ---------- 状態 ----------
        public float CurrentSize { get; protected set; }
        public bool IsAlive { get; protected set; } = true;

        // Jiggle physics state (spring-damper system)
        private Vector3 jiggleDisplacement;  // Current spring displacement
        private Vector3 jiggleVelocity;      // Current spring velocity
        private Vector3 previousVelocity;    // For computing acceleration

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

        /// <summary>エサやblobを吸収した時のサイズ加算</summary>
        public void AddSize(float amount)
        {
            SetSize(CurrentSize + amount);
            TriggerAbsorbPulse();
        }

        /// <summary>Trigger a visual scale pulse when absorbing something</summary>
        private void TriggerAbsorbPulse()
        {
            absorbPulseTimer = absorbPulseDuration;
            absorbPulseScale = absorbPulseMagnitude;
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

        // ---------- Jiggle Physics (Spring-Damper Deformation) ----------

        /// <summary>
        /// Physics-based jiggle deformation simulating a soft-body slime.
        /// Uses a spring-damper system driven by movement acceleration:
        /// - Acceleration pushes the jiggle spring (inertia: blob resists direction change)
        /// - Spring pulls back toward rest shape (stiffness)
        /// - Damping prevents infinite oscillation
        /// - Result: blob squishes opposite to movement, then wobbles back like jelly
        /// </summary>
        protected void ApplySquashAndStretch(Vector3 velocity)
        {
            float dt = Time.deltaTime;
            if (dt < 0.0001f) return;

            float baseScale = CurrentSize * sizeToScaleRatio;

            // --- Absorb pulse ---
            if (absorbPulseTimer > 0f)
            {
                absorbPulseTimer -= dt;
                float t = 1f - Mathf.Clamp01(absorbPulseTimer / absorbPulseDuration);
                absorbPulseScale = absorbPulseMagnitude * Mathf.Sin(t * Mathf.PI);
            }
            else
            {
                absorbPulseScale = 0f;
            }

            // --- Compute acceleration (change in velocity) ---
            Vector3 acceleration = (velocity - previousVelocity) / dt;
            previousVelocity = velocity;

            // Clamp acceleration to prevent explosions on sudden teleports
            acceleration = Vector3.ClampMagnitude(acceleration, 100f);

            // --- Spring-damper simulation ---
            // External force: acceleration pushes the jiggle mass in the opposite direction (inertia)
            Vector3 externalForce = -acceleration * jiggleInertia;

            // Spring restoring force (Hooke's law): pulls displacement back to zero
            Vector3 springForce = -jiggleStiffness * jiggleDisplacement;

            // Damping force: resists velocity
            Vector3 dampingForce = -jiggleDamping * jiggleVelocity;

            // Integrate (semi-implicit Euler)
            jiggleVelocity += (springForce + dampingForce + externalForce) * dt;
            jiggleDisplacement += jiggleVelocity * dt;

            // Clamp displacement to prevent extreme deformation
            jiggleDisplacement = Vector3.ClampMagnitude(jiggleDisplacement, maxJiggle);

            // --- Convert jiggle displacement to scale deformation ---
            // Jiggle displacement in world space → project onto local axes
            // Movement direction gets stretched, perpendicular directions get squished (volume preservation)

            // Decompose jiggle into movement-aligned and perpendicular components
            Vector3 moveDir = velocity.sqrMagnitude > 0.01f ? velocity.normalized : transform.forward;

            // Project jiggle onto movement axis
            float axialJiggle = Vector3.Dot(jiggleDisplacement, moveDir);

            // Perpendicular jiggle (for lateral wobble)
            Vector3 lateralJiggle = jiggleDisplacement - axialJiggle * moveDir;
            float lateralMag = lateralJiggle.magnitude;

            // Movement-direction stretch: positive jiggle = squash (blob compressed in movement dir)
            float stretchX = 1f - axialJiggle;        // Along movement
            // Volume-preserving perpendicular expansion
            float squashYZ = 1f / Mathf.Sqrt(Mathf.Max(stretchX, 0.3f));

            // Add lateral wobble to Y axis (up-down jiggle when turning)
            float wobbleY = squashYZ + lateralMag * 0.5f;
            float wobbleZ = squashYZ - lateralMag * 0.3f;

            // Add absorb pulse (uniform expansion)
            float pulse = 1f + absorbPulseScale;

            // Final scale with all effects combined
            Vector3 targetScale = new Vector3(
                baseScale * stretchX * pulse,
                baseScale * wobbleY * pulse,
                baseScale * wobbleZ * pulse
            );

            // Ensure no axis goes below minimum
            targetScale.x = Mathf.Max(targetScale.x, baseScale * 0.5f);
            targetScale.y = Mathf.Max(targetScale.y, baseScale * 0.5f);
            targetScale.z = Mathf.Max(targetScale.z, baseScale * 0.5f);

            // Gravity squish: slimes are wider at base, flatter on top
            float gravitySquish = 0.15f; // 15% flattening
            float yScale = targetScale.y * (1f - gravitySquish);
            float xzExpand = 1f + gravitySquish * 0.5f; // Volume preservation
            targetScale.x *= xzExpand;
            targetScale.y = yScale;
            targetScale.z *= xzExpand;

            // Apply scale (no extra smoothing needed — spring provides smoothness)
            transform.localScale = targetScale;

            // --- Rotation: align stretch axis with movement direction ---
            if (velocity.sqrMagnitude > 0.1f)
            {
                Quaternion targetRot = Quaternion.LookRotation(moveDir, Vector3.up);
                float rotLerp = 1f - Mathf.Exp(-8f * dt);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, rotLerp);
            }
        }
    }
}
