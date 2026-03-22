using UnityEngine;
using Blob3D.Player;

namespace Blob3D.Core
{
    /// <summary>
    /// Visualizes the circular field boundary as an animated energy barrier / water's edge.
    /// Uses a procedural ring mesh with the BoundaryRipple shader for wave/ripple effects.
    /// Falls back to LineRenderer if the custom shader is unavailable.
    /// </summary>
    [RequireComponent(typeof(LineRenderer))]
    public class FieldBoundary : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private int segments = 64;
        [SerializeField] private float lineWidth = 0.5f;
        [SerializeField] private Color boundaryColor = new Color(0.2f, 0.6f, 1f, 0.6f);
        [SerializeField] private float warningDistance = 20f;
        [SerializeField] private Color shrinkPulseColor = new Color(1f, 0.2f, 0.2f, 0.8f);
        [SerializeField] private float shrinkPulseSpeed = 4f;

        [Header("Ring Mesh")]
        [SerializeField] private float ringWidth = 3f;
        [SerializeField] private float ringHeight = 1.5f;

        private LineRenderer lineRenderer;
        private float previousRadius;

        // Procedural ring mesh for ripple effect
        private GameObject ringMeshObj;
        private MeshFilter ringMeshFilter;
        private MeshRenderer ringMeshRenderer;
        private Material ringMaterial;
        private bool useRingMesh;

        private void Start()
        {
            lineRenderer = GetComponent<LineRenderer>();
            previousRadius = GameManager.Instance.FieldRadius;

            // Try to set up the ring mesh with BoundaryRipple shader
            SetupRingMesh();
            DrawBoundary();
        }

        private void SetupRingMesh()
        {
            Shader rippleShader = Shader.Find("Blob3D/BoundaryRipple");
            if (rippleShader == null)
            {
                // Fallback to LineRenderer if shader not available
                useRingMesh = false;
                return;
            }

            useRingMesh = true;

            // Hide the LineRenderer when using ring mesh
            lineRenderer.enabled = false;

            ringMeshObj = new GameObject("BoundaryRing");
            ringMeshObj.transform.SetParent(transform);
            ringMeshObj.transform.localPosition = Vector3.zero;

            ringMeshFilter = ringMeshObj.AddComponent<MeshFilter>();
            ringMeshRenderer = ringMeshObj.AddComponent<MeshRenderer>();

            ringMaterial = new Material(rippleShader);
            ringMaterial.SetColor("_Color", boundaryColor);
            ringMaterial.SetColor("_PulseColor", shrinkPulseColor);
            ringMeshRenderer.material = ringMaterial;
            ringMeshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ringMeshRenderer.receiveShadows = false;

            // Generate the initial ring mesh
            UpdateRingMesh(previousRadius);
        }

        private void Update()
        {
            float radius = GameManager.Instance.FieldRadius;
            bool needsRedraw = Mathf.Abs(radius - previousRadius) > 0.01f;
            bool needsColorUpdate = radius < previousRadius - 0.01f ||
                (GameManager.Instance.RemainingTime < 90f && GameManager.Instance.RemainingTime > 0f);
            bool playerNearBoundary = BlobController.Instance != null &&
                GetBoundaryProximity(BlobController.Instance.transform.position) > 0f;

            if (needsRedraw || needsColorUpdate || playerNearBoundary)
            {
                DrawBoundary();
            }

            // Update ring mesh shader pulse parameter continuously
            if (useRingMesh && ringMaterial != null)
            {
                bool isShrinking = radius < previousRadius - 0.01f;
                bool isLate = GameManager.Instance.RemainingTime < 90f &&
                              GameManager.Instance.RemainingTime > 0f;
                if (isShrinking || isLate)
                {
                    float pulse = (Mathf.Sin(Time.time * shrinkPulseSpeed * Mathf.PI * 2f) + 1f) * 0.5f;
                    ringMaterial.SetFloat("_PulseAmount", pulse);
                }
                else
                {
                    ringMaterial.SetFloat("_PulseAmount", 0f);
                }

                // Intensify glow when player is near
                float proximity = 0f;
                if (BlobController.Instance != null)
                    proximity = GetBoundaryProximity(BlobController.Instance.transform.position);
                float glowIntensity = Mathf.Lerp(1.2f, 3f, proximity);
                ringMaterial.SetFloat("_GlowIntensity", glowIntensity);
            }
        }

        private void DrawBoundary()
        {
            float radius = GameManager.Instance.FieldRadius;
            bool isShrinking = radius < previousRadius - 0.01f;
            previousRadius = radius;

            if (useRingMesh)
            {
                UpdateRingMesh(radius);
                return;
            }

            // Fallback: LineRenderer-based boundary
            float proximity = 0f;
            if (BlobController.Instance != null)
                proximity = GetBoundaryProximity(BlobController.Instance.transform.position);

            float currentWidth = Mathf.Lerp(lineWidth, lineWidth * 3f, proximity);

            lineRenderer.positionCount = segments + 1;
            lineRenderer.startWidth = currentWidth;
            lineRenderer.endWidth = currentWidth;
            lineRenderer.loop = true;
            lineRenderer.useWorldSpace = true;

            Color currentColor;
            if (isShrinking || (GameManager.Instance.RemainingTime < 90f && GameManager.Instance.RemainingTime > 0f))
            {
                float pulse = (Mathf.Sin(Time.time * shrinkPulseSpeed * Mathf.PI * 2f) + 1f) * 0.5f;
                currentColor = Color.Lerp(boundaryColor, shrinkPulseColor, pulse);
            }
            else
            {
                currentColor = boundaryColor;
            }

            lineRenderer.startColor = currentColor;
            lineRenderer.endColor = currentColor;

            for (int i = 0; i <= segments; i++)
            {
                float angle = (float)i / segments * Mathf.PI * 2f;
                float x = Mathf.Cos(angle) * radius;
                float z = Mathf.Sin(angle) * radius;
                lineRenderer.SetPosition(i, new Vector3(x, 0.1f, z));
            }
        }

        /// <summary>
        /// Generate or update a procedural ring mesh at the given radius.
        /// The ring has inner and outer edges with UV mapping for the ripple shader.
        /// UV.x = angular (0..1), UV.y = radial (0=inner, 1=outer).
        /// </summary>
        private void UpdateRingMesh(float radius)
        {
            if (ringMeshFilter == null) return;

            float innerRadius = radius - ringWidth * 0.5f;
            float outerRadius = radius + ringWidth * 0.5f;

            int vertCount = (segments + 1) * 2;
            var vertices = new Vector3[vertCount];
            var uvs = new Vector2[vertCount];
            var triangles = new int[segments * 6];

            for (int i = 0; i <= segments; i++)
            {
                float t = (float)i / segments;
                float angle = t * Mathf.PI * 2f;
                float cos = Mathf.Cos(angle);
                float sin = Mathf.Sin(angle);

                int baseIdx = i * 2;

                // Inner vertex
                vertices[baseIdx] = new Vector3(cos * innerRadius, 0.05f, sin * innerRadius);
                uvs[baseIdx] = new Vector2(t, 0f);

                // Outer vertex (elevated for wave displacement)
                vertices[baseIdx + 1] = new Vector3(cos * outerRadius, ringHeight * 0.3f, sin * outerRadius);
                uvs[baseIdx + 1] = new Vector2(t, 1f);
            }

            for (int i = 0; i < segments; i++)
            {
                int baseIdx = i * 2;
                int triIdx = i * 6;

                // First triangle
                triangles[triIdx] = baseIdx;
                triangles[triIdx + 1] = baseIdx + 1;
                triangles[triIdx + 2] = baseIdx + 2;

                // Second triangle
                triangles[triIdx + 3] = baseIdx + 2;
                triangles[triIdx + 4] = baseIdx + 1;
                triangles[triIdx + 5] = baseIdx + 3;
            }

            Mesh mesh = ringMeshFilter.mesh;
            if (mesh == null)
            {
                mesh = new Mesh();
                mesh.name = "BoundaryRing";
            }

            mesh.Clear();
            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            ringMeshFilter.mesh = mesh;
        }

        /// <summary>Returns proximity factor (0..1) indicating how close the player is to boundary</summary>
        public float GetBoundaryProximity(Vector3 position)
        {
            float radius = GameManager.Instance.FieldRadius;
            Vector3 flat = new Vector3(position.x, 0f, position.z);
            float distFromEdge = radius - flat.magnitude;

            // Fix: guard against warningDistance being zero (division by zero)
            if (warningDistance <= 0f) return distFromEdge <= 0f ? 1f : 0f;

            if (distFromEdge >= warningDistance) return 0f;
            if (distFromEdge <= 0f) return 1f;

            return 1f - (distFromEdge / warningDistance);
        }
    }
}
