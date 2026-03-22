using UnityEngine;
using Blob3D.Core;

namespace Blob3D.Gameplay
{
    /// <summary>
    /// Collectible feed items scattered across the field.
    /// Deactivated on absorption and respawned by FeedSpawner.
    /// </summary>
    public class Feed : MonoBehaviour
    {
        // Geometric shape variants for visual diversity
        public enum FeedShape { Icosahedron, Diamond, Star }

        [Header("Feed Settings")]
        [SerializeField] private float nutritionValue = 0.5f;
        [SerializeField] private float rotateSpeed = 45f;
        [SerializeField] private float pulseSpeed = 2f;
        [SerializeField] private float pulseMin = 0.93f;
        [SerializeField] private float pulseMax = 1.07f;

        [Header("Crystal Animation")]
        [SerializeField] private float hoverAmplitude = 0.12f;
        [SerializeField] private float hoverSpeed = 1.5f;
        [SerializeField] private float tumbleSpeedX = 20f;
        [SerializeField] private float tumbleSpeedZ = 15f;

        [Header("Sparkle Effect")]
        [SerializeField] private float sparkleInterval = 0.8f;
        [SerializeField] private float sparkleIntensityMin = 0.3f;
        [SerializeField] private float sparkleIntensityMax = 1.5f;

        public float NutritionValue => nutritionValue;
        public bool IsActive { get; private set; } = true;

        /// <summary>Set nutrition value at runtime (e.g., for split blobs)</summary>
        public void SetNutrition(float value)
        {
            nutritionValue = value;
        }

        private Vector3 baseScale;
        private float baseY;
        private float phaseOffset;
        private float sizeVariation;
        private Renderer cachedRenderer;
        private Color baseEmissionColor;
        private float sparkleTimer;
        private float sparklePhase;
        private ParticleSystem sparklePS;

        private void Awake()
        {
            cachedRenderer = GetComponent<Renderer>();
        }

        private void Start()
        {
            baseScale = transform.localScale;
            baseY = transform.position.y;
            phaseOffset = Random.Range(0f, Mathf.PI * 2f);
            sparklePhase = Random.Range(0f, Mathf.PI * 2f);
            // Subtle size variation between individual feed items (0.85x ~ 1.15x)
            sizeVariation = Random.Range(0.85f, 1.15f);
            baseScale *= sizeVariation;

            // Cache the base emission color for sparkle modulation
            if (cachedRenderer != null && cachedRenderer.material != null)
            {
                baseEmissionColor = cachedRenderer.material.GetColor("_EmissionColor");
            }

            // Create sparkle particle system as a child
            CreateSparkleEffect();
        }

        private void Update()
        {
            if (!IsActive) return;

            float dt = Time.deltaTime;

            // Multi-axis tumbling rotation for crystal effect
            transform.Rotate(tumbleSpeedX * dt, rotateSpeed * dt, tumbleSpeedZ * dt);

            // Hover/float animation — bob up and down with sine wave
            float t = Time.time * hoverSpeed + phaseOffset;
            Vector3 pos = transform.position;
            pos.y = baseY + Mathf.Sin(t) * hoverAmplitude;
            transform.position = pos;

            // Subtle scale pulse (breathing effect)
            float pulseT = Time.time * pulseSpeed + phaseOffset;
            float pulse = Mathf.Lerp(pulseMin, pulseMax, (Mathf.Sin(pulseT) + 1f) * 0.5f);
            transform.localScale = baseScale * pulse;

            // Sparkle/twinkle emission modulation
            UpdateSparkle(dt);
        }

        /// <summary>Modulate emission intensity to create a sparkling twinkle effect</summary>
        private void UpdateSparkle(float dt)
        {
            if (cachedRenderer == null) return;

            sparkleTimer += dt;

            // Combine two sine waves at different frequencies for organic twinkle
            float wave1 = Mathf.Sin(Time.time * 4.5f + sparklePhase);
            float wave2 = Mathf.Sin(Time.time * 7.3f + sparklePhase * 1.7f);
            float combined = (wave1 + wave2) * 0.5f; // -1 to 1

            // Map to sparkle intensity range
            float intensity = Mathf.Lerp(sparkleIntensityMin, sparkleIntensityMax,
                (combined + 1f) * 0.5f);

            cachedRenderer.material.SetColor("_EmissionColor", baseEmissionColor * intensity);
        }

        /// <summary>Create a small particle system for ambient sparkle particles</summary>
        private void CreateSparkleEffect()
        {
            GameObject psObj = new GameObject("Sparkle");
            psObj.transform.SetParent(transform, false);
            psObj.transform.localPosition = Vector3.zero;

            sparklePS = psObj.AddComponent<ParticleSystem>();

            var main = sparklePS.main;
            main.maxParticles = 6;
            main.startLifetime = 0.5f;
            main.startSpeed = 0.3f;
            main.startSize = 0.03f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.loop = true;
            main.playOnAwake = true;

            // Use feed color for sparkle particles
            Color sparkleColor = Color.white;
            if (cachedRenderer != null && cachedRenderer.material != null)
                sparkleColor = cachedRenderer.material.color;
            main.startColor = new ParticleSystem.MinMaxGradient(
                Color.Lerp(sparkleColor, Color.white, 0.5f),
                Color.white
            );

            var emission = sparklePS.emission;
            emission.rateOverTime = 4f;

            var shape = sparklePS.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.15f;

            var colorOverLifetime = sparklePS.colorOverLifetime;
            colorOverLifetime.enabled = true;
            Gradient grad = new Gradient();
            grad.SetKeys(
                new GradientColorKey[] {
                    new GradientColorKey(Color.white, 0f),
                    new GradientColorKey(Color.white, 1f)
                },
                new GradientAlphaKey[] {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(1f, 0.3f),
                    new GradientAlphaKey(0f, 1f)
                }
            );
            colorOverLifetime.color = grad;

            var sizeOverLifetime = sparklePS.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f,
                AnimationCurve.Linear(0f, 1f, 1f, 0f));

            // Use default particle material
            var renderer = psObj.GetComponent<ParticleSystemRenderer>();
            renderer.material = new Material(Shader.Find("Particles/Standard Unlit"));
            renderer.material.SetFloat("_Mode", 1f); // Additive blending
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
                if (cachedRenderer != null && cachedRenderer.material != null)
                    feedColor = cachedRenderer.material.color;

                VFXManager.Instance?.PlayFeedBurst(transform.position, feedColor);
                AudioManager.Instance?.PlayFeedPop(nutritionValue);
            }

            gameObject.SetActive(false);

            // Notify FeedSpawner for respawn scheduling
            FeedSpawner.Instance?.ScheduleRespawn(this);
        }

        /// <summary>Re-initialize on respawn</summary>
        public void Respawn(Vector3 position)
        {
            transform.position = position;
            baseY = position.y;
            transform.localScale = baseScale;
            IsActive = true;
            gameObject.SetActive(true);
        }

        // =============================================
        // Procedural Mesh Generation (static helpers)
        // =============================================

        /// <summary>Generate a procedural mesh based on the specified feed shape type</summary>
        public static Mesh CreateFeedMesh(FeedShape shape)
        {
            switch (shape)
            {
                case FeedShape.Icosahedron: return CreateIcosahedronMesh();
                case FeedShape.Diamond:     return CreateDiamondMesh();
                case FeedShape.Star:        return CreateStarMesh();
                default:                    return CreateIcosahedronMesh();
            }
        }

        /// <summary>Create an icosahedron mesh — 20-face crystalline sphere</summary>
        private static Mesh CreateIcosahedronMesh()
        {
            Mesh mesh = new Mesh { name = "FeedIcosahedron" };

            // Golden ratio for icosahedron vertex placement
            float t = (1f + Mathf.Sqrt(5f)) / 2f;

            Vector3[] baseVerts = {
                new Vector3(-1,  t,  0).normalized,
                new Vector3( 1,  t,  0).normalized,
                new Vector3(-1, -t,  0).normalized,
                new Vector3( 1, -t,  0).normalized,
                new Vector3( 0, -1,  t).normalized,
                new Vector3( 0,  1,  t).normalized,
                new Vector3( 0, -1, -t).normalized,
                new Vector3( 0,  1, -t).normalized,
                new Vector3( t,  0, -1).normalized,
                new Vector3( t,  0,  1).normalized,
                new Vector3(-t,  0, -1).normalized,
                new Vector3(-t,  0,  1).normalized,
            };

            int[] triIndices = {
                0,11,5,  0,5,1,   0,1,7,   0,7,10,  0,10,11,
                1,5,9,   5,11,4,  11,10,2,  10,7,6,  7,1,8,
                3,9,4,   3,4,2,   3,2,6,   3,6,8,   3,8,9,
                4,9,5,   2,4,11,  6,2,10,  8,6,7,   9,8,1,
            };

            // Flat-shaded: duplicate vertices per triangle for sharp faceted look
            Vector3[] verts = new Vector3[triIndices.Length];
            Vector3[] normals = new Vector3[triIndices.Length];
            int[] tris = new int[triIndices.Length];

            for (int i = 0; i < triIndices.Length; i += 3)
            {
                Vector3 v0 = baseVerts[triIndices[i]];
                Vector3 v1 = baseVerts[triIndices[i + 1]];
                Vector3 v2 = baseVerts[triIndices[i + 2]];
                Vector3 normal = Vector3.Cross(v1 - v0, v2 - v0).normalized;

                verts[i]     = v0;
                verts[i + 1] = v1;
                verts[i + 2] = v2;
                normals[i]     = normal;
                normals[i + 1] = normal;
                normals[i + 2] = normal;
                tris[i]     = i;
                tris[i + 1] = i + 1;
                tris[i + 2] = i + 2;
            }

            mesh.vertices = verts;
            mesh.normals = normals;
            mesh.triangles = tris;
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>Create a diamond/bipyramid mesh — elongated octahedron crystal</summary>
        private static Mesh CreateDiamondMesh()
        {
            Mesh mesh = new Mesh { name = "FeedDiamond" };

            // Octahedron vertices: top point, bottom point, 4 equatorial
            float h = 1.4f; // Elongated vertically for diamond look
            Vector3[] baseVerts = {
                new Vector3( 0,  h,  0), // top
                new Vector3( 0, -h,  0), // bottom
                new Vector3( 1,  0,  0), // right
                new Vector3(-1,  0,  0), // left
                new Vector3( 0,  0,  1), // front
                new Vector3( 0,  0, -1), // back
            };

            int[] triIndices = {
                // Upper 4 faces
                0,4,2,  0,2,5,  0,5,3,  0,3,4,
                // Lower 4 faces
                1,2,4,  1,5,2,  1,3,5,  1,4,3,
            };

            // Flat-shaded for crystalline facets
            Vector3[] verts = new Vector3[triIndices.Length];
            Vector3[] normals = new Vector3[triIndices.Length];
            int[] tris = new int[triIndices.Length];

            for (int i = 0; i < triIndices.Length; i += 3)
            {
                Vector3 v0 = baseVerts[triIndices[i]];
                Vector3 v1 = baseVerts[triIndices[i + 1]];
                Vector3 v2 = baseVerts[triIndices[i + 2]];
                Vector3 normal = Vector3.Cross(v1 - v0, v2 - v0).normalized;

                verts[i]     = v0;
                verts[i + 1] = v1;
                verts[i + 2] = v2;
                normals[i]     = normal;
                normals[i + 1] = normal;
                normals[i + 2] = normal;
                tris[i]     = i;
                tris[i + 1] = i + 1;
                tris[i + 2] = i + 2;
            }

            mesh.vertices = verts;
            mesh.normals = normals;
            mesh.triangles = tris;
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>Create a 6-pointed star mesh — extruded star shape for pickup look</summary>
        private static Mesh CreateStarMesh()
        {
            Mesh mesh = new Mesh { name = "FeedStar" };

            // Star with 6 outer points and 6 inner valleys, extruded in Y
            int points = 6;
            float outerRadius = 1f;
            float innerRadius = 0.45f;
            float halfThickness = 0.25f;

            int vertCount = points * 2; // outer + inner alternating
            Vector3[] topRing = new Vector3[vertCount];
            Vector3[] bottomRing = new Vector3[vertCount];

            for (int i = 0; i < vertCount; i++)
            {
                float angle = Mathf.PI * 2f * i / vertCount;
                float r = (i % 2 == 0) ? outerRadius : innerRadius;
                float x = Mathf.Cos(angle) * r;
                float z = Mathf.Sin(angle) * r;
                topRing[i] = new Vector3(x, halfThickness, z);
                bottomRing[i] = new Vector3(x, -halfThickness, z);
            }

            // Build triangles: top face fan, bottom face fan, side quads
            var vertsList = new System.Collections.Generic.List<Vector3>();
            var normalsList = new System.Collections.Generic.List<Vector3>();
            var trisList = new System.Collections.Generic.List<int>();

            // Helper to add a flat-shaded triangle
            void AddTri(Vector3 a, Vector3 b, Vector3 c)
            {
                Vector3 n = Vector3.Cross(b - a, c - a).normalized;
                int idx = vertsList.Count;
                vertsList.Add(a); vertsList.Add(b); vertsList.Add(c);
                normalsList.Add(n); normalsList.Add(n); normalsList.Add(n);
                trisList.Add(idx); trisList.Add(idx + 1); trisList.Add(idx + 2);
            }

            Vector3 topCenter = new Vector3(0, halfThickness, 0);
            Vector3 bottomCenter = new Vector3(0, -halfThickness, 0);

            for (int i = 0; i < vertCount; i++)
            {
                int next = (i + 1) % vertCount;

                // Top face
                AddTri(topCenter, topRing[i], topRing[next]);

                // Bottom face (reversed winding)
                AddTri(bottomCenter, bottomRing[next], bottomRing[i]);

                // Side quad (two triangles)
                AddTri(topRing[i], bottomRing[i], bottomRing[next]);
                AddTri(topRing[i], bottomRing[next], topRing[next]);
            }

            mesh.vertices = vertsList.ToArray();
            mesh.normals = normalsList.ToArray();
            mesh.triangles = trisList.ToArray();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
