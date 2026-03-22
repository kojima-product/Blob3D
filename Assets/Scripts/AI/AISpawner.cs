using UnityEngine;
using System.Collections.Generic;
using Blob3D.Core;
using Blob3D.Utils;

namespace Blob3D.AI
{
    /// <summary>
    /// AI blobのスポーン管理。
    /// ObjectPoolを利用し、時間経過に応じてAIの種別バランスを変化させる。
    /// </summary>
    public class AISpawner : MonoBehaviour
    {
        private const string PoolTag = "AIBlob";

        public static AISpawner Instance { get; private set; }

        [Header("Spawn Settings")]
        [SerializeField] private GameObject aiBlobPrefab;
        [SerializeField] private int initialWanderers = 20;
        [SerializeField] private int initialHunters = 8;
        [SerializeField] private int initialCowards = 5;
        [SerializeField] private int initialBosses = 2;

        [Header("Size Settings")]
        [SerializeField] private float minAISize = 1.5f;
        [SerializeField] private float maxAISize = 5f;
        [SerializeField] private float bossMinSize = 10f;
        [SerializeField] private float bossMaxSize = 30f;

        [Header("Respawn")]
        [SerializeField] private float respawnDelay = 3f;

        private List<AIBlobController> activeAIs = new List<AIBlobController>();

        /// <summary>Read-only access for AI flocking and detection</summary>
        public List<AIBlobController> ActiveAIs => activeAIs;
        private int respawnQueue = 0;
        private float respawnTimer;

        private void Awake()
        {
            Instance = this;
        }

        private void Start()
        {
            // Fix: null checks to prevent NullReferenceException if singletons not ready
            if (ObjectPool.Instance == null || GameManager.Instance == null)
            {
                Debug.LogError("AISpawner: ObjectPool or GameManager not available.");
                return;
            }
            int totalAIs = initialWanderers + initialHunters + initialCowards + initialBosses;
            ObjectPool.Instance.RegisterPool(PoolTag, aiBlobPrefab, totalAIs + 10);
            GameManager.Instance.OnGameStart += SpawnInitialAIs;
        }

        private void Update()
        {
            if (respawnQueue <= 0) return;

            respawnTimer -= Time.deltaTime;
            if (respawnTimer <= 0f)
            {
                SpawnRandomAI();
                respawnQueue--;
                respawnTimer = respawnDelay;
            }
        }

        // ---------- スポーン ----------

        private void SpawnInitialAIs()
        {
            activeAIs.Clear();

            for (int i = 0; i < initialWanderers; i++)
                SpawnAI(AIBlobController.AIType.Wanderer);

            for (int i = 0; i < initialHunters; i++)
                SpawnAI(AIBlobController.AIType.Hunter);

            for (int i = 0; i < initialCowards; i++)
                SpawnAI(AIBlobController.AIType.Coward);

            for (int i = 0; i < initialBosses; i++)
                SpawnAI(AIBlobController.AIType.Boss);
        }

        private void SpawnAI(AIBlobController.AIType type)
        {
            Vector3 pos = GameManager.Instance.GetRandomFieldPosition();
            GameObject obj = ObjectPool.Instance.Spawn(PoolTag, pos, Quaternion.identity);
            AIBlobController ai = obj.GetComponent<AIBlobController>();

            float size;
            if (type == AIBlobController.AIType.Boss)
            {
                size = Random.Range(bossMinSize, bossMaxSize);
            }
            else
            {
                size = Random.Range(minAISize, maxAISize);
            }

            ai.Initialize(type, size);

            // ランダムカラー
            Renderer rend = obj.GetComponent<Renderer>();
            if (rend != null)
            {
                rend.material.color = GetColorForType(type);
            }

            activeAIs.Add(ai);
        }

        private void SpawnRandomAI()
        {
            // 時間経過でHunterの割合を増加
            float elapsed = 180f - GameManager.Instance.RemainingTime;
            float hunterChance = Mathf.Lerp(0.2f, 0.5f, elapsed / 180f);

            float roll = Random.value;
            AIBlobController.AIType type;

            if (roll < hunterChance)
                type = AIBlobController.AIType.Hunter;
            else if (roll < hunterChance + 0.3f)
                type = AIBlobController.AIType.Wanderer;
            else
                type = AIBlobController.AIType.Coward;

            SpawnAI(type);
        }

        /// <summary>AIが死亡した時に呼ばれる</summary>
        public void OnAIDied(AIBlobController ai)
        {
            activeAIs.Remove(ai);
            ObjectPool.Instance.Despawn(PoolTag, ai.gameObject);
            respawnQueue++;
            respawnTimer = respawnDelay;
        }

        private Color GetColorForType(AIBlobController.AIType type)
        {
            switch (type)
            {
                case AIBlobController.AIType.Wanderer:
                    return Color.HSVToRGB(Random.value, 0.6f, 0.9f);
                case AIBlobController.AIType.Hunter:
                    return new Color(1f, 0.3f, 0.2f); // 赤系
                case AIBlobController.AIType.Coward:
                    return new Color(0.3f, 0.9f, 0.4f); // 緑系
                case AIBlobController.AIType.Boss:
                    return new Color(0.6f, 0.1f, 0.8f); // 紫系
                default:
                    return Color.white;
            }
        }

        private void OnDestroy()
        {
            if (GameManager.Instance != null)
                GameManager.Instance.OnGameStart -= SpawnInitialAIs;
        }
    }
}
