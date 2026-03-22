using UnityEngine;
using System.Collections.Generic;

namespace Blob3D.Gameplay
{
    /// <summary>
    /// Singleton that creates and pools ParticleSystem effects entirely in code.
    /// No prefabs or external assets required — all effects are configured programmatically.
    /// Enhanced VFX: crystalline feed bursts, viscous blob splashes, speed-line dash trails,
    /// dramatic absorption vortex, satisfying power-up collection, and screen-edge boundary glow.
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
        private const string KeyMagnetAura = "MagnetAura";
        private const string KeyAmbientDust = "AmbientDust";
        private const string KeyGroundShadow = "GroundShadow";
        private const string KeyAbsorptionVortex = "AbsorptionVortex";
        private const string KeyBoundaryGlow = "BoundaryGlow";

        // Ambient particle references
        private ParticleSystem ambientDustSystem;
        private ParticleSystem ambientFirefliesSystem;

        // Boundary glow reference (persistent full-screen edge effect)
        private ParticleSystem boundaryGlowSystem;

        // Ground shadow tracking
        private readonly Dictionary<Transform, GameObject> groundShadows
            = new Dictionary<Transform, GameObject>();

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

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Start()
        {
            // Pre-warm pools for frequently-used effects
            PreWarmPool(KeyFeedBurst, initialPoolSize, CreateFeedBurstSystem);
            PreWarmPool(KeyBlobSplash, initialPoolSize, CreateBlobSplashSystem);
            PreWarmPool(KeyPowerUpAura, initialPoolSize, CreatePowerUpAuraSystem);
            PreWarmPool(KeyDashTrail, 2, CreateDashTrailSystem);
            PreWarmPool(KeyMagnetAura, 2, CreateMagnetAuraSystem);
            PreWarmPool(KeyAbsorptionVortex, 2, CreateAbsorptionVortexSystem);
        }

        // ========================================
        // Distance culling
        // ========================================

        /// <summary>
        /// Skip particle effects too far from camera to save GPU/CPU on mobile.
        /// </summary>
        private bool IsTooFarFromCamera(Vector3 position)
        {
            Camera cam = Camera.main;
            if (cam == null) return false;
            return (cam.transform.position - position).sqrMagnitude > 6400f; // 80^2
        }

        // ========================================
        // Public API
        // ========================================

        /// <summary>
        /// Crystalline sparkle burst when eating feed.
        /// Uses stretched billboard particles with rotation for a faceted gem look,
        /// multi-color gradient from white core to tinted sparkle, and slight gravity
        /// so sparkles arc outward like shattering crystal.
        /// </summary>
        public void PlayFeedBurst(Vector3 position, Color color)
        {
            if (IsTooFarFromCamera(position)) return;
            ParticleSystem ps = GetFromPool(KeyFeedBurst, CreateFeedBurstSystem);
            ps.transform.position = position;

            // Tint the start color; the gradient blends from bright white core to the feed color
            var main = ps.main;
            main.startColor = new ParticleSystem.MinMaxGradient(Color.white, color);

            ps.gameObject.SetActive(true);
            ps.Play();

            ReturnToPoolWhenDone(ps, KeyFeedBurst, main.startLifetime.constantMax + 0.1f);
        }

        /// <summary>
        /// Gel/slime splatter effect when absorbing another blob.
        /// Uses elongated stretched particles with high drag to simulate viscous droplets,
        /// secondary drip burst for gooey residue, and size that pulses before shrinking.
        /// </summary>
        public void PlayBlobSplash(Vector3 position, Color color, float size)
        {
            if (IsTooFarFromCamera(position)) return;
            ParticleSystem ps = GetFromPool(KeyBlobSplash, CreateBlobSplashSystem);
            ps.transform.position = position;

            float scaleFactor = Mathf.Clamp(size * 0.15f, 0.5f, 5f);

            var main = ps.main;
            // Slightly desaturated gel color for viscous look
            Color gelColor = Color.Lerp(color, Color.white, 0.15f);
            main.startColor = new ParticleSystem.MinMaxGradient(gelColor, color);
            main.startSize = new ParticleSystem.MinMaxCurve(0.15f * scaleFactor, 0.6f * scaleFactor);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.5f * scaleFactor, 6f * scaleFactor);

            var shape = ps.shape;
            shape.radius = 0.5f * scaleFactor;

            ps.gameObject.SetActive(true);
            ps.Play();

            ReturnToPoolWhenDone(ps, KeyBlobSplash, main.startLifetime.constantMax + 0.1f);
        }

        /// <summary>
        /// Satisfying collection burst for power-ups.
        /// Two-phase effect: fast radial sparkle ring + slow rising glow particles.
        /// Color is matched to power-up type for instant visual feedback.
        /// </summary>
        public void PlayPowerUpAura(Vector3 position, Color color)
        {
            ParticleSystem ps = GetFromPool(KeyPowerUpAura, CreatePowerUpAuraSystem);
            ps.transform.position = position;

            var main = ps.main;
            // Bright saturated core that fades to the power-up color
            Color brightColor = Color.Lerp(color, Color.white, 0.4f);
            main.startColor = new ParticleSystem.MinMaxGradient(brightColor, color);

            ps.gameObject.SetActive(true);
            ps.Play();

            ReturnToPoolWhenDone(ps, KeyPowerUpAura, main.startLifetime.constantMax + 0.1f);
        }

        /// <summary>
        /// Speed-line streak trail behind blob during dash.
        /// Uses stretched billboard particles aligned to velocity for motion-blur streaks,
        /// combined with small sparkle particles for energy feel.
        /// Returns the ParticleSystem so the caller can stop it when the dash ends.
        /// </summary>
        public ParticleSystem PlayDashTrail(Transform followTarget, Color color)
        {
            ParticleSystem ps = GetFromPool(KeyDashTrail, CreateDashTrailSystem);
            ps.transform.SetParent(followTarget);
            ps.transform.localPosition = Vector3.zero;

            var main = ps.main;
            // Brighter trailing color for energy streak look
            Color streakColor = Color.Lerp(color, Color.white, 0.3f);
            main.startColor = new ParticleSystem.MinMaxGradient(streakColor, color);

            ps.gameObject.SetActive(true);
            ps.Play();

            return ps;
        }

        /// <summary>
        /// Dramatic vortex/spiral pull effect during absorption.
        /// Particles spiral inward toward the absorber using negative radial velocity,
        /// creating a whirlpool-like suction visual. Orbit module adds rotational spin.
        /// </summary>
        public ParticleSystem PlayAbsorptionVortex(Transform absorber, Color color, float radius)
        {
            ParticleSystem ps = GetFromPool(KeyAbsorptionVortex, CreateAbsorptionVortexSystem);
            ps.transform.SetParent(absorber);
            ps.transform.localPosition = Vector3.zero;

            float clampedRadius = Mathf.Clamp(radius, 0.5f, 4f);

            var main = ps.main;
            main.startColor = new ParticleSystem.MinMaxGradient(
                Color.Lerp(color, Color.white, 0.5f), color);

            // Scale the emission shape to match absorber reach
            var shape = ps.shape;
            shape.radius = clampedRadius;

            ps.gameObject.SetActive(true);
            ps.Play();

            return ps;
        }

        /// <summary>
        /// Stop the absorption vortex and return it to the pool.
        /// </summary>
        public void StopAbsorptionVortex(ParticleSystem ps)
        {
            if (ps == null) return;

            ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            Vector3 worldPos = ps.transform.position;
            ps.transform.SetParent(transform);
            ps.transform.position = worldPos;

            float remaining = ps.main.startLifetime.constantMax;
            ReturnToPoolWhenDone(ps, KeyAbsorptionVortex, remaining + 0.1f);
        }

        /// <summary>
        /// Screen-edge glow effect when the player is near the arena boundary.
        /// Creates a ring of inward-facing particles around the camera to simulate
        /// a warning glow at screen edges. Intensity is controlled by proximity (0-1).
        /// </summary>
        public void UpdateBoundaryGlow(float proximity)
        {
            // proximity: 0 = far from boundary, 1 = at boundary edge
            float clamped = Mathf.Clamp01(proximity);

            if (clamped < 0.01f)
            {
                // Not near boundary — stop if running
                if (boundaryGlowSystem != null && boundaryGlowSystem.isPlaying)
                {
                    boundaryGlowSystem.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                }
                return;
            }

            if (boundaryGlowSystem == null)
            {
                boundaryGlowSystem = CreateBoundaryGlowSystem();
            }

            // Parent to main camera for screen-space feel
            Camera cam = Camera.main;
            if (cam != null && boundaryGlowSystem.transform.parent != cam.transform)
            {
                boundaryGlowSystem.transform.SetParent(cam.transform);
                boundaryGlowSystem.transform.localPosition = Vector3.forward * 3f;
                boundaryGlowSystem.transform.localRotation = Quaternion.identity;
            }

            // Scale emission rate and color intensity by proximity
            var emission = boundaryGlowSystem.emission;
            emission.rateOverTime = Mathf.Lerp(5f, 40f, clamped);

            var main = boundaryGlowSystem.main;
            // Warning color shifts from soft amber to intense red at max proximity
            Color glowColor = Color.Lerp(
                new Color(1f, 0.6f, 0.2f, 0.3f),
                new Color(1f, 0.15f, 0.05f, 0.7f),
                clamped);
            main.startColor = glowColor;
            main.startSize = new ParticleSystem.MinMaxCurve(
                Mathf.Lerp(0.3f, 0.8f, clamped),
                Mathf.Lerp(0.6f, 1.5f, clamped));

            if (!boundaryGlowSystem.gameObject.activeSelf)
            {
                boundaryGlowSystem.gameObject.SetActive(true);
            }
            if (!boundaryGlowSystem.isPlaying)
            {
                boundaryGlowSystem.Play();
            }
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

            // Also start fireflies for extra atmosphere
            StartFireflies(followTarget);
        }

        /// <summary>
        /// Start ambient firefly/sparkle particles for atmosphere.
        /// </summary>
        public void StartFireflies(Transform followTarget)
        {
            if (ambientFirefliesSystem != null && ambientFirefliesSystem.isPlaying) return;

            ambientFirefliesSystem = CreateFirefliesSystem();
            ambientFirefliesSystem.transform.SetParent(followTarget);
            ambientFirefliesSystem.transform.localPosition = Vector3.up * 1f;
            ambientFirefliesSystem.gameObject.SetActive(true);
            ambientFirefliesSystem.Play();
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
            if (ambientFirefliesSystem != null)
            {
                ambientFirefliesSystem.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                ambientFirefliesSystem.transform.SetParent(transform);
            }
        }

        // ========================================
        // Ground Shadow API
        // ========================================

        /// <summary>
        /// Create or update a dark circle projected below a blob that scales with blob size.
        /// Call this each frame or when blob size changes.
        /// </summary>
        public void UpdateGroundShadow(Transform blobTransform, float blobSize)
        {
            if (blobTransform == null) return;

            if (!groundShadows.TryGetValue(blobTransform, out GameObject shadow) || shadow == null)
            {
                shadow = CreateGroundShadowQuad();
                groundShadows[blobTransform] = shadow;
            }

            // Position shadow on the ground directly below the blob
            Vector3 pos = blobTransform.position;
            pos.y = 0.01f; // Just above ground to avoid z-fighting
            shadow.transform.position = pos;

            // Scale shadow with blob size (larger blobs cast larger, softer shadows)
            float shadowRadius = blobSize * 0.15f * 1.4f; // Slightly wider than blob
            shadow.transform.localScale = new Vector3(shadowRadius, shadowRadius, shadowRadius);

            // Fade shadow opacity based on blob height (higher = lighter shadow)
            float height = blobTransform.position.y;
            float alpha = Mathf.Clamp01(1f - height * 0.15f) * 0.35f;
            Renderer shadowRenderer = shadow.GetComponent<Renderer>();
            if (shadowRenderer != null)
            {
                MaterialPropertyBlock shadowMpb = new MaterialPropertyBlock();
                shadowRenderer.GetPropertyBlock(shadowMpb);
                shadowMpb.SetColor("_BaseColor", new Color(0f, 0f, 0f, alpha));
                shadowRenderer.SetPropertyBlock(shadowMpb);
            }

            shadow.SetActive(true);
        }

        /// <summary>Remove ground shadow for a blob (when it dies or is absorbed)</summary>
        public void RemoveGroundShadow(Transform blobTransform)
        {
            if (blobTransform == null) return;
            if (groundShadows.TryGetValue(blobTransform, out GameObject shadow))
            {
                if (shadow != null) Destroy(shadow);
                groundShadows.Remove(blobTransform);
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

        /// <summary>
        /// Continuous ring of particles around the player while magnet is active.
        /// Returns the ParticleSystem so the caller can stop it when the effect ends.
        /// </summary>
        public ParticleSystem PlayMagnetAura(Transform followTarget, Color color)
        {
            ParticleSystem ps = GetFromPool(KeyMagnetAura, CreateMagnetAuraSystem);
            ps.transform.SetParent(followTarget);
            ps.transform.localPosition = Vector3.zero;

            var main = ps.main;
            main.startColor = color;

            ps.gameObject.SetActive(true);
            ps.Play();

            return ps;
        }

        /// <summary>
        /// Stop the magnet aura and return it to the pool.
        /// </summary>
        public void StopMagnetAura(ParticleSystem ps)
        {
            if (ps == null) return;

            ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            Vector3 worldPos = ps.transform.position;
            ps.transform.SetParent(transform);
            ps.transform.position = worldPos;

            float remaining = ps.main.startLifetime.constantMax;
            ReturnToPoolWhenDone(ps, KeyMagnetAura, remaining + 0.1f);
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

            // Fix: skip destroyed objects that may have been enqueued before scene reload
            while (pool[key].Count > 0)
            {
                ParticleSystem ps = pool[key].Dequeue();
                if (ps != null) return ps;
            }

            // Pool exhausted or all entries destroyed — create a new instance
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
        // Factory methods — Enhanced VFX
        // ========================================

        /// <summary>
        /// Creates a crystalline sparkle burst particle system.
        /// Particles use rotation and size variation to mimic faceted crystal shards.
        /// A secondary size pulse gives a twinkling shimmer effect.
        /// </summary>
        private ParticleSystem CreateFeedBurstSystem()
        {
            var go = new GameObject("FeedBurst");
            go.transform.SetParent(transform);
            var ps = go.AddComponent<ParticleSystem>();

            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.6f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(2f, 5f);
            // Varied sizes for crystalline facets — small sparkles mixed with larger shards
            main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.2f);
            main.maxParticles = 25;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.playOnAwake = false;
            main.loop = false;
            // Light gravity so sparkles arc like shattering crystal
            main.gravityModifier = 0.3f;
            // Random rotation for each particle to simulate crystal facet glints
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);

            var emission = ps.emission;
            emission.rateOverTime = 0;
            // Two-phase burst: sharp initial pop + trailing sparkle shimmer
            emission.SetBursts(new ParticleSystem.Burst[] {
                new ParticleSystem.Burst(0f, 15),
                new ParticleSystem.Burst(0.05f, 8)
            });

            // Cone shape for a directional spray upward/outward
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 60f;
            shape.radius = 0.15f;

            // Color: bright white core fading through tinted sparkle to transparent
            var colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new GradientColorKey[] {
                    new GradientColorKey(Color.white, 0f),
                    new GradientColorKey(new Color(0.9f, 0.95f, 1f), 0.3f),
                    new GradientColorKey(Color.white, 1f)
                },
                new GradientAlphaKey[] {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(0.9f, 0.2f),
                    new GradientAlphaKey(0.4f, 0.6f),
                    new GradientAlphaKey(0f, 1f)
                }
            );
            colorOverLifetime.color = gradient;

            // Size: quick pop then shrink — gives a twinkle/flash impression
            var sizeOverLifetime = ps.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(
                1f, new AnimationCurve(
                    new Keyframe(0f, 0.5f),
                    new Keyframe(0.15f, 1.2f),  // Brief flash bigger
                    new Keyframe(0.4f, 0.8f),
                    new Keyframe(1f, 0f)         // Fade to nothing
                ));

            // Spin particles over lifetime for glinting crystal facets
            var rotationOverLifetime = ps.rotationOverLifetime;
            rotationOverLifetime.enabled = true;
            rotationOverLifetime.z = new ParticleSystem.MinMaxCurve(-2f, 2f);

            // Slight noise for organic sparkle drift
            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.4f;
            noise.frequency = 2f;
            noise.damping = true;

            // URP particle material with additive blending for sparkle glow
            ConfigureParticleRenderer(go, default, true);

            go.SetActive(false);
            return ps;
        }

        /// <summary>
        /// Creates a viscous gel/slime splatter particle system.
        /// Uses high drag and stretched billboards for gooey droplet trajectories.
        /// Particles expand briefly (surface tension blob) then shrink as they drip away.
        /// </summary>
        private ParticleSystem CreateBlobSplashSystem()
        {
            var go = new GameObject("BlobSplash");
            go.transform.SetParent(transform);
            var ps = go.AddComponent<ParticleSystem>();

            var main = ps.main;
            // Longer lifetime for viscous dripping feel
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 1.4f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 8f);
            // Larger particles to look like gel globs
            main.startSize = new ParticleSystem.MinMaxCurve(0.15f, 0.6f);
            main.maxParticles = 80;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.playOnAwake = false;
            main.loop = false;
            // Heavier gravity pulls viscous droplets downward convincingly
            main.gravityModifier = 1.2f;

            var emission = ps.emission;
            emission.rateOverTime = 0;
            // Staggered bursts: big initial splat + secondary drip + residual ooze
            emission.SetBursts(new ParticleSystem.Burst[] {
                new ParticleSystem.Burst(0f, 35),
                new ParticleSystem.Burst(0.06f, 18),
                new ParticleSystem.Burst(0.15f, 10)
            });

            // Hemisphere shape — splatter erupts upward from impact point
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Hemisphere;
            shape.radius = 0.5f;

            // Color: semi-translucent gel with glossy highlight fade
            var colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new GradientColorKey[] {
                    new GradientColorKey(Color.white, 0f),
                    new GradientColorKey(new Color(0.95f, 0.95f, 1f), 0.2f),
                    new GradientColorKey(new Color(0.85f, 0.85f, 0.9f), 1f)
                },
                new GradientAlphaKey[] {
                    new GradientAlphaKey(0.9f, 0f),
                    new GradientAlphaKey(0.85f, 0.3f),
                    new GradientAlphaKey(0.5f, 0.7f),
                    new GradientAlphaKey(0f, 1f)
                }
            );
            colorOverLifetime.color = gradient;

            // Size: brief expansion (surface tension wobble) then slow shrink like drying gel
            var sizeOverLifetime = ps.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(
                1f, new AnimationCurve(
                    new Keyframe(0f, 0.6f),
                    new Keyframe(0.1f, 1.15f),  // Brief surface-tension expansion
                    new Keyframe(0.3f, 1.0f),
                    new Keyframe(0.7f, 0.6f),
                    new Keyframe(1f, 0.1f)       // Shrink like evaporating droplet
                ));

            // High drag to simulate viscous fluid resistance
            var limitVelocityOverLifetime = ps.limitVelocityOverLifetime;
            limitVelocityOverLifetime.enabled = true;
            limitVelocityOverLifetime.dampen = 0.3f;
            limitVelocityOverLifetime.limit = 6f;

            // Radial inward pull for cohesive splatter (droplets don't fly too far)
            var velocityOverLifetime = ps.velocityOverLifetime;
            velocityOverLifetime.enabled = true;
            velocityOverLifetime.radial = new ParticleSystem.MinMaxCurve(-0.8f);

            // Slight noise for organic wobble in droplet paths
            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.2f;
            noise.frequency = 1.5f;
            noise.damping = true;

            ConfigureParticleRenderer(go);

            go.SetActive(false);
            return ps;
        }

        /// <summary>
        /// Creates a satisfying power-up collection burst.
        /// Two-stage effect: fast radial ring expansion + slow rising glow particles.
        /// Uses bright saturated colors matched to the power-up type for instant feedback.
        /// </summary>
        private ParticleSystem CreatePowerUpAuraSystem()
        {
            var go = new GameObject("PowerUpAura");
            go.transform.SetParent(transform);
            var ps = go.AddComponent<ParticleSystem>();

            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 1.2f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(2f, 6f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.3f);
            main.maxParticles = 60;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.playOnAwake = false;
            main.loop = false;
            // Gentle upward float for "energy rising" feel
            main.gravityModifier = -0.2f;
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);

            var emission = ps.emission;
            emission.rateOverTime = 0;
            // Three-phase burst: initial flash ring + sparkle cloud + trailing shimmer
            emission.SetBursts(new ParticleSystem.Burst[] {
                new ParticleSystem.Burst(0f, 25),
                new ParticleSystem.Burst(0.08f, 18),
                new ParticleSystem.Burst(0.2f, 10)
            });

            // Circle shape for expanding ring burst outward
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.3f;
            shape.arc = 360f;
            shape.arcMode = ParticleSystemShapeMultiModeValue.Random;

            // Color: bright white flash at start, through saturated power-up color, fade out
            var colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new GradientColorKey[] {
                    new GradientColorKey(Color.white, 0f),
                    new GradientColorKey(Color.white, 0.15f),
                    new GradientColorKey(new Color(0.9f, 0.9f, 1f), 0.6f)
                },
                new GradientAlphaKey[] {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(0.9f, 0.2f),
                    new GradientAlphaKey(0.5f, 0.6f),
                    new GradientAlphaKey(0f, 1f)
                }
            );
            colorOverLifetime.color = gradient;

            // Size: initial big flash that rapidly shrinks, then gentle float
            var sizeOverLifetime = ps.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(
                1f, new AnimationCurve(
                    new Keyframe(0f, 0.4f),
                    new Keyframe(0.08f, 1.6f),  // Satisfying pop/flash
                    new Keyframe(0.25f, 1.0f),
                    new Keyframe(0.6f, 0.7f),
                    new Keyframe(1f, 0f)
                ));

            // Spin for sparkle energy
            var rotationOverLifetime = ps.rotationOverLifetime;
            rotationOverLifetime.enabled = true;
            rotationOverLifetime.z = new ParticleSystem.MinMaxCurve(-1.5f, 1.5f);

            // Noise for organic sparkle movement
            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.5f;
            noise.frequency = 1.2f;
            noise.damping = true;

            // Additive blending for bright energy feel
            ConfigureParticleRenderer(go, default, true);

            go.SetActive(false);
            return ps;
        }

        /// <summary>
        /// Creates a speed-line streak dash trail particle system.
        /// Uses velocity-stretched billboard particles to create motion blur streaks.
        /// High emission rate with short lifetime produces dense trailing lines.
        /// </summary>
        private ParticleSystem CreateDashTrailSystem()
        {
            var go = new GameObject("DashTrail");
            go.transform.SetParent(transform);
            var ps = go.AddComponent<ParticleSystem>();

            var main = ps.main;
            // Short lifetime for crisp, fast-fading streaks
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.15f, 0.35f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 1.0f);
            // Thin particles that get stretched by velocity
            main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.15f);
            main.maxParticles = 80;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.playOnAwake = false;
            main.loop = true; // Continuous emission while dashing

            var emission = ps.emission;
            // High emission rate for dense speed-line trail
            emission.rateOverTime = 60;

            // Small sphere emission from blob center
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.25f;

            // Color gradient: bright white core → cool blue-violet hue shift → transparent tail
            var colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new GradientColorKey[] {
                    new GradientColorKey(new Color(1f, 1f, 1f), 0f),
                    new GradientColorKey(new Color(0.7f, 0.85f, 1f), 0.3f),
                    new GradientColorKey(new Color(0.5f, 0.6f, 1f), 0.7f),
                    new GradientColorKey(new Color(0.4f, 0.5f, 0.9f), 1f)
                },
                new GradientAlphaKey[] {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(0.8f, 0.15f),
                    new GradientAlphaKey(0.4f, 0.5f),
                    new GradientAlphaKey(0f, 1f)
                }
            );
            colorOverLifetime.color = gradient;

            // Size: starts at full width, tapers to thin streak tail
            var sizeOverLifetime = ps.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(
                1f, new AnimationCurve(
                    new Keyframe(0f, 1f),
                    new Keyframe(0.2f, 0.7f),
                    new Keyframe(1f, 0f)
                ));

            // Stretch particles along velocity for motion-blur speed lines
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            if (renderer != null)
            {
                renderer.renderMode = ParticleSystemRenderMode.Stretch;
                renderer.lengthScale = 3f;       // Elongate along movement direction
                renderer.velocityScale = 0.15f;   // Scale stretch by speed
            }

            // Slight noise for organic variation in streak paths
            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.15f;
            noise.frequency = 3f;
            noise.damping = true;

            // Additive blending for bright energy streaks
            ConfigureParticleRenderer(go, default, true);

            go.SetActive(false);
            return ps;
        }

        private ParticleSystem CreateMagnetAuraSystem()
        {
            var go = new GameObject("MagnetAura");
            go.transform.SetParent(transform);
            var ps = go.AddComponent<ParticleSystem>();

            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.0f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 3f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.18f);
            main.maxParticles = 40;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.playOnAwake = false;
            main.loop = true; // Continuous emission while magnet is active
            main.gravityModifier = -0.05f; // Slight upward drift

            var emission = ps.emission;
            emission.rateOverTime = 20;

            // Ring shape expanding outward
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.8f;
            shape.arc = 360f;
            shape.arcMode = ParticleSystemShapeMultiModeValue.Random;

            var colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new GradientColorKey[] {
                    new GradientColorKey(Color.white, 0f),
                    new GradientColorKey(Color.white, 0.5f)
                },
                new GradientAlphaKey[] {
                    new GradientAlphaKey(0.8f, 0f),
                    new GradientAlphaKey(0.4f, 0.5f),
                    new GradientAlphaKey(0f, 1f)
                }
            );
            colorOverLifetime.color = gradient;

            var sizeOverLifetime = ps.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(
                1f, AnimationCurve.EaseInOut(0f, 0.5f, 1f, 1.5f));

            // Radial velocity for expanding ring effect
            var velocityOverLifetime = ps.velocityOverLifetime;
            velocityOverLifetime.enabled = true;
            velocityOverLifetime.radial = new ParticleSystem.MinMaxCurve(0.5f);

            ConfigureParticleRenderer(go);

            go.SetActive(false);
            return ps;
        }

        /// <summary>
        /// Creates a dramatic absorption vortex/spiral particle system.
        /// Particles spawn on a ring and spiral inward using negative radial velocity
        /// combined with orbital rotation, creating a whirlpool suction visual.
        /// </summary>
        private ParticleSystem CreateAbsorptionVortexSystem()
        {
            var go = new GameObject("AbsorptionVortex");
            go.transform.SetParent(transform);
            var ps = go.AddComponent<ParticleSystem>();

            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.8f);
            // Low initial speed — radial pull does the work
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.1f, 0.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.2f);
            main.maxParticles = 60;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.playOnAwake = false;
            main.loop = true; // Continuous while absorption is happening

            var emission = ps.emission;
            emission.rateOverTime = 35;

            // Large circle — particles spawn at the outer edge and spiral inward
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 2f;
            shape.arc = 360f;
            shape.arcMode = ParticleSystemShapeMultiModeValue.Random;

            // Strong negative radial velocity pulls particles toward center
            var velocityOverLifetime = ps.velocityOverLifetime;
            velocityOverLifetime.enabled = true;
            velocityOverLifetime.radial = new ParticleSystem.MinMaxCurve(-4f, -2f);
            // Orbital Y rotation creates the spiral/vortex swirl
            velocityOverLifetime.orbitalY = new ParticleSystem.MinMaxCurve(3f, 5f);

            // Color: outer ring is bright, fades and intensifies as it spirals to center
            var colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new GradientColorKey[] {
                    new GradientColorKey(Color.white, 0f),
                    new GradientColorKey(new Color(0.8f, 0.9f, 1f), 0.5f),
                    new GradientColorKey(Color.white, 0.9f)
                },
                new GradientAlphaKey[] {
                    new GradientAlphaKey(0.3f, 0f),
                    new GradientAlphaKey(0.8f, 0.4f),
                    new GradientAlphaKey(1f, 0.8f),
                    new GradientAlphaKey(0f, 1f)
                }
            );
            colorOverLifetime.color = gradient;

            // Size: grows slightly as particles approach center then vanishes
            var sizeOverLifetime = ps.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(
                1f, new AnimationCurve(
                    new Keyframe(0f, 0.5f),
                    new Keyframe(0.6f, 1.2f),
                    new Keyframe(0.9f, 0.8f),
                    new Keyframe(1f, 0f)
                ));

            // Additive blending for energy vortex glow
            ConfigureParticleRenderer(go, default, true);

            go.SetActive(false);
            return ps;
        }

        /// <summary>
        /// Creates a screen-edge boundary glow particle system.
        /// Emits inward-facing particles from a ring around the camera for a
        /// warning glow at the edges of the screen when near arena boundary.
        /// </summary>
        private ParticleSystem CreateBoundaryGlowSystem()
        {
            var go = new GameObject("BoundaryGlow");
            go.transform.SetParent(transform);
            var ps = go.AddComponent<ParticleSystem>();

            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.7f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 1.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.3f, 0.8f);
            main.maxParticles = 50;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.playOnAwake = false;
            main.loop = true;
            // Warning glow amber color (overridden at runtime by proximity)
            main.startColor = new Color(1f, 0.5f, 0.1f, 0.4f);

            var emission = ps.emission;
            // Base rate, scaled at runtime by proximity
            emission.rateOverTime = 15;

            // Large ring around camera — particles glow inward from screen edges
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 4f;
            shape.arc = 360f;
            shape.arcMode = ParticleSystemShapeMultiModeValue.Random;

            // Negative radial velocity — particles drift inward from edges
            var velocityOverLifetime = ps.velocityOverLifetime;
            velocityOverLifetime.enabled = true;
            velocityOverLifetime.radial = new ParticleSystem.MinMaxCurve(-1.5f, -0.5f);

            // Color: warm glow that fades smoothly
            var colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new GradientColorKey[] {
                    new GradientColorKey(new Color(1f, 0.6f, 0.2f), 0f),
                    new GradientColorKey(new Color(1f, 0.3f, 0.1f), 0.5f),
                    new GradientColorKey(new Color(1f, 0.2f, 0.05f), 1f)
                },
                new GradientAlphaKey[] {
                    new GradientAlphaKey(0.8f, 0f),
                    new GradientAlphaKey(0.5f, 0.5f),
                    new GradientAlphaKey(0f, 1f)
                }
            );
            colorOverLifetime.color = gradient;

            // Size: soft glow blob that expands slightly then fades
            var sizeOverLifetime = ps.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(
                1f, new AnimationCurve(
                    new Keyframe(0f, 0.8f),
                    new Keyframe(0.3f, 1.2f),
                    new Keyframe(1f, 0.3f)
                ));

            // Noise for organic pulsing glow
            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.3f;
            noise.frequency = 1f;
            noise.damping = true;

            // Additive blending for glowing edge effect
            ConfigureParticleRenderer(go, new Color(1f, 0.5f, 0.1f), true);

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

            // Height-based clustering: flatten box toward ground (more dust near ground, less high up)
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(20f, 3f, 20f); // Reduced vertical spread for ground clustering
            shape.position = new Vector3(0f, -1.5f, 0f); // Shift emission center downward

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

            // Wind simulation: constant drift in one direction for atmospheric motion
            var velocityOverLifetime = ps.velocityOverLifetime;
            velocityOverLifetime.enabled = true;
            velocityOverLifetime.x = new ParticleSystem.MinMaxCurve(0.15f, 0.35f); // Drift along X
            velocityOverLifetime.y = new ParticleSystem.MinMaxCurve(-0.05f, 0.08f); // Slight vertical variation
            velocityOverLifetime.z = new ParticleSystem.MinMaxCurve(0.05f, 0.15f); // Secondary drift along Z

            // Add noise for organic floating movement on top of wind
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

        private ParticleSystem CreateFirefliesSystem()
        {
            var go = new GameObject("AmbientFireflies");
            go.transform.SetParent(transform);
            var ps = go.AddComponent<ParticleSystem>();

            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(3f, 6f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.1f, 0.4f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.08f);
            main.maxParticles = 40;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.playOnAwake = false;
            main.loop = true;
            main.gravityModifier = -0.03f; // Gentle float upward

            var emission = ps.emission;
            emission.rateOverTime = 8;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(25f, 5f, 25f);

            // Warm yellow-green sparkle colors
            var colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new GradientColorKey[] {
                    new GradientColorKey(new Color(1f, 0.95f, 0.6f), 0f),
                    new GradientColorKey(new Color(0.7f, 1f, 0.5f), 0.5f),
                    new GradientColorKey(new Color(1f, 0.9f, 0.4f), 1f)
                },
                new GradientAlphaKey[] {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(0.6f, 0.15f),
                    new GradientAlphaKey(0.6f, 0.4f),
                    new GradientAlphaKey(0f, 0.55f),
                    new GradientAlphaKey(0.5f, 0.7f),
                    new GradientAlphaKey(0f, 1f)
                }
            );
            colorOverLifetime.color = gradient;

            var sizeOverLifetime = ps.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(
                1f, AnimationCurve.EaseInOut(0f, 0.6f, 1f, 1.0f));

            // Noise for wandering firefly movement
            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.6f;
            noise.frequency = 0.8f;
            noise.scrollSpeed = 0.3f;
            noise.damping = true;

            ConfigureParticleRenderer(go, new Color(1f, 0.95f, 0.5f));

            go.SetActive(false);
            return ps;
        }

        /// <summary>Create a flat quad for ground shadow projection</summary>
        private GameObject CreateGroundShadowQuad()
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = "GroundShadow";

            // Remove collider (shadow is visual only)
            var col = go.GetComponent<Collider>();
            if (col != null) Destroy(col);

            // Rotate to lie flat on the ground
            go.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

            // Generate radial gradient texture (darker center, softer edges)
            Texture2D gradientTex = CreateRadialGradientTexture(64);

            // Create material with radial gradient for soft shadow edges
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Unlit/Transparent");
            if (shader == null) shader = Shader.Find("Unlit/Color");

            var material = new Material(shader);
            material.color = new Color(0f, 0f, 0f, 0.3f);
            material.mainTexture = gradientTex;
            material.SetFloat("_Surface", 1f); // Transparent
            material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.renderQueue = 2999; // Just below transparent
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");

            go.GetComponent<Renderer>().material = material;
            return go;
        }

        /// <summary>Create a radial gradient texture for soft-edged ground shadows</summary>
        private static Texture2D CreateRadialGradientTexture(int resolution)
        {
            var tex = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;

            float center = resolution * 0.5f;
            for (int y = 0; y < resolution; y++)
            {
                for (int x = 0; x < resolution; x++)
                {
                    float dx = (x - center) / center;
                    float dy = (y - center) / center;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    // Smooth falloff: fully opaque at center, transparent at edge
                    float alpha = Mathf.Clamp01(1f - Mathf.SmoothStep(0f, 1f, dist));
                    tex.SetPixel(x, y, new Color(0f, 0f, 0f, alpha));
                }
            }
            tex.Apply();
            return tex;
        }

        // ========================================
        // Shared helpers
        // ========================================

        /// <summary>
        /// Configure the ParticleSystemRenderer with a URP-compatible particle material.
        /// When additive is true, uses additive blending for bright energy/glow effects.
        /// Otherwise uses standard alpha blending for solid/translucent particles.
        /// </summary>
        private static void ConfigureParticleRenderer(GameObject go, Color color = default, bool additive = false)
        {
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            if (renderer == null) return;

            // Default to billboard; caller may override (e.g. Stretch for dash trail)
            if (renderer.renderMode != ParticleSystemRenderMode.Stretch)
            {
                renderer.renderMode = ParticleSystemRenderMode.Billboard;
            }

            // Try URP particle shader first, fall back to legacy
            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null) shader = Shader.Find("Particles/Standard Unlit");
            if (shader == null) shader = Shader.Find("Unlit/Color");

            var material = new Material(shader);
            material.color = color == default ? Color.white : color;
            // Enable transparency
            material.SetFloat("_Surface", 1f); // Transparent

            if (additive)
            {
                // Additive blending: bright energy glow, particles add to background
                material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One);
                material.renderQueue = 3100; // Above standard transparent
            }
            else
            {
                // Standard alpha blending for solid/translucent particles
                material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                material.renderQueue = 3000;
            }

            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            renderer.material = material;
        }
    }
}
