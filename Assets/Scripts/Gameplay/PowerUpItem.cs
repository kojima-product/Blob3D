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
        [SerializeField] private float bobSpeed = 3f;
        [SerializeField] private float bobHeight = 0.5f;
        [SerializeField] private float rotateSpeed = 120f;
        [SerializeField] private float glowIntensity = 2f;

        public bool IsActive { get; private set; } = true;
        public PowerUpType Type => type;

        private Vector3 basePosition;
        private float bobOffset;

        private void Start()
        {
            basePosition = transform.position;
            bobOffset = Random.Range(0f, Mathf.PI * 2f);
        }

        private void Update()
        {
            if (!IsActive) return;

            // 浮遊アニメーション
            float y = basePosition.y + Mathf.Sin(Time.time * bobSpeed + bobOffset) * bobHeight;
            transform.position = new Vector3(basePosition.x, y, basePosition.z);
            transform.Rotate(Vector3.up, rotateSpeed * Time.deltaTime);
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
            basePosition = position;
            type = newType;
            IsActive = true;
            gameObject.SetActive(true);

            // 色を更新
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
