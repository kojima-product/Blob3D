using UnityEngine;
using Blob3D.Core;

namespace Blob3D.Gameplay
{
    /// <summary>
    /// Generates environmental objects: rocks, crystals, barriers, and decorative elements.
    /// Called at game start to populate the field with obstacles.
    /// </summary>
    public class StageGenerator : MonoBehaviour
    {
        [Header("Obstacles")]
        [SerializeField] private int rockCount = 40;
        [SerializeField] private int crystalCount = 20;
        [SerializeField] private int barrierCount = 15;

        [Header("Decoration")]
        [SerializeField] private int smallDebrisCount = 80;

        private void Start()
        {
            if (GameManager.Instance != null)
                GameManager.Instance.OnGameStart += GenerateStage;
        }

        private void OnDestroy()
        {
            if (GameManager.Instance != null)
                GameManager.Instance.OnGameStart -= GenerateStage;
        }

        public void GenerateStage()
        {
            // Clear any existing obstacles
            foreach (Transform child in transform)
                Destroy(child.gameObject);

            // Scale obstacle counts based on quality level for mobile performance
            float qScale = QualitySettings.GetQualityLevel() switch { 0 => 0.5f, 1 => 0.75f, _ => 1f };
            int scaledRockCount = Mathf.RoundToInt(rockCount * qScale);
            int scaledCrystalCount = Mathf.RoundToInt(crystalCount * qScale);
            int scaledBarrierCount = Mathf.RoundToInt(barrierCount * qScale);
            int scaledDebrisCount = Mathf.RoundToInt(smallDebrisCount * qScale);

            float radius = GameManager.Instance.FieldRadius * 0.9f;

            GenerateRocks(radius, scaledRockCount);
            GenerateCrystals(radius, scaledCrystalCount);
            GenerateBarriers(radius, scaledBarrierCount);
            GenerateSmallDebris(radius, scaledDebrisCount);
        }

        private void GenerateRocks(float radius, int count)
        {
            for (int i = 0; i < count; i++)
            {
                Vector3 pos = GetRandomPosition(radius);
                float scale = Random.Range(1.5f, 5f);

                // Use stretched/squashed spheres and capsules as rocks
                GameObject rock = GameObject.CreatePrimitive(
                    Random.value > 0.5f ? PrimitiveType.Sphere : PrimitiveType.Capsule);
                rock.name = $"Rock_{i}";
                rock.transform.SetParent(transform);
                rock.transform.localScale = new Vector3(
                    scale * Random.Range(0.6f, 1.4f),
                    scale * Random.Range(0.3f, 0.8f),
                    scale * Random.Range(0.6f, 1.4f));
                float heightScale = rock.transform.localScale.y;
                rock.transform.position = new Vector3(pos.x, heightScale * 0.5f, pos.z);
                rock.transform.rotation = Quaternion.Euler(
                    Random.Range(-15f, 15f), Random.Range(0f, 360f), Random.Range(-15f, 15f));

                // Dark gray/brown material
                var renderer = rock.GetComponent<Renderer>();
                renderer.material = CreateRockMaterial();

                // Static collider — blobs bounce off
                rock.GetComponent<Collider>().isTrigger = false;
                rock.isStatic = true;
                rock.layer = 0; // Default layer, collides with everything

                // Remove rigidbody if any
                var rb = rock.GetComponent<Rigidbody>();
                if (rb != null) Destroy(rb);
            }
        }

        private void GenerateCrystals(float radius, int count)
        {
            int lightCount = 0;
            const int maxLights = 8; // Limit point lights for mobile performance

            for (int i = 0; i < count; i++)
            {
                Vector3 pos = GetRandomPosition(radius);
                float scale = Random.Range(0.8f, 2.5f);

                // Tall, thin cylinders as crystals
                GameObject crystal = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                crystal.name = $"Crystal_{i}";
                crystal.transform.SetParent(transform);
                crystal.transform.localScale = new Vector3(
                    scale * 0.3f,
                    scale * Random.Range(1.5f, 3f),
                    scale * 0.3f);
                float heightScale = crystal.transform.localScale.y;
                crystal.transform.position = new Vector3(pos.x, heightScale * 0.5f, pos.z);
                crystal.transform.rotation = Quaternion.Euler(
                    Random.Range(-20f, 20f), Random.Range(0f, 360f), Random.Range(-20f, 20f));

                var renderer = crystal.GetComponent<Renderer>();
                Material mat = CreateCrystalMaterial();
                renderer.material = mat;

                // Add point light to larger crystals for glow effect
                if (scale > 1.5f && lightCount < maxLights)
                {
                    GameObject lightObj = new GameObject("CrystalLight");
                    lightObj.transform.SetParent(crystal.transform, false);
                    lightObj.transform.localPosition = Vector3.up * 1.5f;
                    Light pointLight = lightObj.AddComponent<Light>();
                    pointLight.type = LightType.Point;
                    pointLight.color = mat.color;
                    pointLight.intensity = 1.2f;
                    pointLight.range = scale * 5f;
                    pointLight.renderMode = LightRenderMode.Auto;
                    lightCount++;
                }

                crystal.GetComponent<Collider>().isTrigger = false;
                crystal.isStatic = true;

                var rb = crystal.GetComponent<Rigidbody>();
                if (rb != null) Destroy(rb);
            }
        }

        private void GenerateBarriers(float radius, int count)
        {
            for (int i = 0; i < count; i++)
            {
                Vector3 pos = GetRandomPosition(radius);

                // Long, low walls
                GameObject barrier = GameObject.CreatePrimitive(PrimitiveType.Cube);
                barrier.name = $"Barrier_{i}";
                barrier.transform.SetParent(transform);
                barrier.transform.localScale = new Vector3(
                    Random.Range(8f, 20f), Random.Range(0.5f, 1.5f), Random.Range(1f, 2f));
                float heightScale = barrier.transform.localScale.y;
                barrier.transform.position = new Vector3(pos.x, heightScale * 0.5f, pos.z);
                barrier.transform.rotation = Quaternion.Euler(0, Random.Range(0f, 360f), 0);

                var renderer = barrier.GetComponent<Renderer>();
                renderer.material = CreateBarrierMaterial();

                barrier.GetComponent<Collider>().isTrigger = false;
                barrier.isStatic = true;

                var rb = barrier.GetComponent<Rigidbody>();
                if (rb != null) Destroy(rb);
            }
        }

        private void GenerateSmallDebris(float radius, int count)
        {
            for (int i = 0; i < count; i++)
            {
                Vector3 pos = GetRandomPosition(radius);
                float scale = Random.Range(0.2f, 0.8f);

                GameObject debris = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                debris.name = $"Debris_{i}";
                debris.transform.SetParent(transform);
                debris.transform.localScale = Vector3.one * scale;
                debris.transform.position = new Vector3(pos.x, scale * 0.5f + 0.05f, pos.z);

                var renderer = debris.GetComponent<Renderer>();
                renderer.material = CreateDebrisMaterial();

                // No collision for tiny debris, just visual
                Destroy(debris.GetComponent<Collider>());
                debris.isStatic = true;
            }
        }

        private Vector3 GetRandomPosition(float radius)
        {
            // Retry to keep player spawn area clear
            for (int attempt = 0; attempt < 10; attempt++)
            {
                Vector2 circle = Random.insideUnitCircle * radius;
                Vector3 pos = new Vector3(circle.x, 0f, circle.y);
                if (pos.magnitude > 15f) return pos;
            }
            // Fallback: place at edge if all attempts within safe zone
            Vector2 edge = Random.insideUnitCircle.normalized * radius * 0.8f;
            return new Vector3(edge.x, 0f, edge.y);
        }

        private Material CreateRockMaterial()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            Material mat = new Material(shader);
            float gray = Random.Range(0.15f, 0.3f);

            // Add color variation: some rocks have mossy green, some sandy brown
            float variation = Random.value;
            Color rockColor;
            if (variation < 0.3f)
            {
                // Mossy green-gray
                rockColor = new Color(gray * 0.8f, gray * 1.2f, gray * 0.7f);
            }
            else if (variation < 0.5f)
            {
                // Sandy brown
                rockColor = new Color(gray * 1.2f, gray * 1.0f, gray * 0.7f);
            }
            else
            {
                // Standard gray
                rockColor = new Color(gray, gray * 0.9f, gray * 0.85f);
            }
            mat.color = rockColor;
            mat.SetFloat("_Smoothness", Random.Range(0.02f, 0.12f));
            mat.SetFloat("_Metallic", 0f);
            return mat;
        }

        private Material CreateCrystalMaterial()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            Material mat = new Material(shader);
            // Glowing crystal colors
            Color[] colors = {
                new Color(0.3f, 0.8f, 1f, 0.8f),   // Cyan
                new Color(0.8f, 0.3f, 1f, 0.8f),    // Purple
                new Color(0.3f, 1f, 0.5f, 0.8f),    // Green
                new Color(1f, 0.5f, 0.3f, 0.8f),    // Orange
            };
            mat.color = colors[Random.Range(0, colors.Length)];
            mat.SetFloat("_Smoothness", 0.9f);
            mat.SetFloat("_Metallic", 0.3f);
            // Enable emission for glow
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", mat.color * 2f);
            return mat;
        }

        private Material CreateBarrierMaterial()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            Material mat = new Material(shader);
            mat.color = new Color(0.2f, 0.22f, 0.28f);
            mat.SetFloat("_Smoothness", 0.5f);
            return mat;
        }

        private Material CreateDebrisMaterial()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            Material mat = new Material(shader);
            float v = Random.Range(0.1f, 0.25f);
            mat.color = new Color(v, v * 1.1f, v * 1.2f);
            mat.SetFloat("_Smoothness", 0.2f);
            return mat;
        }
    }
}
