using UnityEngine;
using System.Collections.Generic;
using Blob3D.Core;
using Blob3D.Gameplay;
using Blob3D.Player;
using Blob3D.Utils;

namespace Blob3D.AI
{
    /// <summary>
    /// AI blob spawn manager.
    /// Uses ObjectPool and adjusts AI type balance over time.
    /// Scales respawned AI size based on player progress for dynamic challenge.
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

        [Header("Dynamic Difficulty")]
        [SerializeField] private float respawnSizeScaling = 0.5f; // How much player size influences respawn size

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

        // ---------- Spawn ----------

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
            SpawnAI(type, -1f);
        }

        /// <summary>
        /// Spawn an AI blob of the given type with optional size override.
        /// If sizeOverride is negative, uses default random size for the type.
        /// </summary>
        private void SpawnAI(AIBlobController.AIType type, float sizeOverride)
        {
            Vector3 pos = GameManager.Instance.GetRandomFieldPosition();
            GameObject obj = ObjectPool.Instance.Spawn(PoolTag, pos, Quaternion.identity);
            AIBlobController ai = obj.GetComponent<AIBlobController>();

            float size;
            if (sizeOverride > 0f)
            {
                size = sizeOverride;
            }
            else if (type == AIBlobController.AIType.Boss)
            {
                size = Random.Range(bossMinSize, bossMaxSize);
            }
            else
            {
                size = Random.Range(minAISize, maxAISize);
            }

            ai.Initialize(type, size);

            // Random color per type
            Renderer rend = obj.GetComponent<Renderer>();
            if (rend != null)
            {
                rend.material.color = GetColorForType(type);
            }

            activeAIs.Add(ai);
        }

        private void SpawnRandomAI()
        {
            // Fix: use ElapsedTime for Survival/TimeAttack; clamp to prevent NaN
            float elapsed;
            if (GameManager.Instance.CurrentMode == GameManager.GameMode.Classic)
            {
                elapsed = Mathf.Max(0f, 180f - GameManager.Instance.RemainingTime);
            }
            else
            {
                elapsed = GameManager.Instance.ElapsedTime;
            }
            float hunterChance = Mathf.Lerp(0.2f, 0.5f, Mathf.Clamp01(elapsed / 180f));

            float roll = Random.value;
            AIBlobController.AIType type;

            if (roll < hunterChance)
                type = AIBlobController.AIType.Hunter;
            else if (roll < hunterChance + 0.3f)
                type = AIBlobController.AIType.Wanderer;
            else
                type = AIBlobController.AIType.Coward;

            // Dynamic size scaling: respawned AI size adjusts based on player size
            // so the game stays challenging as the player grows
            float dynamicSize = CalculateDynamicSpawnSize(type);
            SpawnAI(type, dynamicSize);
        }

        /// <summary>
        /// Calculate spawn size that scales with player progress.
        /// Ensures respawned AIs remain relevant as the player grows.
        /// </summary>
        private float CalculateDynamicSpawnSize(AIBlobController.AIType type)
        {
            float baseSize = Random.Range(minAISize, maxAISize);

            // Scale based on player size if player exists
            if (BlobController.Instance != null &&
                BlobController.Instance.IsAlive)
            {
                float playerSize = BlobController.Instance.CurrentSize;
                // Respawned AIs are a fraction of player size, creating varied encounters
                float scaledSize = playerSize * Random.Range(0.3f, 0.8f) * respawnSizeScaling;
                baseSize = Mathf.Max(baseSize, scaledSize);

                // Hunters can occasionally spawn as credible threats
                if (type == AIBlobController.AIType.Hunter && Random.value < 0.2f)
                {
                    baseSize = Mathf.Max(baseSize, playerSize * Random.Range(0.7f, 1.0f));
                }
            }

            // Cap to prevent excessively large non-boss AI
            baseSize = Mathf.Clamp(baseSize, minAISize, maxAISize * 3f);
            return baseSize;
        }

        /// <summary>Called when an AI blob dies</summary>
        public void OnAIDied(AIBlobController ai)
        {
            activeAIs.Remove(ai);
            respawnQueue++;
            respawnTimer = respawnDelay;

            // Fix: delay despawn so AbsorptionEffect animation can finish.
            // AbsorptionEffect duration is ~0.3-0.7s; use 1s to be safe.
            StartCoroutine(DespawnAfterAbsorption(ai, 1f));
        }

        private System.Collections.IEnumerator DespawnAfterAbsorption(AIBlobController ai, float delay)
        {
            yield return new WaitForSeconds(delay);

            // Fix: check if AI object still exists (may have been destroyed during scene reload)
            if (ai == null) yield break;

            // Remove AbsorptionEffect component before returning to pool
            var absEffect = ai.GetComponent<AbsorptionEffect>();
            if (absEffect != null) Destroy(absEffect);

            // Re-enable collider and reset rigidbody for pool reuse
            var col = ai.GetComponent<Collider>();
            if (col != null) col.enabled = true;
            var rb = ai.GetComponent<Rigidbody>();
            if (rb != null) rb.isKinematic = false;

            // Reset alive state for pool reuse
            ai.ResetForReuse();

            ObjectPool.Instance?.Despawn(PoolTag, ai.gameObject);
        }

        private Color GetColorForType(AIBlobController.AIType type)
        {
            switch (type)
            {
                case AIBlobController.AIType.Wanderer:
                    return Color.HSVToRGB(Random.value, 0.6f, 0.9f);
                case AIBlobController.AIType.Hunter:
                    return new Color(1f, 0.3f, 0.2f); // Red tones
                case AIBlobController.AIType.Coward:
                    return new Color(0.3f, 0.9f, 0.4f); // Green tones
                case AIBlobController.AIType.Boss:
                    return new Color(0.6f, 0.1f, 0.8f); // Purple tones
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
