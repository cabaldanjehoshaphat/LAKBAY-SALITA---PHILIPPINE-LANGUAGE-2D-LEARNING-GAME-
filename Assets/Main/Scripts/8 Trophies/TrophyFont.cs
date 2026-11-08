using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// TrophyFont
/// Gives text built at run time the same material as the text saved in the scenes. The default TextMesh Pro material of this project has a black face, which turns
/// coloured text black, so the scenes use "LiberationSans SDF Colorable". The Trophy screen, the score card and the unlock message are built while the game runs and load a
/// copy of that material from Assets/Main/Resources/Trophies. Apply is called on each label as it is created.
/// </summary>
public static class TrophyFont
{
    private static Material _material;

    private static Material Colorable
    {
        get
        {
            if (_material == null)
            {
                _material = Resources.Load<Material>("Trophies/LiberationSans SDF Colorable");
            }
            return _material;
        }
    }

    public static TextMeshProUGUI Apply(TextMeshProUGUI label)
    {
        if (label != null && Colorable != null)
        {
            label.fontSharedMaterial = Colorable;
        }
        return label;
    }

    /// <summary>For a pill: applies the material to the label inside it.</summary>
    public static Image Apply(Image pill)
    {
        if (pill != null)
        {
            Apply(pill.GetComponentInChildren<TextMeshProUGUI>(true));
        }
        return pill;
    }
}
