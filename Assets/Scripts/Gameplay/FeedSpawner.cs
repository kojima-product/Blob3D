using UnityEngine;
using System.Collections.Generic;
using Blob3D.Core;
using Blob3D.Utils;

namespace Blob3D.Gameplay
{
    /// <summary>
    /// Manages feed spawning, pooling, and respawning.
    /// Uses ObjectPool for efficient lifecycle management.
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
            new Color(1f, 0.85f, 0.2f),   // Yellow
            new Color(0.3f, 0.9f, 0.5f),  // Green
            new Color(0.4f, 0.6f, 1f),    // Blue
            new Color(1f, 0.4f, 0.6f),    // Pink
            new Color(0.9f, 0.5f, 0.2f),  // Orange
        };

        // Pre-generated meshes for each feed shape variant
        private Mesh meshIcosahedron;
        private Mesh meshDiamond;
        private Mesh meshStar;

        private List<Feed> feedPool = new List<Feed>();
        private Queue<Feed> respawnQueue = new Queue<Feed>();
        private float respawnTimer;

        private void Awake()
        {
            Instance = this;
            // Pre-generate all shape meshes once to avoid per-spawn allocation
            meshIcosahedron = Feed.CreateFeedMesh(Feed.FeedShape.Icosahedron);
            meshDiamond = Feed.CreateFeedMesh(Feed.FeedShape.Diamond);
            meshStar = Feed.CreateFeedMesh(Feed.FeedShape.Star);
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

        // ---------- Spawn ----------

        private void SpawnInitialFeed()
        {
            for (int i = 0; i < maxFeedCount; i++)
            {
                SpawnFeed();
            }
        }

        /// <summary>Apply a random geometric shape, color, and scale to a feed object</summary>
        private void ApplyFeedVariation(GameObject obj)
        {
            // Assign a random geometric mesh shape
            MeshFilter mf = obj.GetComponent<MeshFilter>();
            if (mf != null)
            {
                Feed.FeedShape shape = (Feed.FeedShape)Random.Range(0, 3);
                mf.mesh = GetMeshForShape(shape);

                // Scale varies per shape to ensure similar visual footprint
                float baseSize = Random.Range(0.12f, 0.2f);
                switch (shape)
                {
                    case Feed.FeedShape.Icosahedron:
                        obj.transform.localScale = Vector3.one * baseSize;
                        break;
                    case Feed.FeedShape.Diamond:
                        // Diamond is elongated vertically — shrink XZ slightly
                        obj.transform.localScale = new Vector3(baseSize * 0.7f, baseSize, baseSize * 0.7f);
                        break;
                    case Feed.FeedShape.Star:
                        // Star is flat — scale Y thinner, XZ larger
                        obj.transform.localScale = new Vector3(baseSize * 1.1f, baseSize * 0.6f, baseSize * 1.1f);
                        break;
                }
            }

            // Apply random color with emission
            Renderer rend = obj.GetComponent<Renderer>();
            if (rend != null && feedColors.Length > 0)
            {
                Color color = feedColors[Random.Range(0, feedColors.Length)];
                rend.material.color = color;
                rend.material.EnableKeyword("_EMISSION");
                rend.material.SetColor("_EmissionColor", color * 0.5f);
            }
        }

        /// <summary>Return the pre-generated mesh for the given shape type</summary>
        private Mesh GetMeshForShape(Feed.FeedShape shape)
        {
            switch (shape)
            {
                case Feed.FeedShape.Diamond: return meshDiamond;
                case Feed.FeedShape.Star:    return meshStar;
                default:                     return meshIcosahedron;
            }
        }

        private void SpawnFeed()
        {
            Vector3 pos = GameManager.Instance.GetRandomFieldPosition(5f);
            pos.y = 0.35f;

            // Random rotation on all axes for crystal tumbling look
            Quaternion rot = Quaternion.Euler(Random.Range(0f, 360f), Random.Range(0f, 360f), Random.Range(0f, 360f));
            GameObject obj = ObjectPool.Instance.Spawn(PoolTag, pos, rot);
            Feed feed = obj.GetComponent<Feed>();

            ApplyFeedVariation(obj);

            feedPool.Add(feed);
        }

        /// <summary>Schedule a consumed feed for respawn</summary>
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
            newPos.y = 0.35f;

            // Random rotation on all axes for crystal tumbling look
            Quaternion rot = Quaternion.Euler(Random.Range(0f, 360f), Random.Range(0f, 360f), Random.Range(0f, 360f));
            GameObject obj = ObjectPool.Instance.Spawn(PoolTag, newPos, rot);
            Feed feed = obj.GetComponent<Feed>();

            ApplyFeedVariation(obj);

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
