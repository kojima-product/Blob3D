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

        [Header("Vegetation")]
        [SerializeField] private int treeCount = 25;

        [Header("Water")]
        [SerializeField] private int puddleCount = 10;

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
            int scaledTreeCount = Mathf.RoundToInt(treeCount * qScale);
            int scaledPuddleCount = Mathf.RoundToInt(puddleCount * qScale);

            float radius = GameManager.Instance.FieldRadius * 0.9f;

            GenerateRocks(radius, scaledRockCount);
            GenerateCrystals(radius, scaledCrystalCount);
            GenerateBarriers(radius, scaledBarrierCount);
            GenerateTrees(radius, scaledTreeCount);
            GenerateWaterPuddles(radius, scaledPuddleCount);
            GenerateSmallDebris(radius, scaledDebrisCount);

            // Environmental fog
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.008f;
            RenderSettings.fogColor = new Color(0.08f, 0.10f, 0.15f);
        }

        private void GenerateRocks(float radius, int count)
        {
            for (int i = 0; i < count; i++)
            {
                Vector3 pos = GetRandomPosition(radius);
                float scale = Random.Range(1.5f, 5f);

                // Vary height dramatically: some tall pillars, some flat boulders
                float heightMult = Random.value < 0.2f ? Random.Range(1.5f, 3f) : Random.Range(0.3f, 0.8f);

                // Create composite rock parent
                GameObject rockParent = new GameObject($"Rock_{i}");
                rockParent.transform.SetParent(transform);

                // Main body
                GameObject mainBody = GameObject.CreatePrimitive(
                    Random.value > 0.5f ? PrimitiveType.Sphere : PrimitiveType.Capsule);
                mainBody.name = "MainBody";
                mainBody.transform.SetParent(rockParent.transform);
                mainBody.transform.localScale = new Vector3(
                    scale * Random.Range(0.6f, 1.4f),
                    scale * heightMult,
                    scale * Random.Range(0.6f, 1.4f));
                mainBody.transform.localPosition = Vector3.zero;
                mainBody.transform.localRotation = Quaternion.Euler(
                    Random.Range(-15f, 15f), Random.Range(0f, 360f), Random.Range(-15f, 15f));
                mainBody.GetComponent<Renderer>().material = CreateRockMaterial();
                mainBody.GetComponent<Collider>().isTrigger = false;
                mainBody.isStatic = true;
                var rb = mainBody.GetComponent<Rigidbody>();
                if (rb != null) Destroy(rb);

                // Fused sub-rocks (1-2 smaller spheres at edges)
                int subCount = Random.Range(1, 3);
                for (int s = 0; s < subCount; s++)
                {
                    GameObject sub = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    sub.name = $"SubRock_{s}";
                    sub.transform.SetParent(rockParent.transform);
                    float subScale = scale * Random.Range(0.3f, 0.6f);
                    sub.transform.localScale = new Vector3(
                        subScale * Random.Range(0.8f, 1.3f),
                        subScale * Random.Range(0.5f, 1.0f),
                        subScale * Random.Range(0.8f, 1.3f));
                    sub.transform.localPosition = new Vector3(
                        Random.Range(-scale * 0.4f, scale * 0.4f),
                        Random.Range(-scale * 0.1f, scale * 0.2f),
                        Random.Range(-scale * 0.4f, scale * 0.4f));
                    sub.GetComponent<Renderer>().material = CreateRockMaterial();
                    sub.GetComponent<Collider>().isTrigger = false;
                    sub.isStatic = true;
                    var subRb = sub.GetComponent<Rigidbody>();
                    if (subRb != null) Destroy(subRb);
                }

                // Moss/vegetation on top (30% chance)
                if (Random.value < 0.3f)
                {
                    GameObject moss = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    moss.name = "Moss";
                    moss.transform.SetParent(rockParent.transform);
                    float mossSize = scale * Random.Range(0.3f, 0.6f);
                    moss.transform.localScale = new Vector3(mossSize, 0.1f, mossSize);
                    moss.transform.localPosition = new Vector3(0f, scale * heightMult * 0.4f, 0f);
                    Shader mossShader = Shader.Find("Universal Render Pipeline/Lit");
                    if (mossShader == null) mossShader = Shader.Find("Standard");
                    Material mossMat = new Material(mossShader);
                    mossMat.color = new Color(
                        Random.Range(0.1f, 0.2f),
                        Random.Range(0.3f, 0.5f),
                        Random.Range(0.05f, 0.15f));
                    mossMat.SetFloat("_Smoothness", 0.15f);
                    moss.GetComponent<Renderer>().material = mossMat;
                    Destroy(moss.GetComponent<Collider>());
                    moss.isStatic = true;
                }

                float mainHeight = mainBody.transform.localScale.y;
                rockParent.transform.position = new Vector3(pos.x, mainHeight * 0.5f, pos.z);

                // Rock clusters: 20% chance to spawn 2-3 nearby rocks
                if (Random.value < 0.2f && i + 1 < count)
                {
                    int clusterExtra = Random.Range(1, 3);
                    for (int c = 0; c < clusterExtra && i + 1 < count; c++)
                    {
                        i++;
                        float cScale = scale * Random.Range(0.5f, 0.8f);
                        Vector3 offset = new Vector3(
                            Random.Range(-4f, 4f), 0f, Random.Range(-4f, 4f));
                        GameObject clusterRock = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                        clusterRock.name = $"Rock_{i}";
                        clusterRock.transform.SetParent(transform);
                        clusterRock.transform.localScale = new Vector3(
                            cScale * Random.Range(0.7f, 1.3f),
                            cScale * Random.Range(0.4f, 0.9f),
                            cScale * Random.Range(0.7f, 1.3f));
                        clusterRock.transform.position = rockParent.transform.position + offset;
                        clusterRock.transform.position = new Vector3(
                            clusterRock.transform.position.x,
                            clusterRock.transform.localScale.y * 0.5f,
                            clusterRock.transform.position.z);
                        clusterRock.transform.rotation = Quaternion.Euler(
                            Random.Range(-10f, 10f), Random.Range(0f, 360f), Random.Range(-10f, 10f));
                        clusterRock.GetComponent<Renderer>().material = CreateRockMaterial();
                        clusterRock.GetComponent<Collider>().isTrigger = false;
                        clusterRock.isStatic = true;
                        var cRb = clusterRock.GetComponent<Rigidbody>();
                        if (cRb != null) Destroy(cRb);
                    }
                }
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

                // Crystal cluster parent
                GameObject clusterParent = new GameObject($"Crystal_{i}");
                clusterParent.transform.SetParent(transform);

                // Base platform: small dark cube
                GameObject basePlatform = GameObject.CreatePrimitive(PrimitiveType.Cube);
                basePlatform.name = "CrystalBase";
                basePlatform.transform.SetParent(clusterParent.transform);
                basePlatform.transform.localScale = new Vector3(
                    scale * 1.2f, 0.15f, scale * 1.2f);
                basePlatform.transform.localPosition = Vector3.zero;
                Shader baseShader = Shader.Find("Universal Render Pipeline/Lit");
                if (baseShader == null) baseShader = Shader.Find("Standard");
                Material baseMat = new Material(baseShader);
                baseMat.color = new Color(0.08f, 0.08f, 0.1f);
                baseMat.SetFloat("_Smoothness", 0.7f);
                basePlatform.GetComponent<Renderer>().material = baseMat;
                basePlatform.GetComponent<Collider>().isTrigger = false;
                basePlatform.isStatic = true;

                // Shared crystal material for this cluster
                Material clusterMat = CreateCrystalMaterial();

                // 2-4 sub-crystals at slight angles
                int subCrystalCount = Random.Range(2, 5);
                float tallest = 0f;
                for (int c = 0; c < subCrystalCount; c++)
                {
                    GameObject crystal = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    crystal.name = $"SubCrystal_{c}";
                    crystal.transform.SetParent(clusterParent.transform);
                    float subScale = scale * Random.Range(0.7f, 1.1f);
                    float height = subScale * Random.Range(1.5f, 3f);
                    crystal.transform.localScale = new Vector3(
                        subScale * 0.3f, height, subScale * 0.3f);
                    // Offset from center and tilt slightly
                    crystal.transform.localPosition = new Vector3(
                        Random.Range(-scale * 0.3f, scale * 0.3f),
                        height * 0.5f,
                        Random.Range(-scale * 0.3f, scale * 0.3f));
                    crystal.transform.localRotation = Quaternion.Euler(
                        Random.Range(-25f, 25f), Random.Range(0f, 360f), Random.Range(-25f, 25f));

                    crystal.GetComponent<Renderer>().material = clusterMat;
                    crystal.GetComponent<Collider>().isTrigger = false;
                    crystal.isStatic = true;
                    var rb = crystal.GetComponent<Rigidbody>();
                    if (rb != null) Destroy(rb);

                    if (height > tallest) tallest = height;
                }

                clusterParent.transform.position = new Vector3(pos.x, 0.075f, pos.z);

                // Add point light to larger clusters for glow effect
                if (scale > 1.5f && lightCount < maxLights)
                {
                    GameObject lightObj = new GameObject("CrystalLight");
                    lightObj.transform.SetParent(clusterParent.transform, false);
                    lightObj.transform.localPosition = Vector3.up * tallest * 0.7f;
                    Light pointLight = lightObj.AddComponent<Light>();
                    pointLight.type = LightType.Point;
                    pointLight.color = clusterMat.color;
                    pointLight.intensity = 1.5f;
                    pointLight.range = scale * 6f;
                    pointLight.renderMode = LightRenderMode.Auto;
                    lightCount++;
                }
            }
        }

        private void GenerateBarriers(float radius, int count)
        {
            for (int i = 0; i < count; i++)
            {
                Vector3 pos = GetRandomPosition(radius);
                float barrierLength = Random.Range(8f, 20f);
                float barrierHeight = Random.Range(0.5f, 1.5f);
                float barrierDepth = Random.Range(1f, 2f);
                float yRotation = Random.Range(0f, 360f);

                // Parent for ancient ruin barrier
                GameObject barrierParent = new GameObject($"Barrier_{i}");
                barrierParent.transform.SetParent(transform);
                // Crumble effect: slight random Y rotation offset and height variation
                barrierParent.transform.position = new Vector3(pos.x, 0f, pos.z);
                barrierParent.transform.rotation = Quaternion.Euler(
                    0, yRotation + Random.Range(-5f, 5f), 0);

                // Main wall
                GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                wall.name = "Wall";
                wall.transform.SetParent(barrierParent.transform);
                wall.transform.localScale = new Vector3(
                    barrierLength, barrierHeight + Random.Range(-0.1f, 0.1f), barrierDepth);
                wall.transform.localPosition = new Vector3(0f, barrierHeight * 0.5f, 0f);
                wall.GetComponent<Renderer>().material = CreateBarrierMaterial();
                wall.GetComponent<Collider>().isTrigger = false;
                wall.isStatic = true;
                var wallRb = wall.GetComponent<Rigidbody>();
                if (wallRb != null) Destroy(wallRb);

                // Pillars at each end
                for (int p = 0; p < 2; p++)
                {
                    GameObject pillar = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    pillar.name = $"Pillar_{p}";
                    pillar.transform.SetParent(barrierParent.transform);
                    float pillarHeight = barrierHeight * Random.Range(1.5f, 2.5f);
                    pillar.transform.localScale = new Vector3(
                        barrierDepth * 0.6f, pillarHeight, barrierDepth * 0.6f);
                    float xOffset = (p == 0 ? -1f : 1f) * barrierLength * 0.5f;
                    pillar.transform.localPosition = new Vector3(xOffset, pillarHeight * 0.5f, 0f);
                    pillar.GetComponent<Renderer>().material = CreateBarrierMaterial();
                    pillar.GetComponent<Collider>().isTrigger = false;
                    pillar.isStatic = true;
                    var pRb = pillar.GetComponent<Rigidbody>();
                    if (pRb != null) Destroy(pRb);
                }

                // Small debris cubes scattered around the base
                int debrisCount = Random.Range(3, 7);
                for (int d = 0; d < debrisCount; d++)
                {
                    GameObject debris = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    debris.name = $"WallDebris_{d}";
                    debris.transform.SetParent(barrierParent.transform);
                    float dScale = Random.Range(0.15f, 0.5f);
                    debris.transform.localScale = new Vector3(
                        dScale * Random.Range(0.7f, 1.3f),
                        dScale * Random.Range(0.5f, 1.0f),
                        dScale * Random.Range(0.7f, 1.3f));
                    debris.transform.localPosition = new Vector3(
                        Random.Range(-barrierLength * 0.5f, barrierLength * 0.5f),
                        dScale * 0.25f,
                        Random.Range(-barrierDepth, barrierDepth));
                    debris.transform.localRotation = Quaternion.Euler(
                        Random.Range(-20f, 20f), Random.Range(0f, 360f), Random.Range(-20f, 20f));
                    debris.GetComponent<Renderer>().material = CreateBarrierMaterial();
                    Destroy(debris.GetComponent<Collider>()); // No collision for tiny debris
                    debris.isStatic = true;
                }
            }
        }

        private void GenerateTrees(float radius, int count)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");

            // Trunk material (brown)
            Material trunkMat = new Material(shader);
            trunkMat.color = new Color(0.35f, 0.22f, 0.1f);
            trunkMat.SetFloat("_Smoothness", 0.1f);

            for (int i = 0; i < count; i++)
            {
                Vector3 pos = GetRandomPosition(radius);
                float sizeVar = Random.Range(0.8f, 1.3f);

                GameObject treeParent = new GameObject($"Tree_{i}");
                treeParent.transform.SetParent(transform);
                treeParent.transform.position = new Vector3(pos.x, 0f, pos.z);

                // Trunk: brown capsule
                GameObject trunk = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                trunk.name = "Trunk";
                trunk.transform.SetParent(treeParent.transform);
                trunk.transform.localScale = new Vector3(
                    0.3f * sizeVar, 2f * sizeVar, 0.3f * sizeVar);
                trunk.transform.localPosition = new Vector3(0f, 2f * sizeVar * 0.5f, 0f);
                trunk.GetComponent<Renderer>().material = trunkMat;
                trunk.GetComponent<Collider>().isTrigger = false;
                trunk.isStatic = true;
                var rb = trunk.GetComponent<Rigidbody>();
                if (rb != null) Destroy(rb);

                // Canopy: green sphere on top
                GameObject canopy = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                canopy.name = "Canopy";
                canopy.transform.SetParent(treeParent.transform);
                canopy.transform.localScale = new Vector3(
                    2f * sizeVar, 1.5f * sizeVar, 2f * sizeVar);
                canopy.transform.localPosition = new Vector3(
                    0f, 2f * sizeVar + 0.5f * sizeVar, 0f);

                Material canopyMat = new Material(shader);
                canopyMat.color = new Color(
                    Random.Range(0.1f, 0.25f),
                    Random.Range(0.35f, 0.55f),
                    Random.Range(0.05f, 0.15f));
                canopyMat.SetFloat("_Smoothness", 0.15f);
                canopy.GetComponent<Renderer>().material = canopyMat;
                Destroy(canopy.GetComponent<Collider>()); // No collision for canopy
                canopy.isStatic = true;
            }
        }

        private void GenerateWaterPuddles(float radius, int count)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");

            for (int i = 0; i < count; i++)
            {
                Vector3 pos = GetRandomPosition(radius);

                GameObject puddle = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                puddle.name = $"WaterPuddle_{i}";
                puddle.transform.SetParent(transform);
                float sizeX = Random.Range(3f, 6f);
                float sizeZ = Random.Range(3f, 6f);
                puddle.transform.localScale = new Vector3(sizeX, 0.01f, sizeZ);
                puddle.transform.position = new Vector3(pos.x, 0.02f, pos.z);

                Material mat = new Material(shader);
                mat.color = new Color(0.15f, 0.25f, 0.4f, 0.8f);
                mat.SetFloat("_Smoothness", 0.95f);
                mat.SetFloat("_Metallic", 0.5f);
                puddle.GetComponent<Renderer>().material = mat;

                // No collision for puddles
                Destroy(puddle.GetComponent<Collider>());
                puddle.isStatic = true;
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
                new Color(1f, 0.4f, 0.7f, 0.8f),    // Pink
                new Color(0.9f, 0.9f, 1f, 0.85f),   // White
            };
            mat.color = colors[Random.Range(0, colors.Length)];
            mat.SetFloat("_Smoothness", 0.9f);
            mat.SetFloat("_Metallic", 0.3f);
            // Enable emission for glow (intensity 3.0)
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", mat.color * 3f);
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
