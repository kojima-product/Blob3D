using UnityEngine;

namespace Blob3D.Gameplay
{
    /// <summary>
    /// フィールド上に散在するエサ。
    /// 吸収されると非アクティブになり、FeedSpawnerによってリスポーンされる。
    /// </summary>
    public class Feed : MonoBehaviour
    {
        [Header("Feed Settings")]
        [SerializeField] private float nutritionValue = 0.5f;
        [SerializeField] private float bobSpeed = 2f;
        [SerializeField] private float bobHeight = 0.3f;
        [SerializeField] private float rotateSpeed = 90f;
        [SerializeField] private float pulseSpeed = 3f;
        [SerializeField] private float pulseAmount = 0.15f;

        public float NutritionValue => nutritionValue;
        public bool IsActive { get; private set; } = true;

        /// <summary>Set nutrition value at runtime (e.g., for split blobs)</summary>
        public void SetNutrition(float value)
        {
            nutritionValue = value;
        }

        private Vector3 basePosition;
        private Vector3 baseScale;
        private float bobOffset;

        private void Start()
        {
            basePosition = transform.position;
            baseScale = transform.localScale;
            bobOffset = Random.Range(0f, Mathf.PI * 2f);
        }

        private void Update()
        {
            if (!IsActive) return;

            float t = Time.time + bobOffset;

            // Floating bob animation
            float y = basePosition.y + Mathf.Sin(t * bobSpeed) * bobHeight;
            transform.position = new Vector3(basePosition.x, y, basePosition.z);

            // Rotation
            transform.Rotate(Vector3.up, rotateSpeed * Time.deltaTime);

            // Organic pulse animation (size breathing)
            float pulse = 1f + Mathf.Sin(t * pulseSpeed) * pulseAmount;
            transform.localScale = baseScale * pulse;
        }

        /// <summary>エサが食べられた時</summary>
        public void Consume()
        {
            if (!IsActive) return;
            IsActive = false;

            // TODO: パーティクルエフェクト＆SE
            gameObject.SetActive(false);

            // FeedSpawnerに通知してリスポーンスケジュール
            FeedSpawner.Instance?.ScheduleRespawn(this);
        }

        /// <summary>リスポーン時の再初期化</summary>
        public void Respawn(Vector3 position)
        {
            transform.position = position;
            basePosition = position;
            transform.localScale = baseScale;
            IsActive = true;
            gameObject.SetActive(true);
        }
    }
}
