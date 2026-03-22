using UnityEngine;
using System.Collections.Generic;

namespace Blob3D.Utils
{
    /// <summary>
    /// 汎用オブジェクトプール。
    /// エサ、エフェクト、パーティクルなどの頻繁な生成/破棄を回避する。
    /// </summary>
    public class ObjectPool : MonoBehaviour
    {
        [System.Serializable]
        public class PoolConfig
        {
            public string tag;
            public GameObject prefab;
            public int initialSize = 20;
            public bool expandable = true;
        }

        public static ObjectPool Instance { get; private set; }

        [SerializeField] private PoolConfig[] pools;

        private Dictionary<string, Queue<GameObject>> poolDict = new Dictionary<string, Queue<GameObject>>();
        private Dictionary<string, PoolConfig> configDict = new Dictionary<string, PoolConfig>();

        private void Awake()
        {
            Instance = this;

            foreach (var config in pools)
            {
                Queue<GameObject> queue = new Queue<GameObject>();

                for (int i = 0; i < config.initialSize; i++)
                {
                    GameObject obj = CreateInstance(config.prefab);
                    queue.Enqueue(obj);
                }

                poolDict[config.tag] = queue;
                configDict[config.tag] = config;
            }
        }

        /// <summary>ランタイムでプールを動的に登録する</summary>
        public void RegisterPool(string tag, GameObject prefab, int initialSize, bool expandable = true)
        {
            if (poolDict.ContainsKey(tag)) return;

            var config = new PoolConfig
            {
                tag = tag,
                prefab = prefab,
                initialSize = initialSize,
                expandable = expandable
            };

            Queue<GameObject> queue = new Queue<GameObject>();
            for (int i = 0; i < initialSize; i++)
            {
                GameObject obj = CreateInstance(prefab);
                queue.Enqueue(obj);
            }

            poolDict[tag] = queue;
            configDict[tag] = config;
        }

        /// <summary>プールからオブジェクトを取得する</summary>
        public GameObject Spawn(string tag, Vector3 position, Quaternion rotation)
        {
            if (!poolDict.ContainsKey(tag))
            {
                Debug.LogWarning($"ObjectPool: tag '{tag}' not found.");
                return null;
            }

            Queue<GameObject> queue = poolDict[tag];
            GameObject obj = null;

            // Fix: skip destroyed objects that may have been cleaned up by scene reload
            while (queue.Count > 0 && obj == null)
            {
                obj = queue.Dequeue();
            }

            if (obj == null)
            {
                if (configDict[tag].expandable)
                {
                    obj = CreateInstance(configDict[tag].prefab);
                }
                else
                {
                    Debug.LogWarning($"ObjectPool: pool '{tag}' exhausted and not expandable.");
                    return null;
                }
            }

            obj.transform.position = position;
            obj.transform.rotation = rotation;
            obj.SetActive(true);
            return obj;
        }

        /// <summary>オブジェクトをプールに返却する</summary>
        public void Despawn(string tag, GameObject obj)
        {
            // Fix: null check — object may have been destroyed before despawn
            if (obj == null) return;

            if (!poolDict.ContainsKey(tag))
            {
                Destroy(obj);
                return;
            }

            // Reset transform state
            obj.transform.position = Vector3.zero;
            obj.transform.rotation = Quaternion.identity;
            obj.transform.localScale = Vector3.one;

            // Reset Rigidbody physics state if present
            Rigidbody rb = obj.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = false; // Fix: ensure kinematic is reset (AbsorptionEffect sets it true)
                rb.velocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }

            // Fix: re-enable colliders that may have been disabled by AbsorptionEffect
            Collider col = obj.GetComponent<Collider>();
            if (col != null) col.enabled = true;

            // Stop any active ParticleSystem if present
            ParticleSystem ps = obj.GetComponent<ParticleSystem>();
            if (ps != null)
            {
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }

            obj.SetActive(false);
            poolDict[tag].Enqueue(obj);
        }

        /// <summary>一定時間後に返却する</summary>
        public void DespawnDelayed(string tag, GameObject obj, float delay)
        {
            StartCoroutine(DespawnAfterDelay(tag, obj, delay));
        }

        private System.Collections.IEnumerator DespawnAfterDelay(string tag, GameObject obj, float delay)
        {
            yield return new WaitForSeconds(delay);
            // Fix: check object still exists after delay (may have been destroyed during scene reload)
            if (obj != null)
                Despawn(tag, obj);
        }

        private GameObject CreateInstance(GameObject prefab)
        {
            GameObject obj = Instantiate(prefab, transform);
            obj.SetActive(false);
            return obj;
        }
    }
}
