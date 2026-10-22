using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// AvatarBadge
/// Put on a round Image to show the player's avatar picture on it: the built-in chibi avatar or the picture from the computer that was
/// chosen in Edit profile (AvatarLibrary.SpriteFor). The grey head and body shapes inside the old round icon (children named "head" and
/// "body") are hidden. Used by the "Profile" chip of the Main menu (the "avatar" circle inside "profile_chip"), so the chip shows the
/// same picture as the Profile screen. It updates every time the scene starts or the object is enabled.
/// </summary>
[RequireComponent(typeof(Image))]
public class AvatarBadge : MonoBehaviour
{
    private Image _image;

    private void OnEnable()
    {
        Apply();
    }

    /// <summary>Reads the profile again and puts the current avatar picture on the Image.</summary>
    public void Apply()
    {
        if (_image == null)
        {
            _image = GetComponent<Image>();
        }
        Sprite sprite = AvatarLibrary.SpriteFor(PlayerDatabase.Instance);
        if (sprite == null)
        {
            return;
        }
        _image.sprite = sprite;
        _image.color = Color.white;
        foreach (string shape in new[] { "head", "body" })
        {
            Transform t = transform.Find(shape);
            if (t != null)
            {
                t.gameObject.SetActive(false);
            }
        }
    }
}
