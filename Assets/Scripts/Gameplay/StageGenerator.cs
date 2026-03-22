using UnityEngine;
using Blob3D.Core;
using System.Collections.Generic;

namespace Blob3D.Gameplay
{
    /// <summary>
    /// Generates realistic environmental objects: noise-deformed rock meshes,
    /// hexagonal crystal clusters, organic log barriers, procedural trees/mushrooms,
    /// floating particles, and ambient atmosphere.
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
        [SerializeField] private int mushroomCount = 30;

        [Header("Particles")]
        [SerializeField] private int dustMoteCount = 60;
        [SerializeField] private int fireflyCount = 25;

        // Cached shared meshes to reduce GC and draw calls
        private Mesh cachedSmallRockMesh;
        private Mesh cachedMediumRockMesh;
        private Mesh cachedLargeRockMesh;
        private Mesh cachedCrystalShardMesh;
        private Mesh cachedLogMesh;
        private Mesh cachedMushroomCapMesh;
        private Mesh cachedMushroomStemMesh;

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

            // Pre-generate shared mesh templates
            GenerateCachedMeshes();

            // Scale obstacle counts based on quality level for mobile performance
            float qScale = QualitySettings.GetQualityLevel() switch { 0 => 0.5f, 1 => 0.75f, _ => 1f };
            int scaledRockCount = Mathf.RoundToInt(rockCount * qScale);
            int scaledCrystalCount = Mathf.RoundToInt(crystalCount * qScale);
            int scaledBarrierCount = Mathf.RoundToInt(barrierCount * qScale);
            int scaledDebrisCount = Mathf.RoundToInt(smallDebrisCount * qScale);
            int scaledTreeCount = Mathf.RoundToInt(treeCount * qScale);
            int scaledPuddleCount = Mathf.RoundToInt(puddleCount * qScale);
            int scaledMushroomCount = Mathf.RoundToInt(mushroomCount * qScale);

            float radius = GameManager.Instance.FieldRadius * 0.9f;

            GenerateRocks(radius, scaledRockCount);
            GenerateCrystals(radius, scaledCrystalCount);
            GenerateBarriers(radius, scaledBarrierCount);
            GenerateTrees(radius, scaledTreeCount);
            GenerateWaterPuddles(radius, scaledPuddleCount);
            GenerateSmallDebris(radius, scaledDebrisCount);
            GenerateMushrooms(radius, scaledMushroomCount);
            GenerateFloatingParticles(radius);
            SetupAtmosphere();
        }

        #region Cached Mesh Generation

        private void GenerateCachedMeshes()
        {
            // Generate icosphere-based rock meshes at different detail levels
            cachedSmallRockMesh = CreateNoisyIcosphere(1, 0.25f, 42);
            cachedMediumRockMesh = CreateNoisyIcosphere(1, 0.2f, 137);
            cachedLargeRockMesh = CreateNoisyIcosphere(2, 0.15f, 314);

            // Crystal shard: hexagonal prism with pointed tip
            cachedCrystalShardMesh = CreateHexCrystalMesh();

            // Fallen log: deformed cylinder
            cachedLogMesh = CreateLogMesh(8, 1f, 0.3f, 99);

            // Mushroom parts
            cachedMushroomCapMesh = CreateMushroomCapMesh();
            cachedMushroomStemMesh = CreateMushroomStemMesh();
        }

        #endregion

        #region Rock Generation

        private void GenerateRocks(float radius, int count)
        {
            for (int i = 0; i < count; i++)
            {
                Vector3 pos = GetRandomPosition(radius);

                // Determine rock type: 15% large boulder, 40% medium, 45% small pebble
                float typeRoll = Random.value;
                Mesh rockMesh;
                float baseScale;
                if (typeRoll < 0.15f)
                {
                    // Large boulder
                    rockMesh = cachedLargeRockMesh;
                    baseScale = Random.Range(3f, 6f);
                }
                else if (typeRoll < 0.55f)
                {
                    // Medium rock
                    rockMesh = cachedMediumRockMesh;
                    baseScale = Random.Range(1.5f, 3.5f);
                }
                else
                {
                    // Small pebble
                    rockMesh = cachedSmallRockMesh;
                    baseScale = Random.Range(0.5f, 1.5f);
                }

                // Asymmetric scaling for organic feel
                float scaleX = baseScale * Random.Range(0.7f, 1.4f);
                float scaleY = baseScale * Random.Range(0.4f, 1.0f);
                float scaleZ = baseScale * Random.Range(0.7f, 1.4f);

                GameObject rock = new GameObject($"Rock_{i}");
                rock.transform.SetParent(transform);
                rock.transform.position = new Vector3(pos.x, scaleY * 0.4f, pos.z);
                rock.transform.rotation = Quaternion.Euler(
                    Random.Range(-10f, 10f), Random.Range(0f, 360f), Random.Range(-10f, 10f));
                rock.transform.localScale = new Vector3(scaleX, scaleY, scaleZ);
                rock.isStatic = true;

                // Mesh renderer with Y-based moisture darkening
                var mf = rock.AddComponent<MeshFilter>();
                mf.sharedMesh = rockMesh;
                var mr = rock.AddComponent<MeshRenderer>();
                mr.material = CreateRockMaterial(scaleY * 0.4f, scaleY);

                // Mesh collider for accurate collision with low-poly mesh
                var mc = rock.AddComponent<MeshCollider>();
                mc.sharedMesh = rockMesh;
                mc.convex = true;

                // Moss cap on top of larger rocks (35% chance)
                if (baseScale > 2f && Random.value < 0.35f)
                {
                    GameObject moss = new GameObject("MossCap");
                    moss.transform.SetParent(rock.transform);
                    moss.transform.localPosition = new Vector3(0f, 0.5f, 0f);
                    moss.transform.localScale = new Vector3(
                        Random.Range(0.6f, 0.9f), 0.15f, Random.Range(0.6f, 0.9f));
                    moss.isStatic = true;
                    var mossMf = moss.AddComponent<MeshFilter>();
                    mossMf.sharedMesh = cachedSmallRockMesh;
                    var mossMr = moss.AddComponent<MeshRenderer>();
                    mossMr.material = CreateMossMaterial();
                }

                // Rock clusters: 20% chance to spawn 2-3 nearby smaller rocks
                if (Random.value < 0.2f && i + 1 < count)
                {
                    int clusterExtra = Random.Range(1, 3);
                    for (int c = 0; c < clusterExtra && i + 1 < count; c++)
                    {
                        i++;
                        float cScale = baseScale * Random.Range(0.3f, 0.6f);
                        Vector3 offset = new Vector3(
                            Random.Range(-4f, 4f), 0f, Random.Range(-4f, 4f));

                        GameObject clusterRock = new GameObject($"Rock_{i}");
                        clusterRock.transform.SetParent(transform);
                        clusterRock.transform.position = new Vector3(
                            pos.x + offset.x, cScale * 0.4f, pos.z + offset.z);
                        clusterRock.transform.rotation = Quaternion.Euler(
                            Random.Range(-15f, 15f), Random.Range(0f, 360f), Random.Range(-15f, 15f));
                        clusterRock.transform.localScale = new Vector3(
                            cScale * Random.Range(0.7f, 1.3f),
                            cScale * Random.Range(0.5f, 0.9f),
                            cScale * Random.Range(0.7f, 1.3f));
                        clusterRock.isStatic = true;

                        var cmf = clusterRock.AddComponent<MeshFilter>();
                        cmf.sharedMesh = cachedSmallRockMesh;
                        var cmr = clusterRock.AddComponent<MeshRenderer>();
                        cmr.material = CreateRockMaterial();
                        var cmc = clusterRock.AddComponent<MeshCollider>();
                        cmc.sharedMesh = cachedSmallRockMesh;
                        cmc.convex = true;
                    }
                }
            }
        }

        #endregion

        #region Crystal Generation

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
                clusterParent.transform.position = new Vector3(pos.x, 0f, pos.z);

                // Dark base rock
                GameObject baseRock = new GameObject("CrystalBase");
                baseRock.transform.SetParent(clusterParent.transform);
                baseRock.transform.localPosition = Vector3.zero;
                baseRock.transform.localScale = new Vector3(scale * 1.2f, scale * 0.3f, scale * 1.2f);
                baseRock.isStatic = true;
                var baseMf = baseRock.AddComponent<MeshFilter>();
                baseMf.sharedMesh = cachedSmallRockMesh;
                var baseMr = baseRock.AddComponent<MeshRenderer>();
                baseMr.material = CreateDarkRockMaterial();
                var baseMc = baseRock.AddComponent<MeshCollider>();
                baseMc.sharedMesh = cachedSmallRockMesh;
                baseMc.convex = true;

                // Shared crystal material for this cluster
                Material clusterMat = CreateCrystalMaterial();

                // 2-5 crystal shards at slight angles
                int subCrystalCount = Random.Range(2, 6);
                float tallest = 0f;
                for (int c = 0; c < subCrystalCount; c++)
                {
                    GameObject shard = new GameObject($"Shard_{c}");
                    shard.transform.SetParent(clusterParent.transform);

                    float subScale = scale * Random.Range(0.5f, 1.0f);
                    float height = subScale * Random.Range(1.5f, 3.5f);

                    shard.transform.localScale = new Vector3(subScale * 0.3f, height, subScale * 0.3f);
                    shard.transform.localPosition = new Vector3(
                        Random.Range(-scale * 0.3f, scale * 0.3f),
                        height * 0.4f,
                        Random.Range(-scale * 0.3f, scale * 0.3f));
                    shard.transform.localRotation = Quaternion.Euler(
                        Random.Range(-20f, 20f), Random.Range(0f, 360f), Random.Range(-20f, 20f));
                    shard.isStatic = true;

                    var shardMf = shard.AddComponent<MeshFilter>();
                    shardMf.sharedMesh = cachedCrystalShardMesh;
                    var shardMr = shard.AddComponent<MeshRenderer>();
                    shardMr.material = clusterMat;
                    var shardMc = shard.AddComponent<MeshCollider>();
                    shardMc.sharedMesh = cachedCrystalShardMesh;
                    shardMc.convex = true;

                    if (height > tallest) tallest = height;
                }

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

        #endregion

        #region Barrier Generation (Fallen Logs / Stone Walls)

        private void GenerateBarriers(float radius, int count)
        {
            for (int i = 0; i < count; i++)
            {
                Vector3 pos = GetRandomPosition(radius);
                float yRotation = Random.Range(0f, 360f);
                bool isLog = Random.value < 0.6f; // 60% fallen logs, 40% stone walls

                GameObject barrierParent = new GameObject($"Barrier_{i}");
                barrierParent.transform.SetParent(transform);
                barrierParent.transform.position = new Vector3(pos.x, 0f, pos.z);
                barrierParent.transform.rotation = Quaternion.Euler(0, yRotation, 0);

                if (isLog)
                {
                    GenerateFallenLog(barrierParent);
                }
                else
                {
                    GenerateStoneWall(barrierParent);
                }
            }
        }

        private void GenerateFallenLog(GameObject parent)
        {
            float logLength = Random.Range(6f, 16f);
            float logRadius = Random.Range(0.4f, 1.2f);

            // Main log body
            GameObject log = new GameObject("Log");
            log.transform.SetParent(parent.transform);
            log.transform.localPosition = new Vector3(0f, logRadius * 0.8f, 0f);
            log.transform.localScale = new Vector3(logRadius, logRadius, logLength * 0.5f);
            log.transform.localRotation = Quaternion.Euler(
                Random.Range(-3f, 3f), 0f, Random.Range(-5f, 5f));
            log.isStatic = true;

            var mf = log.AddComponent<MeshFilter>();
            mf.sharedMesh = cachedLogMesh;
            var mr = log.AddComponent<MeshRenderer>();
            mr.material = CreateBarkMaterial();
            var mc = log.AddComponent<MeshCollider>();
            mc.sharedMesh = cachedLogMesh;
            mc.convex = true;

            // Branch stumps along the log (2-4 small protrusions, non-collidable)
            int branchCount = Random.Range(2, 5);
            for (int b = 0; b < branchCount; b++)
            {
                GameObject branch = new GameObject($"BranchStump_{b}");
                branch.transform.SetParent(parent.transform);
                float along = Random.Range(-logLength * 0.4f, logLength * 0.4f);
                float angle = Random.Range(0f, 360f);
                float branchLen = Random.Range(0.3f, 0.8f);
                branch.transform.localPosition = new Vector3(
                    Mathf.Sin(angle * Mathf.Deg2Rad) * logRadius * 0.8f,
                    logRadius * 0.8f + Mathf.Cos(angle * Mathf.Deg2Rad) * logRadius * 0.5f,
                    along);
                branch.transform.localScale = Vector3.one * branchLen;
                branch.transform.localRotation = Quaternion.Euler(
                    Random.Range(-30f, 30f), Random.Range(0f, 360f), Random.Range(-30f, 30f));
                branch.isStatic = true;

                var bmf = branch.AddComponent<MeshFilter>();
                bmf.sharedMesh = cachedSmallRockMesh; // Reuse noisy sphere as branch knob
                var bmr = branch.AddComponent<MeshRenderer>();
                bmr.material = CreateBarkMaterial();
            }

            // Moss patches on top (40% chance)
            if (Random.value < 0.4f)
            {
                GameObject moss = new GameObject("LogMoss");
                moss.transform.SetParent(parent.transform);
                moss.transform.localPosition = new Vector3(0f, logRadius * 1.5f, 0f);
                moss.transform.localScale = new Vector3(
                    logRadius * 0.8f, 0.1f, logLength * 0.3f);
                moss.isStatic = true;
                var mmf = moss.AddComponent<MeshFilter>();
                mmf.sharedMesh = cachedSmallRockMesh;
                var mmr = moss.AddComponent<MeshRenderer>();
                mmr.material = CreateMossMaterial();
            }
        }

        private void GenerateStoneWall(GameObject parent)
        {
            float wallLength = Random.Range(6f, 14f);
            float wallHeight = Random.Range(0.8f, 2.0f);

            // Stacked rocks forming a wall: 4-8 rocks arranged linearly
            int stoneCount = Random.Range(4, 9);
            float spacing = wallLength / stoneCount;

            for (int s = 0; s < stoneCount; s++)
            {
                float xPos = (s - stoneCount * 0.5f) * spacing + Random.Range(-0.3f, 0.3f);
                float stoneScale = Random.Range(0.8f, 1.5f);
                float stoneHeight = wallHeight * Random.Range(0.6f, 1.1f);

                GameObject stone = new GameObject($"WallStone_{s}");
                stone.transform.SetParent(parent.transform);
                stone.transform.localPosition = new Vector3(xPos, stoneHeight * 0.4f, Random.Range(-0.3f, 0.3f));
                stone.transform.localScale = new Vector3(
                    stoneScale * Random.Range(0.8f, 1.2f),
                    stoneHeight,
                    stoneScale * Random.Range(0.6f, 1.0f));
                stone.transform.localRotation = Quaternion.Euler(
                    Random.Range(-8f, 8f), Random.Range(-15f, 15f), Random.Range(-8f, 8f));
                stone.isStatic = true;

                var mf = stone.AddComponent<MeshFilter>();
                mf.sharedMesh = Random.value < 0.5f ? cachedMediumRockMesh : cachedLargeRockMesh;
                var mr = stone.AddComponent<MeshRenderer>();
                mr.material = CreateRockMaterial();
                var mc = stone.AddComponent<MeshCollider>();
                mc.sharedMesh = mf.sharedMesh;
                mc.convex = true;

                // Stacked second layer (30% chance per stone)
                if (Random.value < 0.3f)
                {
                    float topScale = stoneScale * Random.Range(0.5f, 0.8f);
                    GameObject topStone = new GameObject($"TopStone_{s}");
                    topStone.transform.SetParent(parent.transform);
                    topStone.transform.localPosition = new Vector3(
                        xPos + Random.Range(-0.3f, 0.3f),
                        stoneHeight * 0.9f,
                        Random.Range(-0.2f, 0.2f));
                    topStone.transform.localScale = Vector3.one * topScale;
                    topStone.transform.localRotation = Quaternion.Euler(
                        Random.Range(-12f, 12f), Random.Range(0f, 360f), Random.Range(-12f, 12f));
                    topStone.isStatic = true;

                    var tmf = topStone.AddComponent<MeshFilter>();
                    tmf.sharedMesh = cachedSmallRockMesh;
                    var tmr = topStone.AddComponent<MeshRenderer>();
                    tmr.material = CreateRockMaterial();
                    var tmc = topStone.AddComponent<MeshCollider>();
                    tmc.sharedMesh = cachedSmallRockMesh;
                    tmc.convex = true;
                }
            }
        }

        #endregion

        #region Tree Generation

        private void GenerateTrees(float radius, int count)
        {
            for (int i = 0; i < count; i++)
            {
                Vector3 pos = GetRandomPosition(radius);
                float sizeVar = Random.Range(0.8f, 1.3f);

                GameObject treeParent = new GameObject($"Tree_{i}");
                treeParent.transform.SetParent(transform);
                treeParent.transform.position = new Vector3(pos.x, 0f, pos.z);

                float trunkHeight = 2.5f * sizeVar;
                float trunkRadius = 0.2f * sizeVar;

                // Trunk: organic log mesh with slight natural lean
                float trunkTiltX = Random.Range(-5f, 5f);
                float trunkTiltZ = Random.Range(-5f, 5f);
                GameObject trunk = new GameObject("Trunk");
                trunk.transform.SetParent(treeParent.transform);
                trunk.transform.localScale = new Vector3(trunkRadius, trunkRadius, trunkHeight * 0.5f);
                trunk.transform.localPosition = new Vector3(0f, trunkHeight * 0.5f, 0f);
                trunk.transform.localRotation = Quaternion.Euler(90f + trunkTiltX, 0f, trunkTiltZ);
                trunk.isStatic = true;

                var tMf = trunk.AddComponent<MeshFilter>();
                tMf.sharedMesh = cachedLogMesh;
                var tMr = trunk.AddComponent<MeshRenderer>();
                tMr.material = CreateBarkMaterial();
                var tMc = trunk.AddComponent<MeshCollider>();
                tMc.sharedMesh = cachedLogMesh;
                tMc.convex = true;

                // Branch stubs: 1-2 capsule-shaped branches at random angles
                int treeBranchCount = Random.Range(1, 3);
                for (int br = 0; br < treeBranchCount; br++)
                {
                    GameObject branch = new GameObject($"Branch_{br}");
                    branch.transform.SetParent(treeParent.transform);
                    float branchY = trunkHeight * Random.Range(0.4f, 0.85f);
                    float branchAngle = Random.Range(0f, 360f);
                    float branchLength = sizeVar * Random.Range(0.6f, 1.2f);
                    float branchRadius = trunkRadius * Random.Range(0.3f, 0.6f);

                    // Position branch origin on trunk surface
                    branch.transform.localPosition = new Vector3(
                        Mathf.Cos(branchAngle * Mathf.Deg2Rad) * trunkRadius * 0.5f,
                        branchY,
                        Mathf.Sin(branchAngle * Mathf.Deg2Rad) * trunkRadius * 0.5f);
                    // Tilt outward and slightly upward
                    branch.transform.localRotation = Quaternion.Euler(
                        Random.Range(20f, 60f),
                        branchAngle,
                        0f);
                    branch.transform.localScale = new Vector3(branchRadius, branchRadius, branchLength * 0.5f);
                    branch.isStatic = true;

                    var brMf = branch.AddComponent<MeshFilter>();
                    brMf.sharedMesh = cachedLogMesh;
                    var brMr = branch.AddComponent<MeshRenderer>();
                    brMr.material = CreateBarkMaterial();
                    // No collider — decorative branch
                }

                // Canopy: 2-3 overlapping noisy spheres for organic look
                int canopyCount = Random.Range(2, 4);
                for (int c = 0; c < canopyCount; c++)
                {
                    GameObject canopy = new GameObject($"Canopy_{c}");
                    canopy.transform.SetParent(treeParent.transform);
                    float canopySize = sizeVar * Random.Range(1.2f, 2.0f);
                    canopy.transform.localScale = new Vector3(
                        canopySize, canopySize * Random.Range(0.6f, 0.9f), canopySize);
                    canopy.transform.localPosition = new Vector3(
                        Random.Range(-0.4f, 0.4f) * sizeVar,
                        trunkHeight + canopySize * 0.2f * c,
                        Random.Range(-0.4f, 0.4f) * sizeVar);
                    canopy.isStatic = true;

                    var cmf = canopy.AddComponent<MeshFilter>();
                    cmf.sharedMesh = cachedMediumRockMesh; // Reuse noisy sphere for canopy
                    var cmr = canopy.AddComponent<MeshRenderer>();
                    cmr.material = CreateCanopyMaterial();
                    // No collider on canopy — decorative only
                }
            }
        }

        #endregion

        #region Mushroom Generation

        private void GenerateMushrooms(float radius, int count)
        {
            for (int i = 0; i < count; i++)
            {
                Vector3 pos = GetRandomPosition(radius);
                float scale = Random.Range(0.3f, 1.0f);
                bool isCluster = Random.value < 0.3f;
                int mushroomInCluster = isCluster ? Random.Range(2, 5) : 1;

                for (int m = 0; m < mushroomInCluster; m++)
                {
                    Vector3 offset = m == 0 ? Vector3.zero :
                        new Vector3(Random.Range(-0.8f, 0.8f), 0f, Random.Range(-0.8f, 0.8f));
                    float subScale = scale * (m == 0 ? 1f : Random.Range(0.5f, 0.8f));

                    GameObject mushroom = new GameObject($"Mushroom_{i}_{m}");
                    mushroom.transform.SetParent(transform);
                    mushroom.transform.position = pos + offset;
                    mushroom.isStatic = true;

                    // Stem with slight random lean for organic feel
                    float stemLeanX = Random.Range(-12f, 12f);
                    float stemLeanZ = Random.Range(-12f, 12f);
                    GameObject stem = new GameObject("Stem");
                    stem.transform.SetParent(mushroom.transform);
                    stem.transform.localScale = new Vector3(subScale * 0.3f, subScale * 0.6f, subScale * 0.3f);
                    stem.transform.localPosition = new Vector3(0f, subScale * 0.3f, 0f);
                    stem.transform.localRotation = Quaternion.Euler(stemLeanX, 0f, stemLeanZ);
                    stem.isStatic = true;
                    var smf = stem.AddComponent<MeshFilter>();
                    smf.sharedMesh = cachedMushroomStemMesh;
                    var smr = stem.AddComponent<MeshRenderer>();
                    smr.material = CreateMushroomStemMaterial();

                    // Cap — offset slightly to match stem lean direction
                    float capOffsetX = Mathf.Sin(stemLeanZ * Mathf.Deg2Rad) * subScale * 0.15f;
                    float capOffsetZ = -Mathf.Sin(stemLeanX * Mathf.Deg2Rad) * subScale * 0.15f;
                    GameObject cap = new GameObject("Cap");
                    cap.transform.SetParent(mushroom.transform);
                    cap.transform.localScale = new Vector3(subScale * 0.8f, subScale * 0.35f, subScale * 0.8f);
                    cap.transform.localPosition = new Vector3(capOffsetX, subScale * 0.55f, capOffsetZ);
                    cap.transform.localRotation = Quaternion.Euler(
                        Random.Range(-5f, 5f) + stemLeanX * 0.3f,
                        Random.Range(0f, 360f),
                        Random.Range(-5f, 5f) + stemLeanZ * 0.3f);
                    cap.isStatic = true;
                    var cmf = cap.AddComponent<MeshFilter>();
                    cmf.sharedMesh = cachedMushroomCapMesh;
                    var cmr = cap.AddComponent<MeshRenderer>();
                    cmr.material = CreateMushroomCapMaterial();

                    // No collider — decorative only
                }
            }
        }

        #endregion

        #region Water Puddles

        private void GenerateWaterPuddles(float radius, int count)
        {
            Shader shader = FindLitShader();

            for (int i = 0; i < count; i++)
            {
                Vector3 pos = GetRandomPosition(radius);

                // Use a flattened noisy sphere for organic puddle shape
                GameObject puddle = new GameObject($"WaterPuddle_{i}");
                puddle.transform.SetParent(transform);
                float sizeX = Random.Range(3f, 6f);
                float sizeZ = Random.Range(3f, 6f);
                puddle.transform.localScale = new Vector3(sizeX, 0.05f, sizeZ);
                puddle.transform.position = new Vector3(pos.x, 0.02f, pos.z);
                puddle.isStatic = true;

                var mf = puddle.AddComponent<MeshFilter>();
                mf.sharedMesh = cachedMediumRockMesh; // Flattened noisy sphere = organic puddle
                var mr = puddle.AddComponent<MeshRenderer>();

                Material mat = new Material(shader);
                mat.color = new Color(0.12f, 0.22f, 0.35f, 0.85f);
                mat.SetFloat("_Smoothness", 0.95f);
                mat.SetFloat("_Metallic", 0.4f);

                // Subtle emission for water surface reflection shimmer
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", new Color(0.03f, 0.06f, 0.1f));

                // Enable transparency for water
                mat.SetFloat("_Surface", 1f);
                mat.SetOverrideTag("RenderType", "Transparent");
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetInt("_ZWrite", 0);
                mat.renderQueue = 3000;
                mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");

                mr.material = mat;
                // No collider for puddles
            }
        }

        #endregion

        #region Small Debris

        private void GenerateSmallDebris(float radius, int count)
        {
            for (int i = 0; i < count; i++)
            {
                Vector3 pos = GetRandomPosition(radius);
                float scale = Random.Range(0.15f, 0.6f);

                GameObject debris = new GameObject($"Debris_{i}");
                debris.transform.SetParent(transform);
                debris.transform.localScale = new Vector3(
                    scale * Random.Range(0.7f, 1.3f),
                    scale * Random.Range(0.5f, 1.0f),
                    scale * Random.Range(0.7f, 1.3f));
                debris.transform.position = new Vector3(pos.x, scale * 0.3f + 0.05f, pos.z);
                debris.transform.rotation = Quaternion.Euler(
                    Random.Range(-20f, 20f), Random.Range(0f, 360f), Random.Range(-20f, 20f));
                debris.isStatic = true;

                var mf = debris.AddComponent<MeshFilter>();
                mf.sharedMesh = cachedSmallRockMesh;
                var mr = debris.AddComponent<MeshRenderer>();
                mr.material = CreateDebrisMaterial();
                // No collision for tiny debris
            }
        }

        #endregion

        #region Floating Particles

        private void GenerateFloatingParticles(float radius)
        {
            // Dust motes — gentle floating particles
            GameObject dustObj = new GameObject("DustMotes");
            dustObj.transform.SetParent(transform);
            dustObj.transform.position = Vector3.up * 3f;
            var dust = dustObj.AddComponent<ParticleSystem>();
            ConfigureDustParticles(dust, radius);

            // Fireflies — glowing particles near the ground
            GameObject fireflyObj = new GameObject("Fireflies");
            fireflyObj.transform.SetParent(transform);
            fireflyObj.transform.position = Vector3.up * 1.5f;
            var firefly = fireflyObj.AddComponent<ParticleSystem>();
            ConfigureFireflyParticles(firefly, radius);
        }

        private void ConfigureDustParticles(ParticleSystem ps, float radius)
        {
            var main = ps.main;
            main.maxParticles = dustMoteCount;
            main.startLifetime = 8f;
            main.startSpeed = 0.2f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.15f);
            main.startColor = new Color(0.8f, 0.8f, 0.7f, 0.3f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.loop = true;

            var emission = ps.emission;
            emission.rateOverTime = dustMoteCount * 0.5f;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = radius * 0.6f;

            var vel = ps.velocityOverLifetime;
            vel.enabled = true;
            vel.x = new ParticleSystem.MinMaxCurve(-0.1f, 0.1f);
            vel.y = new ParticleSystem.MinMaxCurve(0.05f, 0.2f);
            vel.z = new ParticleSystem.MinMaxCurve(-0.1f, 0.1f);

            var col = ps.colorOverLifetime;
            col.enabled = true;
            Gradient g = new Gradient();
            g.SetKeys(
                new[] { new GradientColorKey(new Color(0.8f, 0.8f, 0.7f), 0f),
                        new GradientColorKey(new Color(0.9f, 0.9f, 0.8f), 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.25f, 0.3f),
                        new GradientAlphaKey(0.25f, 0.7f), new GradientAlphaKey(0f, 1f) });
            col.color = g;

            // Use default particle material
            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.material = CreateParticleMaterial(new Color(0.9f, 0.9f, 0.8f, 0.3f));
        }

        private void ConfigureFireflyParticles(ParticleSystem ps, float radius)
        {
            var main = ps.main;
            main.maxParticles = fireflyCount;
            main.startLifetime = 6f;
            main.startSpeed = 0.3f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.2f);
            main.startColor = new Color(0.6f, 1f, 0.3f, 0.8f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.loop = true;

            var emission = ps.emission;
            emission.rateOverTime = fireflyCount * 0.3f;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = radius * 0.5f;
            shape.position = Vector3.down * 1f; // Near ground

            var vel = ps.velocityOverLifetime;
            vel.enabled = true;
            vel.x = new ParticleSystem.MinMaxCurve(-0.3f, 0.3f);
            vel.y = new ParticleSystem.MinMaxCurve(-0.1f, 0.3f);
            vel.z = new ParticleSystem.MinMaxCurve(-0.3f, 0.3f);

            var col = ps.colorOverLifetime;
            col.enabled = true;
            Gradient g = new Gradient();
            g.SetKeys(
                new[] { new GradientColorKey(new Color(0.4f, 0.9f, 0.2f), 0f),
                        new GradientColorKey(new Color(0.7f, 1f, 0.4f), 0.5f),
                        new GradientColorKey(new Color(0.3f, 0.8f, 0.1f), 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.8f, 0.2f),
                        new GradientAlphaKey(0.8f, 0.8f), new GradientAlphaKey(0f, 1f) });
            col.color = g;

            var sizeOverLifetime = ps.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f,
                new AnimationCurve(
                    new Keyframe(0f, 0.5f), new Keyframe(0.3f, 1f),
                    new Keyframe(0.7f, 1f), new Keyframe(1f, 0.3f)));

            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.material = CreateParticleMaterial(new Color(0.5f, 1f, 0.3f, 0.9f), true);
        }

        #endregion

        #region Atmosphere Setup

        private void SetupAtmosphere()
        {
            // Environmental fog — depth-based for natural feel
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.006f;
            RenderSettings.fogColor = new Color(0.06f, 0.08f, 0.12f);

            // Skybox-like ambient: gradient from dark blue to green-gray horizon
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.05f, 0.08f, 0.15f);
            RenderSettings.ambientEquatorColor = new Color(0.12f, 0.15f, 0.12f);
            RenderSettings.ambientGroundColor = new Color(0.04f, 0.05f, 0.03f);
        }

        #endregion

        #region Procedural Mesh Builders

        /// <summary>
        /// Create a noise-deformed icosphere mesh. Subdivision level controls detail,
        /// noiseAmount controls irregularity. Seed for deterministic variation.
        /// </summary>
        private Mesh CreateNoisyIcosphere(int subdivisions, float noiseAmount, int seed)
        {
            // Build base icosahedron
            float t = (1f + Mathf.Sqrt(5f)) / 2f;
            var verts = new List<Vector3>
            {
                new Vector3(-1, t, 0).normalized,
                new Vector3(1, t, 0).normalized,
                new Vector3(-1, -t, 0).normalized,
                new Vector3(1, -t, 0).normalized,
                new Vector3(0, -1, t).normalized,
                new Vector3(0, 1, t).normalized,
                new Vector3(0, -1, -t).normalized,
                new Vector3(0, 1, -t).normalized,
                new Vector3(t, 0, -1).normalized,
                new Vector3(t, 0, 1).normalized,
                new Vector3(-t, 0, -1).normalized,
                new Vector3(-t, 0, 1).normalized,
            };

            var tris = new List<int>
            {
                0,11,5, 0,5,1, 0,1,7, 0,7,10, 0,10,11,
                1,5,9, 5,11,4, 11,10,2, 10,7,6, 7,1,8,
                3,9,4, 3,4,2, 3,2,6, 3,6,8, 3,8,9,
                4,9,5, 2,4,11, 6,2,10, 8,6,7, 9,8,1,
            };

            // Subdivide
            var midpointCache = new Dictionary<long, int>();
            for (int s = 0; s < subdivisions; s++)
            {
                var newTris = new List<int>();
                for (int i = 0; i < tris.Count; i += 3)
                {
                    int a = tris[i];
                    int b = tris[i + 1];
                    int c = tris[i + 2];

                    int ab = GetMidpoint(a, b, verts, midpointCache);
                    int bc = GetMidpoint(b, c, verts, midpointCache);
                    int ca = GetMidpoint(c, a, verts, midpointCache);

                    newTris.AddRange(new[] { a, ab, ca, b, bc, ab, c, ca, bc, ab, bc, ca });
                }
                tris = newTris;
                midpointCache.Clear();
            }

            // Apply noise displacement for organic rock shape
            Random.State prevState = Random.state;
            Random.InitState(seed);
            for (int i = 0; i < verts.Count; i++)
            {
                Vector3 v = verts[i];
                // Multi-frequency noise displacement
                float noise = 1f + (Random.value - 0.5f) * noiseAmount * 2f;
                // Bias: flatten bottom slightly for stable placement
                float yBias = v.y < -0.3f ? 0.85f : 1f;
                verts[i] = v * noise * yBias;
            }
            Random.state = prevState;

            Mesh mesh = new Mesh();
            mesh.name = $"NoisyIcosphere_s{subdivisions}";
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private int GetMidpoint(int i1, int i2, List<Vector3> verts, Dictionary<long, int> cache)
        {
            long key = ((long)Mathf.Min(i1, i2) << 32) + Mathf.Max(i1, i2);
            if (cache.TryGetValue(key, out int mid)) return mid;

            Vector3 p = ((verts[i1] + verts[i2]) * 0.5f).normalized;
            verts.Add(p);
            mid = verts.Count - 1;
            cache[key] = mid;
            return mid;
        }

        /// <summary>
        /// Create a hexagonal prism with pointed tip — realistic crystal shard shape.
        /// </summary>
        private Mesh CreateHexCrystalMesh()
        {
            int sides = 6;
            float bodyHeight = 0.6f;
            float tipHeight = 0.4f;
            float radius = 0.5f;

            var verts = new List<Vector3>();
            var tris = new List<int>();

            // Bottom center
            verts.Add(new Vector3(0f, 0f, 0f)); // 0

            // Bottom ring
            for (int i = 0; i < sides; i++)
            {
                float angle = i * Mathf.PI * 2f / sides;
                verts.Add(new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius));
            }

            // Top ring (at bodyHeight)
            for (int i = 0; i < sides; i++)
            {
                float angle = i * Mathf.PI * 2f / sides;
                // Slight taper at top
                float topRadius = radius * 0.85f;
                verts.Add(new Vector3(Mathf.Cos(angle) * topRadius, bodyHeight, Mathf.Sin(angle) * topRadius));
            }

            // Tip
            verts.Add(new Vector3(0f, bodyHeight + tipHeight, 0f)); // index = 1 + sides*2

            int tipIdx = 1 + sides * 2;

            // Bottom face (fan)
            for (int i = 0; i < sides; i++)
            {
                int next = (i + 1) % sides;
                tris.Add(0);
                tris.Add(1 + next);
                tris.Add(1 + i);
            }

            // Body sides
            for (int i = 0; i < sides; i++)
            {
                int next = (i + 1) % sides;
                int bCur = 1 + i;
                int bNext = 1 + next;
                int tCur = 1 + sides + i;
                int tNext = 1 + sides + next;

                tris.Add(bCur); tris.Add(tCur); tris.Add(bNext);
                tris.Add(bNext); tris.Add(tCur); tris.Add(tNext);
            }

            // Tip cone
            for (int i = 0; i < sides; i++)
            {
                int next = (i + 1) % sides;
                int tCur = 1 + sides + i;
                int tNext = 1 + sides + next;
                tris.Add(tCur); tris.Add(tipIdx); tris.Add(tNext);
            }

            Mesh mesh = new Mesh();
            mesh.name = "HexCrystal";
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>
        /// Create a deformed cylinder for fallen log / tree trunk.
        /// </summary>
        private Mesh CreateLogMesh(int segments, float length, float radius, int seed)
        {
            int rings = 8;
            var verts = new List<Vector3>();
            var tris = new List<int>();

            Random.State prevState = Random.state;
            Random.InitState(seed);

            for (int r = 0; r <= rings; r++)
            {
                float t2 = (float)r / rings;
                float z = (t2 - 0.5f) * length * 2f;
                // Slight radius variation along length for organic shape
                float rVar = radius * (1f + (Random.value - 0.5f) * 0.2f);
                // Taper at ends
                float taper = 1f - Mathf.Pow(Mathf.Abs(t2 - 0.5f) * 2f, 2f) * 0.3f;

                for (int s = 0; s < segments; s++)
                {
                    float angle = (float)s / segments * Mathf.PI * 2f;
                    float rNoise = rVar * taper * (1f + (Random.value - 0.5f) * 0.15f);
                    verts.Add(new Vector3(
                        Mathf.Cos(angle) * rNoise,
                        Mathf.Sin(angle) * rNoise,
                        z));
                }
            }

            // End caps: center vertices
            verts.Add(new Vector3(0f, 0f, -length)); // bottom cap center
            verts.Add(new Vector3(0f, 0f, length));  // top cap center
            int bottomCapIdx = verts.Count - 2;
            int topCapIdx = verts.Count - 1;

            Random.state = prevState;

            // Body quads
            for (int r = 0; r < rings; r++)
            {
                for (int s = 0; s < segments; s++)
                {
                    int cur = r * segments + s;
                    int next = r * segments + (s + 1) % segments;
                    int curUp = (r + 1) * segments + s;
                    int nextUp = (r + 1) * segments + (s + 1) % segments;

                    tris.Add(cur); tris.Add(curUp); tris.Add(next);
                    tris.Add(next); tris.Add(curUp); tris.Add(nextUp);
                }
            }

            // Bottom cap
            for (int s = 0; s < segments; s++)
            {
                int next = (s + 1) % segments;
                tris.Add(bottomCapIdx); tris.Add(next); tris.Add(s);
            }

            // Top cap
            int topRingStart = rings * segments;
            for (int s = 0; s < segments; s++)
            {
                int next = (s + 1) % segments;
                tris.Add(topCapIdx); tris.Add(topRingStart + s); tris.Add(topRingStart + next);
            }

            Mesh mesh = new Mesh();
            mesh.name = "Log";
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>
        /// Create a mushroom cap mesh — dome shape with slight organic deformation.
        /// </summary>
        private Mesh CreateMushroomCapMesh()
        {
            int segments = 8;
            int rings = 4;
            var verts = new List<Vector3>();
            var tris = new List<int>();

            // Dome vertices: hemisphere with flared bottom edge
            for (int r = 0; r <= rings; r++)
            {
                float phi = (float)r / rings * Mathf.PI * 0.5f; // 0 to PI/2
                float y = Mathf.Cos(phi);
                float ringRadius = Mathf.Sin(phi);

                // Flare the bottom ring outward
                if (r == rings) ringRadius *= 1.3f;

                for (int s = 0; s < segments; s++)
                {
                    float theta = (float)s / segments * Mathf.PI * 2f;
                    verts.Add(new Vector3(
                        Mathf.Cos(theta) * ringRadius,
                        y * 0.6f, // Flatten dome
                        Mathf.Sin(theta) * ringRadius));
                }
            }

            // Top vertex
            verts.Add(new Vector3(0f, 0.6f, 0f));
            int topIdx = verts.Count - 1;

            // Body quads
            for (int r = 0; r < rings; r++)
            {
                for (int s = 0; s < segments; s++)
                {
                    int cur = r * segments + s;
                    int next = r * segments + (s + 1) % segments;
                    int curUp = (r + 1) * segments + s;
                    int nextUp = (r + 1) * segments + (s + 1) % segments;

                    tris.Add(cur); tris.Add(next); tris.Add(curUp);
                    tris.Add(next); tris.Add(nextUp); tris.Add(curUp);
                }
            }

            // Top cap fan (connect first ring to top vertex)
            for (int s = 0; s < segments; s++)
            {
                int next = (s + 1) % segments;
                tris.Add(s); tris.Add(topIdx); tris.Add(next);
            }

            Mesh mesh = new Mesh();
            mesh.name = "MushroomCap";
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>
        /// Create a mushroom stem mesh — tapered cylinder.
        /// </summary>
        private Mesh CreateMushroomStemMesh()
        {
            int segments = 6;
            var verts = new List<Vector3>();
            var tris = new List<int>();

            float bottomRadius = 0.5f;
            float topRadius = 0.35f;

            // Bottom ring
            for (int s = 0; s < segments; s++)
            {
                float angle = (float)s / segments * Mathf.PI * 2f;
                verts.Add(new Vector3(Mathf.Cos(angle) * bottomRadius, 0f, Mathf.Sin(angle) * bottomRadius));
            }

            // Top ring
            for (int s = 0; s < segments; s++)
            {
                float angle = (float)s / segments * Mathf.PI * 2f;
                verts.Add(new Vector3(Mathf.Cos(angle) * topRadius, 1f, Mathf.Sin(angle) * topRadius));
            }

            // Sides
            for (int s = 0; s < segments; s++)
            {
                int next = (s + 1) % segments;
                tris.Add(s); tris.Add(segments + s); tris.Add(next);
                tris.Add(next); tris.Add(segments + s); tris.Add(segments + next);
            }

            Mesh mesh = new Mesh();
            mesh.name = "MushroomStem";
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        #endregion

        #region Material Factories

        private Shader FindLitShader()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            return shader;
        }

        /// <summary>
        /// Create rock material with natural color tinting and optional Y-based darkening.
        /// </summary>
        private Material CreateRockMaterial(float yPosition = 0f, float rockHeight = 1f)
        {
            Material mat = new Material(FindLitShader());
            float gray = Random.Range(0.15f, 0.3f);

            // Color variation: warm/cool tints for geological realism
            float variation = Random.value;
            Color rockColor;
            if (variation < 0.18f)
            {
                // Mossy green-gray
                rockColor = new Color(gray * 0.7f, gray * 1.3f, gray * 0.6f);
            }
            else if (variation < 0.32f)
            {
                // Sandy warm brown
                rockColor = new Color(gray * 1.3f, gray * 1.05f, gray * 0.65f);
            }
            else if (variation < 0.46f)
            {
                // Reddish-brown (iron-rich)
                rockColor = new Color(gray * 1.2f, gray * 0.8f, gray * 0.7f);
            }
            else if (variation < 0.58f)
            {
                // Blue-gray (slate-like)
                rockColor = new Color(gray * 0.75f, gray * 0.8f, gray * 1.2f);
            }
            else if (variation < 0.70f)
            {
                // Warm brown (sandstone)
                rockColor = new Color(gray * 1.4f, gray * 1.1f, gray * 0.75f);
            }
            else if (variation < 0.82f)
            {
                // Cool blue-green (weathered copper tint)
                rockColor = new Color(gray * 0.7f, gray * 1.0f, gray * 1.1f);
            }
            else
            {
                // Standard cool gray
                rockColor = new Color(gray * 0.9f, gray * 0.9f, gray);
            }

            // Bottom-darkening: simulate moisture accumulation near ground level
            float darkenFactor = 1f;
            if (rockHeight > 0f)
            {
                float normalizedY = Mathf.Clamp01(yPosition / rockHeight);
                if (normalizedY < 0.3f)
                {
                    // Lower 30% gets darker to simulate moisture wicking
                    darkenFactor = Mathf.Lerp(0.6f, 1f, normalizedY / 0.3f);
                }
            }
            rockColor *= darkenFactor;

            mat.color = rockColor;
            mat.SetFloat("_Smoothness", Random.Range(0.02f, 0.15f));
            mat.SetFloat("_Metallic", 0f);
            return mat;
        }

        private Material CreateDarkRockMaterial()
        {
            Material mat = new Material(FindLitShader());
            mat.color = new Color(0.06f, 0.06f, 0.08f);
            mat.SetFloat("_Smoothness", 0.6f);
            return mat;
        }

        private Material CreateMossMaterial()
        {
            Material mat = new Material(FindLitShader());
            mat.color = new Color(
                Random.Range(0.08f, 0.15f),
                Random.Range(0.25f, 0.45f),
                Random.Range(0.04f, 0.1f));
            mat.SetFloat("_Smoothness", 0.1f);
            mat.SetFloat("_Metallic", 0f);
            return mat;
        }

        private Material CreateCrystalMaterial()
        {
            Material mat = new Material(FindLitShader());
            Color[] colors = {
                new Color(0.3f, 0.8f, 1f, 0.7f),   // Cyan
                new Color(0.8f, 0.3f, 1f, 0.7f),    // Purple
                new Color(0.3f, 1f, 0.5f, 0.7f),    // Green
                new Color(1f, 0.5f, 0.3f, 0.7f),    // Amber
                new Color(1f, 0.4f, 0.7f, 0.7f),    // Pink
                new Color(0.9f, 0.9f, 1f, 0.75f),   // White quartz
            };
            mat.color = colors[Random.Range(0, colors.Length)];
            mat.SetFloat("_Smoothness", 0.95f);
            mat.SetFloat("_Metallic", 0.35f); // Higher metallic for surface detail and facet reflections

            // Enable transparency for translucent crystal look
            mat.SetFloat("_Surface", 1f); // Transparent
            mat.SetFloat("_Blend", 0f);
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.renderQueue = 3000;
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.EnableKeyword("_ALPHAPREMULTIPLY_ON");

            // Emission for inner glow — boosted to simulate Fresnel-like edge highlighting
            mat.EnableKeyword("_EMISSION");
            Color emissionBase = mat.color;
            // Brighter emission simulates edge light scattering on crystal facets
            mat.SetColor("_EmissionColor", emissionBase * 3.2f);
            // Set specular highlights high for faceted edge catch-light
            mat.SetFloat("_SpecularHighlights", 1f);
            mat.EnableKeyword("_SPECULARHIGHLIGHTS_OFF"); // Ensure keyword toggling is correct
            mat.DisableKeyword("_SPECULARHIGHLIGHTS_OFF");
            return mat;
        }

        private Material CreateBarkMaterial()
        {
            Material mat = new Material(FindLitShader());
            float r = Random.Range(0.25f, 0.4f);
            float g = Random.Range(0.15f, 0.25f);
            float b = Random.Range(0.08f, 0.14f);
            mat.color = new Color(r, g, b);
            mat.SetFloat("_Smoothness", 0.08f);
            mat.SetFloat("_Metallic", 0f);
            return mat;
        }

        private Material CreateCanopyMaterial()
        {
            Material mat = new Material(FindLitShader());
            mat.color = new Color(
                Random.Range(0.08f, 0.2f),
                Random.Range(0.3f, 0.55f),
                Random.Range(0.04f, 0.12f));
            mat.SetFloat("_Smoothness", 0.12f);
            mat.SetFloat("_Metallic", 0f);
            return mat;
        }

        private Material CreateMushroomCapMaterial()
        {
            Material mat = new Material(FindLitShader());
            // Extended variety of mushroom cap colors
            float roll = Random.value;
            if (roll < 0.2f)
            {
                // Red/orange toadstool (Amanita-like)
                mat.color = new Color(
                    Random.Range(0.6f, 0.9f),
                    Random.Range(0.1f, 0.3f),
                    Random.Range(0.05f, 0.15f));
            }
            else if (roll < 0.38f)
            {
                // Dark brown (Porcini-like)
                mat.color = new Color(
                    Random.Range(0.25f, 0.4f),
                    Random.Range(0.15f, 0.25f),
                    Random.Range(0.08f, 0.15f));
            }
            else if (roll < 0.52f)
            {
                // Light tan/golden (Chanterelle-like)
                mat.color = new Color(
                    Random.Range(0.7f, 0.9f),
                    Random.Range(0.5f, 0.7f),
                    Random.Range(0.15f, 0.3f));
            }
            else if (roll < 0.65f)
            {
                // Deep red/burgundy (Wine cap)
                mat.color = new Color(
                    Random.Range(0.35f, 0.55f),
                    Random.Range(0.08f, 0.18f),
                    Random.Range(0.1f, 0.2f));
            }
            else if (roll < 0.78f)
            {
                // Brown/tan
                mat.color = new Color(
                    Random.Range(0.3f, 0.5f),
                    Random.Range(0.2f, 0.35f),
                    Random.Range(0.1f, 0.2f));
            }
            else
            {
                // Pale/white with slight color
                float v = Random.Range(0.6f, 0.85f);
                mat.color = new Color(v, v * 0.95f, v * 0.85f);
            }
            mat.SetFloat("_Smoothness", Random.Range(0.3f, 0.55f));
            return mat;
        }

        private Material CreateMushroomStemMaterial()
        {
            Material mat = new Material(FindLitShader());
            float v = Random.Range(0.6f, 0.8f);
            mat.color = new Color(v, v * 0.95f, v * 0.85f);
            mat.SetFloat("_Smoothness", 0.2f);
            return mat;
        }

        private Material CreateDebrisMaterial()
        {
            Material mat = new Material(FindLitShader());
            float v = Random.Range(0.1f, 0.25f);
            mat.color = new Color(v * 1.1f, v, v * 0.9f);
            mat.SetFloat("_Smoothness", 0.15f);
            return mat;
        }

        private Material CreateParticleMaterial(Color color, bool emissive = false)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null) shader = Shader.Find("Particles/Standard Unlit");
            if (shader == null) shader = FindLitShader();

            Material mat = new Material(shader);
            mat.color = color;

            if (emissive)
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", color * 2f);
            }

            // Enable transparency for particles
            mat.SetFloat("_Surface", 1f);
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.renderQueue = 3000;
            return mat;
        }

        #endregion

        #region Utility

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

        #endregion
    }
}
