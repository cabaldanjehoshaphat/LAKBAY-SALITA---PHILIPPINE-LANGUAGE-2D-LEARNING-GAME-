using System;
using System.Runtime.InteropServices;
using UnityEngine;

/// <summary>
/// FilePicker
/// Opens the computer's own "Open file" window so the player can pick a picture from any folder (Documents, Pictures, Desktop, a USB
/// stick, ...). PickImage() returns the full path of the chosen .png / .jpg / .jpeg file, or null when the player cancels.
///  - Windows build: the standard Windows window (comdlg32 GetOpenFileName), starting in the Pictures folder. The game waits until the
///    window is closed.
///  - Unity editor: the editor's own open-file panel.
///  - Other platforms: not available, PickImage() returns null and IsAvailable is false.
/// Used by the Edit profile window ("Choose a picture from your computer", see ProfileScreenController and AvatarLibrary).
/// </summary>
public static class FilePicker
{
    /// <summary>True when this build can open a file window.</summary>
    public static bool IsAvailable
    {
        get
        {
#if UNITY_EDITOR || UNITY_STANDALONE_WIN
            return true;
#else
            return false;
#endif
        }
    }

    /// <summary>Shows the file window and returns the chosen picture file, or null when it was cancelled (or can not be shown here).</summary>
    public static string PickImage(string title)
    {
#if UNITY_EDITOR
        string chosen = UnityEditor.EditorUtility.OpenFilePanel(title, Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "png,jpg,jpeg");
        return string.IsNullOrEmpty(chosen) ? null : chosen;
#elif UNITY_STANDALONE_WIN
        return PickWindows(title);
#else
        return null;
#endif
    }

#if !UNITY_EDITOR && UNITY_STANDALONE_WIN
    private const int OfnFileMustExist = 0x00001000;
    private const int OfnPathMustExist = 0x00000800;
    private const int OfnNoChangeDir = 0x00000008;
    private const int OfnExplorer = 0x00080000;
    private const int OfnHideReadOnly = 0x00000004;
    private const int PathChars = 1024;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private class OpenFileName
    {
        public int structSize;
        public IntPtr owner;
        public IntPtr instance;
        public string filter;
        public string customFilter;
        public int maxCustomFilter;
        public int filterIndex;
        public IntPtr file;
        public int maxFile;
        public string fileTitle;
        public int maxFileTitle;
        public string initialDir;
        public string title;
        public int flags;
        public short fileOffset;
        public short fileExtension;
        public string defaultExtension;
        public IntPtr customData;
        public IntPtr hook;
        public string templateName;
        public IntPtr reserved;
        public int reserved2;
        public int flagsEx;
    }

    [DllImport("comdlg32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool GetOpenFileName([In, Out] OpenFileName ofn);

    [DllImport("user32.dll")]
    private static extern IntPtr GetActiveWindow();

    private static string PickWindows(string title)
    {
        IntPtr buffer = Marshal.AllocHGlobal(PathChars * 2);
        try
        {
            Marshal.WriteInt16(buffer, 0, 0);   // empty start name
            var ofn = new OpenFileName();
            ofn.structSize = Marshal.SizeOf(ofn);
            ofn.owner = GetActiveWindow();
            ofn.filter = "Pictures (*.png, *.jpg, *.jpeg)\0*.png;*.jpg;*.jpeg\0All files (*.*)\0*.*\0\0";
            ofn.filterIndex = 1;
            ofn.file = buffer;
            ofn.maxFile = PathChars;
            ofn.initialDir = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
            ofn.title = title;
            ofn.flags = OfnExplorer | OfnFileMustExist | OfnPathMustExist | OfnNoChangeDir | OfnHideReadOnly;
            if (!GetOpenFileName(ofn))
            {
                return null;   // cancelled
            }
            string path = Marshal.PtrToStringUni(buffer);
            return string.IsNullOrEmpty(path) ? null : path;
        }
        catch (Exception ex)
        {
            Debug.LogWarning("FilePicker: could not open the file window (" + ex.Message + ")");
            return null;
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }
#endif
}
