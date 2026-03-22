using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
using System;
using System.IO;
using System.Linq;

/// <summary>
/// CLI build script for Blob3D.
/// Usage:
///   Unity -batchmode -nographics -quit -projectPath <path> -executeMethod BuildScript.BuildAndroid
///   Unity -batchmode -nographics -quit -projectPath <path> -executeMethod BuildScript.BuildiOS
///   Unity -batchmode -nographics -quit -projectPath <path> -executeMethod BuildScript.BuildAll
///
/// Optional args (pass via command line after '--'):
///   --output <path>   Override output directory (default: Builds/)
///   --dev             Development build with debugging
/// </summary>
public static class BuildScript
{
    private static readonly string DefaultOutputDir = "Builds";

    // ========================================
    // Public entry points (called via -executeMethod)
    // ========================================

    public static void BuildAndroid()
    {
        var args = ParseArgs();
        var outputDir = args.outputDir ?? Path.Combine(DefaultOutputDir, "Android");
        var outputPath = Path.Combine(outputDir, "Blob3D.apk");

        EnsureDirectory(outputDir);
        RunSetupIfNeeded();

        var options = CreateBuildOptions(outputPath, BuildTarget.Android, args.isDev);
        ExecuteBuild(options, "Android");
    }

    public static void BuildiOS()
    {
        var outputDir = ParseArgs().outputDir ?? Path.Combine(DefaultOutputDir, "iOS");
        var args = ParseArgs();

        EnsureDirectory(outputDir);
        RunSetupIfNeeded();

        var options = CreateBuildOptions(outputDir, BuildTarget.iOS, args.isDev);
        ExecuteBuild(options, "iOS");
    }

    public static void BuildAll()
    {
        BuildAndroid();
        BuildiOS();
    }

    // ========================================
    // Setup
    // ========================================

    private static void RunSetupIfNeeded()
    {
        // Check if GameScene exists; if not, run auto setup
        var scenePath = "Assets/Scenes/GameScene.unity";
        if (!File.Exists(scenePath))
        {
            Debug.Log("[BuildScript] GameScene not found. Running Blob3DProjectSetup...");

            // Use reflection to call the private RunFullSetup
            var setupType = typeof(Blob3DProjectSetup);
            var window = EditorWindow.CreateInstance<Blob3DProjectSetup>();
            var method = setupType.GetMethod("RunFullSetup",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            if (method != null)
            {
                method.Invoke(window, null);
                Debug.Log("[BuildScript] Auto setup completed.");
            }
            else
            {
                Debug.LogWarning("[BuildScript] Could not find RunFullSetup method. " +
                    "Please run 'Blob3D > Setup Project' manually first.");
            }
        }
    }

    // ========================================
    // Build
    // ========================================

    private static BuildPlayerOptions CreateBuildOptions(
        string outputPath, BuildTarget target, bool isDev)
    {
        var scenes = GetBuildScenes();

        var options = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = outputPath,
            target = target,
            options = isDev
                ? BuildOptions.Development | BuildOptions.AllowDebugging
                : BuildOptions.None
        };

        return options;
    }

    private static void ExecuteBuild(BuildPlayerOptions options, string platformName)
    {
        Debug.Log($"[BuildScript] Starting {platformName} build...");
        Debug.Log($"[BuildScript] Output: {options.locationPathName}");
        Debug.Log($"[BuildScript] Scenes: {string.Join(", ", options.scenes)}");

        var report = BuildPipeline.BuildPlayer(options);
        var summary = report.summary;

        Debug.Log($"[BuildScript] {platformName} build result: {summary.result}");
        Debug.Log($"[BuildScript] Duration: {summary.totalTime}");
        Debug.Log($"[BuildScript] Size: {summary.totalSize / (1024 * 1024):F1} MB");
        Debug.Log($"[BuildScript] Warnings: {summary.totalWarnings}");
        Debug.Log($"[BuildScript] Errors: {summary.totalErrors}");

        if (summary.result != BuildResult.Succeeded)
        {
            Debug.LogError($"[BuildScript] {platformName} build FAILED!");
            EditorApplication.Exit(1);
        }
        else
        {
            Debug.Log($"[BuildScript] {platformName} build SUCCEEDED!");
        }
    }

    private static string[] GetBuildScenes()
    {
        // Prefer GameScene; fall back to all enabled scenes in build settings
        var gameScene = "Assets/Scenes/GameScene.unity";
        if (File.Exists(gameScene))
        {
            return new[] { gameScene };
        }

        var enabledScenes = EditorBuildSettings.scenes
            .Where(s => s.enabled)
            .Select(s => s.path)
            .ToArray();

        if (enabledScenes.Length == 0)
        {
            Debug.LogError("[BuildScript] No scenes found for build!");
            EditorApplication.Exit(1);
        }

        return enabledScenes;
    }

    // ========================================
    // Utilities
    // ========================================

    private static void EnsureDirectory(string path)
    {
        if (!Directory.Exists(path))
        {
            Directory.CreateDirectory(path);
        }
    }

    private struct BuildArgs
    {
        public string outputDir;
        public bool isDev;
    }

    private static BuildArgs ParseArgs()
    {
        var args = new BuildArgs();
        var cmdArgs = Environment.GetCommandLineArgs();

        for (int i = 0; i < cmdArgs.Length; i++)
        {
            if (cmdArgs[i] == "--output" && i + 1 < cmdArgs.Length)
            {
                args.outputDir = cmdArgs[i + 1];
            }
            else if (cmdArgs[i] == "--dev")
            {
                args.isDev = true;
            }
        }

        return args;
    }
}
