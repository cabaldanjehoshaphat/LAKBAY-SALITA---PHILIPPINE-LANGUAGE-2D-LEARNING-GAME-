using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// WebGLBuilder
/// Editor tool that makes the browser version of the game (for itch.io or any web host). Menu: Tools > Build.
///  - "Prepare WebGL settings": sets the WebGL player settings (Gzip compression with the decompression fallback, so any web server can host it
///    without special headers; 512 MB memory for the Library's 63,540 words; WebGL 2 only (needed by the Linear color space); the web page template Assets/WebGLTemplates/Lakbay (the canvas fills the window or the itch.io frame); no exception support, which makes it smaller)
///    and gives the music and sound files a WebGL-only import setting (Vorbis, quality 40 %, compressed in memory). The Windows import settings
///    are not touched.
///  - "Build WebGL": builds all scenes of Build Settings (the intro first) into "Builds/WebGL" in the project folder (outside Assets).
///    Switching the active platform to WebGL re-imports the assets, which takes a few minutes the first time; use "Switch back to Windows"
///    afterwards to continue working on the Windows game.
/// The result is a folder with index.html, Build/ and TemplateData/; zip the folder's content (index.html at the top of the zip) and upload it
/// to itch.io as an HTML5 game ("This file will be played in the browser").
/// </summary>
public static class WebGLBuilder
{
    private const string OutputFolder = "Builds/WebGL";
    private static readonly string[] AudioFolders =
    {
        "Assets/Main/Resources/Audio Soundtrack", "Assets/Main/Resources/Audio SFX", "Assets/Main/Resources/Audio Words"
    };

    [MenuItem("Tools/Build/Prepare WebGL settings")]
    public static void PrepareMenu()
    {
        Debug.Log(Prepare());
    }

    public static string Prepare()
    {
        // ---- player settings for the browser
        PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
        PlayerSettings.WebGL.decompressionFallback = true;
        PlayerSettings.WebGL.dataCaching = true;
        PlayerSettings.WebGL.memorySize = 512;
        PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.None;
        PlayerSettings.WebGL.template = "PROJECT:Lakbay";   // Assets/WebGLTemplates/Lakbay: the canvas fills the whole window
        // the project uses the Linear color space, which needs WebGL 2 (the default list also has WebGL 1, which makes the build fail)
        PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.WebGL, false);
        PlayerSettings.SetGraphicsAPIs(BuildTarget.WebGL, new[] { UnityEngine.Rendering.GraphicsDeviceType.OpenGLES3 });
        PlayerSettings.defaultWebScreenWidth = 1280;
        PlayerSettings.defaultWebScreenHeight = 720;
        PlayerSettings.runInBackground = true;

        // ---- smaller sounds for the download (WebGL only)
        int changed = 0;
        string[] guids = AssetDatabase.FindAssets("t:AudioClip", AudioFolders);
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var importer = AssetImporter.GetAtPath(path) as AudioImporter;
            if (importer == null)
            {
                continue;
            }
            var settings = new AudioImporterSampleSettings
            {
                loadType = AudioClipLoadType.CompressedInMemory,
                compressionFormat = AudioCompressionFormat.Vorbis,
                quality = 0.4f,
                sampleRateSetting = AudioSampleRateSetting.OptimizeSampleRate
            };
            importer.SetOverrideSampleSettings("WebGL", settings);
            importer.SaveAndReimport();
            changed++;
        }
        AssetDatabase.SaveAssets();
        return "WebGL settings set; WebGL sound settings on " + changed + " clips";
    }

    [MenuItem("Tools/Build/Switch to WebGL")]
    public static void SwitchToWebGL()
    {
        EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.WebGL, BuildTarget.WebGL);
    }

    [MenuItem("Tools/Build/Switch back to Windows")]
    public static void SwitchToWindows()
    {
        EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64);
    }

    [MenuItem("Tools/Build/Build WebGL")]
    public static void BuildMenu()
    {
        Debug.Log(Build());
    }

    public static string Build()
    {
        var scenes = new System.Collections.Generic.List<string>();
        foreach (EditorBuildSettingsScene s in EditorBuildSettings.scenes)
        {
            if (s.enabled)
            {
                scenes.Add(s.path);
            }
        }
        string output = Path.Combine(Directory.GetParent(Application.dataPath).FullName, OutputFolder);
        if (Directory.Exists(output))
        {
            Directory.Delete(output, true);
        }
        var options = new BuildPlayerOptions
        {
            scenes = scenes.ToArray(),
            locationPathName = output,
            target = BuildTarget.WebGL,
            options = BuildOptions.None
        };
        var report = BuildPipeline.BuildPlayer(options);
        return "WebGL build: " + report.summary.result + ", errors " + report.summary.totalErrors + ", size " + report.summary.totalSize + " bytes, "
            + scenes.Count + " scenes, first " + scenes[0] + " -> " + output;
    }
}
