using UnityEngine;
using Blob3D.Core;
using Blob3D.Data;
using Blob3D.Player;

namespace Blob3D.Gameplay
{
    /// <summary>
    /// Power-up item on the field.
    /// Grants a temporary effect to the player upon collection.
    /// Each type has a unique procedural mesh, particle aura, and animation style.
    /// </summary>
    public class PowerUpItem : MonoBehaviour
    {
        public enum PowerUpType
        {
            SpeedBoost,   // 移動速度1.5倍
            Shield,       // 1回だけ吸収を防ぐ
            Magnet,       // 周囲のエサを自動吸引
            Ghost,        // 他blobをすり抜ける
            MegaGrowth    // 即座にサイズ2倍
        }

        [Header("PowerUp Settings")]
        [SerializeField] private PowerUpType type;
        [SerializeField] private float duration = 5f;
        [SerializeField] private float rotateSpeed = 120f;
        [SerializeField] private float glowIntensity = 2f;
        [SerializeField] private float glowPulseSpeed = 2f;
        [SerializeField] private float glowPulseMin = 0.5f;
        [SerializeField] private float glowPulseMax = 2f;

        [Header("Hover Animation")]
        [SerializeField] private float hoverAmplitude = 0.15f;
        [SerializeField] private float hoverFrequency = 1.5f;

        [Header("Orbit Effect")]
        [SerializeField] private float orbitRadius = 0.35f;
        [SerializeField] private float orbitSpeed1 = 180f;
        [SerializeField] private float orbitSpeed2 = 240f;

        public bool IsActive { get; private set; } = true;
        public PowerUpType Type => type;

        private float phaseOffset;
        private Renderer cachedRenderer;
        private Color baseTypeColor;
        private Vector3 basePosition;
        private MeshFilter meshFilter;
        private ParticleSystem auraParticles;

        // Orbit child objects
        private Transform orbitChild1;
        private Transform orbitChild2;

        // Cached meshes per type (shared across instances to avoid GC)
        private static Mesh meshSpeedBolt;
        private static Mesh meshShieldHex;
        private static Mesh meshMagnetU;
        private static Mesh meshGhostRing;
        private static Mesh meshMegaDiamond;

        private void Start()
        {
            phaseOffset = Random.Range(0f, Mathf.PI * 2f);
            cachedRenderer = GetComponent<Renderer>();
            meshFilter = GetComponent<MeshFilter>();
            baseTypeColor = GetTypeColor();
            basePosition = transform.position;
            ApplyTypeShape();
            CreateOrbitChildren();
            CreateAuraParticles();
        }

        private void Update()
        {
            if (!IsActive) return;

            // Type-specific rotation styles for variety
            float dt = Time.deltaTime;
            switch (type)
            {
                case PowerUpType.SpeedBoost:
                    // Fast spin around Y with slight wobble
                    transform.Rotate(Vector3.up, rotateSpeed * 1.5f * dt, Space.World);
                    transform.Rotate(Vector3.forward, Mathf.Sin(Time.time * 3f) * 15f * dt, Space.Self);
                    break;
                case PowerUpType.Shield:
                    // Slow majestic rotation
                    transform.Rotate(Vector3.up, rotateSpeed * 0.6f * dt, Space.World);
                    break;
                case PowerUpType.Magnet:
                    // Rocking motion like a compass needle
                    transform.Rotate(Vector3.up, rotateSpeed * 0.8f * dt, Space.World);
                    float rockAngle = Mathf.Sin(Time.time * 2f + phaseOffset) * 12f;
                    transform.localRotation = Quaternion.Euler(rockAngle, transform.localEulerAngles.y, 0f);
                    break;
                case PowerUpType.Ghost:
                    // Ethereal slow drift with multi-axis wobble
                    transform.Rotate(Vector3.up, rotateSpeed * 0.4f * dt, Space.World);
                    transform.Rotate(Vector3.right, rotateSpeed * 0.2f * dt, Space.Self);
                    break;
                case PowerUpType.MegaGrowth:
                    // Pulsing scale + rotation
                    transform.Rotate(Vector3.up, rotateSpeed * dt, Space.World);
                    transform.Rotate(Vector3.right, rotateSpeed * 0.5f * dt, Space.Self);
                    float scalePulse = 1f + Mathf.Sin(Time.time * 3f + phaseOffset) * 0.08f;
                    transform.localScale = GetBaseScale() * scalePulse;
                    break;
                default:
                    transform.Rotate(Vector3.up, rotateSpeed * dt, Space.World);
                    break;
            }

            // Hover / bobbing animation (sine wave)
            float hoverOffset = Mathf.Sin((Time.time * hoverFrequency + phaseOffset) * Mathf.PI * 2f) * hoverAmplitude;
            Vector3 pos = transform.position;
            pos.y = basePosition.y + hoverOffset;
            transform.position = pos;

            // Pulsing emission glow
            if (cachedRenderer != null)
            {
                float t = Time.time * glowPulseSpeed + phaseOffset;
                float intensity = Mathf.Lerp(glowPulseMin, glowPulseMax, (Mathf.Sin(t) + 1f) * 0.5f);
                cachedRenderer.material.SetColor("_EmissionColor", baseTypeColor * intensity);
            }

            // Update orbit children positions
            UpdateOrbitChildren();
        }

        /// <summary>Return the base scale vector for the current type.</summary>
        private Vector3 GetBaseScale()
        {
            return type switch
            {
                PowerUpType.SpeedBoost => Vector3.one * 0.35f,
                PowerUpType.Shield     => Vector3.one * 0.38f,
                PowerUpType.Magnet     => Vector3.one * 0.35f,
                PowerUpType.Ghost      => new Vector3(0.4f, 0.2f, 0.4f),
                PowerUpType.MegaGrowth => Vector3.one * 0.4f,
                _ => Vector3.one * 0.3f
            };
        }

        /// <summary>Apply a unique procedural mesh and scale per power-up type.</summary>
        private void ApplyTypeShape()
        {
            if (meshFilter == null) return;

            transform.localScale = GetBaseScale();

            switch (type)
            {
                case PowerUpType.SpeedBoost:
                    meshFilter.sharedMesh = GetOrCreateSpeedBoltMesh();
                    break;
                case PowerUpType.Shield:
                    meshFilter.sharedMesh = GetOrCreateShieldHexMesh();
                    break;
                case PowerUpType.Magnet:
                    meshFilter.sharedMesh = GetOrCreateMagnetUMesh();
                    break;
                case PowerUpType.Ghost:
                    meshFilter.sharedMesh = GetOrCreateGhostRingMesh();
                    SetGhostTransparency();
                    break;
                case PowerUpType.MegaGrowth:
                    meshFilter.sharedMesh = GetOrCreateMegaDiamondMesh();
                    break;
            }
        }

        // ========================================
        // Procedural Mesh Generators (static/cached)
        // ========================================

        /// <summary>Lightning bolt / arrow shape for SpeedBoost.</summary>
        private static Mesh GetOrCreateSpeedBoltMesh()
        {
            if (meshSpeedBolt != null) return meshSpeedBolt;

            meshSpeedBolt = new Mesh { name = "PowerUp_SpeedBolt" };

            // 2D lightning bolt profile extruded into 3D
            // The bolt shape: top point -> zigzag -> bottom point
            float depth = 0.25f;

            // Front face vertices (lightning bolt silhouette)
            Vector3[] frontProfile = new Vector3[]
            {
                new Vector3( 0.0f,  1.0f,  0f),  // 0: top tip
                new Vector3(-0.25f, 0.35f, 0f),  // 1: upper-left notch
                new Vector3( 0.15f, 0.25f, 0f),  // 2: upper-right jog
                new Vector3(-0.15f,-0.25f, 0f),  // 3: lower-left jog
                new Vector3( 0.25f,-0.35f, 0f),  // 4: lower-right notch
                new Vector3( 0.0f, -1.0f,  0f),  // 5: bottom tip
            };

            // Build extruded mesh with front, back, and side faces
            int profileCount = frontProfile.Length;
            // We'll create a prism by duplicating front/back and connecting sides
            Vector3[] vertices = new Vector3[profileCount * 2 + (profileCount) * 4];
            Vector3[] normals = new Vector3[vertices.Length];
            int[] triangles;

            // Front face (z = -depth/2)
            for (int i = 0; i < profileCount; i++)
            {
                vertices[i] = frontProfile[i] + Vector3.back * depth * 0.5f;
                normals[i] = Vector3.back;
            }
            // Back face (z = +depth/2)
            for (int i = 0; i < profileCount; i++)
            {
                vertices[profileCount + i] = frontProfile[i] + Vector3.forward * depth * 0.5f;
                normals[profileCount + i] = Vector3.forward;
            }

            // Front and back face triangles (fan from vertex 0)
            var triList = new System.Collections.Generic.List<int>();

            // Front face (CW winding looking from -Z)
            for (int i = 1; i < profileCount - 1; i++)
            {
                triList.Add(0);
                triList.Add(i + 1);
                triList.Add(i);
            }
            // Back face (CCW winding looking from +Z)
            for (int i = 1; i < profileCount - 1; i++)
            {
                triList.Add(profileCount);
                triList.Add(profileCount + i);
                triList.Add(profileCount + i + 1);
            }

            // Side faces - quads connecting front edge to back edge
            int sideStart = profileCount * 2;
            for (int i = 0; i < profileCount; i++)
            {
                int next = (i + 1) % profileCount;
                int vi = sideStart + i * 4;

                vertices[vi + 0] = frontProfile[i] + Vector3.back * depth * 0.5f;
                vertices[vi + 1] = frontProfile[next] + Vector3.back * depth * 0.5f;
                vertices[vi + 2] = frontProfile[next] + Vector3.forward * depth * 0.5f;
                vertices[vi + 3] = frontProfile[i] + Vector3.forward * depth * 0.5f;

                Vector3 edge1 = frontProfile[next] - frontProfile[i];
                Vector3 sideNormal = Vector3.Cross(edge1, Vector3.forward).normalized;
                normals[vi + 0] = sideNormal;
                normals[vi + 1] = sideNormal;
                normals[vi + 2] = sideNormal;
                normals[vi + 3] = sideNormal;

                triList.Add(vi + 0);
                triList.Add(vi + 1);
                triList.Add(vi + 2);
                triList.Add(vi + 0);
                triList.Add(vi + 2);
                triList.Add(vi + 3);
            }

            meshSpeedBolt.vertices = vertices;
            meshSpeedBolt.normals = normals;
            meshSpeedBolt.triangles = triList.ToArray();
            meshSpeedBolt.RecalculateBounds();
            return meshSpeedBolt;
        }

        /// <summary>Hexagonal shield shape for Shield power-up.</summary>
        private static Mesh GetOrCreateShieldHexMesh()
        {
            if (meshShieldHex != null) return meshShieldHex;

            meshShieldHex = new Mesh { name = "PowerUp_ShieldHex" };

            // Hexagonal shield: a thick hexagonal disc with beveled edges
            int sides = 6;
            float outerRadius = 1.0f;
            float innerRadius = 0.65f;
            float halfThick = 0.15f;

            var verts = new System.Collections.Generic.List<Vector3>();
            var norms = new System.Collections.Generic.List<Vector3>();
            var tris = new System.Collections.Generic.List<int>();

            // Generate outer and inner hex rings on top and bottom
            // Top outer, Top inner, Bottom outer, Bottom inner
            Vector3[] topOuter = new Vector3[sides];
            Vector3[] topInner = new Vector3[sides];
            Vector3[] botOuter = new Vector3[sides];
            Vector3[] botInner = new Vector3[sides];

            for (int i = 0; i < sides; i++)
            {
                float angle = i * Mathf.PI * 2f / sides - Mathf.PI / 6f;
                float cos = Mathf.Cos(angle);
                float sin = Mathf.Sin(angle);
                topOuter[i] = new Vector3(cos * outerRadius, halfThick, sin * outerRadius);
                topInner[i] = new Vector3(cos * innerRadius, halfThick, sin * innerRadius);
                botOuter[i] = new Vector3(cos * outerRadius, -halfThick, sin * outerRadius);
                botInner[i] = new Vector3(cos * innerRadius, -halfThick, sin * innerRadius);
            }

            // Helper: add a quad
            void AddQuad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 n)
            {
                int idx = verts.Count;
                verts.Add(a); verts.Add(b); verts.Add(c); verts.Add(d);
                norms.Add(n); norms.Add(n); norms.Add(n); norms.Add(n);
                tris.Add(idx); tris.Add(idx + 1); tris.Add(idx + 2);
                tris.Add(idx); tris.Add(idx + 2); tris.Add(idx + 3);
            }

            for (int i = 0; i < sides; i++)
            {
                int next = (i + 1) % sides;

                // Top face (ring segment): two triangles for the trapezoid
                AddQuad(topInner[i], topOuter[i], topOuter[next], topInner[next], Vector3.up);

                // Bottom face
                AddQuad(botInner[next], botOuter[next], botOuter[i], botInner[i], Vector3.down);

                // Outer edge
                Vector3 outNorm = new Vector3(
                    (topOuter[i].x + topOuter[next].x) * 0.5f,
                    0f,
                    (topOuter[i].z + topOuter[next].z) * 0.5f
                ).normalized;
                AddQuad(topOuter[i], botOuter[i], botOuter[next], topOuter[next], outNorm);

                // Inner edge
                Vector3 inNorm = -new Vector3(
                    (topInner[i].x + topInner[next].x) * 0.5f,
                    0f,
                    (topInner[i].z + topInner[next].z) * 0.5f
                ).normalized;
                AddQuad(topInner[next], botInner[next], botInner[i], topInner[i], inNorm);
            }

            // Fill the center hole with a solid hex on top and bottom
            int centerTopIdx = verts.Count;
            verts.Add(new Vector3(0, halfThick, 0));
            norms.Add(Vector3.up);
            for (int i = 0; i < sides; i++)
            {
                verts.Add(topInner[i]);
                norms.Add(Vector3.up);
            }
            for (int i = 0; i < sides; i++)
            {
                int next = (i + 1) % sides;
                tris.Add(centerTopIdx);
                tris.Add(centerTopIdx + 1 + i);
                tris.Add(centerTopIdx + 1 + next);
            }

            int centerBotIdx = verts.Count;
            verts.Add(new Vector3(0, -halfThick, 0));
            norms.Add(Vector3.down);
            for (int i = 0; i < sides; i++)
            {
                verts.Add(botInner[i]);
                norms.Add(Vector3.down);
            }
            for (int i = 0; i < sides; i++)
            {
                int next = (i + 1) % sides;
                tris.Add(centerBotIdx);
                tris.Add(centerBotIdx + 1 + next);
                tris.Add(centerBotIdx + 1 + i);
            }

            meshShieldHex.vertices = verts.ToArray();
            meshShieldHex.normals = norms.ToArray();
            meshShieldHex.triangles = tris.ToArray();
            meshShieldHex.RecalculateBounds();
            return meshShieldHex;
        }

        /// <summary>Horseshoe / U-shape for Magnet power-up.</summary>
        private static Mesh GetOrCreateMagnetUMesh()
        {
            if (meshMagnetU != null) return meshMagnetU;

            meshMagnetU = new Mesh { name = "PowerUp_MagnetU" };

            // U-shape: half-torus with two straight prongs
            int arcSegments = 12;
            float outerR = 0.8f;
            float innerR = 0.45f;
            float tubeR = (outerR - innerR) * 0.5f;
            float midR = (outerR + innerR) * 0.5f;
            float prongLength = 0.5f;
            float halfThick = tubeR;

            var verts = new System.Collections.Generic.List<Vector3>();
            var norms = new System.Collections.Generic.List<Vector3>();
            var tris = new System.Collections.Generic.List<int>();

            // Generate the U-shaped tube cross-section along a half-circle arc
            int tubeSegs = 8;

            // Get a ring of vertices around the tube at a given arc position
            Vector3[] GetTubeRing(Vector3 center, Vector3 forward, Vector3 up, float radius, int segs)
            {
                Vector3[] ring = new Vector3[segs];
                Vector3 right = Vector3.Cross(forward, up).normalized;
                for (int i = 0; i < segs; i++)
                {
                    float a = i * Mathf.PI * 2f / segs;
                    ring[i] = center + (up * Mathf.Cos(a) + right * Mathf.Sin(a)) * radius;
                }
                return ring;
            }

            Vector3[] GetTubeNormals(Vector3 center, Vector3[] ring, int segs)
            {
                Vector3[] normArr = new Vector3[segs];
                for (int i = 0; i < segs; i++)
                    normArr[i] = (ring[i] - center).normalized;
                return normArr;
            }

            void ConnectRings(Vector3[] ringA, Vector3[] normA, Vector3[] ringB, Vector3[] normB, int segs)
            {
                int baseIdx = verts.Count;
                for (int i = 0; i < segs; i++)
                {
                    verts.Add(ringA[i]); norms.Add(normA[i]);
                    verts.Add(ringB[i]); norms.Add(normB[i]);
                }
                for (int i = 0; i < segs; i++)
                {
                    int next = (i + 1) % segs;
                    int a0 = baseIdx + i * 2;
                    int a1 = baseIdx + i * 2 + 1;
                    int b0 = baseIdx + next * 2;
                    int b1 = baseIdx + next * 2 + 1;
                    tris.Add(a0); tris.Add(b0); tris.Add(a1);
                    tris.Add(a1); tris.Add(b0); tris.Add(b1);
                }
            }

            void CapRing(Vector3[] ring, Vector3 normal, int segs)
            {
                int center = verts.Count;
                Vector3 c = Vector3.zero;
                for (int i = 0; i < segs; i++) c += ring[i];
                c /= segs;
                verts.Add(c); norms.Add(normal);
                for (int i = 0; i < segs; i++) { verts.Add(ring[i]); norms.Add(normal); }

                bool flip = Vector3.Dot(normal, Vector3.up) < 0 || Vector3.Dot(normal, Vector3.down) < 0;
                for (int i = 0; i < segs; i++)
                {
                    int next = (i + 1) % segs;
                    if (Vector3.Dot(normal, Vector3.forward) > 0 || Vector3.Dot(normal, Vector3.down) > 0)
                    {
                        tris.Add(center); tris.Add(center + 1 + next); tris.Add(center + 1 + i);
                    }
                    else
                    {
                        tris.Add(center); tris.Add(center + 1 + i); tris.Add(center + 1 + next);
                    }
                }
            }

            // Arc portion of the U (from angle 0 to PI, i.e. bottom half-circle)
            Vector3[] prevRing = null;
            Vector3[] prevNorm = null;

            for (int i = 0; i <= arcSegments; i++)
            {
                float angle = Mathf.PI * i / arcSegments; // 0 to PI
                Vector3 arcCenter = new Vector3(Mathf.Cos(angle) * midR, Mathf.Sin(angle) * midR, 0f);
                Vector3 arcForward = new Vector3(-Mathf.Sin(angle), Mathf.Cos(angle), 0f);
                Vector3 arcUp = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);

                Vector3[] ring = GetTubeRing(arcCenter, arcForward, arcUp, tubeR, tubeSegs);
                Vector3[] ringN = GetTubeNormals(arcCenter, ring, tubeSegs);

                if (prevRing != null)
                    ConnectRings(prevRing, prevNorm, ring, ringN, tubeSegs);

                prevRing = ring;
                prevNorm = ringN;
            }

            // Left prong (extends downward from arc start at angle=0 -> right side)
            Vector3 rightProngBase = new Vector3(midR, 0, 0);
            Vector3 rightProngEnd = new Vector3(midR, -prongLength, 0);

            Vector3[] rightBaseRing = GetTubeRing(rightProngBase, Vector3.down, Vector3.right, tubeR, tubeSegs);
            Vector3[] rightBaseNorm = GetTubeNormals(rightProngBase, rightBaseRing, tubeSegs);
            ConnectRings(prevRing, prevNorm, rightBaseRing, rightBaseNorm, tubeSegs);

            Vector3[] rightEndRing = GetTubeRing(rightProngEnd, Vector3.down, Vector3.right, tubeR, tubeSegs);
            Vector3[] rightEndNorm = GetTubeNormals(rightProngEnd, rightEndRing, tubeSegs);
            ConnectRings(rightBaseRing, rightBaseNorm, rightEndRing, rightEndNorm, tubeSegs);
            CapRing(rightEndRing, Vector3.down, tubeSegs);

            // Right prong (extends downward from arc end at angle=PI -> left side)
            Vector3 leftProngBase = new Vector3(-midR, 0, 0);
            Vector3 leftProngEnd = new Vector3(-midR, -prongLength, 0);

            // Rebuild the ring at the arc start (angle=0) for the left prong
            Vector3 arcStartCenter = new Vector3(-midR, 0, 0);
            Vector3[] leftArcRing = GetTubeRing(arcStartCenter, Vector3.down, Vector3.left, tubeR, tubeSegs);
            Vector3[] leftArcNorm = GetTubeNormals(arcStartCenter, leftArcRing, tubeSegs);

            // Get the first ring of the arc for connecting
            float firstAngle = 0f;
            Vector3 firstCenter = new Vector3(Mathf.Cos(firstAngle) * midR, Mathf.Sin(firstAngle) * midR, 0f);
            Vector3[] firstRing = GetTubeRing(firstCenter, Vector3.up, Vector3.right, tubeR, tubeSegs);
            Vector3[] firstNorm = GetTubeNormals(firstCenter, firstRing, tubeSegs);

            Vector3[] leftBaseRing = GetTubeRing(leftProngBase, Vector3.down, Vector3.left, tubeR, tubeSegs);
            Vector3[] leftBaseNorm = GetTubeNormals(leftProngBase, leftBaseRing, tubeSegs);

            Vector3[] leftEndRing = GetTubeRing(leftProngEnd, Vector3.down, Vector3.left, tubeR, tubeSegs);
            Vector3[] leftEndNorm = GetTubeNormals(leftProngEnd, leftEndRing, tubeSegs);
            ConnectRings(leftBaseRing, leftBaseNorm, leftEndRing, leftEndNorm, tubeSegs);
            CapRing(leftEndRing, Vector3.down, tubeSegs);

            // Cap the prong at right start
            Vector3[] firstProngRing = GetTubeRing(rightProngBase, Vector3.up, Vector3.right, tubeR, tubeSegs);
            Vector3[] firstProngNorm = GetTubeNormals(rightProngBase, firstProngRing, tubeSegs);

            meshMagnetU.vertices = verts.ToArray();
            meshMagnetU.normals = norms.ToArray();
            meshMagnetU.triangles = tris.ToArray();
            meshMagnetU.RecalculateBounds();
            meshMagnetU.RecalculateNormals();
            return meshMagnetU;
        }

        /// <summary>Ethereal torus / ring shape for Ghost power-up.</summary>
        private static Mesh GetOrCreateGhostRingMesh()
        {
            if (meshGhostRing != null) return meshGhostRing;

            meshGhostRing = new Mesh { name = "PowerUp_GhostRing" };

            // Torus with wavy distortion for ethereal look
            int mainSegs = 24;   // around the ring
            int tubeSegs = 10;   // cross-section of the tube
            float mainRadius = 0.8f;
            float tubeRadius = 0.25f;

            int vertCount = mainSegs * tubeSegs;
            Vector3[] vertices = new Vector3[vertCount];
            Vector3[] normals = new Vector3[vertCount];
            int[] triangles = new int[mainSegs * tubeSegs * 6];

            for (int i = 0; i < mainSegs; i++)
            {
                float mainAngle = i * Mathf.PI * 2f / mainSegs;
                // Add wave distortion for ethereal effect
                float waveOffset = Mathf.Sin(mainAngle * 3f) * 0.08f;
                float currentTubeR = tubeRadius + Mathf.Sin(mainAngle * 5f) * 0.04f;

                Vector3 center = new Vector3(
                    Mathf.Cos(mainAngle) * mainRadius,
                    waveOffset,
                    Mathf.Sin(mainAngle) * mainRadius
                );

                Vector3 radialDir = new Vector3(Mathf.Cos(mainAngle), 0f, Mathf.Sin(mainAngle)).normalized;

                for (int j = 0; j < tubeSegs; j++)
                {
                    float tubeAngle = j * Mathf.PI * 2f / tubeSegs;
                    Vector3 tubeOffset = radialDir * (Mathf.Cos(tubeAngle) * currentTubeR)
                                       + Vector3.up * (Mathf.Sin(tubeAngle) * currentTubeR);

                    int idx = i * tubeSegs + j;
                    vertices[idx] = center + tubeOffset;
                    normals[idx] = tubeOffset.normalized;
                }
            }

            // Triangles
            int triIdx = 0;
            for (int i = 0; i < mainSegs; i++)
            {
                int nextI = (i + 1) % mainSegs;
                for (int j = 0; j < tubeSegs; j++)
                {
                    int nextJ = (j + 1) % tubeSegs;
                    int a = i * tubeSegs + j;
                    int b = nextI * tubeSegs + j;
                    int c = nextI * tubeSegs + nextJ;
                    int d = i * tubeSegs + nextJ;

                    triangles[triIdx++] = a;
                    triangles[triIdx++] = b;
                    triangles[triIdx++] = c;
                    triangles[triIdx++] = a;
                    triangles[triIdx++] = c;
                    triangles[triIdx++] = d;
                }
            }

            meshGhostRing.vertices = vertices;
            meshGhostRing.normals = normals;
            meshGhostRing.triangles = triangles;
            meshGhostRing.RecalculateBounds();
            return meshGhostRing;
        }

        /// <summary>Diamond / octahedron shape for MegaGrowth power-up.</summary>
        private static Mesh GetOrCreateMegaDiamondMesh()
        {
            if (meshMegaDiamond != null) return meshMegaDiamond;

            meshMegaDiamond = new Mesh { name = "PowerUp_MegaDiamond" };

            // Beveled octahedron (diamond shape) with 6 vertices
            float h = 1.2f;  // top/bottom height
            float w = 0.8f;  // equatorial width

            Vector3 top    = new Vector3( 0,  h, 0);
            Vector3 bottom = new Vector3( 0, -h, 0);
            Vector3 front  = new Vector3( 0,  0,  w);
            Vector3 back   = new Vector3( 0,  0, -w);
            Vector3 left   = new Vector3(-w,  0,  0);
            Vector3 right  = new Vector3( w,  0,  0);

            // 8 triangular faces — each face gets its own vertices for flat shading
            Vector3[][] faces = new Vector3[][]
            {
                // Upper 4 faces
                new[] { top, front, right },
                new[] { top, right, back },
                new[] { top, back, left },
                new[] { top, left, front },
                // Lower 4 faces
                new[] { bottom, right, front },
                new[] { bottom, back, right },
                new[] { bottom, left, back },
                new[] { bottom, front, left },
            };

            Vector3[] vertices = new Vector3[faces.Length * 3];
            Vector3[] normals = new Vector3[vertices.Length];
            int[] triangles = new int[vertices.Length];

            for (int f = 0; f < faces.Length; f++)
            {
                int i0 = f * 3;
                vertices[i0] = faces[f][0];
                vertices[i0 + 1] = faces[f][1];
                vertices[i0 + 2] = faces[f][2];

                Vector3 normal = Vector3.Cross(
                    faces[f][1] - faces[f][0],
                    faces[f][2] - faces[f][0]
                ).normalized;

                normals[i0] = normal;
                normals[i0 + 1] = normal;
                normals[i0 + 2] = normal;

                triangles[i0] = i0;
                triangles[i0 + 1] = i0 + 1;
                triangles[i0 + 2] = i0 + 2;
            }

            meshMegaDiamond.vertices = vertices;
            meshMegaDiamond.normals = normals;
            meshMegaDiamond.triangles = triangles;
            meshMegaDiamond.RecalculateBounds();
            return meshMegaDiamond;
        }

        /// <summary>Make the ghost powerup semi-transparent at runtime.</summary>
        private void SetGhostTransparency()
        {
            if (cachedRenderer == null) return;

            Material mat = cachedRenderer.material;
            Color c = mat.color;
            c.a = 0.45f;
            mat.color = c;

            // Enable transparency rendering
            mat.SetFloat("_Surface", 1);
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.renderQueue = 3000;
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        }

        /// <summary>Create small sphere children that orbit around the powerup.</summary>
        private void CreateOrbitChildren()
        {
            orbitChild1 = CreateOrbitSphere("Orbit1", 0.06f);
            orbitChild2 = CreateOrbitSphere("Orbit2", 0.05f);
        }

        private Transform CreateOrbitSphere(string name, float size)
        {
            GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.name = name;
            sphere.transform.SetParent(transform.parent);
            sphere.transform.localScale = Vector3.one * size;

            // Remove collider so it does not interfere with gameplay
            Collider col = sphere.GetComponent<Collider>();
            if (col != null) Destroy(col);

            // Copy material color from parent
            Renderer rend = sphere.GetComponent<Renderer>();
            if (rend != null && cachedRenderer != null)
            {
                rend.material.color = baseTypeColor;
                rend.material.EnableKeyword("_EMISSION");
                rend.material.SetColor("_EmissionColor", baseTypeColor * glowIntensity);
            }

            return sphere.transform;
        }

        /// <summary>Create a particle system aura that trails/glows around the power-up.</summary>
        private void CreateAuraParticles()
        {
            // Create particle system as child
            GameObject psObj = new GameObject("AuraParticles");
            psObj.transform.SetParent(transform);
            psObj.transform.localPosition = Vector3.zero;

            auraParticles = psObj.AddComponent<ParticleSystem>();

            var main = auraParticles.main;
            main.loop = true;
            main.startLifetime = 0.8f;
            main.startSpeed = 0.3f;
            main.startSize = 0.06f;
            main.maxParticles = 30;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startColor = new ParticleSystem.MinMaxGradient(
                baseTypeColor,
                new Color(baseTypeColor.r, baseTypeColor.g, baseTypeColor.b, 0.3f)
            );

            var emission = auraParticles.emission;
            emission.rateOverTime = 20f;

            var shape = auraParticles.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.15f;

            var sizeOverLifetime = auraParticles.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            AnimationCurve sizeCurve = new AnimationCurve(
                new Keyframe(0f, 1f),
                new Keyframe(1f, 0f)
            );
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

            var colorOverLifetime = auraParticles.colorOverLifetime;
            colorOverLifetime.enabled = true;
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new GradientColorKey[] {
                    new GradientColorKey(baseTypeColor, 0f),
                    new GradientColorKey(baseTypeColor, 1f)
                },
                new GradientAlphaKey[] {
                    new GradientAlphaKey(0.8f, 0f),
                    new GradientAlphaKey(0f, 1f)
                }
            );
            colorOverLifetime.color = gradient;

            // Use additive particle material
            ParticleSystemRenderer psRenderer = psObj.GetComponent<ParticleSystemRenderer>();
            psRenderer.material = new Material(Shader.Find("Particles/Standard Unlit") ?? Shader.Find("Legacy Shaders/Particles/Additive"));
            psRenderer.material.color = baseTypeColor;

            // Type-specific particle adjustments
            switch (type)
            {
                case PowerUpType.SpeedBoost:
                    // Trailing upward sparks
                    main.startSpeed = 0.6f;
                    main.startLifetime = 0.5f;
                    main.startSize = 0.04f;
                    var velSpeed = auraParticles.velocityOverLifetime;
                    velSpeed.enabled = true;
                    velSpeed.y = 0.5f;
                    emission.rateOverTime = 25f;
                    break;
                case PowerUpType.Shield:
                    // Slow outward expanding ring particles
                    main.startSpeed = 0.15f;
                    main.startLifetime = 1.2f;
                    main.startSize = 0.05f;
                    shape.shapeType = ParticleSystemShapeType.Circle;
                    shape.radius = 0.25f;
                    emission.rateOverTime = 15f;
                    break;
                case PowerUpType.Magnet:
                    // Particles drawn inward (negative speed)
                    main.startSpeed = -0.2f;
                    shape.radius = 0.35f;
                    emission.rateOverTime = 25f;
                    break;
                case PowerUpType.Ghost:
                    // Wispy, slow-moving fog-like particles
                    main.startLifetime = 1.5f;
                    main.startSpeed = 0.1f;
                    main.startSize = 0.1f;
                    main.startColor = new ParticleSystem.MinMaxGradient(
                        new Color(0.8f, 0.8f, 1f, 0.3f),
                        new Color(1f, 1f, 1f, 0.1f)
                    );
                    emission.rateOverTime = 12f;
                    break;
                case PowerUpType.MegaGrowth:
                    // Outward burst particles
                    main.startSpeed = 0.5f;
                    main.startSize = 0.07f;
                    shape.radius = 0.1f;
                    emission.rateOverTime = 30f;
                    break;
            }
        }

        private void UpdateOrbitChildren()
        {
            if (orbitChild1 != null)
            {
                float angle1 = Time.time * orbitSpeed1 + phaseOffset * Mathf.Rad2Deg;
                Vector3 offset1 = new Vector3(
                    Mathf.Cos(angle1 * Mathf.Deg2Rad) * orbitRadius,
                    Mathf.Sin(angle1 * 2f * Mathf.Deg2Rad) * 0.08f,
                    Mathf.Sin(angle1 * Mathf.Deg2Rad) * orbitRadius
                );
                orbitChild1.position = transform.position + offset1;
            }

            if (orbitChild2 != null)
            {
                float angle2 = Time.time * orbitSpeed2 + (phaseOffset + Mathf.PI) * Mathf.Rad2Deg;
                Vector3 offset2 = new Vector3(
                    Mathf.Sin(angle2 * Mathf.Deg2Rad) * orbitRadius * 0.8f,
                    Mathf.Cos(angle2 * 1.5f * Mathf.Deg2Rad) * 0.06f,
                    Mathf.Cos(angle2 * Mathf.Deg2Rad) * orbitRadius * 0.8f
                );
                orbitChild2.position = transform.position + offset2;
            }
        }

        /// <summary>Called when a player collects this power-up.</summary>
        public void Collect(BlobController player)
        {
            if (!IsActive) return;
            IsActive = false;

            // VFX: get type color before deactivating
            Color typeColor = GetTypeColor();
            Vector3 pos = transform.position;

            ApplyEffect(player);
            AchievementManager.Instance?.CheckInstant("first_powerup");
            AudioManager.Instance?.PlayPowerUp();
            VFXManager.Instance?.PlayPowerUpAura(pos, typeColor);

            // Hide orbit children
            if (orbitChild1 != null) orbitChild1.gameObject.SetActive(false);
            if (orbitChild2 != null) orbitChild2.gameObject.SetActive(false);

            // Stop aura particles
            if (auraParticles != null) auraParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            gameObject.SetActive(false);

            // Notify spawner
            PowerUpSpawner.Instance?.OnPowerUpCollected(this);
        }

        private void ApplyEffect(BlobController player)
        {
            switch (type)
            {
                case PowerUpType.SpeedBoost:
                    PowerUpEffectManager.Instance?.ApplySpeedBoost(player, duration);
                    break;
                case PowerUpType.Shield:
                    player.HasShield = true;
                    break;
                case PowerUpType.Magnet:
                    PowerUpEffectManager.Instance?.ApplyMagnet(player, duration);
                    break;
                case PowerUpType.Ghost:
                    PowerUpEffectManager.Instance?.ApplyGhost(player, duration);
                    break;
                case PowerUpType.MegaGrowth:
                    player.AddSize(Mathf.Min(player.CurrentSize * 0.5f, 15f));
                    break;
            }
        }

        /// <summary>Respawn this power-up at a new position with a new type.</summary>
        public void Respawn(Vector3 position, PowerUpType newType)
        {
            transform.position = position;
            basePosition = position;
            type = newType;
            IsActive = true;
            gameObject.SetActive(true);

            // Cache renderer and mesh filter, update color
            cachedRenderer = GetComponent<Renderer>();
            meshFilter = GetComponent<MeshFilter>();
            baseTypeColor = GetTypeColor();
            ApplyTypeShape();
            UpdateVisual();

            // Re-show orbit children
            if (orbitChild1 != null) orbitChild1.gameObject.SetActive(true);
            if (orbitChild2 != null) orbitChild2.gameObject.SetActive(true);

            // Update orbit child colors
            UpdateOrbitChildColors();

            // Restart aura particles with new color
            UpdateAuraParticles();
        }

        /// <summary>Update aura particle colors to match current type.</summary>
        private void UpdateAuraParticles()
        {
            if (auraParticles == null) return;

            var main = auraParticles.main;
            main.startColor = new ParticleSystem.MinMaxGradient(
                baseTypeColor,
                new Color(baseTypeColor.r, baseTypeColor.g, baseTypeColor.b, 0.3f)
            );

            var colorOverLifetime = auraParticles.colorOverLifetime;
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new GradientColorKey[] {
                    new GradientColorKey(baseTypeColor, 0f),
                    new GradientColorKey(baseTypeColor, 1f)
                },
                new GradientAlphaKey[] {
                    new GradientAlphaKey(0.8f, 0f),
                    new GradientAlphaKey(0f, 1f)
                }
            );
            colorOverLifetime.color = gradient;

            ParticleSystemRenderer psRenderer = auraParticles.GetComponent<ParticleSystemRenderer>();
            if (psRenderer != null && psRenderer.material != null)
            {
                psRenderer.material.color = baseTypeColor;
            }

            auraParticles.Play();
        }

        private void UpdateOrbitChildColors()
        {
            UpdateOrbitChildColor(orbitChild1);
            UpdateOrbitChildColor(orbitChild2);
        }

        private void UpdateOrbitChildColor(Transform orbitChild)
        {
            if (orbitChild == null) return;
            Renderer rend = orbitChild.GetComponent<Renderer>();
            if (rend != null)
            {
                rend.material.color = baseTypeColor;
                rend.material.SetColor("_EmissionColor", baseTypeColor * glowIntensity);
            }
        }

        /// <summary>Return the display color for the current power-up type.</summary>
        private Color GetTypeColor()
        {
            return type switch
            {
                PowerUpType.SpeedBoost => new Color(0f, 0.8f, 1f),    // Cyan
                PowerUpType.Shield     => new Color(1f, 0.9f, 0.2f),  // Yellow
                PowerUpType.Magnet     => new Color(1f, 0.2f, 0.8f),  // Magenta
                PowerUpType.Ghost      => new Color(0.8f, 0.8f, 1f, 0.5f), // White translucent
                PowerUpType.MegaGrowth => new Color(1f, 0.4f, 0.1f),  // Orange
                _ => Color.white
            };
        }

        private void UpdateVisual()
        {
            if (cachedRenderer == null) return;

            Color color = GetTypeColor();
            cachedRenderer.material.color = color;
            cachedRenderer.material.EnableKeyword("_EMISSION");
            cachedRenderer.material.SetColor("_EmissionColor", color * glowIntensity);
        }

        private void OnDestroy()
        {
            // Clean up orbit children
            if (orbitChild1 != null) Destroy(orbitChild1.gameObject);
            if (orbitChild2 != null) Destroy(orbitChild2.gameObject);
        }

        /// <summary>Clear static mesh caches (useful for editor reload).</summary>
        public static void ClearMeshCache()
        {
            meshSpeedBolt = null;
            meshShieldHex = null;
            meshMagnetU = null;
            meshGhostRing = null;
            meshMegaDiamond = null;
        }
    }
}
