using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// ReleaseBuilder
/// Editor tool that builds the game into a folder chosen by the developer, for example "BUild Project/Lakbay Salita - 23": the Windows app
/// goes into the subfolder "Windows" (Lakbay-Salita.exe) and the browser version into the subfolder "WebGL" (index.html, Build, TemplateData;
/// zip its content for itch.io). Both builds use all enabled scenes of Build Settings, the intro first. It is driven from code (not from a
/// menu) so a script can run the steps one after the other, because switching the active platform reloads the editor scripts:
///  1. BuildWindows(folder)  - builds the Windows 64-bit app (the editor must be on the Windows platform).
///  2. BuildWebGL(folder)    - applies WebGLBuilder.Prepare, switches the platform to WebGL if needed and builds the browser version.
///  3. WebGLBuilder.SwitchToWindows() - puts the editor back on Windows afterwards.
/// Each step appends its result to "build-log.txt" in the folder.
/// </summary>
public static class ReleaseBuilder
{
    private static string[] EnabledScenes()
    {
        var scenes = new List<string>();
        foreach (EditorBuildSettingsScene s in EditorBuildSettings.scenes)
        {
            if (s.enabled)
            {
                scenes.Add(s.path);
            }
        }
        return scenes.ToArray();
    }

    private static void Log(string folder, string text)
    {
        Directory.CreateDirectory(folder);
        File.AppendAllText(Path.Combine(folder, "build-log.txt"), System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  " + text + "\n");
        Debug.Log(text);
    }

    /// <summary>Builds the Windows 64-bit app into folder/Windows.</summary>
    public static string BuildWindows(string folder)
    {
        string output = Path.Combine(folder, "Windows");
        if (Directory.Exists(output))
        {
            Directory.Delete(output, true);
        }
        string[] scenes = EnabledScenes();
        var options = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = Path.Combine(output, "Lakbay-Salita.exe"),
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.None
        };
        var report = BuildPipeline.BuildPlayer(options);
        string text = "Windows build: " + report.summary.result + ", errors " + report.summary.totalErrors + ", size " + report.summary.totalSize
            + " bytes, " + scenes.Length + " scenes -> " + output;
        Log(folder, text);
        return text;
    }

    /// <summary>Builds the browser version into folder/WebGL (switches the platform to WebGL first when needed).</summary>
    public static string BuildWebGL(string folder)
    {
        WebGLBuilder.Prepare();
        if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.WebGL)
        {
            EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.WebGL, BuildTarget.WebGL);
        }
        string output = Path.Combine(folder, "WebGL");
        if (Directory.Exists(output))
        {
            Directory.Delete(output, true);
        }
        string[] scenes = EnabledScenes();
        var options = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = output,
            target = BuildTarget.WebGL,
            options = BuildOptions.None
        };
        var report = BuildPipeline.BuildPlayer(options);
        string text = "WebGL build: " + report.summary.result + ", errors " + report.summary.totalErrors + ", size " + report.summary.totalSize
            + " bytes, " + scenes.Length + " scenes -> " + output;
        Log(folder, text);
        return text;
    }

    // ---- menu entries (the folder is the EditorPrefs value "ReleaseBuilder.Folder", set by the script that starts the build)
    private const string FolderKey = "ReleaseBuilder.Folder";

    [MenuItem("Tools/Build/Release: Windows app into the chosen folder")]
    public static void WindowsMenu()
    {
        BuildWindows(EditorPrefs.GetString(FolderKey));
    }

    [MenuItem("Tools/Build/Release: WebGL into the chosen folder")]
    public static void WebGLMenu()
    {
        BuildWebGL(EditorPrefs.GetString(FolderKey));
    }
}
