using UnityEngine;
using Blob3D.Core;

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
        [SerializeField] private float rotateSpeed = 45f;
        [SerializeField] private float pulseSpeed = 2f;
        [SerializeField] private float pulseMin = 0.95f;
        [SerializeField] private float pulseMax = 1.05f;

        public float NutritionValue => nutritionValue;
        public bool IsActive { get; private set; } = true;

        /// <summary>Set nutrition value at runtime (e.g., for split blobs)</summary>
        public void SetNutrition(float value)
        {
            nutritionValue = value;
        }

        private Vector3 baseScale;
        private float phaseOffset;

        private void Start()
        {
            baseScale = transform.localScale;
            phaseOffset = Random.Range(0f, Mathf.PI * 2f);
        }

        private void Update()
        {
            if (!IsActive) return;

            // Gentle spin around Y axis
            transform.Rotate(Vector3.up, rotateSpeed * Time.deltaTime);

            // Subtle scale pulse (breathing effect)
            float t = Time.time * pulseSpeed + phaseOffset;
            float pulse = Mathf.Lerp(pulseMin, pulseMax, (Mathf.Sin(t) + 1f) * 0.5f);
            transform.localScale = baseScale * pulse;
        }

        /// <summary>Called when feed is consumed by a blob</summary>
        /// <param name="playEffects">Set false if the caller handles its own VFX/audio</param>
        public void Consume(bool playEffects = true)
        {
            if (!IsActive) return;
            IsActive = false;

            if (playEffects)
            {
                // Resolve feed color for particle burst
                Color feedColor = Color.white;
                Renderer rend = GetComponent<Renderer>();
                if (rend != null && rend.material != null)
                    feedColor = rend.material.color;

                VFXManager.Instance?.PlayFeedBurst(transform.position, feedColor);
                AudioManager.Instance?.PlayFeedPop(nutritionValue);
            }

            gameObject.SetActive(false);

            // Notify FeedSpawner for respawn scheduling
            FeedSpawner.Instance?.ScheduleRespawn(this);
        }

        /// <summary>リスポーン時の再初期化</summary>
        public void Respawn(Vector3 position)
        {
            transform.position = position;
            transform.localScale = baseScale;
            IsActive = true;
            gameObject.SetActive(true);
        }
    }
}
