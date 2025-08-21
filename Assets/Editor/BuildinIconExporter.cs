using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEditor;

namespace EditorTools
{
    public class BuildinIconExporter
    {
        public class IconGroup
        {
            public List<string> IconNameList = new List<string>();
            public List<string> IconPathList = new List<string>();
        }

        [MenuItem("Tools/Generate README.md", priority = -1000)]
        private static void GenerateReadme()
        {
            EditorUtility.DisplayProgressBar("Generate README.md", "Generating...", 0.0f);
            try
            {
                var editorAssetBundle = GetEditorAssetBundle();
                var iconsPath = GetIconsPath();
                var readmeContents = new StringBuilder();

                readmeContents.AppendLine($"Unity Editor Built-in Icons");
                readmeContents.AppendLine($"==============================");
                readmeContents.AppendLine($"Unity version: {Application.unityVersion}");
                readmeContents.AppendLine($"Icons can load using `EditorGUIUtility.IconContent(\"xxxx\").image`");


                Dictionary<string, IconGroup> iconDic = new Dictionary<string, IconGroup>();
                var desktopPath = GetDesktopPath();
                var pathRoot = Path.Combine(desktopPath, "UnityBuildinIcon");

                var assetNames = EnumerateIcons(editorAssetBundle, iconsPath).ToArray();
                for (var i = 0; i < assetNames.Length; i++)
                {
                    var assetName = assetNames[i];
                    var icon = editorAssetBundle.LoadAsset<Texture2D>(assetName);
                    if (icon == null)
                        continue;

                    EditorUtility.DisplayProgressBar("Generate README.md",
                        $"Generating... ({i + 1}/{assetNames.Length})",
                        (float)i / assetNames.Length);

                    var readableTexture = new Texture2D(icon.width, icon.height, icon.format, icon.mipmapCount > 1);

                    Graphics.CopyTexture(icon, readableTexture);

                    var folderPath =
                        Path.GetDirectoryName(Path.Combine(pathRoot, "icons", assetName.Substring(iconsPath.Length)));
                    var relativeFolderPath =
                        Path.GetDirectoryName(Path.Combine("icons", assetName.Substring(iconsPath.Length)));
                    if (Directory.Exists(folderPath) == false)
                        Directory.CreateDirectory(folderPath);

                    var iconPath = Path.Combine(folderPath, icon.name + ".png");

                    Texture2D srcTex = readableTexture; // 原始贴图
                    Texture2D readableTex = new Texture2D(srcTex.width, srcTex.height, TextureFormat.RGBA32, false);
                    readableTex.SetPixels(srcTex.GetPixels());
                    readableTex.Apply();

                    var png = readableTex.EncodeToPNG();
                    File.WriteAllBytes(iconPath, png);

                    // 先移除分辨率后缀
                    var normalIconName = Regex.Replace(icon.name, @"@\d+x$", "");
                    ;

                    if (!iconDic.ContainsKey(normalIconName))
                    {
                        iconDic.Add(normalIconName, new IconGroup());
                    }

                    iconDic[normalIconName].IconNameList.Add(icon.name);
                    iconDic[normalIconName].IconPathList.Add(relativeFolderPath);
                }

                readmeContents.AppendLine($"Icon Count: {iconDic.Count}");
                readmeContents.AppendLine();
                readmeContents.AppendLine($"| Icon | Name |");
                readmeContents.AppendLine($"|------|------|");

                foreach (var pair in iconDic.Values)
                {
                    var iconPaths = "";
                    var iconNames = "";
                    for (int i = 0; i < pair.IconNameList.Count; i++)
                    {
                        var tempIconPath = pair.IconPathList[i].Replace(" ", "%20").Replace('\\', '/');
                        var tempIconName = pair.IconNameList[i];
                        var escapedUrl = $"{tempIconPath}/{tempIconName}.png";
                        iconPaths += $" ![]({escapedUrl})";
                        if (!string.IsNullOrEmpty(iconNames))
                        {
                            iconNames += "<br/>";
                        }

                        iconNames += $"`{tempIconName}`";
                    }

                    readmeContents.AppendLine($"|{iconPaths} | {iconNames}|");
                }

                File.WriteAllText(pathRoot + "/README.md", readmeContents.ToString());
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        private static IEnumerable<string> EnumerateIcons(AssetBundle editorAssetBundle, string iconsPath)
        {
            foreach (var assetName in editorAssetBundle.GetAllAssetNames())
            {
                if (assetName.StartsWith(iconsPath, StringComparison.OrdinalIgnoreCase) == false)
                    continue;
                if (assetName.EndsWith(".png", StringComparison.OrdinalIgnoreCase) == false &&
                    assetName.EndsWith(".asset", StringComparison.OrdinalIgnoreCase) == false)
                    continue;

                yield return assetName;
            }
        }

        private static AssetBundle GetEditorAssetBundle()
        {
            var editorGUIUtility = typeof(EditorGUIUtility);
            var getEditorAssetBundle = editorGUIUtility.GetMethod(
                "GetEditorAssetBundle",
                BindingFlags.NonPublic | BindingFlags.Static);

            return (AssetBundle)getEditorAssetBundle?.Invoke(null, new object[] { });
        }

        private static string GetIconsPath()
        {
            return UnityEditor.Experimental.EditorResources.iconsPath;
        }

        public static string GetDesktopPath()
        {
            return Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        }
    }
}