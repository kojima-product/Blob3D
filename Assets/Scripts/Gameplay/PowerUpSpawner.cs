using UnityEngine;
using System.Collections.Generic;
using Blob3D.Core;
using Blob3D.Utils;

namespace Blob3D.Gameplay
{
    /// <summary>
    /// パワーアップアイテムのスポーン管理。
    /// ObjectPoolを利用して効率的に管理する。
    /// </summary>
    public class PowerUpSpawner : MonoBehaviour
    {
        private const string PoolTag = "PowerUp";

        public static PowerUpSpawner Instance { get; private set; }

        [Header("Spawn Settings")]
        [SerializeField] private GameObject powerUpPrefab;
        [SerializeField] private int maxPowerUps = 8;
        [SerializeField] private float spawnInterval = 15f;

        [Header("出現確率 (合計1.0)")]
        [SerializeField] private float speedBoostWeight = 0.30f;
        [SerializeField] private float shieldWeight = 0.25f;
        [SerializeField] private float magnetWeight = 0.20f;
        [SerializeField] private float ghostWeight = 0.15f;
        [SerializeField] private float megaGrowthWeight = 0.10f;

        private List<PowerUpItem> activePowerUps = new List<PowerUpItem>();
        private float spawnTimer;

        private void Awake()
        {
            Instance = this;
        }

        private void Start()
        {
            ObjectPool.Instance.RegisterPool(PoolTag, powerUpPrefab, maxPowerUps + 5);
            GameManager.Instance.OnGameStart += OnGameStart;
        }

        private void OnGameStart()
        {
            spawnTimer = spawnInterval * 0.5f; // 最初は早めにスポーン
        }

        private void Update()
        {
            if (GameManager.Instance.CurrentState != GameManager.GameState.Playing) return;

            spawnTimer -= Time.deltaTime;
            if (spawnTimer <= 0f && activePowerUps.Count < maxPowerUps)
            {
                SpawnPowerUp();
                spawnTimer = spawnInterval;
            }
        }

        private void SpawnPowerUp()
        {
            Vector3 pos = GameManager.Instance.GetRandomFieldPosition(15f);
            pos.y = 0.25f; // Sit on ground (half cylinder height + slight hover)

            GameObject obj = ObjectPool.Instance.Spawn(PoolTag, pos, Quaternion.identity);
            PowerUpItem item = obj.GetComponent<PowerUpItem>();

            // 重み付きランダムでタイプ決定
            PowerUpItem.PowerUpType type = GetRandomType();
            item.Respawn(pos, type);

            activePowerUps.Add(item);
        }

        public void OnPowerUpCollected(PowerUpItem item)
        {
            activePowerUps.Remove(item);
            ObjectPool.Instance.Despawn(PoolTag, item.gameObject);
        }

        private PowerUpItem.PowerUpType GetRandomType()
        {
            float roll = Random.value;
            float cumulative = 0f;

            cumulative += speedBoostWeight;
            if (roll < cumulative) return PowerUpItem.PowerUpType.SpeedBoost;

            cumulative += shieldWeight;
            if (roll < cumulative) return PowerUpItem.PowerUpType.Shield;

            cumulative += magnetWeight;
            if (roll < cumulative) return PowerUpItem.PowerUpType.Magnet;

            cumulative += ghostWeight;
            if (roll < cumulative) return PowerUpItem.PowerUpType.Ghost;

            return PowerUpItem.PowerUpType.MegaGrowth;
        }

        private void OnDestroy()
        {
            if (GameManager.Instance != null)
                GameManager.Instance.OnGameStart -= OnGameStart;
        }
    }
}
