using System;
using System.IO;
using UnityEngine;

/// <summary>
/// AvatarLibrary
/// The pictures a player can use as a profile avatar.
///  - Built-in avatars: the round chibi portraits in Assets/Main/Resources/Avatars (avatar_01 ... avatar_24). Get(index) gives one of them;
///    Count says how many there are. The player's choice is PlayerDatabase.AvatarIndex.
///  - A picture from the player's own computer (Documents, Pictures, ...): ImportCustom(path) reads a .png or .jpg, cuts the biggest
///    centred square out of it, shrinks it to 256 x 256, rounds the corners into a circle and saves it as "Avatars/custom_avatar.png"
///    inside the save folder (SaveStore.Folder, which is Lakbay-Salita_Data/SaveData in a built game), so the profile keeps working
///    even if the original file is moved or deleted. The player's choice is PlayerDatabase.UseCustomAvatar. LoadCustom() gives it back
///    as a sprite and DeleteCustom() removes it (used by "Reset progress").
/// SpriteFor(db) returns the avatar the profile should show right now; screens that show the player's avatar (Profile, the Profile chip of
/// the Main menu) call it.
/// </summary>
public static class AvatarLibrary
{
    private const string Folder = "Avatars";
    private const string CustomFileName = "custom_avatar.png";
    private const int CustomSize = 256;

    private static Sprite[] _builtIn;
    private static Sprite _custom;

    /// <summary>The number of built-in avatars.</summary>
    public static int Count
    {
        get
        {
            LoadBuiltIn();
            return _builtIn.Length;
        }
    }

    /// <summary>The built-in avatar with this index (clamped into range), or null when there are none.</summary>
    public static Sprite Get(int index)
    {
        LoadBuiltIn();
        if (_builtIn.Length == 0)
        {
            return null;
        }
        return _builtIn[Mathf.Clamp(index, 0, _builtIn.Length - 1)];
    }

    private static void LoadBuiltIn()
    {
        if (_builtIn != null)
        {
            return;
        }
        Sprite[] all = Resources.LoadAll<Sprite>(Folder);
        Array.Sort(all, delegate (Sprite a, Sprite b) { return string.CompareOrdinal(a.name, b.name); });
        _builtIn = all;
    }

    /// <summary>The avatar the given profile shows: the player's own picture when chosen and still on disk, else the chosen built-in one.</summary>
    public static Sprite SpriteFor(PlayerDatabase db)
    {
        if (db == null)
        {
            return Get(0);
        }
        if (db.UseCustomAvatar)
        {
            Sprite custom = LoadCustom();
            if (custom != null)
            {
                return custom;
            }
        }
        return Get(db.AvatarIndex);
    }

    // ------------------------------------------------------------------ the player's own picture

    /// <summary>The full path of the saved picture of the player (may not exist).</summary>
    public static string CustomPath
    {
        get { return Path.Combine(Path.Combine(SaveStore.Folder, Folder), CustomFileName); }
    }

    public static bool HasCustom
    {
        get { return File.Exists(CustomPath); }
    }

    /// <summary>The saved picture of the player as a sprite, or null when there is none.</summary>
    public static Sprite LoadCustom()
    {
        if (_custom != null)
        {
            return _custom;
        }
        if (!HasCustom)
        {
            return null;
        }
        try
        {
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!texture.LoadImage(File.ReadAllBytes(CustomPath)))
            {
                UnityEngine.Object.Destroy(texture);
                return null;
            }
            texture.wrapMode = TextureWrapMode.Clamp;
            _custom = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
            return _custom;
        }
        catch (Exception ex)
        {
            Debug.LogWarning("AvatarLibrary: could not load the saved picture (" + ex.Message + ")");
            return null;
        }
    }

    /// <summary>
    /// Takes a picture file (.png, .jpg or .jpeg) from anywhere on the computer, makes it the player's round avatar picture and saves it.
    /// Returns true on success; otherwise error says what went wrong in words the player understands.
    /// </summary>
    public static bool ImportCustom(string sourcePath, out string error)
    {
        error = null;
        if (string.IsNullOrEmpty(sourcePath) || !File.Exists(sourcePath))
        {
            error = "That file could not be found.";
            return false;
        }
        string extension = Path.GetExtension(sourcePath).ToLowerInvariant();
        if (extension != ".png" && extension != ".jpg" && extension != ".jpeg")
        {
            error = "Please choose a .png or .jpg picture.";
            return false;
        }

        Texture2D source = null;
        try
        {
            source = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!source.LoadImage(File.ReadAllBytes(sourcePath)))
            {
                error = "That picture could not be opened.";
                return false;
            }
            byte[] png = MakeRoundPicture(source).EncodeToPNG();
            string path = CustomPath;
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            string temporary = path + ".tmp";   // written to a temporary file first, so a crash can not leave half a picture
            File.WriteAllBytes(temporary, png);
            if (File.Exists(path))
            {
                File.Delete(path);
            }
            File.Move(temporary, path);
            _custom = null;   // load the new one next time
            return true;
        }
        catch (Exception ex)
        {
            error = "That picture could not be used (" + ex.Message + ").";
            return false;
        }
        finally
        {
            if (source != null)
            {
                UnityEngine.Object.Destroy(source);
            }
        }
    }

    /// <summary>Forgets and deletes the player's own picture (called when the profile is reset).</summary>
    public static void DeleteCustom()
    {
        _custom = null;
        try
        {
            if (File.Exists(CustomPath))
            {
                File.Delete(CustomPath);
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning("AvatarLibrary: could not delete the saved picture (" + ex.Message + ")");
        }
    }

    /// <summary>The biggest centred square of the picture, scaled to 256 x 256, with everything outside a circle made transparent.</summary>
    private static Texture2D MakeRoundPicture(Texture2D source)
    {
        int side = Mathf.Min(source.width, source.height);
        float x0 = (source.width - side) / 2f;
        float y0 = (source.height - side) / 2f;
        var result = new Texture2D(CustomSize, CustomSize, TextureFormat.RGBA32, false);
        var pixels = new Color32[CustomSize * CustomSize];
        float radius = CustomSize / 2f;
        for (int y = 0; y < CustomSize; y++)
        {
            for (int x = 0; x < CustomSize; x++)
            {
                float u = (x0 + (x + 0.5f) / CustomSize * side) / source.width;
                float v = (y0 + (y + 0.5f) / CustomSize * side) / source.height;
                Color color = source.GetPixelBilinear(u, v);
                float distance = Mathf.Sqrt((x + 0.5f - radius) * (x + 0.5f - radius) + (y + 0.5f - radius) * (y + 0.5f - radius));
                color.a = Mathf.Clamp01(radius - distance);   // a one pixel soft edge
                pixels[y * CustomSize + x] = color;
            }
        }
        result.SetPixels32(pixels);
        result.Apply(false);
        return result;
    }
}
