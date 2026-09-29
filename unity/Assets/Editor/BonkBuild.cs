using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;

// Unity -batchmode -projectPath unity -executeMethod BonkBuild.WebGL -quit
public static class BonkBuild
{
    const string ScenePath = "Assets/Scenes/Main.unity";

    [MenuItem("BONK BRAWL/Setup Scene")]
    public static void Setup()
    {
        Directory.CreateDirectory("Assets/Scenes");
        if (!AssetDatabase.LoadAssetAtPath<Material>("Assets/Resources/UnlitAlpha.mat"))
            AssetDatabase.CreateAsset(new Material(Shader.Find("Sprites/Default")), "Assets/Resources/UnlitAlpha.mat");

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var camGo = new GameObject("Main Camera");
        camGo.tag = "MainCamera";
        var cam = camGo.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color32(20, 10, 40, 255);
        camGo.transform.position = new Vector3(0, 12, -6);
        camGo.transform.rotation = Quaternion.Euler(53, 0, 0);
        camGo.AddComponent<AudioListener>();
        new GameObject("Game").AddComponent<Game>();
        EditorSceneManager.SaveScene(scene, ScenePath);
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        AssetDatabase.SaveAssets();
    }

    [MenuItem("BONK BRAWL/Build WebGL")]
    // Per-gamepad stick axes (the default InputManager only has a merged "Horizontal"/"Vertical").
    static void EnsureAxes()
    {
        var obj = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/InputManager.asset")[0];
        var so = new SerializedObject(obj);
        var axes = so.FindProperty("m_Axes");
        for (int joy = 1; joy <= 2; joy++)
            for (int ax = 0; ax < 2; ax++)
            {
                string name = "J" + joy + (ax == 0 ? "X" : "Y");
                bool exists = false;
                for (int i = 0; i < axes.arraySize; i++) if (axes.GetArrayElementAtIndex(i).FindPropertyRelative("m_Name").stringValue == name) exists = true;
                if (exists) continue;
                axes.arraySize++;
                var e = axes.GetArrayElementAtIndex(axes.arraySize - 1);
                e.FindPropertyRelative("m_Name").stringValue = name;
                e.FindPropertyRelative("descriptiveName").stringValue = "";
                e.FindPropertyRelative("descriptiveNegativeName").stringValue = "";
                e.FindPropertyRelative("negativeButton").stringValue = "";
                e.FindPropertyRelative("positiveButton").stringValue = "";
                e.FindPropertyRelative("altNegativeButton").stringValue = "";
                e.FindPropertyRelative("altPositiveButton").stringValue = "";
                e.FindPropertyRelative("gravity").floatValue = 0f;
                e.FindPropertyRelative("dead").floatValue = 0.19f;
                e.FindPropertyRelative("sensitivity").floatValue = 1f;
                e.FindPropertyRelative("snap").boolValue = false;
                e.FindPropertyRelative("invert").boolValue = ax == 1;
                e.FindPropertyRelative("type").intValue = 2;
                e.FindPropertyRelative("axis").intValue = ax;
                e.FindPropertyRelative("joyNum").intValue = joy;
            }
        so.ApplyModifiedProperties();
    }

    public static void WebGL()
    {
        EnsureAxes();
        Setup();
        PlayerSettings.companyName = "BonkBrawl";
        PlayerSettings.productName = "Bonk Brawl";
        PlayerSettings.bundleVersion = "1.0.0";
        PlayerSettings.colorSpace = ColorSpace.Gamma;
        PlayerSettings.runInBackground = false;
        PlayerSettings.SplashScreen.show = false;
        PlayerSettings.WebGL.template = "PROJECT:Bonk";
        PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Brotli;
        PlayerSettings.WebGL.decompressionFallback = true;
        PlayerSettings.WebGL.nameFilesAsHashes = true;
        PlayerSettings.WebGL.dataCaching = true;
        PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.None;
        PlayerSettings.WebGL.showDiagnostics = false;
        PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.WebGL, ManagedStrippingLevel.Medium);
        PlayerSettings.stripEngineCode = true;
        PlayerSettings.SetIl2CppCodeGeneration(NamedBuildTarget.WebGL, Il2CppCodeGeneration.OptimizeSize);

        // One quality level tuned for phones: hard shadows, 2x MSAA.
        QualitySettings.SetQualityLevel(QualitySettings.names.Length - 1, true);
        EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.WebGL, BuildTarget.WebGL);

        var outDir = Path.GetFullPath(Path.Combine(Application.dataPath, "../../dist"));
        if (Directory.Exists(outDir)) Directory.Delete(outDir, true);
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = new[] { ScenePath },
            locationPathName = outDir,
            target = BuildTarget.WebGL,
            options = BuildOptions.None,
        });
        Debug.Log("BONK BUILD RESULT: " + report.summary.result + " size=" + report.summary.totalSize + " errors=" + report.summary.totalErrors);
        if (Application.isBatchMode) EditorApplication.Exit(report.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded ? 0 : 1);
    }
}
