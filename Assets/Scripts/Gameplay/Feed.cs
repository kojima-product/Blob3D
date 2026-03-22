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

        public float NutritionValue => nutritionValue;
        public bool IsActive { get; private set; } = true;

        /// <summary>Set nutrition value at runtime (e.g., for split blobs)</summary>
        public void SetNutrition(float value)
        {
            nutritionValue = value;
        }

        private Vector3 basePosition;
        private float bobOffset;

        private void Start()
        {
            basePosition = transform.position;
            bobOffset = Random.Range(0f, Mathf.PI * 2f); // ランダムオフセットで同期を避ける
        }

        private void Update()
        {
            if (!IsActive) return;

            // 上下にフワフワ浮遊
            float y = basePosition.y + Mathf.Sin(Time.time * bobSpeed + bobOffset) * bobHeight;
            transform.position = new Vector3(basePosition.x, y, basePosition.z);

            // 回転
            transform.Rotate(Vector3.up, rotateSpeed * Time.deltaTime);
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
            IsActive = true;
            gameObject.SetActive(true);
        }
    }
}
