using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using TMPro;
using System.IO;
using Blob3D.Data;

/// <summary>
/// Blob3D one-click project setup.
/// Run from Unity menu "Blob3D > Setup Project" to auto-generate everything.
///
/// Generated assets:
///   - Materials (Player, AI variants, Feed colors, PowerUp colors, Ground, Boundary)
///   - Prefabs (PlayerBlob, AIBlob, Feed, PowerUp)
///   - Scene (GameScene: all managers + UI + Player + Camera + Field)
///   - Physics layer collision matrix settings
/// </summary>
public class Blob3DProjectSetup : EditorWindow
{
    [MenuItem("Blob3D/Setup Project (Full Auto)", false, 1)]
    public static void ShowWindow()
    {
        GetWindow<Blob3DProjectSetup>("Blob3D Setup").Show();
    }

    private bool setupComplete = false;
    private string statusMessage = "";

    private void OnGUI()
    {
        GUILayout.Label("Blob3D Project Setup", EditorStyles.boldLabel);
        GUILayout.Space(10);
        GUILayout.Label("This tool auto-generates the complete project:\n" +
            "• Generate materials\n" +
            "• Generate prefabs (Player / AI / Feed / PowerUp)\n" +
            "• Generate game scene (Managers + UI + Camera)\n" +
            "• Physics layer settings", EditorStyles.wordWrappedLabel);
        GUILayout.Space(20);

        if (!setupComplete)
        {
            if (GUILayout.Button("Start Setup", GUILayout.Height(40)))
            {
                RunFullSetup();
            }
        }
        else
        {
            EditorGUILayout.HelpBox("Setup complete! Open GameScene and press Play.\n" +
                "Controls: WASD=Move, Space=DASH, F=Split, Right-drag=Camera rotate",
                MessageType.Info);
        }

        if (!string.IsNullOrEmpty(statusMessage))
        {
            GUILayout.Space(10);
            GUILayout.Label(statusMessage, EditorStyles.wordWrappedLabel);
        }
    }

    private void RunFullSetup()
    {
        try
        {
            statusMessage = "Configuring URP pipeline...";
            Repaint();
            ConfigureURP();

            statusMessage = "Generating materials...";
            Repaint();
            CreateDirectories();
            CreateMaterials();

            statusMessage = "Generating skin data...";
            Repaint();
            CreateDefaultSkins();

            statusMessage = "Generating prefabs...";
            Repaint();
            CreatePrefabs();

            // Japanese font is optional — all UI text is English-compatible
            try
            {
                statusMessage = "Generating font...";
                Repaint();
                japaneseFont = CreateJapaneseFontAsset();
            }
            catch (System.Exception fontEx)
            {
                Debug.LogWarning($"[Blob3D] Font generation skipped: {fontEx.Message}");
                japaneseFont = null;
            }

            statusMessage = "Building scene...";
            Repaint();
            CreateGameScene();

            statusMessage = "Configuring physics layers...";
            Repaint();
            ConfigurePhysicsLayers();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            setupComplete = true;
            statusMessage = "Setup complete! Open Assets/Scenes/GameScene.unity.";
            Debug.Log("[Blob3D] Project setup complete!");
        }
        catch (System.Exception e)
        {
            statusMessage = $"Error: {e.Message}";
            Debug.LogError($"[Blob3D] Setup Error: {e}");
        }
    }

    // ========================================
    // URP Pipeline Configuration
    // ========================================

    private void ConfigureURP()
    {
        string pipelinePath = "Assets/Resources/Blob3D_URP.asset";
        string rendererPath = "Assets/Resources/Blob3D_URP_Renderer.asset";

        if (GraphicsSettings.currentRenderPipeline != null)
        {
            Debug.Log("[Blob3D] URP pipeline already configured.");
            return;
        }

        // Create URP renderer data
        var rendererData = ScriptableObject.CreateInstance<UniversalRendererData>();
        AssetDatabase.CreateAsset(rendererData, rendererPath);

        // Create pipeline asset — use CreateInstance for URP 14.x (Unity 2022.3)
        var pipelineAsset = ScriptableObject.CreateInstance<UniversalRenderPipelineAsset>();

        // Set renderer via SerializedObject
        SerializedObject pipelineSO = new SerializedObject(pipelineAsset);
        var rendererListProp = pipelineSO.FindProperty("m_RendererDataList");
        rendererListProp.arraySize = 1;
        rendererListProp.GetArrayElementAtIndex(0).objectReferenceValue = rendererData;

        var defaultRendererProp = pipelineSO.FindProperty("m_DefaultRendererIndex");
        if (defaultRendererProp != null) defaultRendererProp.intValue = 0;

        pipelineSO.ApplyModifiedPropertiesWithoutUndo();
        AssetDatabase.CreateAsset(pipelineAsset, pipelinePath);

        // Set as default render pipeline
        GraphicsSettings.defaultRenderPipeline = pipelineAsset;

        // Set in all quality levels
        var qualitySettings = AssetDatabase.LoadAssetAtPath<Object>("ProjectSettings/QualitySettings.asset");
        if (qualitySettings != null)
        {
            SerializedObject qualSO = new SerializedObject(qualitySettings);
            var qualityArray = qualSO.FindProperty("m_QualitySettings");
            for (int i = 0; i < qualityArray.arraySize; i++)
            {
                var element = qualityArray.GetArrayElementAtIndex(i);
                var rpProp = element.FindPropertyRelative("customRenderPipeline");
                if (rpProp != null)
                    rpProp.objectReferenceValue = pipelineAsset;
            }
            qualSO.ApplyModifiedPropertiesWithoutUndo();
        }

        Debug.Log("[Blob3D] URP pipeline configured successfully!");
    }

    // ========================================
    // Font Asset Generation
    // ========================================

    private TMP_FontAsset CreateJapaneseFontAsset()
    {
        string fontDir = "Assets/Resources/Fonts";
        string fontAssetPath = $"{fontDir}/NotoSansJP SDF.asset";

        // Return existing font if already created
        TMP_FontAsset existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(fontAssetPath);
        if (existing != null)
        {
            Debug.Log("[Blob3D] Using existing font asset");
            return existing;
        }

        if (!Directory.Exists(fontDir))
            Directory.CreateDirectory(fontDir);

        // Try system Japanese fonts in order of preference
        string[] japaneseFontNames = {
            "Hiragino Sans",
            "Hiragino Kaku Gothic ProN",
            "Hiragino Sans GB",
            "MS Gothic",
            "Yu Gothic"
        };

        Font sourceFont = null;
        foreach (string fontName in japaneseFontNames)
        {
            sourceFont = Font.CreateDynamicFontFromOSFont(fontName, 32);
            if (sourceFont != null)
            {
                Debug.Log($"[Blob3D] Using system font '{fontName}'");
                break;
            }
        }

        // Final fallback: built-in font with some CJK coverage
        if (sourceFont == null)
        {
            sourceFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            Debug.LogWarning("[Blob3D] No system font found. Falling back to built-in font");
        }

        if (sourceFont == null)
        {
            Debug.LogError("[Blob3D] Failed to create font");
            return null;
        }

        // Create TMP_FontAsset from the source font
        TMP_FontAsset fontAsset = TMP_FontAsset.CreateFontAsset(sourceFont);
        if (fontAsset == null)
        {
            Debug.LogError("[Blob3D] Failed to create TMP_FontAsset");
            return null;
        }

        // Configure atlas via SerializedObject (atlasWidth/Height are read-only properties)
        SerializedObject fontSO = new SerializedObject(fontAsset);
        var widthProp = fontSO.FindProperty("m_AtlasWidth");
        var heightProp = fontSO.FindProperty("m_AtlasHeight");
        if (widthProp != null) widthProp.intValue = 2048;
        if (heightProp != null) heightProp.intValue = 2048;
        fontSO.ApplyModifiedPropertiesWithoutUndo();

        AssetDatabase.CreateAsset(fontAsset, fontAssetPath);
        Debug.Log($"[Blob3D] Font asset created: {fontAssetPath}");
        return fontAsset;
    }

    // ========================================
    // Directories
    // ========================================

    private void CreateDirectories()
    {
        string[] dirs = {
            "Assets/Materials",
            "Assets/Prefabs",
            "Assets/Scenes",
            "Assets/Resources",
            "Assets/Resources/Fonts"
        };
        foreach (var dir in dirs)
        {
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);
        }
    }

    // ========================================
    // Material Generation
    // ========================================

    private Material CreateMat(string name, Color color, bool transparent = false)
    {
        string path = $"Assets/Materials/{name}.mat";
        Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing != null) return existing;

        // Find URP Lit shader, fall back to Standard if not found
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");

        Material mat = new Material(shader);
        mat.color = color;

        if (transparent)
        {
            mat.SetFloat("_Surface", 1); // URP Transparent
            mat.SetFloat("_Blend", 0);
            mat.SetFloat("_AlphaClip", 0);
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.renderQueue = 3000;
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        }

        AssetDatabase.CreateAsset(mat, path);
        return mat;
    }

    /// <summary>Create a material with emission enabled for power-up items</summary>
    private Material CreatePowerUpMat(string name, Color color, float emissionIntensity = 2.5f, bool transparent = false)
    {
        Material mat = CreateMat(name, color, transparent);
        mat.EnableKeyword("_EMISSION");
        mat.SetColor("_EmissionColor", color * emissionIntensity);
        mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        return mat;
    }

    /// <summary>Create a feed material with subtle emission glow</summary>
    private Material CreateFeedMat(string name, Color color)
    {
        Material mat = CreateMat(name, color);
        mat.EnableKeyword("_EMISSION");
        mat.SetColor("_EmissionColor", color * 0.5f);
        mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        return mat;
    }

    /// <summary>Create or update a material using BlobSurface shader with full visual properties</summary>
    private Material CreateBlobMat(string name, Color baseColor, Color subsurfaceColor,
        float fresnelPower = 3f, float fresnelIntensity = 1.2f, float innerGlow = 0.4f,
        float subsurfaceIntensity = 0.8f, float smoothness = 0.92f, float envReflect = 0.35f)
    {
        string path = $"Assets/Materials/{name}.mat";

        Shader shader = Shader.Find("Blob3D/BlobSurface");
        if (shader == null)
        {
            Debug.LogWarning($"[Blob3D] BlobSurface shader not found, falling back to URP/Lit for {name}");
            return CreateMat(name, baseColor);
        }

        Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        bool isNew = (mat == null);
        if (isNew)
        {
            mat = new Material(shader);
        }
        else
        {
            mat.shader = shader; // Ensure shader is up to date
        }

        mat.SetColor("_BaseColor", baseColor);
        mat.SetColor("_SubsurfaceColor", subsurfaceColor);
        mat.SetFloat("_FresnelPower", fresnelPower);
        mat.SetFloat("_FresnelIntensity", fresnelIntensity);
        mat.SetFloat("_InnerGlow", innerGlow);
        mat.SetFloat("_SubsurfaceIntensity", subsurfaceIntensity);
        mat.SetFloat("_SubsurfaceDistortion", 0.5f);
        mat.SetFloat("_SubsurfaceWrap", 0.6f);
        mat.SetFloat("_Smoothness", smoothness);
        mat.SetFloat("_Metallic", 0f);
        mat.SetColor("_RimColor", Color.white);
        mat.SetFloat("_RimPower", 2.5f);
        mat.SetFloat("_PulseSpeed", 1.5f);
        mat.SetFloat("_PulseAmount", 0.01f);
        mat.SetFloat("_WobbleSpeed", 3f);
        mat.SetFloat("_WobbleAmount", 0.008f);
        mat.SetFloat("_EnvReflectIntensity", envReflect);

        // New translucent gel properties
        mat.SetFloat("_Opacity", 0.82f);
        mat.SetFloat("_RefractionStrength", 0.06f);
        mat.SetFloat("_ChromaticSpread", 0.08f);
        mat.SetFloat("_DepthColorShift", 0.3f);

        // Configure transparent rendering
        mat.renderQueue = 3000; // Transparent queue
        mat.SetOverrideTag("RenderType", "Transparent");

        if (isNew)
        {
            AssetDatabase.CreateAsset(mat, path);
        }
        else
        {
            EditorUtility.SetDirty(mat);
        }
        return mat;
    }

    /// <summary>Create or update a material using GridGround shader</summary>
    private Material CreateGroundMat(string name, Color baseColor, Color gridColor,
        float gridSize = 8f, Color? centerColor = null)
    {
        string path = $"Assets/Materials/{name}.mat";

        Shader shader = Shader.Find("Blob3D/GridGround");
        if (shader == null)
        {
            Debug.LogWarning($"[Blob3D] GridGround shader not found, falling back to URP/Lit for {name}");
            return CreateMat(name, baseColor);
        }

        Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        bool isNew = (mat == null);
        if (isNew)
        {
            mat = new Material(shader);
        }
        else
        {
            mat.shader = shader;
        }

        mat.SetColor("_BaseColor", baseColor);
        mat.SetColor("_GridColor", gridColor);
        mat.SetColor("_CenterColor", centerColor ?? new Color(0.15f, 0.16f, 0.13f));
        mat.SetFloat("_GridSize", gridSize);
        mat.SetFloat("_GridWidth", 0.005f);
        mat.SetFloat("_FadeDistance", 150f);
        mat.SetFloat("_GradientRadius", 120f);
        mat.SetFloat("_ReflectIntensity", 0.02f);
        mat.SetFloat("_AOFadeStart", 80f);
        mat.SetFloat("_AOIntensity", 0.4f);

        // Caustic effect properties
        mat.SetFloat("_CausticIntensity", 0.15f);
        mat.SetFloat("_CausticScale", 0.08f);
        mat.SetFloat("_CausticSpeed", 0.5f);
        mat.SetColor("_CausticColor", new Color(0.3f, 0.5f, 0.8f));

        // Terrain properties (grass, dirt, height variation)
        mat.SetColor("_GrassColor1", new Color(0.15f, 0.28f, 0.08f));
        mat.SetColor("_GrassColor2", new Color(0.22f, 0.38f, 0.12f));
        mat.SetColor("_DirtColor", new Color(0.18f, 0.14f, 0.09f));
        mat.SetFloat("_GrassPatchScale", 0.04f);
        mat.SetFloat("_GrassBladeScale", 0.25f);
        mat.SetFloat("_GrassDensity", 0.6f);
        mat.SetFloat("_TerrainBumpScale", 0.015f);
        mat.SetFloat("_TerrainBumpFreq", 0.05f);
        mat.SetFloat("_HeightDisplacement", 0.8f);
        mat.SetFloat("_HeightFrequency", 0.02f);

        if (isNew)
        {
            AssetDatabase.CreateAsset(mat, path);
        }
        else
        {
            EditorUtility.SetDirty(mat);
        }
        return mat;
    }

    private Material matPlayer, matGround, matBoundary;
    private Material matAIWanderer, matAIHunter, matAICoward, matAIBoss;
    private Material[] matFeeds;
    private Material matPowerUpSpeed, matPowerUpShield, matPowerUpMagnet, matPowerUpGhost, matPowerUpMega;

    private void CreateMaterials()
    {
        // Player blob — blue with warm orange subsurface
        matPlayer = CreateBlobMat("M_Player",
            new Color(0.2f, 0.7f, 1f),
            new Color(1f, 0.4f, 0.15f),
            fresnelPower: 2.8f, fresnelIntensity: 1.4f, innerGlow: 0.45f);

        // Ground — rich natural earth surface with subtle grid
        matGround = CreateGroundMat("M_Ground",
            new Color(0.14f, 0.11f, 0.07f),   // Warm dark earth brown
            new Color(0.20f, 0.16f, 0.10f),    // Subtle warm grid lines
            gridSize: 10f,
            centerColor: new Color(0.16f, 0.13f, 0.09f));

        matBoundary = CreateMat("M_Boundary", new Color(1f, 0.3f, 0.3f, 0.4f), true);

        // AI blobs — each with distinct subsurface color for visual variety
        matAIWanderer = CreateBlobMat("M_AI_Wanderer",
            new Color(0.5f, 0.8f, 0.3f),
            new Color(0.8f, 1f, 0.3f),
            fresnelPower: 3f, innerGlow: 0.35f);

        matAIHunter = CreateBlobMat("M_AI_Hunter",
            new Color(1f, 0.3f, 0.2f),
            new Color(1f, 0.6f, 0.1f),
            fresnelPower: 2.5f, fresnelIntensity: 1.3f, innerGlow: 0.5f);

        matAICoward = CreateBlobMat("M_AI_Coward",
            new Color(0.3f, 0.9f, 0.4f),
            new Color(0.5f, 1f, 0.6f),
            fresnelPower: 3.2f, innerGlow: 0.3f);

        matAIBoss = CreateBlobMat("M_AI_Boss",
            new Color(0.6f, 0.1f, 0.8f),
            new Color(0.9f, 0.2f, 1f),
            fresnelPower: 2.2f, fresnelIntensity: 1.6f, innerGlow: 0.55f, subsurfaceIntensity: 1f);

        matFeeds = new Material[] {
            CreateFeedMat("M_Feed_Yellow", new Color(1f, 0.85f, 0.2f)),
            CreateFeedMat("M_Feed_Green",  new Color(0.3f, 0.9f, 0.5f)),
            CreateFeedMat("M_Feed_Blue",   new Color(0.4f, 0.6f, 1f)),
            CreateFeedMat("M_Feed_Pink",   new Color(1f, 0.4f, 0.6f)),
            CreateFeedMat("M_Feed_Orange", new Color(0.9f, 0.5f, 0.2f)),
        };

        matPowerUpSpeed  = CreatePowerUpMat("M_PowerUp_Speed",  new Color(0f, 0.8f, 1f), 2.5f);
        matPowerUpShield = CreatePowerUpMat("M_PowerUp_Shield", new Color(1f, 0.9f, 0.2f), 2.5f);
        matPowerUpMagnet = CreatePowerUpMat("M_PowerUp_Magnet", new Color(1f, 0.2f, 0.8f), 3.5f);
        matPowerUpGhost  = CreatePowerUpMat("M_PowerUp_Ghost",  new Color(0.8f, 0.8f, 1f, 0.5f), 2.5f, true);
        matPowerUpMega   = CreatePowerUpMat("M_PowerUp_Mega",   new Color(1f, 0.4f, 0.1f), 2.5f);
    }

    // ========================================
    // Skin Data Generation
    // ========================================

    private SkinData[] defaultSkins;

    private void CreateDefaultSkins()
    {
        string skinsDir = "Assets/Resources/Skins";
        if (!Directory.Exists(skinsDir))
            Directory.CreateDirectory(skinsDir);

        defaultSkins = new SkinData[6];

        defaultSkins[0] = CreateSkinAsset(skinsDir, "Default Blue",
            new Color(0.2f, 0.7f, 1f), new Color(1f, 0.4f, 0.15f),
            SkinData.UnlockType.Default, 0, 0);

        defaultSkins[1] = CreateSkinAsset(skinsDir, "Lime Green",
            new Color(0.3f, 0.9f, 0.3f), new Color(0.9f, 1f, 0.3f),
            SkinData.UnlockType.ScoreReached, 500, 50);

        defaultSkins[2] = CreateSkinAsset(skinsDir, "Hot Pink",
            new Color(1f, 0.3f, 0.6f), new Color(1f, 0.6f, 0.8f),
            SkinData.UnlockType.GamesPlayed, 5, 100);

        defaultSkins[3] = CreateSkinAsset(skinsDir, "Golden",
            new Color(1f, 0.85f, 0.2f), new Color(1f, 0.6f, 0.1f),
            SkinData.UnlockType.MaxSizeReached, 30, 200);

        defaultSkins[4] = CreateSkinAsset(skinsDir, "Shadow",
            new Color(0.15f, 0.15f, 0.2f), new Color(0.3f, 0.1f, 0.4f),
            SkinData.UnlockType.BlobsAbsorbed, 20, 300);

        defaultSkins[5] = CreateSkinAsset(skinsDir, "Rainbow",
            new Color(0.8f, 0.3f, 1f), new Color(0.3f, 1f, 0.8f),
            SkinData.UnlockType.SpecialChallenge, 1000, 500);

        Debug.Log("[Blob3D] Default skin data generated");
    }

    private SkinData CreateSkinAsset(string dir, string skinName, Color primary, Color secondary,
        SkinData.UnlockType unlockType, int unlockThreshold, int coinCost)
    {
        string safeName = skinName.Replace(" ", "_");
        string path = $"{dir}/Skin_{safeName}.asset";
        SkinData existing = AssetDatabase.LoadAssetAtPath<SkinData>(path);
        if (existing != null) return existing;

        SkinData skin = ScriptableObject.CreateInstance<SkinData>();
        skin.skinName = skinName;
        skin.primaryColor = primary;
        skin.secondaryColor = secondary;
        skin.unlockType = unlockType;
        skin.unlockThreshold = unlockThreshold;
        skin.coinCost = coinCost;
        AssetDatabase.CreateAsset(skin, path);
        return skin;
    }

    // ========================================
    // Prefab Generation
    // ========================================

    private GameObject prefabPlayerBlob, prefabAIBlob, prefabFeed, prefabPowerUp;

    private void CreatePrefabs()
    {
        prefabPlayerBlob = CreatePlayerBlobPrefab();
        prefabAIBlob = CreateAIBlobPrefab();
        prefabFeed = CreateFeedPrefab();
        prefabPowerUp = CreatePowerUpPrefab();
    }

    private GameObject CreatePlayerBlobPrefab()
    {
        string path = "Assets/Prefabs/PlayerBlob.prefab";
        GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (existing != null) return existing;

        GameObject obj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        obj.name = "PlayerBlob";
        obj.tag = "Player";
        obj.layer = LayerMask.NameToLayer("Player") != -1 ? LayerMask.NameToLayer("Player") : 0;

        // Renderer
        obj.GetComponent<MeshRenderer>().sharedMaterial = matPlayer;

        // Collider → Trigger
        SphereCollider col = obj.GetComponent<SphereCollider>();
        col.isTrigger = true;
        col.radius = 0.5f;

        // Rigidbody
        Rigidbody rb = obj.AddComponent<Rigidbody>();
        rb.useGravity = false;
        rb.constraints = RigidbodyConstraints.FreezeRotation | RigidbodyConstraints.FreezePositionY;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        // BlobController
        obj.AddComponent<Blob3D.Player.BlobController>();

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(obj, path);
        DestroyImmediate(obj);
        return prefab;
    }

    private GameObject CreateAIBlobPrefab()
    {
        string path = "Assets/Prefabs/AIBlob.prefab";
        GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (existing != null) return existing;

        GameObject obj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        obj.name = "AIBlob";

        obj.GetComponent<MeshRenderer>().sharedMaterial = matAIWanderer;

        SphereCollider col = obj.GetComponent<SphereCollider>();
        col.isTrigger = true;

        Rigidbody rb = obj.AddComponent<Rigidbody>();
        rb.useGravity = false;
        rb.constraints = RigidbodyConstraints.FreezeRotation | RigidbodyConstraints.FreezePositionY;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        obj.AddComponent<Blob3D.AI.AIBlobController>();

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(obj, path);
        DestroyImmediate(obj);
        return prefab;
    }

    private GameObject CreateFeedPrefab()
    {
        string path = "Assets/Prefabs/Feed.prefab";
        GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (existing != null) return existing;

        // Procedural icosahedron mesh — crystalline gem shape, distinct from blobs
        GameObject obj = new GameObject("Feed");
        obj.transform.localScale = Vector3.one * 0.15f;
        obj.layer = LayerMask.NameToLayer("Feed") != -1 ? LayerMask.NameToLayer("Feed") : 0;

        // Add mesh components with procedural icosahedron geometry
        MeshFilter mf = obj.AddComponent<MeshFilter>();
        mf.sharedMesh = Blob3D.Gameplay.Feed.CreateFeedMesh(Blob3D.Gameplay.Feed.FeedShape.Icosahedron);

        MeshRenderer mr = obj.AddComponent<MeshRenderer>();
        mr.sharedMaterial = matFeeds[0];

        // Sphere collider fits all geometric shape variants
        SphereCollider col = obj.AddComponent<SphereCollider>();
        col.isTrigger = true;
        col.radius = 1.2f; // Slightly larger than mesh for reliable pickup

        obj.AddComponent<Blob3D.Gameplay.Feed>();

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(obj, path);
        DestroyImmediate(obj);
        return prefab;
    }

    private GameObject CreatePowerUpPrefab()
    {
        string path = "Assets/Prefabs/PowerUp.prefab";
        GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (existing != null) return existing;

        // Empty GameObject — procedural mesh is generated at runtime by PowerUpItem
        GameObject obj = new GameObject("PowerUp");
        obj.transform.localScale = new Vector3(0.35f, 0.35f, 0.35f);
        obj.layer = LayerMask.NameToLayer("PowerUp") != -1 ? LayerMask.NameToLayer("PowerUp") : 0;

        // MeshFilter and MeshRenderer for procedural mesh assignment at runtime
        obj.AddComponent<MeshFilter>();
        MeshRenderer mr = obj.AddComponent<MeshRenderer>();
        mr.sharedMaterial = matPowerUpSpeed;

        // Box collider trigger for collection detection
        BoxCollider col = obj.AddComponent<BoxCollider>();
        col.isTrigger = true;

        obj.AddComponent<Blob3D.Gameplay.PowerUpItem>();

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(obj, path);
        DestroyImmediate(obj);
        return prefab;
    }

    // ========================================
    // Scene Generation
    // ========================================

    private void CreateGameScene()
    {
        // Create new scene
        var scene = UnityEditor.SceneManagement.EditorSceneManager.NewScene(
            UnityEditor.SceneManagement.NewSceneSetup.EmptyScene,
            UnityEditor.SceneManagement.NewSceneMode.Single);

        // --- Directional Light (main) ---
        GameObject light = new GameObject("Directional Light");
        Light lightComp = light.AddComponent<Light>();
        lightComp.type = LightType.Directional;
        lightComp.color = new Color(1f, 0.95f, 0.84f);
        lightComp.intensity = 1.4f;
        lightComp.shadows = LightShadows.Soft;
        light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

        // --- Fill Light (soft blue from opposite direction) ---
        GameObject fillLightObj = new GameObject("Fill Light");
        Light fillLight = fillLightObj.AddComponent<Light>();
        fillLight.type = LightType.Directional;
        fillLight.color = new Color(0.6f, 0.7f, 1f);
        fillLight.intensity = 0.3f;
        fillLight.shadows = LightShadows.None;
        fillLightObj.transform.rotation = Quaternion.Euler(30f, 150f, 0f);

        // --- Rim Light (warm orange from behind) ---
        GameObject rimLightObj = new GameObject("Rim Light");
        Light rimLight = rimLightObj.AddComponent<Light>();
        rimLight.type = LightType.Directional;
        rimLight.color = new Color(1f, 0.7f, 0.4f);
        rimLight.intensity = 0.5f;
        rimLight.shadows = LightShadows.None;
        rimLightObj.transform.rotation = Quaternion.Euler(15f, -160f, 0f);

        // --- Main Camera ---
        GameObject cameraObj = new GameObject("Main Camera");
        cameraObj.tag = "MainCamera";
        Camera cam = cameraObj.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.1f, 0.12f, 0.18f);
        cam.fieldOfView = 60f;
        cam.nearClipPlane = 0.3f;
        cam.farClipPlane = 500f;
        cameraObj.AddComponent<AudioListener>();
        cameraObj.AddComponent<Blob3D.Player.BlobCameraController>();
        cameraObj.transform.position = new Vector3(0, 15, -20);

        // --- Ground (solid, matte earth surface) ---
        // Use Cylinder mesh for visual (circular field), but replace collider with BoxCollider
        // to prevent physics sliding on CapsuleCollider's curved edges
        GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        ground.name = "Ground";
        ground.transform.position = new Vector3(0f, -0.05f, 0f);
        ground.transform.localScale = new Vector3(400f, 0.1f, 400f);
        ground.GetComponent<MeshRenderer>().sharedMaterial = matGround;
        // Replace CapsuleCollider with flat BoxCollider
        Object.DestroyImmediate(ground.GetComponent<Collider>());
        BoxCollider groundBox = ground.AddComponent<BoxCollider>();
        groundBox.center = new Vector3(0f, 0.5f, 0f); // Top of cylinder mesh
        groundBox.size = new Vector3(1f, 0.01f, 1f);   // Thin flat surface
        ground.isStatic = true;

        // --- Atmospheric ambient lighting (enhanced) ---
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.18f, 0.20f, 0.30f);       // Richer blue-violet sky
        RenderSettings.ambientEquatorColor = new Color(0.32f, 0.28f, 0.25f);   // Warm earthy equator
        RenderSettings.ambientGroundColor = new Color(0.10f, 0.08f, 0.06f);    // Deep warm ground

        // --- Fog — atmospheric depth for distant objects ---
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogColor = new Color(0.65f, 0.75f, 0.88f); // Soft blue-grey haze
        RenderSettings.fogDensity = 0.012f; // Subtle — visible at distance, not overpowering

        // --- Post-Processing Volume (Bloom + Vignette) ---
        GameObject ppVolume = new GameObject("PostProcessVolume");
        Volume volume = ppVolume.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.priority = 1;

        VolumeProfile profile = ScriptableObject.CreateInstance<VolumeProfile>();

        // Bloom — enhanced glow for crystals and blob highlights
        Bloom bloom = profile.Add<Bloom>(true);
        bloom.intensity.Override(1.5f);
        bloom.threshold.Override(0.4f);
        bloom.scatter.Override(0.85f); // Softer, wider glow

        // Vignette — atmospheric darkening at screen edges
        Vignette vignette = profile.Add<Vignette>(true);
        vignette.intensity.Override(0.42f);
        vignette.smoothness.Override(0.45f);

        // Tonemapping — filmic tone for richer colors
        Tonemapping tonemapping = profile.Add<Tonemapping>(true);
        tonemapping.mode.Override(TonemappingMode.ACES);

        // Color Adjustments — slight cool tint for atmosphere
        ColorAdjustments colorAdj = profile.Add<ColorAdjustments>(true);
        colorAdj.postExposure.Override(0.15f);
        colorAdj.contrast.Override(10f);
        colorAdj.saturation.Override(10f);

        // Depth of Field — cinematic focus
        DepthOfField dof = profile.Add<DepthOfField>(true);
        dof.mode.Override(DepthOfFieldMode.Gaussian);
        dof.gaussianStart.Override(25f);
        dof.gaussianEnd.Override(120f);
        dof.gaussianMaxRadius.Override(0.8f);

        // Film Grain — subtle realism
        FilmGrain filmGrain = profile.Add<FilmGrain>(true);
        filmGrain.type.Override(FilmGrainLookup.Thin1);
        filmGrain.intensity.Override(0.15f);
        filmGrain.response.Override(0.6f);

        // Chromatic Aberration — very subtle
        ChromaticAberration chromaticAberration = profile.Add<ChromaticAberration>(true);
        chromaticAberration.intensity.Override(0.08f);

        // Save profile as asset
        string ppProfilePath = "Assets/Resources/Blob3D_PostProcessProfile.asset";
        AssetDatabase.CreateAsset(profile, ppProfilePath);
        volume.profile = profile;

        // Enable post-processing on the camera
        var camData = cameraObj.GetComponent<UniversalAdditionalCameraData>();
        if (camData == null) camData = cameraObj.AddComponent<UniversalAdditionalCameraData>();
        camData.renderPostProcessing = true;

        // --- Player Blob ---
        GameObject player = PrefabUtility.InstantiatePrefab(prefabPlayerBlob) as GameObject;
        if (player == null) player = Instantiate(prefabPlayerBlob);
        player.name = "PlayerBlob";
        player.transform.position = new Vector3(0, 0.3f, 0);

        // Set camera target
        var camCtrl = cameraObj.GetComponent<Blob3D.Player.BlobCameraController>();
        // Set target field via SerializedObject
        SerializedObject camSO = new SerializedObject(camCtrl);
        camSO.FindProperty("target").objectReferenceValue = player.transform;
        camSO.ApplyModifiedProperties();

        // --- Managers ---
        CreateManager("GameManager", typeof(Blob3D.Core.GameManager));
        CreateManager("ScoreManager", typeof(Blob3D.Gameplay.ScoreManager));
        CreateManager("PowerUpEffectManager", typeof(Blob3D.Gameplay.PowerUpEffectManager));
        CreateManager("MobileInputHandler", typeof(Blob3D.Player.MobileInputHandler));
        CreateManager("ObjectPool", typeof(Blob3D.Utils.ObjectPool));
        CreateManager("AudioManager", typeof(Blob3D.Core.AudioManager));
        CreateManager("VFXManager", typeof(Blob3D.Gameplay.VFXManager));

        // FeedSpawner
        GameObject feedSpawnerObj = CreateManager("FeedSpawner", typeof(Blob3D.Gameplay.FeedSpawner));
        SerializedObject feedSO = new SerializedObject(feedSpawnerObj.GetComponent<Blob3D.Gameplay.FeedSpawner>());
        feedSO.FindProperty("feedPrefab").objectReferenceValue = prefabFeed;
        feedSO.ApplyModifiedProperties();

        // AISpawner
        GameObject aiSpawnerObj = CreateManager("AISpawner", typeof(Blob3D.AI.AISpawner));
        SerializedObject aiSO = new SerializedObject(aiSpawnerObj.GetComponent<Blob3D.AI.AISpawner>());
        aiSO.FindProperty("aiBlobPrefab").objectReferenceValue = prefabAIBlob;
        aiSO.ApplyModifiedProperties();

        // PowerUpSpawner
        GameObject puSpawnerObj = CreateManager("PowerUpSpawner", typeof(Blob3D.Gameplay.PowerUpSpawner));
        SerializedObject puSO = new SerializedObject(puSpawnerObj.GetComponent<Blob3D.Gameplay.PowerUpSpawner>());
        puSO.FindProperty("powerUpPrefab").objectReferenceValue = prefabPowerUp;
        puSO.ApplyModifiedProperties();

        // StageGenerator
        CreateManager("StageGenerator", typeof(Blob3D.Gameplay.StageGenerator));

        // AchievementManager
        CreateManager("AchievementManager", typeof(Blob3D.Data.AchievementManager));

        // DailyRewardManager
        CreateManager("DailyRewardManager", typeof(Blob3D.Data.DailyRewardManager));

        // SkinManager
        GameObject skinMgrObj = CreateManager("SkinManager", typeof(Blob3D.Data.SkinManager));
        SerializedObject skinSO = new SerializedObject(skinMgrObj.GetComponent<Blob3D.Data.SkinManager>());
        skinSO.FindProperty("playerRenderer").objectReferenceValue = player.GetComponent<MeshRenderer>();

        // Wire default skin assets to SkinManager
        if (defaultSkins == null || defaultSkins.Length == 0)
        {
            // Load from Resources if already created
            var loaded = Resources.LoadAll<SkinData>("Skins");
            if (loaded != null && loaded.Length > 0)
                defaultSkins = loaded;
        }
        if (defaultSkins != null && defaultSkins.Length > 0)
        {
            var allSkinsProp = skinSO.FindProperty("allSkins");
            allSkinsProp.arraySize = defaultSkins.Length;
            for (int i = 0; i < defaultSkins.Length; i++)
            {
                allSkinsProp.GetArrayElementAtIndex(i).objectReferenceValue = defaultSkins[i];
            }
        }
        skinSO.ApplyModifiedProperties();

        // FieldBoundary
        GameObject boundaryObj = new GameObject("FieldBoundary");
        boundaryObj.AddComponent<LineRenderer>();
        boundaryObj.AddComponent<Blob3D.Core.FieldBoundary>();
        LineRenderer lr = boundaryObj.GetComponent<LineRenderer>();
        lr.material = matBoundary;
        lr.startWidth = 0.5f;
        lr.endWidth = 0.5f;

        // --- UI ---
        CreateUI();

        // Save scene
        string scenePath = "Assets/Scenes/GameScene.unity";
        UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene, scenePath);
        Debug.Log($"[Blob3D] Scene saved: {scenePath}");
    }

    private GameObject CreateManager(string name, System.Type componentType)
    {
        GameObject obj = new GameObject(name);
        obj.AddComponent(componentType);
        return obj;
    }

    // ========================================
    // UI Generation — Modern mobile game UI
    // ========================================

    // Color palette — premium mobile game aesthetic
    private static readonly Color ColBgDark = new Color(0.04f, 0.05f, 0.08f, 0.92f);
    private static readonly Color ColBgCard = new Color(0.08f, 0.09f, 0.13f, 0.95f);
    private static readonly Color ColAccent = new Color(0.25f, 0.85f, 0.95f);
    private static readonly Color ColGreen = new Color(0.15f, 0.85f, 0.50f);
    private static readonly Color ColOrange = new Color(1f, 0.60f, 0.25f);
    private static readonly Color ColPurple = new Color(0.60f, 0.45f, 1f);
    private static readonly Color ColGold = new Color(1f, 0.85f, 0.30f);
    private static readonly Color ColGray = new Color(0.25f, 0.28f, 0.35f);
    private static readonly Color ColTextPrimary = new Color(0.95f, 0.95f, 0.98f);
    private static readonly Color ColTextSecondary = new Color(0.55f, 0.60f, 0.70f);
    private static readonly Color ColTextMuted = new Color(0.35f, 0.40f, 0.50f);

    // Japanese font asset (created dynamically from OS font)
    private TMP_FontAsset japaneseFont;

    private void CreateUI()
    {
        // EventSystem
        if (Object.FindFirstObjectByType<EventSystem>() == null)
        {
            GameObject es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<StandaloneInputModule>();
        }

        // Canvas
        GameObject canvasObj = new GameObject("Canvas");
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920);
        scaler.matchWidthOrHeight = 0.5f;

        canvasObj.AddComponent<GraphicRaycaster>();

        // UIManager
        Blob3D.UI.UIManager uiMgr = canvasObj.AddComponent<Blob3D.UI.UIManager>();
        canvasObj.AddComponent<Blob3D.UI.TutorialController>();

        // ====== Title Panel ======
        GameObject titlePanel = CreatePanel(canvasObj.transform, "TitlePanel");

        // Semi-transparent dark overlay for title
        Image titleBgImg = titlePanel.GetComponent<Image>();
        titleBgImg.color = ColBgDark;
        titleBgImg.raycastTarget = true;

        // Title Logo — large, bold, near top with outline for glow feel
        GameObject titleText = CreateTMPText(titlePanel.transform, "TitleLogo", "BLOB 3D",
            100, TextAlignmentOptions.Center, new Vector2(0, 350));
        var titleTMP = titleText.GetComponent<TextMeshProUGUI>();
        titleTMP.color = ColTextPrimary;
        titleTMP.fontStyle = FontStyles.Bold;
        titleTMP.enableVertexGradient = true;
        titleTMP.colorGradient = new VertexGradient(
            ColAccent, ColAccent,
            new Color(0.15f, 0.55f, 0.75f), new Color(0.15f, 0.55f, 0.75f));
        titleTMP.outlineWidth = 0.15f;
        titleTMP.outlineColor = new Color32(64, 216, 242, 80);
        titleTMP.characterSpacing = 12f;

        // Subtitle — small, muted, letter-spaced
        GameObject subtitleText = CreateTMPText(titlePanel.transform, "SubtitleText", "EAT  GROW  DOMINATE",
            22, TextAlignmentOptions.Center, new Vector2(0, 270));
        subtitleText.GetComponent<TextMeshProUGUI>().color = ColTextMuted;
        subtitleText.GetComponent<TextMeshProUGUI>().characterSpacing = 10f;

        // Mode cycle button — pill-shaped, small
        GameObject modeBtn = CreateStyledButton(titlePanel.transform, "ModeButton", "CLASSIC",
            new Vector2(0, 80), new Vector2(240, 50), ColOrange, 20f);
        TextMeshProUGUI modeLabelTMP = modeBtn.transform.GetChild(0).GetComponent<TextMeshProUGUI>();

        // Play Button — very large, prominent, gradient green
        GameObject playBtn = CreateStyledButton(titlePanel.transform, "PlayButton", "PLAY",
            new Vector2(0, -30), new Vector2(480, 120), ColGreen, 48f);

        // Row of 3 uniform buttons: SKINS | STATS | SHOP
        GameObject skinBtn = CreateStyledButton(titlePanel.transform, "SkinButton", "SKINS",
            new Vector2(-220, -180), new Vector2(190, 65), ColPurple, 22f);

        GameObject statsBtn = CreateStyledButton(titlePanel.transform, "StatsButton", "STATS",
            new Vector2(0, -180), new Vector2(190, 65), ColGray, 22f);

        GameObject shopBtn = CreateStyledButton(titlePanel.transform, "ShopButton", "SHOP",
            new Vector2(220, -180), new Vector2(190, 65), ColGold, 22f);

        // Bottom row: Coins (left), Best (center), Settings (right)
        // Coin display — bottom-left
        GameObject titleCoinText = CreateTMPText(titlePanel.transform, "TitleCoinDisplay", "COINS: 0",
            22, TextAlignmentOptions.Left, Vector2.zero);
        var titleCoinTMP = titleCoinText.GetComponent<TextMeshProUGUI>();
        titleCoinTMP.color = ColGold;
        titleCoinTMP.characterSpacing = 2f;
        SetAnchoredPosition(titleCoinText, new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 0), new Vector2(30, 30), new Vector2(250, 40));

        // HighScore label — bottom center
        GameObject highScoreLabel = CreateTMPText(titlePanel.transform, "HighScoreLabel", "BEST: 0",
            24, TextAlignmentOptions.Center, new Vector2(0, -340));
        var hsTMP = highScoreLabel.GetComponent<TextMeshProUGUI>();
        hsTMP.color = ColTextSecondary;
        hsTMP.characterSpacing = 4f;

        // ====== Stats Panel ======
        GameObject statsPanel = CreatePanel(canvasObj.transform, "StatsPanel");
        statsPanel.SetActive(false);
        Image statsBgImg = statsPanel.GetComponent<Image>();
        statsBgImg.color = ColBgDark;
        statsBgImg.raycastTarget = true;

        // Stats card background with subtle border
        GameObject statsCard = new GameObject("StatsCard");
        statsCard.transform.SetParent(statsPanel.transform, false);
        RectTransform statsCardRT = statsCard.AddComponent<RectTransform>();
        statsCardRT.anchorMin = new Vector2(0.5f, 0.5f);
        statsCardRT.anchorMax = new Vector2(0.5f, 0.5f);
        statsCardRT.sizeDelta = new Vector2(800, 1400);
        statsCardRT.anchoredPosition = new Vector2(0, 30);
        Image statsCardImg = statsCard.AddComponent<Image>();
        statsCardImg.color = ColBgCard;
        statsCardImg.raycastTarget = false;
        // Add outline for subtle border
        Outline statsCardOutline = statsCard.AddComponent<Outline>();
        statsCardOutline.effectColor = new Color(1f, 1f, 1f, 0.06f);
        statsCardOutline.effectDistance = new Vector2(1, 1);

        CreateTMPText(statsCard.transform, "StatsTitle", "STATISTICS", 48,
            TextAlignmentOptions.Center, new Vector2(0, 370));
        statsCard.transform.GetChild(0).GetComponent<TextMeshProUGUI>().fontStyle = FontStyles.Bold;

        // Stats rows
        CreateTMPText(statsCard.transform, "LabelTotalGames", "Games Played", 24,
            TextAlignmentOptions.Left, new Vector2(-300, 250)).GetComponent<TextMeshProUGUI>().color = ColTextSecondary;
        GameObject statsTotalGamesText = CreateTMPText(statsCard.transform, "StatsTotalGamesText", "0", 32,
            TextAlignmentOptions.Right, new Vector2(300, 250));

        CreateTMPText(statsCard.transform, "LabelHighScore", "High Score", 24,
            TextAlignmentOptions.Left, new Vector2(-300, 170)).GetComponent<TextMeshProUGUI>().color = ColTextSecondary;
        GameObject statsHighScoreText = CreateTMPText(statsCard.transform, "StatsHighScoreText", "0", 32,
            TextAlignmentOptions.Right, new Vector2(300, 170));

        CreateTMPText(statsCard.transform, "LabelTotalFeed", "Total Feed", 24,
            TextAlignmentOptions.Left, new Vector2(-300, 90)).GetComponent<TextMeshProUGUI>().color = ColTextSecondary;
        GameObject statsTotalFeedText = CreateTMPText(statsCard.transform, "StatsTotalFeedText", "0", 32,
            TextAlignmentOptions.Right, new Vector2(300, 90));

        CreateTMPText(statsCard.transform, "LabelTotalBlobs", "Total Blobs", 24,
            TextAlignmentOptions.Left, new Vector2(-300, 10)).GetComponent<TextMeshProUGUI>().color = ColTextSecondary;
        GameObject statsTotalBlobsText = CreateTMPText(statsCard.transform, "StatsTotalBlobsText", "0", 32,
            TextAlignmentOptions.Right, new Vector2(300, 10));

        CreateTMPText(statsCard.transform, "LabelMaxSize", "Max Size", 24,
            TextAlignmentOptions.Left, new Vector2(-300, -70)).GetComponent<TextMeshProUGUI>().color = ColTextSecondary;
        GameObject statsMaxSizeText = CreateTMPText(statsCard.transform, "StatsMaxSizeText", "x1.0", 32,
            TextAlignmentOptions.Right, new Vector2(300, -70));

        CreateTMPText(statsCard.transform, "LabelBestTA", "Best TA", 24,
            TextAlignmentOptions.Left, new Vector2(-300, -150)).GetComponent<TextMeshProUGUI>().color = ColTextSecondary;
        GameObject statsBestTATimeText = CreateTMPText(statsCard.transform, "StatsBestTATimeText", "--:--.0", 32,
            TextAlignmentOptions.Right, new Vector2(300, -150));

        // Leaderboard section
        CreateTMPText(statsCard.transform, "LabelLeaderboard", "TOP SCORES", 36,
            TextAlignmentOptions.Center, new Vector2(0, -240));
        statsCard.transform.GetChild(statsCard.transform.childCount - 1).GetComponent<TextMeshProUGUI>().fontStyle = FontStyles.Bold;

        GameObject statsLeaderboardText = CreateTMPText(statsCard.transform, "StatsLeaderboardText", "No scores yet", 22,
            TextAlignmentOptions.Left, new Vector2(0, -420));
        var lbRT = statsLeaderboardText.GetComponent<RectTransform>();
        lbRT.sizeDelta = new Vector2(700, 300);
        statsLeaderboardText.GetComponent<TextMeshProUGUI>().color = ColTextSecondary;

        // Stats back button
        GameObject statsBackBtn = CreateStyledButton(statsCard.transform, "StatsBackButton", "BACK",
            new Vector2(0, -620), new Vector2(270, 90), ColGray, 32f);

        // ====== Game Panel (HUD) ======
        GameObject gamePanel = CreatePanel(canvasObj.transform, "GamePanel");
        gamePanel.SetActive(false);

        // HUDController
        Blob3D.UI.HUDController hud = gamePanel.AddComponent<Blob3D.UI.HUDController>();

        // -- Top bar background — semi-transparent gradient feel --
        GameObject topBar = new GameObject("TopBar");
        topBar.transform.SetParent(gamePanel.transform, false);
        RectTransform topBarRT = topBar.AddComponent<RectTransform>();
        topBarRT.anchorMin = new Vector2(0, 1);
        topBarRT.anchorMax = new Vector2(1, 1);
        topBarRT.pivot = new Vector2(0.5f, 1);
        topBarRT.anchoredPosition = Vector2.zero;
        topBarRT.sizeDelta = new Vector2(0, 170);
        Image topBarImg = topBar.AddComponent<Image>();
        topBarImg.color = new Color(0.02f, 0.03f, 0.05f, 0.55f);
        topBarImg.raycastTarget = false;

        // Score (top-left) — larger, bold, with subtle shadow
        GameObject scoreText = CreateTMPText(gamePanel.transform, "ScoreText", "0",
            56, TextAlignmentOptions.TopLeft, Vector2.zero);
        var scoreTMP = scoreText.GetComponent<TextMeshProUGUI>();
        scoreTMP.fontStyle = FontStyles.Bold;
        scoreTMP.enableVertexGradient = true;
        scoreTMP.colorGradient = new VertexGradient(
            ColTextPrimary, ColTextPrimary,
            new Color(0.80f, 0.82f, 0.88f), new Color(0.80f, 0.82f, 0.88f));
        SetAnchoredPosition(scoreText, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(40, -20), new Vector2(300, 70));

        // Rank badge — small pill with colored background
        GameObject rankBadge = new GameObject("RankBadge");
        rankBadge.transform.SetParent(gamePanel.transform, false);
        RectTransform rankBadgeRT = rankBadge.AddComponent<RectTransform>();
        Image rankBadgeImg = rankBadge.AddComponent<Image>();
        rankBadgeImg.color = new Color(ColAccent.r, ColAccent.g, ColAccent.b, 0.25f);
        rankBadgeImg.raycastTarget = false;
        SetAnchoredPosition(rankBadge, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(40, -95), new Vector2(120, 32));

        GameObject rankText = CreateTMPText(rankBadge.transform, "RankText", "Tiny",
            20, TextAlignmentOptions.Center, Vector2.zero);
        rankText.GetComponent<TextMeshProUGUI>().color = ColAccent;
        rankText.GetComponent<TextMeshProUGUI>().fontStyle = FontStyles.Bold;
        rankText.GetComponent<RectTransform>().anchorMin = Vector2.zero;
        rankText.GetComponent<RectTransform>().anchorMax = Vector2.one;
        rankText.GetComponent<RectTransform>().offsetMin = Vector2.zero;
        rankText.GetComponent<RectTransform>().offsetMax = Vector2.zero;

        // Size
        GameObject sizeText = CreateTMPText(gamePanel.transform, "SizeText", "x1.0",
            22, TextAlignmentOptions.TopLeft, Vector2.zero);
        sizeText.GetComponent<TextMeshProUGUI>().color = ColTextMuted;
        SetAnchoredPosition(sizeText, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(170, -95), new Vector2(150, 32));

        // Timer (top-right) — mono-style treatment
        GameObject timerText = CreateTMPText(gamePanel.transform, "TimerText", "3:00",
            52, TextAlignmentOptions.TopRight, Vector2.zero);
        var timerTMP = timerText.GetComponent<TextMeshProUGUI>();
        timerTMP.fontStyle = FontStyles.Bold;
        timerTMP.characterSpacing = 4f;
        SetAnchoredPosition(timerText, new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-40, -20), new Vector2(200, 65));

        // Pause Button — subtle, circular feel
        GameObject pauseBtn = CreateStyledButton(gamePanel.transform, "PauseButton", "||",
            Vector2.zero, new Vector2(60, 60), new Color(0.20f, 0.22f, 0.28f, 0.60f), 24f);
        SetAnchoredPosition(pauseBtn, new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-40, -95), new Vector2(60, 60));

        // Joystick (bottom-left, above minimap area)
        GameObject joystickArea = CreateStyledJoystick(gamePanel.transform, canvas);

        // Dash Button (bottom-right) — circular with icon feel
        GameObject dashBtn = CreateStyledButton(gamePanel.transform, "DashButton", "DASH",
            Vector2.zero, new Vector2(120, 120), ColOrange, 22f);
        SetAnchoredPosition(dashBtn, new Vector2(1, 0), new Vector2(1, 0), new Vector2(1, 0), new Vector2(-60, 100), new Vector2(120, 120));

        // PowerUp Icons — centered top
        GameObject speedIcon = CreateStyledPowerUpIcon(gamePanel.transform, "SpeedBoostIcon", ColAccent, 0);
        GameObject shieldIcon = CreateStyledPowerUpIcon(gamePanel.transform, "ShieldIcon", new Color(1f, 0.85f, 0.15f), 1);
        GameObject magnetIcon = CreateStyledPowerUpIcon(gamePanel.transform, "MagnetIcon", new Color(1f, 0.25f, 0.75f), 2);
        GameObject ghostIcon = CreateStyledPowerUpIcon(gamePanel.transform, "GhostIcon", new Color(0.85f, 0.85f, 1f, 0.8f), 3);

        // HUD references
        SerializedObject hudSO = new SerializedObject(hud);
        hudSO.FindProperty("scoreText").objectReferenceValue = scoreText.GetComponent<TextMeshProUGUI>();
        hudSO.FindProperty("timerText").objectReferenceValue = timerText.GetComponent<TextMeshProUGUI>();
        hudSO.FindProperty("rankText").objectReferenceValue = rankText.GetComponentInChildren<TextMeshProUGUI>();
        hudSO.FindProperty("sizeText").objectReferenceValue = sizeText.GetComponent<TextMeshProUGUI>();
        hudSO.FindProperty("speedBoostIcon").objectReferenceValue = speedIcon;
        hudSO.FindProperty("shieldIcon").objectReferenceValue = shieldIcon;
        hudSO.FindProperty("magnetIcon").objectReferenceValue = magnetIcon;
        hudSO.FindProperty("ghostIcon").objectReferenceValue = ghostIcon;
        hudSO.ApplyModifiedProperties();

        // LeaderboardController
        gamePanel.AddComponent<Blob3D.UI.LeaderboardController>();

        // ====== Result Panel ======
        GameObject resultPanel = CreatePanel(canvasObj.transform, "ResultPanel");
        resultPanel.SetActive(false);
        Image resultBg = resultPanel.GetComponent<Image>();
        resultBg.color = ColBgDark;
        resultBg.raycastTarget = true;

        // Result card with subtle border
        GameObject resultCard = new GameObject("ResultCard");
        resultCard.transform.SetParent(resultPanel.transform, false);
        RectTransform cardRT = resultCard.AddComponent<RectTransform>();
        cardRT.anchorMin = new Vector2(0.5f, 0.5f);
        cardRT.anchorMax = new Vector2(0.5f, 0.5f);
        cardRT.sizeDelta = new Vector2(800, 1050);
        cardRT.anchoredPosition = new Vector2(0, 20);
        Image cardImg = resultCard.AddComponent<Image>();
        cardImg.color = ColBgCard;
        cardImg.raycastTarget = false;
        Outline resultCardOutline = resultCard.AddComponent<Outline>();
        resultCardOutline.effectColor = new Color(1f, 1f, 1f, 0.06f);
        resultCardOutline.effectDistance = new Vector2(1, 1);

        CreateTMPText(resultCard.transform, "ResultTitle", "GAME OVER", 42,
            TextAlignmentOptions.Center, new Vector2(0, 400));
        resultCard.transform.GetChild(0).GetComponent<TextMeshProUGUI>().fontStyle = FontStyles.Bold;
        resultCard.transform.GetChild(0).GetComponent<TextMeshProUGUI>().color = ColTextSecondary;

        // Score — huge font with accent color
        GameObject resultScoreText = CreateTMPText(resultCard.transform, "ResultScoreText", "0",
            90, TextAlignmentOptions.Center, new Vector2(0, 280));
        var rScoreTMP = resultScoreText.GetComponent<TextMeshProUGUI>();
        rScoreTMP.color = ColAccent;
        rScoreTMP.fontStyle = FontStyles.Bold;
        rScoreTMP.enableVertexGradient = true;
        rScoreTMP.colorGradient = new VertexGradient(
            ColAccent, ColAccent,
            new Color(0.15f, 0.60f, 0.70f), new Color(0.15f, 0.60f, 0.70f));

        // Rank
        GameObject resultRankText = CreateTMPText(resultCard.transform, "ResultRankText", "Tiny",
            32, TextAlignmentOptions.Center, new Vector2(0, 180));
        resultRankText.GetComponent<TextMeshProUGUI>().color = ColTextSecondary;

        // Divider line — thin 1px line
        GameObject divider = new GameObject("Divider");
        divider.transform.SetParent(resultCard.transform, false);
        RectTransform divRT = divider.AddComponent<RectTransform>();
        divRT.anchoredPosition = new Vector2(0, 130);
        divRT.sizeDelta = new Vector2(650, 1);
        Image divImg = divider.AddComponent<Image>();
        divImg.color = new Color(1, 1, 1, 0.08f);
        divImg.raycastTarget = false;

        // Stats in clean 3-column grid
        GameObject resultFeedText = CreateTMPText(resultCard.transform, "ResultFeedText", "0",
            36, TextAlignmentOptions.Center, new Vector2(-220, 60));
        resultFeedText.GetComponent<TextMeshProUGUI>().fontStyle = FontStyles.Bold;
        CreateTMPText(resultCard.transform, "FeedLabel", "FEED", 18,
            TextAlignmentOptions.Center, new Vector2(-220, 20)).GetComponent<TextMeshProUGUI>().color = ColTextMuted;

        GameObject resultBlobsText = CreateTMPText(resultCard.transform, "ResultBlobsText", "0",
            36, TextAlignmentOptions.Center, new Vector2(0, 60));
        resultBlobsText.GetComponent<TextMeshProUGUI>().fontStyle = FontStyles.Bold;
        CreateTMPText(resultCard.transform, "BlobsLabel", "BLOBS", 18,
            TextAlignmentOptions.Center, new Vector2(0, 20)).GetComponent<TextMeshProUGUI>().color = ColTextMuted;

        GameObject resultMaxSizeText = CreateTMPText(resultCard.transform, "ResultMaxSizeText", "x1.0",
            36, TextAlignmentOptions.Center, new Vector2(220, 60));
        resultMaxSizeText.GetComponent<TextMeshProUGUI>().fontStyle = FontStyles.Bold;
        CreateTMPText(resultCard.transform, "MaxLabel", "MAX SIZE", 18,
            TextAlignmentOptions.Center, new Vector2(220, 20)).GetComponent<TextMeshProUGUI>().color = ColTextMuted;

        // New high score badge — animation-ready gold
        GameObject newHighBadge = CreateTMPText(resultCard.transform, "NewHighScoreBadge", "NEW HIGH SCORE!",
            36, TextAlignmentOptions.Center, new Vector2(0, -40));
        var nhTMP = newHighBadge.GetComponent<TextMeshProUGUI>();
        nhTMP.color = ColGold;
        nhTMP.fontStyle = FontStyles.Bold;
        nhTMP.characterSpacing = 6f;
        newHighBadge.SetActive(false);

        // Leaderboard rank text
        GameObject resultLBRankText = CreateTMPText(resultCard.transform, "ResultLeaderboardRank", "",
            24, TextAlignmentOptions.Center, new Vector2(0, -80));
        resultLBRankText.GetComponent<TextMeshProUGUI>().color = ColAccent;
        resultLBRankText.GetComponent<TextMeshProUGUI>().fontStyle = FontStyles.Bold;
        resultLBRankText.SetActive(false);

        // Coins earned
        GameObject resultCoinsText = CreateTMPText(resultCard.transform, "ResultCoinsEarned", "+0 COINS EARNED",
            26, TextAlignmentOptions.Center, new Vector2(0, -110));
        var rcTMP = resultCoinsText.GetComponent<TextMeshProUGUI>();
        rcTMP.color = ColGold;
        rcTMP.fontStyle = FontStyles.Bold;

        // Divider before buttons
        GameObject divider2 = new GameObject("Divider2");
        divider2.transform.SetParent(resultCard.transform, false);
        RectTransform div2RT = divider2.AddComponent<RectTransform>();
        div2RT.anchoredPosition = new Vector2(0, -160);
        div2RT.sizeDelta = new Vector2(650, 1);
        Image div2Img = divider2.AddComponent<Image>();
        div2Img.color = new Color(1, 1, 1, 0.08f);
        div2Img.raycastTarget = false;

        // Buttons — well spaced with rounded feel
        GameObject retryBtn = CreateStyledButton(resultCard.transform, "RetryButton", "RETRY",
            new Vector2(-170, -260), new Vector2(280, 90), ColGreen, 32f);

        GameObject homeBtn = CreateStyledButton(resultCard.transform, "HomeButton", "HOME",
            new Vector2(170, -260), new Vector2(280, 90), ColGray, 32f);

        // ====== Pause Panel ======
        GameObject pausePanel = CreatePanel(canvasObj.transform, "PausePanel");
        pausePanel.SetActive(false);
        Image pauseBgImg = pausePanel.GetComponent<Image>();
        pauseBgImg.color = new Color(0.02f, 0.03f, 0.05f, 0.85f);
        pauseBgImg.raycastTarget = true;

        // Pause card
        GameObject pauseCard = new GameObject("PauseCard");
        pauseCard.transform.SetParent(pausePanel.transform, false);
        RectTransform pauseCardRT = pauseCard.AddComponent<RectTransform>();
        pauseCardRT.anchorMin = new Vector2(0.5f, 0.5f);
        pauseCardRT.anchorMax = new Vector2(0.5f, 0.5f);
        pauseCardRT.sizeDelta = new Vector2(500, 400);
        pauseCardRT.anchoredPosition = Vector2.zero;
        Image pauseCardImg = pauseCard.AddComponent<Image>();
        pauseCardImg.color = ColBgCard;
        pauseCardImg.raycastTarget = false;
        Outline pauseCardOutline = pauseCard.AddComponent<Outline>();
        pauseCardOutline.effectColor = new Color(1f, 1f, 1f, 0.06f);
        pauseCardOutline.effectDistance = new Vector2(1, 1);

        CreateTMPText(pauseCard.transform, "PauseTitle", "PAUSED", 48,
            TextAlignmentOptions.Center, new Vector2(0, 130)).GetComponent<TextMeshProUGUI>().fontStyle = FontStyles.Bold;

        GameObject resumeBtn = CreateStyledButton(pauseCard.transform, "ResumeButton", "RESUME",
            new Vector2(0, 0), new Vector2(320, 85), ColGreen, 30f);

        GameObject pauseHomeBtn = CreateStyledButton(pauseCard.transform, "PauseHomeButton", "HOME",
            new Vector2(0, -120), new Vector2(320, 85), ColGray, 30f);

        // ====== Settings Panel ======
        GameObject settingsPanel = CreatePanel(canvasObj.transform, "SettingsPanel");
        settingsPanel.SetActive(false);
        Image settingsBg = settingsPanel.GetComponent<Image>();
        settingsBg.color = ColBgDark;
        settingsBg.raycastTarget = true;

        // Settings card — clean layout
        GameObject settingsCard = new GameObject("SettingsCard");
        settingsCard.transform.SetParent(settingsPanel.transform, false);
        RectTransform sCardRT = settingsCard.AddComponent<RectTransform>();
        sCardRT.anchorMin = new Vector2(0.5f, 0.5f);
        sCardRT.anchorMax = new Vector2(0.5f, 0.5f);
        sCardRT.sizeDelta = new Vector2(700, 850);
        sCardRT.anchoredPosition = new Vector2(0, 30);
        Image sCardImg = settingsCard.AddComponent<Image>();
        sCardImg.color = ColBgCard;
        sCardImg.raycastTarget = false;
        Outline settingsCardOutline = settingsCard.AddComponent<Outline>();
        settingsCardOutline.effectColor = new Color(1f, 1f, 1f, 0.06f);
        settingsCardOutline.effectDistance = new Vector2(1, 1);

        CreateTMPText(settingsCard.transform, "SettingsTitle", "SETTINGS", 42,
            TextAlignmentOptions.Center, new Vector2(0, 330));
        settingsCard.transform.GetChild(0).GetComponent<TextMeshProUGUI>().fontStyle = FontStyles.Bold;

        // Thin divider under title
        GameObject settingsTitleDiv = new GameObject("SettingsTitleDivider");
        settingsTitleDiv.transform.SetParent(settingsCard.transform, false);
        RectTransform stdRT = settingsTitleDiv.AddComponent<RectTransform>();
        stdRT.anchoredPosition = new Vector2(0, 280);
        stdRT.sizeDelta = new Vector2(580, 1);
        Image stdImg = settingsTitleDiv.AddComponent<Image>();
        stdImg.color = new Color(1, 1, 1, 0.06f);
        stdImg.raycastTarget = false;

        // BGM Volume — label left, slider below
        CreateTMPText(settingsCard.transform, "BGMLabel", "BGM Volume", 22,
            TextAlignmentOptions.Left, new Vector2(-240, 220)).GetComponent<TextMeshProUGUI>().color = ColTextSecondary;
        GameObject bgmSliderObj = CreateStyledSlider(settingsCard.transform, "BGMSlider",
            new Vector2(0, 170), new Vector2(520, 36), 0.3f);

        // Thin divider
        GameObject settingsDiv1 = new GameObject("SettingsDiv1");
        settingsDiv1.transform.SetParent(settingsCard.transform, false);
        RectTransform sd1RT = settingsDiv1.AddComponent<RectTransform>();
        sd1RT.anchoredPosition = new Vector2(0, 128);
        sd1RT.sizeDelta = new Vector2(580, 1);
        Image sd1Img = settingsDiv1.AddComponent<Image>();
        sd1Img.color = new Color(1, 1, 1, 0.04f);
        sd1Img.raycastTarget = false;

        // SE Volume
        CreateTMPText(settingsCard.transform, "SELabel", "SE Volume", 22,
            TextAlignmentOptions.Left, new Vector2(-240, 90)).GetComponent<TextMeshProUGUI>().color = ColTextSecondary;
        GameObject seSliderObj = CreateStyledSlider(settingsCard.transform, "SESlider",
            new Vector2(0, 40), new Vector2(520, 36), 0.6f);

        // Thin divider
        GameObject settingsDiv2 = new GameObject("SettingsDiv2");
        settingsDiv2.transform.SetParent(settingsCard.transform, false);
        RectTransform sd2RT = settingsDiv2.AddComponent<RectTransform>();
        sd2RT.anchoredPosition = new Vector2(0, -2);
        sd2RT.sizeDelta = new Vector2(580, 1);
        Image sd2Img = settingsDiv2.AddComponent<Image>();
        sd2Img.color = new Color(1, 1, 1, 0.04f);
        sd2Img.raycastTarget = false;

        // Sensitivity
        CreateTMPText(settingsCard.transform, "SensLabel", "Sensitivity", 22,
            TextAlignmentOptions.Left, new Vector2(-240, -40)).GetComponent<TextMeshProUGUI>().color = ColTextSecondary;
        GameObject sensSliderObj = CreateStyledSlider(settingsCard.transform, "SensitivitySlider",
            new Vector2(0, -90), new Vector2(520, 36), 0.25f);

        // Thin divider
        GameObject settingsDiv3 = new GameObject("SettingsDiv3");
        settingsDiv3.transform.SetParent(settingsCard.transform, false);
        RectTransform sd3RT = settingsDiv3.AddComponent<RectTransform>();
        sd3RT.anchoredPosition = new Vector2(0, -132);
        sd3RT.sizeDelta = new Vector2(580, 1);
        Image sd3Img = settingsDiv3.AddComponent<Image>();
        sd3Img.color = new Color(1, 1, 1, 0.04f);
        sd3Img.raycastTarget = false;

        // Language toggle
        CreateTMPText(settingsCard.transform, "LangLabel", "Language", 22,
            TextAlignmentOptions.Left, new Vector2(-240, -170)).GetComponent<TextMeshProUGUI>().color = ColTextSecondary;
        GameObject langToggleBtn = CreateStyledButton(settingsCard.transform, "LanguageToggleButton", "JP / en",
            new Vector2(100, -170), new Vector2(180, 48), new Color(0.20f, 0.45f, 0.75f, 0.85f), 22f);

        // Back button at bottom, not cramped
        GameObject settingsBackBtn = CreateStyledButton(settingsCard.transform, "SettingsBackButton", "BACK",
            new Vector2(0, -340), new Vector2(270, 80), ColGray, 28f);

        // Settings button on title screen — bottom-right, consistent with layout
        GameObject settingsBtn = CreateStyledButton(titlePanel.transform, "SettingsButton", "SETTINGS",
            Vector2.zero, new Vector2(160, 50), new Color(0.20f, 0.22f, 0.28f, 0.75f), 18f);
        SetAnchoredPosition(settingsBtn, new Vector2(1, 0), new Vector2(1, 0), new Vector2(1, 0), new Vector2(-30, 30), new Vector2(160, 50));

        // ====== Minimap on Game Panel ======
        GameObject minimapObj = new GameObject("Minimap");
        minimapObj.transform.SetParent(gamePanel.transform, false);
        minimapObj.AddComponent<Blob3D.UI.MinimapController>();

        // ====== Shop Panel ======
        GameObject shopPanel = CreatePanel(canvasObj.transform, "ShopPanel");
        shopPanel.SetActive(false);
        Image shopBgImg = shopPanel.GetComponent<Image>();
        shopBgImg.color = ColBgDark;
        shopBgImg.raycastTarget = true;

        // Shop card with subtle border
        GameObject shopCard = new GameObject("ShopCard");
        shopCard.transform.SetParent(shopPanel.transform, false);
        RectTransform shopCardRT = shopCard.AddComponent<RectTransform>();
        shopCardRT.anchorMin = new Vector2(0.5f, 0.5f);
        shopCardRT.anchorMax = new Vector2(0.5f, 0.5f);
        shopCardRT.sizeDelta = new Vector2(800, 900);
        shopCardRT.anchoredPosition = new Vector2(0, 30);
        Image shopCardImg = shopCard.AddComponent<Image>();
        shopCardImg.color = ColBgCard;
        shopCardImg.raycastTarget = false;
        Outline shopCardOutline = shopCard.AddComponent<Outline>();
        shopCardOutline.effectColor = new Color(1f, 1f, 1f, 0.06f);
        shopCardOutline.effectDistance = new Vector2(1, 1);

        CreateTMPText(shopCard.transform, "ShopTitle", "SHOP", 48,
            TextAlignmentOptions.Center, new Vector2(0, 380));
        shopCard.transform.GetChild(0).GetComponent<TextMeshProUGUI>().fontStyle = FontStyles.Bold;

        // Coin display in shop
        GameObject shopCoinText = CreateTMPText(shopCard.transform, "ShopCoinDisplay", "COINS: 0",
            32, TextAlignmentOptions.Center, new Vector2(0, 310));
        var shopCoinTMP = shopCoinText.GetComponent<TextMeshProUGUI>();
        shopCoinTMP.color = ColGold;
        shopCoinTMP.fontStyle = FontStyles.Bold;

        // Scroll content area for shop items
        GameObject shopContent = new GameObject("ShopContent");
        shopContent.transform.SetParent(shopCard.transform, false);
        RectTransform shopContentRT = shopContent.AddComponent<RectTransform>();
        shopContentRT.anchorMin = new Vector2(0.5f, 0.5f);
        shopContentRT.anchorMax = new Vector2(0.5f, 0.5f);
        shopContentRT.pivot = new Vector2(0.5f, 1f);
        shopContentRT.anchoredPosition = new Vector2(0, 260);
        shopContentRT.sizeDelta = new Vector2(700, 600);

        // ShopController component
        Blob3D.UI.ShopController shopCtrl = shopPanel.AddComponent<Blob3D.UI.ShopController>();
        SerializedObject shopSO = new SerializedObject(shopCtrl);
        shopSO.FindProperty("coinDisplay").objectReferenceValue = shopCoinTMP;
        shopSO.FindProperty("contentParent").objectReferenceValue = shopContent.transform;
        shopSO.ApplyModifiedProperties();

        // Shop back button
        GameObject shopBackBtn = CreateStyledButton(shopCard.transform, "ShopBackButton", "BACK",
            new Vector2(0, -380), new Vector2(270, 80), ColGray, 28f);


        // ====== Daily Reward Panel ======
        GameObject dailyRewardPanel = CreatePanel(canvasObj.transform, "DailyRewardPanel");
        dailyRewardPanel.SetActive(false);
        Image dailyRewardBg = dailyRewardPanel.GetComponent<Image>();
        dailyRewardBg.color = ColBgDark;
        dailyRewardBg.raycastTarget = true;

        GameObject dailyRewardCard = new GameObject("DailyRewardCard");
        dailyRewardCard.transform.SetParent(dailyRewardPanel.transform, false);
        RectTransform drCardRT = dailyRewardCard.AddComponent<RectTransform>();
        drCardRT.anchorMin = new Vector2(0.5f, 0.5f);
        drCardRT.anchorMax = new Vector2(0.5f, 0.5f);
        drCardRT.sizeDelta = new Vector2(500, 380);
        drCardRT.anchoredPosition = Vector2.zero;
        Image drCardImg = dailyRewardCard.AddComponent<Image>();
        drCardImg.color = ColBgCard;
        drCardImg.raycastTarget = false;
        Outline drCardOutline = dailyRewardCard.AddComponent<Outline>();
        drCardOutline.effectColor = new Color(1f, 1f, 1f, 0.06f);
        drCardOutline.effectDistance = new Vector2(1, 1);

        CreateTMPText(dailyRewardCard.transform, "DailyRewardTitle", "DAILY BONUS", 42,
            TextAlignmentOptions.Center, new Vector2(0, 130)).GetComponent<TextMeshProUGUI>().fontStyle = FontStyles.Bold;

        GameObject dailyRewardText = CreateTMPText(dailyRewardCard.transform, "DailyRewardText", "Day 1\n+10 COINS", 30,
            TextAlignmentOptions.Center, new Vector2(0, 20));

        GameObject dailyRewardClaimBtn = CreateStyledButton(dailyRewardCard.transform, "DailyRewardClaimButton", "CLAIM",
            new Vector2(0, -80), new Vector2(320, 85), ColGreen, 30f);

        GameObject dailyRewardCloseBtn = CreateStyledButton(dailyRewardCard.transform, "DailyRewardCloseButton", "CLOSE",
            new Vector2(0, -170), new Vector2(200, 60), ColGray, 22f);

        // ====== Loading Panel ======
        GameObject loadingPanel = CreatePanel(canvasObj.transform, "LoadingPanel");
        loadingPanel.SetActive(false);
        Image loadingBg = loadingPanel.GetComponent<Image>();
        loadingBg.color = new Color(0.02f, 0.03f, 0.05f, 0.97f);
        loadingBg.raycastTarget = true;

        GameObject loadingText = CreateTMPText(loadingPanel.transform, "LoadingText", "LOADING...",
            42, TextAlignmentOptions.Center, Vector2.zero);
        loadingText.GetComponent<TextMeshProUGUI>().fontStyle = FontStyles.Bold;
        loadingText.GetComponent<TextMeshProUGUI>().color = ColAccent;
        loadingText.GetComponent<TextMeshProUGUI>().characterSpacing = 4f;

        // ====== UIManager references ======
        SerializedObject uiSO = new SerializedObject(uiMgr);
        uiSO.FindProperty("titlePanel").objectReferenceValue = titlePanel;
        uiSO.FindProperty("gamePanel").objectReferenceValue = gamePanel;
        uiSO.FindProperty("resultPanel").objectReferenceValue = resultPanel;
        uiSO.FindProperty("pausePanel").objectReferenceValue = pausePanel;
        uiSO.FindProperty("settingsPanel").objectReferenceValue = settingsPanel;
        uiSO.FindProperty("shopPanel").objectReferenceValue = shopPanel;

        uiSO.FindProperty("playButton").objectReferenceValue = playBtn.GetComponent<Button>();
        uiSO.FindProperty("skinButton").objectReferenceValue = skinBtn.GetComponent<Button>();
        uiSO.FindProperty("highScoreLabel").objectReferenceValue = highScoreLabel.GetComponent<TextMeshProUGUI>();
        uiSO.FindProperty("settingsButton").objectReferenceValue = settingsBtn.GetComponent<Button>();
        uiSO.FindProperty("statsButton").objectReferenceValue = statsBtn.GetComponent<Button>();
        uiSO.FindProperty("modeButton").objectReferenceValue = modeBtn.GetComponent<Button>();
        uiSO.FindProperty("modeLabelText").objectReferenceValue = modeLabelTMP;
        uiSO.FindProperty("shopButton").objectReferenceValue = shopBtn.GetComponent<Button>();
        uiSO.FindProperty("shopBackButton").objectReferenceValue = shopBackBtn.GetComponent<Button>();
        uiSO.FindProperty("titleCoinDisplay").objectReferenceValue = titleCoinTMP;
        uiSO.FindProperty("shopContentParent").objectReferenceValue = shopContent.transform;

        uiSO.FindProperty("statsPanel").objectReferenceValue = statsPanel;
        uiSO.FindProperty("statsBackButton").objectReferenceValue = statsBackBtn.GetComponent<Button>();
        uiSO.FindProperty("statsTotalGamesText").objectReferenceValue = statsTotalGamesText.GetComponent<TextMeshProUGUI>();
        uiSO.FindProperty("statsHighScoreText").objectReferenceValue = statsHighScoreText.GetComponent<TextMeshProUGUI>();
        uiSO.FindProperty("statsTotalFeedText").objectReferenceValue = statsTotalFeedText.GetComponent<TextMeshProUGUI>();
        uiSO.FindProperty("statsTotalBlobsText").objectReferenceValue = statsTotalBlobsText.GetComponent<TextMeshProUGUI>();
        uiSO.FindProperty("statsMaxSizeText").objectReferenceValue = statsMaxSizeText.GetComponent<TextMeshProUGUI>();
        uiSO.FindProperty("statsBestTATimeText").objectReferenceValue = statsBestTATimeText.GetComponent<TextMeshProUGUI>();

        uiSO.FindProperty("resultScoreText").objectReferenceValue = resultScoreText.GetComponent<TextMeshProUGUI>();
        uiSO.FindProperty("resultRankText").objectReferenceValue = resultRankText.GetComponent<TextMeshProUGUI>();
        uiSO.FindProperty("resultFeedText").objectReferenceValue = resultFeedText.GetComponent<TextMeshProUGUI>();
        uiSO.FindProperty("resultBlobsText").objectReferenceValue = resultBlobsText.GetComponent<TextMeshProUGUI>();
        uiSO.FindProperty("resultMaxSizeText").objectReferenceValue = resultMaxSizeText.GetComponent<TextMeshProUGUI>();
        uiSO.FindProperty("newHighScoreBadge").objectReferenceValue = newHighBadge;
        uiSO.FindProperty("resultCoinsEarnedText").objectReferenceValue = resultCoinsText.GetComponent<TextMeshProUGUI>();

        uiSO.FindProperty("retryButton").objectReferenceValue = retryBtn.GetComponent<Button>();
        uiSO.FindProperty("homeButton").objectReferenceValue = homeBtn.GetComponent<Button>();

        uiSO.FindProperty("pauseButton").objectReferenceValue = pauseBtn.GetComponent<Button>();
        uiSO.FindProperty("dashButton").objectReferenceValue = dashBtn.GetComponent<Button>();

        uiSO.FindProperty("resumeButton").objectReferenceValue = resumeBtn.GetComponent<Button>();
        uiSO.FindProperty("pauseHomeButton").objectReferenceValue = pauseHomeBtn.GetComponent<Button>();

        uiSO.FindProperty("settingsBackButton").objectReferenceValue = settingsBackBtn.GetComponent<Button>();
        uiSO.FindProperty("bgmSlider").objectReferenceValue = bgmSliderObj.GetComponent<Slider>();
        uiSO.FindProperty("seSlider").objectReferenceValue = seSliderObj.GetComponent<Slider>();
        uiSO.FindProperty("sensitivitySlider").objectReferenceValue = sensSliderObj.GetComponent<Slider>();
        uiSO.FindProperty("languageToggleButton").objectReferenceValue = langToggleBtn.GetComponent<Button>();
        uiSO.FindProperty("languageToggleLabel").objectReferenceValue = langToggleBtn.GetComponentInChildren<TextMeshProUGUI>();

        uiSO.FindProperty("loadingPanel").objectReferenceValue = loadingPanel;
        uiSO.FindProperty("statsLeaderboardText").objectReferenceValue = statsLeaderboardText.GetComponent<TextMeshProUGUI>();
        uiSO.FindProperty("resultLeaderboardRankText").objectReferenceValue = resultLBRankText.GetComponent<TextMeshProUGUI>();

        uiSO.FindProperty("dailyRewardPanel").objectReferenceValue = dailyRewardPanel;
        uiSO.FindProperty("dailyRewardText").objectReferenceValue = dailyRewardText.GetComponent<TextMeshProUGUI>();
        uiSO.FindProperty("dailyRewardClaimButton").objectReferenceValue = dailyRewardClaimBtn.GetComponent<Button>();
        uiSO.FindProperty("dailyRewardCloseButton").objectReferenceValue = dailyRewardCloseBtn.GetComponent<Button>();

        uiSO.ApplyModifiedProperties();
    }

    // ---------- UI Helpers ----------

    private void SetAnchoredPosition(GameObject obj, Vector2 anchorMin, Vector2 anchorMax,
        Vector2 pivot, Vector2 position, Vector2 size)
    {
        RectTransform rt = obj.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = pivot;
        rt.anchoredPosition = position;
        rt.sizeDelta = size;
    }

    private GameObject CreatePanel(Transform parent, string name)
    {
        GameObject panel = new GameObject(name);
        panel.transform.SetParent(parent, false);
        RectTransform rt = panel.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        Image img = panel.AddComponent<Image>();
        img.color = new Color(0, 0, 0, 0);
        img.raycastTarget = false;
        return panel;
    }

    private GameObject CreateTMPText(Transform parent, string name, string text,
        float fontSize, TextAlignmentOptions alignment, Vector2 position)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        RectTransform rt = obj.AddComponent<RectTransform>();
        rt.anchoredPosition = position;
        rt.sizeDelta = new Vector2(600, fontSize + 24);

        TextMeshProUGUI tmp = obj.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.alignment = alignment;
        tmp.color = ColTextPrimary;
        tmp.raycastTarget = false;
        if (japaneseFont != null)
            tmp.font = japaneseFont;
        return obj;
    }

    private GameObject CreateStyledButton(Transform parent, string name, string label,
        Vector2 position, Vector2 size, Color color, float fontSize)
    {
        GameObject btnObj = new GameObject(name);
        btnObj.transform.SetParent(parent, false);
        RectTransform rt = btnObj.AddComponent<RectTransform>();
        rt.anchoredPosition = position;
        rt.sizeDelta = size;

        Image img = btnObj.AddComponent<Image>();
        img.color = color;
        img.type = Image.Type.Sliced;

        Button btn = btnObj.AddComponent<Button>();
        btn.targetGraphic = img;

        // Hover/press color transitions
        var colors = btn.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(0.9f, 0.9f, 0.9f);
        colors.pressedColor = new Color(0.7f, 0.7f, 0.7f);
        colors.fadeDuration = 0.08f;
        btn.colors = colors;

        // Button label
        GameObject textObj = new GameObject("Label");
        textObj.transform.SetParent(btnObj.transform, false);
        RectTransform textRT = textObj.AddComponent<RectTransform>();
        textRT.anchorMin = Vector2.zero;
        textRT.anchorMax = Vector2.one;
        textRT.offsetMin = Vector2.zero;
        textRT.offsetMax = Vector2.zero;

        TextMeshProUGUI tmp = textObj.AddComponent<TextMeshProUGUI>();
        tmp.text = label;
        tmp.fontSize = fontSize;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;
        tmp.fontStyle = FontStyles.Bold;
        tmp.characterSpacing = 3f;
        tmp.raycastTarget = false;
        if (japaneseFont != null)
            tmp.font = japaneseFont;

        return btnObj;
    }

    private GameObject CreateStyledJoystick(Transform parent, Canvas canvas)
    {
        // Joystick area
        GameObject area = new GameObject("JoystickArea");
        area.transform.SetParent(parent, false);
        RectTransform areaRT = area.AddComponent<RectTransform>();
        areaRT.anchorMin = new Vector2(0, 0);
        areaRT.anchorMax = new Vector2(0, 0);
        areaRT.pivot = new Vector2(0, 0);
        areaRT.anchoredPosition = new Vector2(30, 190);
        areaRT.sizeDelta = new Vector2(280, 280);

        // Outer ring
        GameObject bg = new GameObject("Background");
        bg.transform.SetParent(area.transform, false);
        RectTransform bgRT = bg.AddComponent<RectTransform>();
        bgRT.anchorMin = new Vector2(0.5f, 0.5f);
        bgRT.anchorMax = new Vector2(0.5f, 0.5f);
        bgRT.sizeDelta = new Vector2(260, 260);
        Image bgImg = bg.AddComponent<Image>();
        bgImg.color = new Color(1, 1, 1, 0.10f);

        // Inner handle
        GameObject handle = new GameObject("Handle");
        handle.transform.SetParent(bg.transform, false);
        RectTransform handleRT = handle.AddComponent<RectTransform>();
        handleRT.sizeDelta = new Vector2(90, 90);
        Image handleImg = handle.AddComponent<Image>();
        handleImg.color = new Color(1, 1, 1, 0.35f);

        // VirtualJoystick
        Blob3D.UI.VirtualJoystick joystick = area.AddComponent<Blob3D.UI.VirtualJoystick>();
        SerializedObject jsSO = new SerializedObject(joystick);
        jsSO.FindProperty("background").objectReferenceValue = bgRT;
        jsSO.FindProperty("handle").objectReferenceValue = handleRT;
        jsSO.FindProperty("canvas").objectReferenceValue = canvas;
        jsSO.ApplyModifiedProperties();

        return area;
    }

    private GameObject CreateStyledPowerUpIcon(Transform parent, string name, Color color, int index)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        RectTransform rt = obj.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 1);
        rt.anchorMax = new Vector2(0.5f, 1);
        rt.pivot = new Vector2(0.5f, 1);
        rt.anchoredPosition = new Vector2(-100 + index * 65, -110);
        rt.sizeDelta = new Vector2(50, 50);

        Image img = obj.AddComponent<Image>();
        img.color = color;

        obj.SetActive(false);
        return obj;
    }

    private GameObject CreateStyledSlider(Transform parent, string name,
        Vector2 position, Vector2 size, float defaultValue)
    {
        GameObject sliderObj = new GameObject(name);
        sliderObj.transform.SetParent(parent, false);
        RectTransform rt = sliderObj.AddComponent<RectTransform>();
        rt.anchoredPosition = position;
        rt.sizeDelta = size;

        // Background
        GameObject bgObj = new GameObject("Background");
        bgObj.transform.SetParent(sliderObj.transform, false);
        RectTransform bgRT = bgObj.AddComponent<RectTransform>();
        bgRT.anchorMin = new Vector2(0, 0.25f);
        bgRT.anchorMax = new Vector2(1, 0.75f);
        bgRT.offsetMin = Vector2.zero;
        bgRT.offsetMax = Vector2.zero;
        Image bgImg = bgObj.AddComponent<Image>();
        bgImg.color = new Color(0.15f, 0.17f, 0.22f, 0.7f);

        // Fill area
        GameObject fillArea = new GameObject("Fill Area");
        fillArea.transform.SetParent(sliderObj.transform, false);
        RectTransform fillAreaRT = fillArea.AddComponent<RectTransform>();
        fillAreaRT.anchorMin = new Vector2(0, 0.25f);
        fillAreaRT.anchorMax = new Vector2(1, 0.75f);
        fillAreaRT.offsetMin = new Vector2(5, 0);
        fillAreaRT.offsetMax = new Vector2(-5, 0);

        GameObject fill = new GameObject("Fill");
        fill.transform.SetParent(fillArea.transform, false);
        RectTransform fillRT = fill.AddComponent<RectTransform>();
        fillRT.anchorMin = Vector2.zero;
        fillRT.anchorMax = new Vector2(0, 1);
        fillRT.offsetMin = Vector2.zero;
        fillRT.offsetMax = Vector2.zero;
        Image fillImg = fill.AddComponent<Image>();
        fillImg.color = ColAccent;

        // Handle slide area
        GameObject handleArea = new GameObject("Handle Slide Area");
        handleArea.transform.SetParent(sliderObj.transform, false);
        RectTransform handleAreaRT = handleArea.AddComponent<RectTransform>();
        handleAreaRT.anchorMin = new Vector2(0, 0);
        handleAreaRT.anchorMax = new Vector2(1, 1);
        handleAreaRT.offsetMin = new Vector2(10, 0);
        handleAreaRT.offsetMax = new Vector2(-10, 0);

        // Handle
        GameObject handle = new GameObject("Handle");
        handle.transform.SetParent(handleArea.transform, false);
        RectTransform handleRT = handle.AddComponent<RectTransform>();
        handleRT.sizeDelta = new Vector2(30, 30);
        Image handleImg = handle.AddComponent<Image>();
        handleImg.color = Color.white;

        // Slider component
        Slider slider = sliderObj.AddComponent<Slider>();
        slider.fillRect = fillRT;
        slider.handleRect = handleRT;
        slider.targetGraphic = handleImg;
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.value = defaultValue;

        return sliderObj;
    }

    // ========================================
    // Physics Layer Configuration
    // ========================================

    private void ConfigurePhysicsLayers()
    {
        // Layer numbers: Player=8, AIBlob=9, Feed=10, PowerUp=11
        // (Defined in TagManager.asset)
        // Collision matrix configured via SerializedObject

        // Physics LayerCollisionMatrix configuration
        // Player(8) <-> AIBlob(9): ON
        // Player(8) <-> Feed(10): ON
        // Player(8) <-> PowerUp(11): ON
        // AIBlob(9) <-> AIBlob(9): ON
        // AIBlob(9) <-> Feed(10): ON
        // AIBlob(9) <-> PowerUp(11): OFF
        // Feed(10) <-> Feed(10): OFF
        // PowerUp(11) <-> PowerUp(11): OFF

        // Note: Configure layer collisions from code
        for (int i = 0; i < 32; i++)
        {
            for (int j = 0; j < 32; j++)
            {
                // Default: all enabled
                // Only disable specific combinations
            }
        }

        // Disable Feed-Feed collision (layer 10)
        Physics.IgnoreLayerCollision(10, 10, true);
        // Disable PowerUp-PowerUp collision (layer 11)
        Physics.IgnoreLayerCollision(11, 11, true);
        // Disable Feed-PowerUp collision
        Physics.IgnoreLayerCollision(10, 11, true);
        // Disable AIBlob-PowerUp collision
        Physics.IgnoreLayerCollision(9, 11, true);

        Debug.Log("[Blob3D] Physics layer collision matrix configured");
    }
}
