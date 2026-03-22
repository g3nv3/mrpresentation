using System.IO;

using UnityEditor;
using UnityEngine;

using ZXing;
using ZXing.Common;
using ZXing.QrCode;

public sealed class QrMarkerGeneratorWindow : EditorWindow
{
    private const string DefaultOutputFolder = "Assets/Project/GeneratedQr";

    private QrMarkerRegistry registry;
    private string outputFolder = DefaultOutputFolder;
    private int textureSize = 512;
    private int margin = 1;
    private bool overwriteExisting = true;

    [MenuItem("Tools/QR/Generate Marker QR Codes")]
    public static void OpenWindow()
    {
        GetWindow<QrMarkerGeneratorWindow>("QR Generator");
    }

    public static void OpenWindow(QrMarkerRegistry targetRegistry)
    {
        var window = GetWindow<QrMarkerGeneratorWindow>("QR Generator");
        window.registry = targetRegistry;
        window.Focus();
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Source", EditorStyles.boldLabel);
        registry = (QrMarkerRegistry)EditorGUILayout.ObjectField("Registry", registry, typeof(QrMarkerRegistry), true);

        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Output", EditorStyles.boldLabel);
        using (new EditorGUILayout.HorizontalScope())
        {
            outputFolder = EditorGUILayout.TextField("Folder", outputFolder);
            if (GUILayout.Button("Browse", GUILayout.Width(70f)))
            {
                BrowseOutputFolder();
            }
        }

        textureSize = EditorGUILayout.IntSlider("Texture Size", textureSize, 128, 2048);
        margin = EditorGUILayout.IntSlider("Margin", margin, 0, 8);
        overwriteExisting = EditorGUILayout.Toggle("Overwrite", overwriteExisting);

        EditorGUILayout.Space(8f);
        EditorGUILayout.HelpBox(
            "Payload для каждого QR берётся из markerId. JSON вручную вводить не нужно.",
            MessageType.Info);

        using (new EditorGUI.DisabledScope(registry == null))
        {
            if (GUILayout.Button("Generate All QR Codes", GUILayout.Height(30f)))
            {
                GenerateAll();
            }
        }
    }

    private void BrowseOutputFolder()
    {
        var currentAbsolutePath = GetAbsoluteFolderPath(outputFolder);
        var selectedPath = EditorUtility.OpenFolderPanel("Select QR Output Folder", currentAbsolutePath, string.Empty);
        if (string.IsNullOrWhiteSpace(selectedPath))
        {
            return;
        }

        var projectPath = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        var normalizedSelectedPath = Path.GetFullPath(selectedPath);
        if (!normalizedSelectedPath.StartsWith(projectPath))
        {
            EditorUtility.DisplayDialog("Invalid Folder", "Please select a folder inside this Unity project.", "OK");
            return;
        }

        outputFolder = normalizedSelectedPath.Replace(projectPath, string.Empty).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        outputFolder = outputFolder.Replace('\\', '/');
    }

    private void GenerateAll()
    {
        if (registry == null)
        {
            EditorUtility.DisplayDialog("QR Generator", "Assign a QrMarkerRegistry first.", "OK");
            return;
        }

        if (!TryEnsureOutputFolder(out var absoluteFolderPath))
        {
            return;
        }

        var generatedCount = 0;
        var skippedCount = 0;

        var definitions = registry.Markers;
        for (var i = 0; i < definitions.Count; i++)
        {
            var definition = definitions[i];
            if (definition == null || string.IsNullOrWhiteSpace(definition.MarkerId))
            {
                skippedCount++;
                continue;
            }

            var fileName = SanitizeFileName(definition.MarkerId) + ".png";
            var filePath = Path.Combine(absoluteFolderPath, fileName);
            if (!overwriteExisting && File.Exists(filePath))
            {
                skippedCount++;
                continue;
            }

            var pngBytes = GenerateQrPng(definition.MarkerId);
            File.WriteAllBytes(filePath, pngBytes);
            generatedCount++;
        }

        AssetDatabase.Refresh();
        EditorUtility.DisplayDialog(
            "QR Generator",
            $"Generated: {generatedCount}\nSkipped: {skippedCount}\nFolder: {outputFolder}",
            "OK");
    }

    private bool TryEnsureOutputFolder(out string absoluteFolderPath)
    {
        absoluteFolderPath = GetAbsoluteFolderPath(outputFolder);
        if (!absoluteFolderPath.StartsWith(Path.GetFullPath(Path.Combine(Application.dataPath, ".."))))
        {
            EditorUtility.DisplayDialog("Invalid Folder", "Output folder must be inside this Unity project.", "OK");
            return false;
        }

        Directory.CreateDirectory(absoluteFolderPath);
        return true;
    }

    private string GetAbsoluteFolderPath(string assetRelativeFolder)
    {
        if (string.IsNullOrWhiteSpace(assetRelativeFolder))
        {
            assetRelativeFolder = DefaultOutputFolder;
        }

        if (Path.IsPathRooted(assetRelativeFolder))
        {
            return assetRelativeFolder;
        }

        var projectPath = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        return Path.GetFullPath(Path.Combine(projectPath, assetRelativeFolder));
    }

    private byte[] GenerateQrPng(string payload)
    {
        var writer = new BarcodeWriterPixelData
        {
            Format = BarcodeFormat.QR_CODE,
            Options = new QrCodeEncodingOptions
            {
                Width = textureSize,
                Height = textureSize,
                Margin = margin,
                CharacterSet = "UTF-8"
            }
        };

        var pixelData = writer.Write(payload);
        var texture = new Texture2D(pixelData.Width, pixelData.Height, TextureFormat.BGRA32, false, false);

        try
        {
            texture.LoadRawTextureData(pixelData.Pixels);
            texture.Apply(false, false);
            return texture.EncodeToPNG();
        }
        finally
        {
            DestroyImmediate(texture);
        }
    }

    private static string SanitizeFileName(string markerId)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = markerId.Trim();

        for (var i = 0; i < invalidChars.Length; i++)
        {
            sanitized = sanitized.Replace(invalidChars[i], '_');
        }

        return string.IsNullOrWhiteSpace(sanitized) ? "marker" : sanitized;
    }
}
