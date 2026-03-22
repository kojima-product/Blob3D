using UnityEngine;
using Blob3D.Core;
using Blob3D.Player;

namespace Blob3D.Gameplay
{
    /// <summary>
    /// フィールド上のパワーアップアイテム。
    /// 取得するとプレイヤーに一時的な効果を付与する。
    /// </summary>
    public class PowerUpItem : MonoBehaviour
    {
        public enum PowerUpType
        {
            SpeedBoost,   // 移動速度1.5倍
            Shield,       // 1回だけ吸収を防ぐ
            Magnet,       // 周囲のエサを自動吸引
            Ghost,        // 他blobをすり抜ける
            MegaGrowth    // 即座にサイズ2倍
        }

        [Header("PowerUp Settings")]
        [SerializeField] private PowerUpType type;
        [SerializeField] private float duration = 5f;
        [SerializeField] private float rotateSpeed = 60f;
        [SerializeField] private float glowIntensity = 2f;
        [SerializeField] private float glowPulseSpeed = 2f;
        [SerializeField] private float glowPulseMin = 0.5f;
        [SerializeField] private float glowPulseMax = 2f;

        public bool IsActive { get; private set; } = true;
        public PowerUpType Type => type;

        private float phaseOffset;
        private Renderer cachedRenderer;
        private Color baseTypeColor;

        private void Start()
        {
            phaseOffset = Random.Range(0f, Mathf.PI * 2f);
            cachedRenderer = GetComponent<Renderer>();
            baseTypeColor = GetTypeColor();
        }

        private void Update()
        {
            if (!IsActive) return;

            // Slow Y rotation
            transform.Rotate(Vector3.up, rotateSpeed * Time.deltaTime);

            // Pulsing emission glow
            if (cachedRenderer != null)
            {
                float t = Time.time * glowPulseSpeed + phaseOffset;
                float intensity = Mathf.Lerp(glowPulseMin, glowPulseMax, (Mathf.Sin(t) + 1f) * 0.5f);
                cachedRenderer.material.SetColor("_EmissionColor", baseTypeColor * intensity);
            }
        }

        /// <summary>プレイヤーが取得した時</summary>
        public void Collect(BlobController player)
        {
            if (!IsActive) return;
            IsActive = false;

            // VFX: get type color before deactivating
            Color typeColor = GetTypeColor();
            Vector3 pos = transform.position;

            ApplyEffect(player);
            AudioManager.Instance?.PlayPowerUp();
            VFXManager.Instance?.PlayPowerUpAura(pos, typeColor);
            gameObject.SetActive(false);

            // スポーナーに通知
            PowerUpSpawner.Instance?.OnPowerUpCollected(this);
        }

        private void ApplyEffect(BlobController player)
        {
            switch (type)
            {
                case PowerUpType.SpeedBoost:
                    PowerUpEffectManager.Instance?.ApplySpeedBoost(player, duration);
                    break;
                case PowerUpType.Shield:
                    player.HasShield = true;
                    break;
                case PowerUpType.Magnet:
                    PowerUpEffectManager.Instance?.ApplyMagnet(player, duration);
                    break;
                case PowerUpType.Ghost:
                    PowerUpEffectManager.Instance?.ApplyGhost(player, duration);
                    break;
                case PowerUpType.MegaGrowth:
                    player.AddSize(Mathf.Min(player.CurrentSize * 0.5f, 15f));
                    break;
            }
        }

        /// <summary>リスポーン</summary>
        public void Respawn(Vector3 position, PowerUpType newType)
        {
            transform.position = position;
            type = newType;
            IsActive = true;
            gameObject.SetActive(true);

            // Cache renderer and update color
            cachedRenderer = GetComponent<Renderer>();
            baseTypeColor = GetTypeColor();
            UpdateVisual();
        }

        /// <summary>Return the display color for the current power-up type.</summary>
        private Color GetTypeColor()
        {
            return type switch
            {
                PowerUpType.SpeedBoost => new Color(0f, 0.8f, 1f),    // シアン
                PowerUpType.Shield     => new Color(1f, 0.9f, 0.2f),  // 黄
                PowerUpType.Magnet     => new Color(1f, 0.2f, 0.8f),  // マゼンタ
                PowerUpType.Ghost      => new Color(0.8f, 0.8f, 1f, 0.5f), // 白半透明
                PowerUpType.MegaGrowth => new Color(1f, 0.4f, 0.1f),  // オレンジ
                _ => Color.white
            };
        }

        private void UpdateVisual()
        {
            Renderer rend = GetComponent<Renderer>();
            if (rend == null) return;

            Color color = GetTypeColor();
            rend.material.color = color;
            rend.material.SetColor("_EmissionColor", color * glowIntensity);
        }
    }
}
