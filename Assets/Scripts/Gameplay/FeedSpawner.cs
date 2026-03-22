using UnityEngine;
using System.Collections.Generic;
using Blob3D.Core;
using Blob3D.Utils;

namespace Blob3D.Gameplay
{
    /// <summary>
    /// エサの生成・管理・リスポーンを担当。
    /// ObjectPoolを利用して効率的に管理する。
    /// </summary>
    public class FeedSpawner : MonoBehaviour
    {
        private const string PoolTag = "Feed";

        public static FeedSpawner Instance { get; private set; }

        [Header("Spawn Settings")]
        [SerializeField] private GameObject feedPrefab;
        [SerializeField] private int maxFeedCount = 300;
        [SerializeField] private float respawnInterval = 2f;

        [Header("Feed Variation")]
        [SerializeField] private Color[] feedColors = {
            new Color(1f, 0.85f, 0.2f),   // 黄
            new Color(0.3f, 0.9f, 0.5f),   // 緑
            new Color(0.4f, 0.6f, 1f),     // 青
            new Color(1f, 0.4f, 0.6f),     // ピンク
            new Color(0.9f, 0.5f, 0.2f),   // オレンジ
        };

        private List<Feed> feedPool = new List<Feed>();
        private Queue<Feed> respawnQueue = new Queue<Feed>();
        private float respawnTimer;

        private void Awake()
        {
            Instance = this;
        }

        private void Start()
        {
            ObjectPool.Instance.RegisterPool(PoolTag, feedPrefab, maxFeedCount + 10);
            GameManager.Instance.OnGameStart += SpawnInitialFeed;
        }

        private void Update()
        {
            if (respawnQueue.Count == 0) return;

            respawnTimer -= Time.deltaTime;
            if (respawnTimer <= 0f)
            {
                RespawnNext();
                respawnTimer = respawnInterval;
            }
        }

        // ---------- スポーン ----------

        private void SpawnInitialFeed()
        {
            for (int i = 0; i < maxFeedCount; i++)
            {
                SpawnFeed();
            }
        }

        private void SpawnFeed()
        {
            Vector3 pos = GameManager.Instance.GetRandomFieldPosition(5f);
            pos.y = 0.8f;

            GameObject obj = ObjectPool.Instance.Spawn(PoolTag, pos, Quaternion.identity);
            Feed feed = obj.GetComponent<Feed>();

            // ランダムカラー
            Renderer rend = obj.GetComponent<Renderer>();
            if (rend != null && feedColors.Length > 0)
            {
                rend.material.color = feedColors[Random.Range(0, feedColors.Length)];
            }

            // Larger feed for better visibility
            obj.transform.localScale = Vector3.one * Random.Range(0.3f, 0.6f);

            feedPool.Add(feed);
        }

        /// <summary>エサのリスポーンをスケジュールする</summary>
        public void ScheduleRespawn(Feed feed)
        {
            ObjectPool.Instance.Despawn(PoolTag, feed.gameObject);
            respawnQueue.Enqueue(feed);
        }

        private void RespawnNext()
        {
            if (respawnQueue.Count == 0) return;

            Feed oldFeed = respawnQueue.Dequeue();

            Vector3 newPos = GameManager.Instance.GetRandomFieldPosition(5f);
            newPos.y = 0.5f;

            GameObject obj = ObjectPool.Instance.Spawn(PoolTag, newPos, Quaternion.identity);
            Feed feed = obj.GetComponent<Feed>();

            // ランダムカラー
            Renderer rend = obj.GetComponent<Renderer>();
            if (rend != null && feedColors.Length > 0)
            {
                rend.material.color = feedColors[Random.Range(0, feedColors.Length)];
            }

            obj.transform.localScale = Vector3.one * Random.Range(0.3f, 0.6f);

            // Replace old reference in pool list
            int idx = feedPool.IndexOf(oldFeed);
            if (idx >= 0)
                feedPool[idx] = feed;
            else
                feedPool.Add(feed);
        }

        private void OnDestroy()
        {
            if (GameManager.Instance != null)
                GameManager.Instance.OnGameStart -= SpawnInitialFeed;
        }
    }
}
