using UnityEngine;
using System;
using Blob3D.Core;
using Blob3D.Gameplay;
using Blob3D.Utils;

namespace Blob3D.Player
{
    /// <summary>
    /// プレイヤー操作のblobコントローラー。
    /// バーチャルジョイスティック入力 → 移動 → 吸収判定を処理。
    /// Fires screen shake events on absorb for UI feedback.
    /// </summary>
    public class BlobController : BlobBase
    {
        // ---------- 設定 ----------
        [Header("Player Settings")]
        [SerializeField] private float dashSpeedMultiplier = 2f;
        [SerializeField] private float dashDuration = 0.3f;
        [SerializeField] private float dashSizeCost = 0.5f;
        [SerializeField] private float dashCooldown = 1.5f;
        [SerializeField] private float splitForce = 25f;

        [Header("Screen Shake")]
        [SerializeField] private float feedShakeIntensity = 2f;
        [SerializeField] private float feedShakeDuration = 0.1f;
        [SerializeField] private float blobShakeIntensity = 6f;
        [SerializeField] private float blobShakeDuration = 0.25f;

        // ---------- 状態 ----------
        private Vector2 inputDirection;
        private bool isDashing;
        private float dashTimer;
        private float dashCooldownTimer;
        private bool hasShield;
        private ParticleSystem dashTrailPS;

        // ---------- ダッシュクールダウン ----------
        public float DashCooldownRatio => dashCooldown > 0 ? Mathf.Clamp01(dashCooldownTimer / dashCooldown) : 0f;

        // ---------- パワーアップ状態 ----------
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

        // ---------- イベント ----------

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

        // ---------- 参照 ----------
        public static BlobController Instance { get; private set; }

        protected override void Awake()
        {
            base.Awake();
            Instance = this;
        }

        private void OnDestroy()
        {
            // Fix: clear static singleton to prevent stale reference after scene reload
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            if (!IsAlive || GameManager.Instance == null || GameManager.Instance.CurrentState != GameManager.GameState.Playing)
                return;

            UpdateDash();
        }

        private void FixedUpdate()
        {
            if (!IsAlive || GameManager.Instance == null || GameManager.Instance.CurrentState != GameManager.GameState.Playing)
                return;

            Move();
            ApplySquashAndStretch(rb.velocity);

            // フィールド境界クランプ
            transform.position = GameManager.Instance.ClampToField(transform.position);

            ClampYPosition();
        }

        // ---------- 入力 ----------

        /// <summary>UIのジョイスティックから呼ばれる</summary>
        public void SetInput(Vector2 direction)
        {
            inputDirection = direction;
        }

        /// <summary>ダッシュボタンから呼ばれる</summary>
        public void TriggerDash()
        {
            if (dashCooldownTimer > 0f || isDashing) return;
            if (CurrentSize < dashSizeCost * 2f) return; // サイズ不足

            isDashing = true;
            dashTimer = dashDuration;
            dashCooldownTimer = dashCooldown;
            ReduceSize(dashSizeCost);
            AudioManager.Instance?.PlayDash();
            HapticHelper.LightImpact();

            // Dash trail VFX
            var renderer = GetComponent<Renderer>();
            Color trailColor = renderer != null ? renderer.material.color : Color.cyan;
            dashTrailPS = VFXManager.Instance?.PlayDashTrail(transform, trailColor);
        }

        /// <summary>Split mechanic (double tap): eject a temporary sphere blob</summary>
        public void TriggerSplit()
        {
            if (CurrentSize < 2f) return; // Minimum size check

            float halfSize = CurrentSize * 0.5f;
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

            // Determine shoot direction from input or forward
            Vector3 shootDir = transform.forward;
            if (inputDirection.sqrMagnitude > 0.01f)
            {
                shootDir = GetCameraRelativeDirection(inputDirection);
            }
            splitRb.AddForce(shootDir * splitForce, ForceMode.Impulse);

            // Auto-destroy after 5 seconds
            Destroy(splitObj, 5f);
        }

        // ---------- 移動処理 ----------

        private void Move()
        {
            if (inputDirection.sqrMagnitude < 0.01f)
            {
                rb.velocity = Vector3.Lerp(rb.velocity, Vector3.zero, Time.fixedDeltaTime * 5f);
                return;
            }

            // Convert 2D input to camera-relative 3D direction
            Vector3 moveDir = GetCameraRelativeDirection(inputDirection);
            float speed = GetCurrentSpeed() * SpeedMultiplier;

            if (isDashing)
            {
                speed *= dashSpeedMultiplier;
            }

            // Smooth velocity interpolation for responsive yet fluid movement
            Vector3 targetVelocity = moveDir * speed;
            rb.velocity = Vector3.Lerp(rb.velocity, targetVelocity, Time.fixedDeltaTime * 12f);
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

        // ---------- 衝突処理 ----------

        private void OnTriggerEnter(Collider other)
        {
            if (!IsAlive) return;

            // エサの吸収
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

            // パワーアップ取得
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

                    // We get absorbed → game over
                    string killerName = otherBlob.gameObject.name;
                    OnPlayerDied?.Invoke(killerName);

                    var effect = gameObject.AddComponent<AbsorptionEffect>();
                    effect.Initialize(otherBlob.transform, CurrentSize);
                    effect.SetBlobPair(otherBlob, this);
                    GetAbsorbed();
                    GameManager.Instance.PlayerDied();
                }
            }
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
