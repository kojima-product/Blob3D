using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using TMPro;
using System.IO;

/// <summary>
/// Blob3D ワンクリックプロジェクトセットアップ。
/// Unity メニュー「Blob3D > Setup Project」で全てを自動生成する。
///
/// 生成されるもの:
///   - マテリアル（Player, AI各種, Feed各色, PowerUp各色, Ground, Boundary）
///   - プレハブ（PlayerBlob, AIBlob, Feed, PowerUp）
///   - シーン（GameScene: 全マネージャー + UI + プレイヤー + カメラ + フィールド）
///   - 物理レイヤーの衝突マトリクス設定
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
        GUILayout.Label("このツールはプロジェクト全体を自動セットアップします：\n" +
            "• マテリアル生成\n" +
            "• プレハブ生成（Player / AI / Feed / PowerUp）\n" +
            "• ゲームシーン生成（全マネージャー + UI + カメラ）\n" +
            "• 物理レイヤー設定", EditorStyles.wordWrappedLabel);
        GUILayout.Space(20);

        if (!setupComplete)
        {
            if (GUILayout.Button("セットアップ開始", GUILayout.Height(40)))
            {
                RunFullSetup();
            }
        }
        else
        {
            EditorGUILayout.HelpBox("セットアップ完了！ GameScene を開いて Play してください。\n" +
                "操作: WASD=移動, Space=ダッシュ, F=分裂, 右ドラッグ=カメラ回転",
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
            statusMessage = "URP パイプライン設定中...";
            Repaint();
            ConfigureURP();

            statusMessage = "マテリアル生成中...";
            Repaint();
            CreateDirectories();
            CreateMaterials();

            statusMessage = "プレハブ生成中...";
            Repaint();
            CreatePrefabs();

            statusMessage = "シーン構築中...";
            Repaint();
            CreateGameScene();

            statusMessage = "物理レイヤー設定中...";
            Repaint();
            ConfigurePhysicsLayers();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            setupComplete = true;
            statusMessage = "✓ 全セットアップ完了！ Assets/Scenes/GameScene.unity を開いてください。";
            Debug.Log("[Blob3D] プロジェクトセットアップ完了！");
        }
        catch (System.Exception e)
        {
            statusMessage = $"エラー: {e.Message}";
            Debug.LogError($"[Blob3D] Setup Error: {e}");
        }
    }

    // ========================================
    // URP パイプライン設定
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
    // ディレクトリ
    // ========================================

    private void CreateDirectories()
    {
        string[] dirs = {
            "Assets/Materials",
            "Assets/Prefabs",
            "Assets/Scenes",
            "Assets/Resources"
        };
        foreach (var dir in dirs)
        {
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);
        }
    }

    // ========================================
    // マテリアル生成
    // ========================================

    private Material CreateMat(string name, Color color, bool transparent = false)
    {
        string path = $"Assets/Materials/{name}.mat";
        Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing != null) return existing;

        // URP Lit シェーダーを探す。なければ Standard にフォールバック
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

        // Ground — matte earth-like surface with subtle grid
        matGround = CreateGroundMat("M_Ground",
            new Color(0.12f, 0.13f, 0.11f),   // Earthy dark gray-brown
            new Color(0.18f, 0.19f, 0.16f),    // Subtle warm grid lines
            gridSize: 10f);

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
            CreateMat("M_Feed_Yellow", new Color(1f, 0.85f, 0.2f)),
            CreateMat("M_Feed_Green",  new Color(0.3f, 0.9f, 0.5f)),
            CreateMat("M_Feed_Blue",   new Color(0.4f, 0.6f, 1f)),
            CreateMat("M_Feed_Pink",   new Color(1f, 0.4f, 0.6f)),
            CreateMat("M_Feed_Orange", new Color(0.9f, 0.5f, 0.2f)),
        };

        matPowerUpSpeed  = CreateMat("M_PowerUp_Speed",  new Color(0f, 0.8f, 1f));
        matPowerUpShield = CreateMat("M_PowerUp_Shield", new Color(1f, 0.9f, 0.2f));
        matPowerUpMagnet = CreateMat("M_PowerUp_Magnet", new Color(1f, 0.2f, 0.8f));
        matPowerUpGhost  = CreateMat("M_PowerUp_Ghost",  new Color(0.8f, 0.8f, 1f, 0.5f), true);
        matPowerUpMega   = CreateMat("M_PowerUp_Mega",   new Color(1f, 0.4f, 0.1f));
    }

    // ========================================
    // プレハブ生成
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

        GameObject obj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        obj.name = "Feed";
        obj.transform.localScale = Vector3.one * 0.25f;

        obj.GetComponent<MeshRenderer>().sharedMaterial = matFeeds[0];

        SphereCollider col = obj.GetComponent<SphereCollider>();
        col.isTrigger = true;

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

        GameObject obj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        obj.name = "PowerUp";
        obj.transform.localScale = Vector3.one * 0.5f;

        obj.GetComponent<MeshRenderer>().sharedMaterial = matPowerUpSpeed;

        SphereCollider col = obj.GetComponent<SphereCollider>();
        col.isTrigger = true;

        obj.AddComponent<Blob3D.Gameplay.PowerUpItem>();

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(obj, path);
        DestroyImmediate(obj);
        return prefab;
    }

    // ========================================
    // シーン生成
    // ========================================

    private void CreateGameScene()
    {
        // 新しいシーンを作成
        var scene = UnityEditor.SceneManagement.EditorSceneManager.NewScene(
            UnityEditor.SceneManagement.NewSceneSetup.EmptyScene,
            UnityEditor.SceneManagement.NewSceneMode.Single);

        // --- Directional Light ---
        GameObject light = new GameObject("Directional Light");
        Light lightComp = light.AddComponent<Light>();
        lightComp.type = LightType.Directional;
        lightComp.color = new Color(1f, 0.95f, 0.84f);
        lightComp.intensity = 1.4f;
        lightComp.shadows = LightShadows.Soft;
        light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

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
        GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        ground.name = "Ground";
        ground.transform.position = Vector3.zero;
        ground.transform.localScale = new Vector3(400f, 0.1f, 400f);
        ground.GetComponent<MeshRenderer>().sharedMaterial = matGround;
        // Collider enabled for physical collision with blobs
        ground.GetComponent<Collider>().enabled = true;
        ground.isStatic = true;

        // --- Atmospheric ambient lighting ---
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.15f, 0.17f, 0.25f);       // Warmer blue-gray sky
        RenderSettings.ambientEquatorColor = new Color(0.28f, 0.30f, 0.35f);   // Warmer mid-gray equator
        RenderSettings.ambientGroundColor = new Color(0.08f, 0.09f, 0.12f);    // Warmer ground

        // --- Post-Processing Volume (Bloom + Vignette) ---
        GameObject ppVolume = new GameObject("PostProcessVolume");
        Volume volume = ppVolume.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.priority = 1;

        VolumeProfile profile = ScriptableObject.CreateInstance<VolumeProfile>();

        // Bloom — enhanced glow for crystals and blob highlights
        Bloom bloom = profile.Add<Bloom>(true);
        bloom.intensity.Override(1.0f);
        bloom.threshold.Override(0.6f);
        bloom.scatter.Override(0.75f);

        // Vignette — atmospheric darkening at screen edges
        Vignette vignette = profile.Add<Vignette>(true);
        vignette.intensity.Override(0.35f);
        vignette.smoothness.Override(0.45f);

        // Tonemapping — filmic tone for richer colors
        Tonemapping tonemapping = profile.Add<Tonemapping>(true);
        tonemapping.mode.Override(TonemappingMode.ACES);

        // Color Adjustments — slight cool tint for underwater atmosphere
        ColorAdjustments colorAdj = profile.Add<ColorAdjustments>(true);
        colorAdj.postExposure.Override(0.15f);
        colorAdj.contrast.Override(10f);
        colorAdj.saturation.Override(10f);

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
        player.transform.position = new Vector3(0, 0.5f, 0);

        // カメラにターゲット設定
        var camCtrl = cameraObj.GetComponent<Blob3D.Player.BlobCameraController>();
        // SerializedObject でtargetフィールドを設定
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

        // SkinManager
        GameObject skinMgrObj = CreateManager("SkinManager", typeof(Blob3D.Data.SkinManager));
        SerializedObject skinSO = new SerializedObject(skinMgrObj.GetComponent<Blob3D.Data.SkinManager>());
        skinSO.FindProperty("playerRenderer").objectReferenceValue = player.GetComponent<MeshRenderer>();
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

        // シーン保存
        string scenePath = "Assets/Scenes/GameScene.unity";
        UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene, scenePath);
        Debug.Log($"[Blob3D] シーン保存: {scenePath}");
    }

    private GameObject CreateManager(string name, System.Type componentType)
    {
        GameObject obj = new GameObject(name);
        obj.AddComponent(componentType);
        return obj;
    }

    // ========================================
    // UI 生成 — モダンなモバイルゲーム UI
    // ========================================

    // Color palette
    private static readonly Color ColBg = new Color(0.06f, 0.07f, 0.10f, 0.85f);
    private static readonly Color ColAccent = new Color(0.20f, 0.78f, 1f);
    private static readonly Color ColGreen = new Color(0.18f, 0.82f, 0.45f);
    private static readonly Color ColOrange = new Color(1f, 0.55f, 0.20f);
    private static readonly Color ColPurple = new Color(0.55f, 0.40f, 0.95f);
    private static readonly Color ColRed = new Color(0.95f, 0.30f, 0.30f);
    private static readonly Color ColGray = new Color(0.35f, 0.38f, 0.45f);
    private static readonly Color ColTextPrimary = Color.white;
    private static readonly Color ColTextSecondary = new Color(0.70f, 0.75f, 0.82f);

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
        titleBgImg.color = new Color(0.04f, 0.05f, 0.08f, 0.6f);
        titleBgImg.raycastTarget = true;

        // Title Logo — large, bold, near top
        GameObject titleText = CreateTMPText(titlePanel.transform, "TitleLogo", "BLOB 3D",
            96, TextAlignmentOptions.Center, new Vector2(0, 300));
        var titleTMP = titleText.GetComponent<TextMeshProUGUI>();
        titleTMP.color = ColAccent;
        titleTMP.fontStyle = FontStyles.Bold;

        // Subtitle — small, below title
        GameObject subtitleText = CreateTMPText(titlePanel.transform, "SubtitleText", "EAT  GROW  DOMINATE",
            26, TextAlignmentOptions.Center, new Vector2(0, 220));
        subtitleText.GetComponent<TextMeshProUGUI>().color = ColTextSecondary;
        subtitleText.GetComponent<TextMeshProUGUI>().characterSpacing = 8f;

        // Mode cycle button — small, shows current mode
        GameObject modeBtn = CreateStyledButton(titlePanel.transform, "ModeButton", "CLASSIC",
            new Vector2(0, 50), new Vector2(280, 60), ColOrange, 22f);
        // Store the label text reference for runtime updates
        TextMeshProUGUI modeLabelTMP = modeBtn.transform.GetChild(0).GetComponent<TextMeshProUGUI>();

        // Play Button — largest, most prominent, green
        GameObject playBtn = CreateStyledButton(titlePanel.transform, "PlayButton", "PLAY",
            new Vector2(0, -50), new Vector2(420, 110), ColGreen, 42f);

        // Row of 3 smaller buttons side by side: SKINS | STATS | SHOP
        GameObject skinBtn = CreateStyledButton(titlePanel.transform, "SkinButton", "SKINS",
            new Vector2(-240, -200), new Vector2(210, 70), ColPurple, 24f);

        GameObject statsBtn = CreateStyledButton(titlePanel.transform, "StatsButton", "STATS",
            new Vector2(0, -200), new Vector2(210, 70), ColGray, 24f);

        GameObject shopBtn = CreateStyledButton(titlePanel.transform, "ShopButton", "SHOP",
            new Vector2(240, -200), new Vector2(210, 70), new Color(0.9f, 0.7f, 0.1f), 24f);

        // Coin display — top-right corner
        GameObject titleCoinText = CreateTMPText(titlePanel.transform, "TitleCoinDisplay", "COINS: 0",
            24, TextAlignmentOptions.TopRight, Vector2.zero);
        var titleCoinTMP = titleCoinText.GetComponent<TextMeshProUGUI>();
        titleCoinTMP.color = new Color(0.9f, 0.7f, 0.1f);
        titleCoinTMP.characterSpacing = 3f;
        SetAnchoredPosition(titleCoinText, new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-30, -30), new Vector2(250, 40));

        // HighScore label — bottom center
        GameObject highScoreLabel = CreateTMPText(titlePanel.transform, "HighScoreLabel", "BEST: 0",
            30, TextAlignmentOptions.Center, new Vector2(0, -350));
        var hsTMP = highScoreLabel.GetComponent<TextMeshProUGUI>();
        hsTMP.color = ColTextSecondary;
        hsTMP.characterSpacing = 4f;

        // ====== Stats Panel ======
        GameObject statsPanel = CreatePanel(canvasObj.transform, "StatsPanel");
        statsPanel.SetActive(false);
        Image statsBgImg = statsPanel.GetComponent<Image>();
        statsBgImg.color = new Color(0.03f, 0.04f, 0.06f, 0.88f);
        statsBgImg.raycastTarget = true;

        // Stats card background
        GameObject statsCard = new GameObject("StatsCard");
        statsCard.transform.SetParent(statsPanel.transform, false);
        RectTransform statsCardRT = statsCard.AddComponent<RectTransform>();
        statsCardRT.anchorMin = new Vector2(0.5f, 0.5f);
        statsCardRT.anchorMax = new Vector2(0.5f, 0.5f);
        statsCardRT.sizeDelta = new Vector2(800, 900);
        statsCardRT.anchoredPosition = new Vector2(0, 30);
        Image statsCardImg = statsCard.AddComponent<Image>();
        statsCardImg.color = new Color(0.10f, 0.12f, 0.16f, 0.95f);
        statsCardImg.raycastTarget = false;

        CreateTMPText(statsCard.transform, "StatsTitle", "STATISTICS", 48,
            TextAlignmentOptions.Center, new Vector2(0, 370));
        statsCard.transform.GetChild(0).GetComponent<TextMeshProUGUI>().fontStyle = FontStyles.Bold;

        // Stats rows
        CreateTMPText(statsCard.transform, "LabelTotalGames", "GAMES PLAYED", 24,
            TextAlignmentOptions.Left, new Vector2(-300, 250)).GetComponent<TextMeshProUGUI>().color = ColTextSecondary;
        GameObject statsTotalGamesText = CreateTMPText(statsCard.transform, "StatsTotalGamesText", "0", 32,
            TextAlignmentOptions.Right, new Vector2(300, 250));

        CreateTMPText(statsCard.transform, "LabelHighScore", "HIGH SCORE", 24,
            TextAlignmentOptions.Left, new Vector2(-300, 170)).GetComponent<TextMeshProUGUI>().color = ColTextSecondary;
        GameObject statsHighScoreText = CreateTMPText(statsCard.transform, "StatsHighScoreText", "0", 32,
            TextAlignmentOptions.Right, new Vector2(300, 170));

        CreateTMPText(statsCard.transform, "LabelTotalFeed", "TOTAL FEED EATEN", 24,
            TextAlignmentOptions.Left, new Vector2(-300, 90)).GetComponent<TextMeshProUGUI>().color = ColTextSecondary;
        GameObject statsTotalFeedText = CreateTMPText(statsCard.transform, "StatsTotalFeedText", "0", 32,
            TextAlignmentOptions.Right, new Vector2(300, 90));

        CreateTMPText(statsCard.transform, "LabelTotalBlobs", "TOTAL BLOBS ABSORBED", 24,
            TextAlignmentOptions.Left, new Vector2(-300, 10)).GetComponent<TextMeshProUGUI>().color = ColTextSecondary;
        GameObject statsTotalBlobsText = CreateTMPText(statsCard.transform, "StatsTotalBlobsText", "0", 32,
            TextAlignmentOptions.Right, new Vector2(300, 10));

        CreateTMPText(statsCard.transform, "LabelMaxSize", "MAX SIZE", 24,
            TextAlignmentOptions.Left, new Vector2(-300, -70)).GetComponent<TextMeshProUGUI>().color = ColTextSecondary;
        GameObject statsMaxSizeText = CreateTMPText(statsCard.transform, "StatsMaxSizeText", "x1.0", 32,
            TextAlignmentOptions.Right, new Vector2(300, -70));

        CreateTMPText(statsCard.transform, "LabelBestTA", "BEST TIME ATTACK", 24,
            TextAlignmentOptions.Left, new Vector2(-300, -150)).GetComponent<TextMeshProUGUI>().color = ColTextSecondary;
        GameObject statsBestTATimeText = CreateTMPText(statsCard.transform, "StatsBestTATimeText", "--:--.0", 32,
            TextAlignmentOptions.Right, new Vector2(300, -150));

        // Stats back button
        GameObject statsBackBtn = CreateStyledButton(statsCard.transform, "StatsBackButton", "BACK",
            new Vector2(0, -320), new Vector2(270, 90), ColGray, 32f);

        // ====== Game Panel (HUD) ======
        GameObject gamePanel = CreatePanel(canvasObj.transform, "GamePanel");
        gamePanel.SetActive(false);

        // HUDController
        Blob3D.UI.HUDController hud = gamePanel.AddComponent<Blob3D.UI.HUDController>();

        // -- Top bar background --
        GameObject topBar = new GameObject("TopBar");
        topBar.transform.SetParent(gamePanel.transform, false);
        RectTransform topBarRT = topBar.AddComponent<RectTransform>();
        topBarRT.anchorMin = new Vector2(0, 1);
        topBarRT.anchorMax = new Vector2(1, 1);
        topBarRT.pivot = new Vector2(0.5f, 1);
        topBarRT.anchoredPosition = Vector2.zero;
        topBarRT.sizeDelta = new Vector2(0, 160);
        Image topBarImg = topBar.AddComponent<Image>();
        topBarImg.color = new Color(0, 0, 0, 0.45f);
        topBarImg.raycastTarget = false;

        // Score (top-left)
        GameObject scoreText = CreateTMPText(gamePanel.transform, "ScoreText", "0",
            52, TextAlignmentOptions.TopLeft, Vector2.zero);
        scoreText.GetComponent<TextMeshProUGUI>().fontStyle = FontStyles.Bold;
        SetAnchoredPosition(scoreText, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(40, -20), new Vector2(300, 65));

        // Rank (below score)
        GameObject rankText = CreateTMPText(gamePanel.transform, "RankText", "Tiny",
            26, TextAlignmentOptions.TopLeft, Vector2.zero);
        rankText.GetComponent<TextMeshProUGUI>().color = ColAccent;
        rankText.GetComponent<TextMeshProUGUI>().characterSpacing = 3f;
        SetAnchoredPosition(rankText, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(40, -85), new Vector2(200, 35));

        // Size
        GameObject sizeText = CreateTMPText(gamePanel.transform, "SizeText", "x1.0",
            24, TextAlignmentOptions.TopLeft, Vector2.zero);
        sizeText.GetComponent<TextMeshProUGUI>().color = ColTextSecondary;
        SetAnchoredPosition(sizeText, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(40, -118), new Vector2(200, 35));

        // Timer (top-right) — bold, accent color
        GameObject timerText = CreateTMPText(gamePanel.transform, "TimerText", "3:00",
            52, TextAlignmentOptions.TopRight, Vector2.zero);
        var timerTMP = timerText.GetComponent<TextMeshProUGUI>();
        timerTMP.fontStyle = FontStyles.Bold;
        SetAnchoredPosition(timerText, new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-40, -20), new Vector2(200, 65));

        // Pause Button — subtle, top-right
        GameObject pauseBtn = CreateStyledButton(gamePanel.transform, "PauseButton", "||",
            Vector2.zero, new Vector2(70, 70), new Color(0.3f, 0.33f, 0.4f, 0.7f), 28f);
        SetAnchoredPosition(pauseBtn, new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-40, -90), new Vector2(70, 70));

        // Joystick (bottom-left, above minimap area)
        GameObject joystickArea = CreateStyledJoystick(gamePanel.transform, canvas);

        // Dash Button (bottom-right) — circular feel, well spaced from edge
        GameObject dashBtn = CreateStyledButton(gamePanel.transform, "DashButton", "DASH",
            Vector2.zero, new Vector2(130, 130), ColOrange, 26f);
        SetAnchoredPosition(dashBtn, new Vector2(1, 0), new Vector2(1, 0), new Vector2(1, 0), new Vector2(-60, 100), new Vector2(130, 130));

        // PowerUp Icons — centered top
        GameObject speedIcon = CreateStyledPowerUpIcon(gamePanel.transform, "SpeedBoostIcon", ColAccent, 0);
        GameObject shieldIcon = CreateStyledPowerUpIcon(gamePanel.transform, "ShieldIcon", new Color(1f, 0.85f, 0.15f), 1);
        GameObject magnetIcon = CreateStyledPowerUpIcon(gamePanel.transform, "MagnetIcon", new Color(1f, 0.25f, 0.75f), 2);
        GameObject ghostIcon = CreateStyledPowerUpIcon(gamePanel.transform, "GhostIcon", new Color(0.85f, 0.85f, 1f, 0.8f), 3);

        // HUD references
        SerializedObject hudSO = new SerializedObject(hud);
        hudSO.FindProperty("scoreText").objectReferenceValue = scoreText.GetComponent<TextMeshProUGUI>();
        hudSO.FindProperty("timerText").objectReferenceValue = timerText.GetComponent<TextMeshProUGUI>();
        hudSO.FindProperty("rankText").objectReferenceValue = rankText.GetComponent<TextMeshProUGUI>();
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
        resultBg.color = new Color(0.03f, 0.04f, 0.06f, 0.88f);
        resultBg.raycastTarget = true;

        // Result card background
        GameObject resultCard = new GameObject("ResultCard");
        resultCard.transform.SetParent(resultPanel.transform, false);
        RectTransform cardRT = resultCard.AddComponent<RectTransform>();
        cardRT.anchorMin = new Vector2(0.5f, 0.5f);
        cardRT.anchorMax = new Vector2(0.5f, 0.5f);
        cardRT.sizeDelta = new Vector2(800, 1000);
        cardRT.anchoredPosition = new Vector2(0, 20);
        Image cardImg = resultCard.AddComponent<Image>();
        cardImg.color = new Color(0.10f, 0.12f, 0.16f, 0.95f);
        cardImg.raycastTarget = false;

        CreateTMPText(resultCard.transform, "ResultTitle", "GAME OVER", 48,
            TextAlignmentOptions.Center, new Vector2(0, 360));
        resultCard.transform.GetChild(0).GetComponent<TextMeshProUGUI>().fontStyle = FontStyles.Bold;

        // Score — large accent
        GameObject resultScoreText = CreateTMPText(resultCard.transform, "ResultScoreText", "0",
            80, TextAlignmentOptions.Center, new Vector2(0, 240));
        resultScoreText.GetComponent<TextMeshProUGUI>().color = ColAccent;
        resultScoreText.GetComponent<TextMeshProUGUI>().fontStyle = FontStyles.Bold;

        // Rank
        GameObject resultRankText = CreateTMPText(resultCard.transform, "ResultRankText", "Tiny",
            36, TextAlignmentOptions.Center, new Vector2(0, 145));
        resultRankText.GetComponent<TextMeshProUGUI>().color = ColTextSecondary;

        // Divider line
        GameObject divider = new GameObject("Divider");
        divider.transform.SetParent(resultCard.transform, false);
        RectTransform divRT = divider.AddComponent<RectTransform>();
        divRT.anchoredPosition = new Vector2(0, 90);
        divRT.sizeDelta = new Vector2(600, 2);
        Image divImg = divider.AddComponent<Image>();
        divImg.color = new Color(1, 1, 1, 0.12f);
        divImg.raycastTarget = false;

        // Stats row
        GameObject resultFeedText = CreateTMPText(resultCard.transform, "ResultFeedText", "0",
            32, TextAlignmentOptions.Center, new Vector2(-200, 20));
        CreateTMPText(resultCard.transform, "FeedLabel", "FEED", 20,
            TextAlignmentOptions.Center, new Vector2(-200, -20)).GetComponent<TextMeshProUGUI>().color = ColTextSecondary;

        GameObject resultBlobsText = CreateTMPText(resultCard.transform, "ResultBlobsText", "0",
            32, TextAlignmentOptions.Center, new Vector2(0, 20));
        CreateTMPText(resultCard.transform, "BlobsLabel", "BLOBS", 20,
            TextAlignmentOptions.Center, new Vector2(0, -20)).GetComponent<TextMeshProUGUI>().color = ColTextSecondary;

        GameObject resultMaxSizeText = CreateTMPText(resultCard.transform, "ResultMaxSizeText", "x1.0",
            32, TextAlignmentOptions.Center, new Vector2(200, 20));
        CreateTMPText(resultCard.transform, "MaxLabel", "MAX SIZE", 20,
            TextAlignmentOptions.Center, new Vector2(200, -20)).GetComponent<TextMeshProUGUI>().color = ColTextSecondary;

        // New high score badge
        GameObject newHighBadge = CreateTMPText(resultCard.transform, "NewHighScoreBadge", "NEW HIGH SCORE!",
            32, TextAlignmentOptions.Center, new Vector2(0, -90));
        var nhTMP = newHighBadge.GetComponent<TextMeshProUGUI>();
        nhTMP.color = new Color(1f, 0.85f, 0.15f);
        nhTMP.fontStyle = FontStyles.Bold;
        newHighBadge.SetActive(false);

        // Coins earned
        GameObject resultCoinsText = CreateTMPText(resultCard.transform, "ResultCoinsEarned", "+0 COINS",
            28, TextAlignmentOptions.Center, new Vector2(0, -140));
        var rcTMP = resultCoinsText.GetComponent<TextMeshProUGUI>();
        rcTMP.color = new Color(0.9f, 0.7f, 0.1f);
        rcTMP.fontStyle = FontStyles.Bold;

        // Buttons — well spaced, prominent
        GameObject retryBtn = CreateStyledButton(resultCard.transform, "RetryButton", "RETRY",
            new Vector2(-160, -260), new Vector2(270, 90), ColGreen, 32f);

        GameObject homeBtn = CreateStyledButton(resultCard.transform, "HomeButton", "HOME",
            new Vector2(160, -260), new Vector2(270, 90), ColGray, 32f);

        // ====== Pause Panel ======
        GameObject pausePanel = CreatePanel(canvasObj.transform, "PausePanel");
        pausePanel.SetActive(false);
        Image pauseBgImg = pausePanel.GetComponent<Image>();
        pauseBgImg.color = new Color(0.03f, 0.04f, 0.06f, 0.80f);
        pauseBgImg.raycastTarget = true;

        CreateTMPText(pausePanel.transform, "PauseTitle", "PAUSED", 56,
            TextAlignmentOptions.Center, new Vector2(0, 100)).GetComponent<TextMeshProUGUI>().fontStyle = FontStyles.Bold;

        GameObject resumeBtn = CreateStyledButton(pausePanel.transform, "ResumeButton", "RESUME",
            new Vector2(0, -40), new Vector2(320, 90), ColGreen, 32f);

        GameObject pauseHomeBtn = CreateStyledButton(pausePanel.transform, "PauseHomeButton", "HOME",
            new Vector2(0, -170), new Vector2(320, 90), ColGray, 32f);

        // ====== Settings Panel ======
        GameObject settingsPanel = CreatePanel(canvasObj.transform, "SettingsPanel");
        settingsPanel.SetActive(false);
        Image settingsBg = settingsPanel.GetComponent<Image>();
        settingsBg.color = new Color(0.03f, 0.04f, 0.06f, 0.88f);
        settingsBg.raycastTarget = true;

        // Settings card
        GameObject settingsCard = new GameObject("SettingsCard");
        settingsCard.transform.SetParent(settingsPanel.transform, false);
        RectTransform sCardRT = settingsCard.AddComponent<RectTransform>();
        sCardRT.anchorMin = new Vector2(0.5f, 0.5f);
        sCardRT.anchorMax = new Vector2(0.5f, 0.5f);
        sCardRT.sizeDelta = new Vector2(700, 700);
        sCardRT.anchoredPosition = new Vector2(0, 30);
        Image sCardImg = settingsCard.AddComponent<Image>();
        sCardImg.color = new Color(0.10f, 0.12f, 0.16f, 0.95f);
        sCardImg.raycastTarget = false;

        CreateTMPText(settingsCard.transform, "SettingsTitle", "SETTINGS", 48,
            TextAlignmentOptions.Center, new Vector2(0, 280));
        settingsCard.transform.GetChild(0).GetComponent<TextMeshProUGUI>().fontStyle = FontStyles.Bold;

        // BGM Volume slider
        CreateTMPText(settingsCard.transform, "BGMLabel", "BGM VOLUME", 24,
            TextAlignmentOptions.Left, new Vector2(-200, 150)).GetComponent<TextMeshProUGUI>().color = ColTextSecondary;
        GameObject bgmSliderObj = CreateStyledSlider(settingsCard.transform, "BGMSlider",
            new Vector2(0, 100), new Vector2(500, 40), 0.3f);

        // SE Volume slider
        CreateTMPText(settingsCard.transform, "SELabel", "SE VOLUME", 24,
            TextAlignmentOptions.Left, new Vector2(-200, 20)).GetComponent<TextMeshProUGUI>().color = ColTextSecondary;
        GameObject seSliderObj = CreateStyledSlider(settingsCard.transform, "SESlider",
            new Vector2(0, -30), new Vector2(500, 40), 0.6f);

        // Sensitivity slider
        CreateTMPText(settingsCard.transform, "SensLabel", "SENSITIVITY", 24,
            TextAlignmentOptions.Left, new Vector2(-200, -110)).GetComponent<TextMeshProUGUI>().color = ColTextSecondary;
        GameObject sensSliderObj = CreateStyledSlider(settingsCard.transform, "SensitivitySlider",
            new Vector2(0, -160), new Vector2(500, 40), 0.25f);

        // Back button
        GameObject settingsBackBtn = CreateStyledButton(settingsCard.transform, "SettingsBackButton", "BACK",
            new Vector2(0, -280), new Vector2(270, 90), ColGray, 32f);

        // Settings button on title screen — bottom-right corner, small gear style
        GameObject settingsBtn = CreateStyledButton(titlePanel.transform, "SettingsButton", "SETTINGS",
            Vector2.zero, new Vector2(180, 60), new Color(0.30f, 0.33f, 0.40f, 0.8f), 20f);
        SetAnchoredPosition(settingsBtn, new Vector2(1, 0), new Vector2(1, 0), new Vector2(1, 0), new Vector2(-30, 30), new Vector2(180, 60));

        // ====== Minimap on Game Panel ======
        GameObject minimapObj = new GameObject("Minimap");
        minimapObj.transform.SetParent(gamePanel.transform, false);
        minimapObj.AddComponent<Blob3D.UI.MinimapController>();

        // ====== Shop Panel ======
        GameObject shopPanel = CreatePanel(canvasObj.transform, "ShopPanel");
        shopPanel.SetActive(false);
        Image shopBgImg = shopPanel.GetComponent<Image>();
        shopBgImg.color = new Color(0.03f, 0.04f, 0.06f, 0.88f);
        shopBgImg.raycastTarget = true;

        // Shop card background
        GameObject shopCard = new GameObject("ShopCard");
        shopCard.transform.SetParent(shopPanel.transform, false);
        RectTransform shopCardRT = shopCard.AddComponent<RectTransform>();
        shopCardRT.anchorMin = new Vector2(0.5f, 0.5f);
        shopCardRT.anchorMax = new Vector2(0.5f, 0.5f);
        shopCardRT.sizeDelta = new Vector2(800, 900);
        shopCardRT.anchoredPosition = new Vector2(0, 30);
        Image shopCardImg = shopCard.AddComponent<Image>();
        shopCardImg.color = new Color(0.10f, 0.12f, 0.16f, 0.95f);
        shopCardImg.raycastTarget = false;

        CreateTMPText(shopCard.transform, "ShopTitle", "SHOP", 48,
            TextAlignmentOptions.Center, new Vector2(0, 380));
        shopCard.transform.GetChild(0).GetComponent<TextMeshProUGUI>().fontStyle = FontStyles.Bold;

        // Coin display in shop
        GameObject shopCoinText = CreateTMPText(shopCard.transform, "ShopCoinDisplay", "COINS: 0",
            32, TextAlignmentOptions.Center, new Vector2(0, 310));
        var shopCoinTMP = shopCoinText.GetComponent<TextMeshProUGUI>();
        shopCoinTMP.color = new Color(0.9f, 0.7f, 0.1f);
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

        uiSO.ApplyModifiedProperties();
    }

    // ---------- UI ヘルパー ----------

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
        bgImg.color = new Color(0.2f, 0.22f, 0.28f, 0.8f);

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
    // 物理レイヤー設定
    // ========================================

    private void ConfigurePhysicsLayers()
    {
        // レイヤー番号: Player=8, AIBlob=9, Feed=10, PowerUp=11
        // （TagManager.assetで定義済み）
        // 衝突マトリクスの設定はSerializedObjectで行う

        // Physics の LayerCollisionMatrix を設定
        // Player(8) ↔ AIBlob(9): ON
        // Player(8) ↔ Feed(10): ON
        // Player(8) ↔ PowerUp(11): ON
        // AIBlob(9) ↔ AIBlob(9): ON
        // AIBlob(9) ↔ Feed(10): ON
        // AIBlob(9) ↔ PowerUp(11): OFF
        // Feed(10) ↔ Feed(10): OFF
        // PowerUp(11) ↔ PowerUp(11): OFF

        // Note: コードからレイヤー衝突を設定
        for (int i = 0; i < 32; i++)
        {
            for (int j = 0; j < 32; j++)
            {
                // デフォルトは全て有効のまま
                // 特定の組み合わせのみ無効化
            }
        }

        // Feed同士の衝突を無効化 (layer 10)
        Physics.IgnoreLayerCollision(10, 10, true);
        // PowerUp同士の衝突を無効化 (layer 11)
        Physics.IgnoreLayerCollision(11, 11, true);
        // Feed ↔ PowerUp の衝突を無効化
        Physics.IgnoreLayerCollision(10, 11, true);
        // AIBlob ↔ PowerUp の衝突を無効化
        Physics.IgnoreLayerCollision(9, 11, true);

        Debug.Log("[Blob3D] 物理レイヤー衝突マトリクス設定完了");
    }
}
