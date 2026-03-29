using TMPro;
using UnityEditor;
using UnityEngine;
using System.Text;

public static class TMPLiberationSansFallbackRepair
{
    private const string MainFontPath = "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";
    private const string FallbackFontPath = "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF - Fallback.asset";

    [MenuItem("Tools/TextMeshPro/Repair LiberationSans Fallback")]
    public static void RepairFallbackFont()
    {
        var mainFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(MainFontPath);
        var fallbackFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FallbackFontPath);

        if (fallbackFont == null)
        {
            Debug.LogError($"TMP repair failed: font asset not found at '{FallbackFontPath}'.");
            return;
        }

        var requiredCharacters = BuildRussianCharacterSet();
        EnsureMainFontHasFallback(mainFont, fallbackFont);

        var originalPopulationMode = fallbackFont.atlasPopulationMode;
        if (originalPopulationMode == AtlasPopulationMode.Static)
        {
            // Static atlas cannot add characters at edit time.
            fallbackFont.atlasPopulationMode = AtlasPopulationMode.Dynamic;
        }

        var added = fallbackFont.TryAddCharacters(requiredCharacters, out var missingCharacters);

        if (originalPopulationMode == AtlasPopulationMode.Static)
        {
            fallbackFont.atlasPopulationMode = originalPopulationMode;
        }

        EditorUtility.SetDirty(fallbackFont);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        if (!string.IsNullOrEmpty(missingCharacters))
        {
            Debug.LogWarning(
                $"TMP repair completed with missing characters: '{missingCharacters}'. " +
                "Check source font supports them or use another fallback font.");
            return;
        }

        if (!added)
        {
            Debug.Log("TMP repair: no new characters were added (they may already exist).");
            return;
        }

        Debug.Log("TMP repair: LiberationSans fallback updated successfully (full Cyrillic set checked).");
    }

    private static void EnsureMainFontHasFallback(TMP_FontAsset mainFont, TMP_FontAsset fallbackFont)
    {
        if (mainFont == null)
        {
            Debug.LogWarning($"TMP repair: main font not found at '{MainFontPath}'.");
            return;
        }

        if (mainFont.fallbackFontAssetTable.Contains(fallbackFont))
        {
            return;
        }

        mainFont.fallbackFontAssetTable.Add(fallbackFont);
        EditorUtility.SetDirty(mainFont);
        Debug.Log("TMP repair: added fallback font reference to LiberationSans SDF.");
    }

    private static string BuildRussianCharacterSet()
    {
        var sb = new StringBuilder();

        // Uppercase and lowercase Russian alphabet ranges.
        for (var code = 0x0410; code <= 0x044F; code++)
        {
            sb.Append((char)code);
        }

        // Dedicated Russian letters outside the contiguous ranges.
        sb.Append('\u0401'); // Ё
        sb.Append('\u0451'); // ё

        return sb.ToString();
    }
}
