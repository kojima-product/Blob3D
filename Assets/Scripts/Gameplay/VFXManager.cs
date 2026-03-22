using UnityEngine;
using System.Collections.Generic;

namespace Blob3D.Gameplay
{
    /// <summary>
    /// Singleton that creates and pools ParticleSystem effects entirely in code.
    /// No prefabs or external assets required — all effects are configured programmatically.
    /// </summary>
    public class VFXManager : MonoBehaviour
    {
        // ---------- Singleton ----------
        public static VFXManager Instance { get; private set; }

        // ---------- Pool ----------
        private readonly Dictionary<string, Queue<ParticleSystem>> pool
            = new Dictionary<string, Queue<ParticleSystem>>();

        // Pool key constants
        private const string KeyFeedBurst = "FeedBurst";
        private const string KeyBlobSplash = "BlobSplash";
        private const string KeyPowerUpAura = "PowerUpAura";
        private const string KeyDashTrail = "DashTrail";
        private const string KeyAmbientDust = "AmbientDust";

        // Ambient particle reference
        private ParticleSystem ambientDustSystem;

        // Initial pool size per effect type
        [Header("Pool Settings")]
        [SerializeField] private int initialPoolSize = 3;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void Start()
        {
            // Pre-warm pools
            PreWarmPool(KeyFeedBurst, initialPoolSize, CreateFeedBurstSystem);
            PreWarmPool(KeyBlobSplash, initialPoolSize, CreateBlobSplashSystem);
            PreWarmPool(KeyPowerUpAura, initialPoolSize, CreatePowerUpAuraSystem);
            PreWarmPool(KeyDashTrail, 2, CreateDashTrailSystem);
        }

        // ========================================
        // Public API
        // ========================================

        /// <summary>
        /// Small colorful burst when eating feed (10-15 particles, small, fast fade).
        /// </summary>
        public void PlayFeedBurst(Vector3 position, Color color)
        {
            ParticleSystem ps = GetFromPool(KeyFeedBurst, CreateFeedBurstSystem);
            ps.transform.position = position;

            var main = ps.main;
            main.startColor = color;

            ps.gameObject.SetActive(true);
            ps.Play();

            ReturnToPoolWhenDone(ps, KeyFeedBurst, main.startLifetime.constantMax + 0.1f);
        }

        /// <summary>
        /// Larger splash when absorbing another blob (30-40 particles, size-scaled).
        /// </summary>
        public void PlayBlobSplash(Vector3 position, Color color, float size)
        {
            ParticleSystem ps = GetFromPool(KeyBlobSplash, CreateBlobSplashSystem);
            ps.transform.position = position;

            float scaleFactor = Mathf.Clamp(size * 0.15f, 0.5f, 5f);

            var main = ps.main;
            main.startColor = color;
            main.startSize = new ParticleSystem.MinMaxCurve(0.2f * scaleFactor, 0.5f * scaleFactor);
            main.startSpeed = new ParticleSystem.MinMaxCurve(2f * scaleFactor, 5f * scaleFactor);

            var shape = ps.shape;
            shape.radius = 0.5f * scaleFactor;

            ps.gameObject.SetActive(true);
            ps.Play();

            ReturnToPoolWhenDone(ps, KeyBlobSplash, main.startLifetime.constantMax + 0.1f);
        }

        /// <summary>
        /// Ring of particles expanding outward when picking up a powerup.
        /// </summary>
        public void PlayPowerUpAura(Vector3 position, Color color)
        {
            ParticleSystem ps = GetFromPool(KeyPowerUpAura, CreatePowerUpAuraSystem);
            ps.transform.position = position;

            var main = ps.main;
            main.startColor = color;

            ps.gameObject.SetActive(true);
            ps.Play();

            ReturnToPoolWhenDone(ps, KeyPowerUpAura, main.startLifetime.constantMax + 0.1f);
        }

        /// <summary>
        /// Trail particles behind blob during dash (continuous emission, follows target).
        /// Returns the ParticleSystem so the caller can stop it when the dash ends.
        /// </summary>
        public ParticleSystem PlayDashTrail(Transform followTarget, Color color)
        {
            ParticleSystem ps = GetFromPool(KeyDashTrail, CreateDashTrailSystem);
            ps.transform.SetParent(followTarget);
            ps.transform.localPosition = Vector3.zero;

            var main = ps.main;
            main.startColor = color;

            ps.gameObject.SetActive(true);
            ps.Play();

            return ps;
        }

        /// <summary>
        /// Start ambient floating particles around the player for atmosphere.
        /// </summary>
        public void StartAmbientParticles(Transform followTarget)
        {
            if (ambientDustSystem != null && ambientDustSystem.isPlaying) return;

            ambientDustSystem = CreateAmbientDustSystem();
            ambientDustSystem.transform.SetParent(followTarget);
            ambientDustSystem.transform.localPosition = Vector3.up * 2f;
            ambientDustSystem.gameObject.SetActive(true);
            ambientDustSystem.Play();
        }

        /// <summary>
        /// Stop ambient particles.
        /// </summary>
        public void StopAmbientParticles()
        {
            if (ambientDustSystem != null)
            {
                ambientDustSystem.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                ambientDustSystem.transform.SetParent(transform);
            }
        }

        /// <summary>
        /// Stop a dash trail and return it to the pool.
        /// </summary>
        public void StopDashTrail(ParticleSystem ps)
        {
            if (ps == null) return;

            ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            Vector3 worldPos = ps.transform.position;
            ps.transform.SetParent(transform);
            ps.transform.position = worldPos;

            float remaining = ps.main.startLifetime.constantMax;
            ReturnToPoolWhenDone(ps, KeyDashTrail, remaining + 0.1f);
        }

        // ========================================
        // Pool management
        // ========================================

        private void PreWarmPool(string key, int count, System.Func<ParticleSystem> factory)
        {
            if (!pool.ContainsKey(key))
            {
                pool[key] = new Queue<ParticleSystem>();
            }

            for (int i = 0; i < count; i++)
            {
                ParticleSystem ps = factory();
                pool[key].Enqueue(ps);
            }
        }

        private ParticleSystem GetFromPool(string key, System.Func<ParticleSystem> factory)
        {
            if (!pool.ContainsKey(key))
            {
                pool[key] = new Queue<ParticleSystem>();
            }

            if (pool[key].Count > 0)
            {
                return pool[key].Dequeue();
            }

            // Pool exhausted — create a new instance
            return factory();
        }

        private void ReturnToPoolWhenDone(ParticleSystem ps, string key, float delay)
        {
            StartCoroutine(ReturnAfterDelay(ps, key, delay));
        }

        private System.Collections.IEnumerator ReturnAfterDelay(ParticleSystem ps, string key, float delay)
        {
            yield return new WaitForSeconds(delay);

            if (ps == null) yield break;

            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ps.gameObject.SetActive(false);

            if (!pool.ContainsKey(key))
            {
                pool[key] = new Queue<ParticleSystem>();
            }
            pool[key].Enqueue(ps);
        }

        // ========================================
        // Factory methods
        // ========================================

        private ParticleSystem CreateFeedBurstSystem()
        {
            var go = new GameObject("FeedBurst");
            go.transform.SetParent(transform);
            var ps = go.AddComponent<ParticleSystem>();

            var main = ps.main;
            main.startLifetime = 0.4f;
            main.startSpeed = 3f;
            main.startSize = 0.15f;
            main.maxParticles = 20;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.playOnAwake = false;
            main.loop = false;
            main.gravityModifier = 0.5f;

            var emission = ps.emission;
            emission.rateOverTime = 0;
            emission.SetBurst(0, new ParticleSystem.Burst(0f, 12));

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.3f;

            var colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new GradientColorKey[] {
                    new GradientColorKey(Color.white, 0f),
                    new GradientColorKey(Color.white, 1f)
                },
                new GradientAlphaKey[] {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(0f, 1f)
                }
            );
            colorOverLifetime.color = gradient;

            var sizeOverLifetime = ps.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(
                1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));

            // URP particle material
            ConfigureParticleRenderer(go);

            go.SetActive(false);
            return ps;
        }

        private ParticleSystem CreateBlobSplashSystem()
        {
            var go = new GameObject("BlobSplash");
            go.transform.SetParent(transform);
            var ps = go.AddComponent<ParticleSystem>();

            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 0.8f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(2f, 5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.2f, 0.5f);
            main.maxParticles = 50;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.playOnAwake = false;
            main.loop = false;
            main.gravityModifier = 0.8f;

            var emission = ps.emission;
            emission.rateOverTime = 0;
            emission.SetBurst(0, new ParticleSystem.Burst(0f, 35));

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.5f;

            var colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new GradientColorKey[] {
                    new GradientColorKey(Color.white, 0f),
                    new GradientColorKey(Color.white, 0.5f)
                },
                new GradientAlphaKey[] {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(0.8f, 0.3f),
                    new GradientAlphaKey(0f, 1f)
                }
            );
            colorOverLifetime.color = gradient;

            var sizeOverLifetime = ps.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(
                1f, AnimationCurve.Linear(0f, 1f, 1f, 0.2f));

            // Sub-emitter-like secondary burst via velocity over lifetime
            var velocityOverLifetime = ps.velocityOverLifetime;
            velocityOverLifetime.enabled = true;
            velocityOverLifetime.radial = new ParticleSystem.MinMaxCurve(-1f);

            ConfigureParticleRenderer(go);

            go.SetActive(false);
            return ps;
        }

        private ParticleSystem CreatePowerUpAuraSystem()
        {
            var go = new GameObject("PowerUpAura");
            go.transform.SetParent(transform);
            var ps = go.AddComponent<ParticleSystem>();

            var main = ps.main;
            main.startLifetime = 0.8f;
            main.startSpeed = 4f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.1f, 0.25f);
            main.maxParticles = 40;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.playOnAwake = false;
            main.loop = false;
            main.gravityModifier = -0.1f; // Slight upward drift

            var emission = ps.emission;
            emission.rateOverTime = 0;
            emission.SetBurst(0, new ParticleSystem.Burst(0f, 30));

            // Ring shape for expanding outward aura
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.5f;
            shape.arc = 360f;
            shape.arcMode = ParticleSystemShapeMultiModeValue.Random;

            var colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new GradientColorKey[] {
                    new GradientColorKey(Color.white, 0f),
                    new GradientColorKey(Color.white, 0.6f)
                },
                new GradientAlphaKey[] {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(0.6f, 0.4f),
                    new GradientAlphaKey(0f, 1f)
                }
            );
            colorOverLifetime.color = gradient;

            var sizeOverLifetime = ps.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(
                1f, AnimationCurve.EaseInOut(0f, 0.5f, 1f, 1.5f));

            ConfigureParticleRenderer(go);

            go.SetActive(false);
            return ps;
        }

        private ParticleSystem CreateDashTrailSystem()
        {
            var go = new GameObject("DashTrail");
            go.transform.SetParent(transform);
            var ps = go.AddComponent<ParticleSystem>();

            var main = ps.main;
            main.startLifetime = 0.3f;
            main.startSpeed = 0.5f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.1f, 0.2f);
            main.maxParticles = 60;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.playOnAwake = false;
            main.loop = true; // Continuous emission while dashing

            var emission = ps.emission;
            emission.rateOverTime = 40;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.2f;

            var colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new GradientColorKey[] {
                    new GradientColorKey(Color.white, 0f),
                    new GradientColorKey(Color.white, 1f)
                },
                new GradientAlphaKey[] {
                    new GradientAlphaKey(0.8f, 0f),
                    new GradientAlphaKey(0f, 1f)
                }
            );
            colorOverLifetime.color = gradient;

            var sizeOverLifetime = ps.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(
                1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));

            ConfigureParticleRenderer(go);

            go.SetActive(false);
            return ps;
        }

        private ParticleSystem CreateAmbientDustSystem()
        {
            var go = new GameObject("AmbientDust");
            go.transform.SetParent(transform);
            var ps = go.AddComponent<ParticleSystem>();

            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(4f, 7f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.2f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.02f, 0.06f);
            main.startColor = new Color(0.8f, 0.9f, 1f, 0.25f);
            main.maxParticles = 80;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.playOnAwake = false;
            main.loop = true;
            main.gravityModifier = -0.02f; // Slight upward float

            var emission = ps.emission;
            emission.rateOverTime = 15;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(20f, 8f, 20f);

            var colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new GradientColorKey[] {
                    new GradientColorKey(new Color(0.7f, 0.85f, 1f), 0f),
                    new GradientColorKey(new Color(0.9f, 0.95f, 1f), 0.5f),
                    new GradientColorKey(new Color(0.7f, 0.85f, 1f), 1f)
                },
                new GradientAlphaKey[] {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(0.3f, 0.2f),
                    new GradientAlphaKey(0.3f, 0.8f),
                    new GradientAlphaKey(0f, 1f)
                }
            );
            colorOverLifetime.color = gradient;

            var sizeOverLifetime = ps.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(
                1f, AnimationCurve.EaseInOut(0f, 0.5f, 1f, 1.2f));

            // Add slight noise for organic floating movement
            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.3f;
            noise.frequency = 0.5f;
            noise.scrollSpeed = 0.1f;
            noise.damping = true;

            ConfigureParticleRenderer(go);

            go.SetActive(false);
            return ps;
        }

        // ========================================
        // Shared helpers
        // ========================================

        /// <summary>
        /// Configure the ParticleSystemRenderer with a URP-compatible unlit particle material.
        /// </summary>
        private static void ConfigureParticleRenderer(GameObject go, Color color = default)
        {
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            if (renderer == null) return;

            renderer.renderMode = ParticleSystemRenderMode.Billboard;

            // Try URP particle shader first, fall back to legacy
            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null) shader = Shader.Find("Particles/Standard Unlit");
            if (shader == null) shader = Shader.Find("Unlit/Color");

            var material = new Material(shader);
            material.color = color == default ? Color.white : color;
            // Enable alpha blending for particles
            material.SetFloat("_Surface", 1f); // Transparent
            material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.renderQueue = 3000;
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            renderer.material = material;
        }
    }
}
