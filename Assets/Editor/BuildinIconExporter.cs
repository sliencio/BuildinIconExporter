using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;
using UnityEditor.UIElements;

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
        public static void GenerateReadme()
        {
            try
            {
                EditorUtility.DisplayProgressBar("Generate README.md", "Generating...", 0.0f);
                var data = GetGenerateData();
                DoGenerateReadme(data);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        [MenuItem("Tools/Built-in Icons Viewer", priority = -999)]
        private static void ShowBuiltInIconsViewer()
        {
            var wnd = EditorWindow.GetWindow<BuiltInIconsWindow>();
            wnd.titleContent = new GUIContent("Built-in Icons");
            wnd.Show();
        }

        private static Dictionary<string, IconGroup> GetGenerateData()
        {
            Dictionary<string, IconGroup> iconDic = new Dictionary<string, IconGroup>();
            var editorAssetBundle = GetEditorAssetBundle();
            var iconsPath = GetIconsPath();


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

            return iconDic;
        }


        private static void DoGenerateReadme(Dictionary<string, IconGroup> iconDic)
        {
            var readmeContents = new StringBuilder();

            readmeContents.AppendLine($"Unity Editor Built-in Icons");
            readmeContents.AppendLine($"==============================");
            readmeContents.AppendLine($"Unity version: {Application.unityVersion}");
            readmeContents.AppendLine($"Icons can load using `EditorGUIUtility.IconContent(\"xxxx\").image`");


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

            var pathRoot = Path.Combine(GetDesktopPath(), "UnityBuildinIcon");
            File.WriteAllText(pathRoot + "/README.md", readmeContents.ToString());
        }

        public static IEnumerable<string> EnumerateIcons(AssetBundle editorAssetBundle, string iconsPath)
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

        public static AssetBundle GetEditorAssetBundle()
        {
            var editorGUIUtility = typeof(EditorGUIUtility);
            var getEditorAssetBundle = editorGUIUtility.GetMethod(
                "GetEditorAssetBundle",
                BindingFlags.NonPublic | BindingFlags.Static);

            return (AssetBundle)getEditorAssetBundle?.Invoke(null, new object[] { });
        }

        public static string GetIconsPath()
        {
            return UnityEditor.Experimental.EditorResources.iconsPath;
        }

        public static string GetDesktopPath()
        {
            return Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        }
    }

    public class BuiltInIconsWindow : EditorWindow
    {
        
        public class IconGroupUI
        {
            public List<Texture2D> Textures = new List<Texture2D>();
            public List<string> Names = new List<string>();
        }
        
        private Dictionary<string, IconGroupUI> _data;
        private ScrollView _scroll;
        private ToolbarSearchField _search;
        private Label _info;

        private void OnEnable()
        {
            var root = rootVisualElement;
            root.style.flexDirection = FlexDirection.Column;
            root.style.flexGrow = 1;

            var toolbar = new Toolbar();

            _search = new ToolbarSearchField();
            _search.style.flexGrow = 1;
            _search.RegisterValueChangedCallback(_ => Rebuild());
            toolbar.Add(_search);

            var btnRefresh = new ToolbarButton(Refresh) { text = "Refresh" };
            toolbar.Add(btnRefresh);

            var btnReadme = new ToolbarButton(() => BuildinIconExporter.GenerateReadme()) { text = "Generate README.md" };
            toolbar.Add(btnReadme);

            root.Add(toolbar);

            _info = new Label();
            _info.style.marginLeft = 4;
            _info.style.marginTop = 4;
            root.Add(_info);

            _scroll = new ScrollView(ScrollViewMode.Vertical);
            _scroll.style.flexGrow = 1;
            root.Add(_scroll);

            Refresh();
        }

        private void Refresh()
        {
            EditorUtility.DisplayProgressBar("Built-in Icons Viewer", "Loading icons...", 0f);
            try
            {
                _data = GetUIData();
                _info.text = $"Unity Editor Built-in Icons  ·  Unity {Application.unityVersion}  ·  Icon Groups: {_data.Count}";
                Rebuild();
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        private void Rebuild()
        {
            if (_data == null) return;

            _scroll.Clear();

            string filter = _search?.value?.Trim();
            IEnumerable<KeyValuePair<string, IconGroupUI>> items = _data;

            if (!string.IsNullOrEmpty(filter))
            {
                var f = filter.ToLowerInvariant();
                items = items.Where(p =>
                    p.Key.ToLowerInvariant().Contains(f) ||
                    p.Value.Names.Any(n => n.ToLowerInvariant().Contains(f)));
            }

            var gridColor = EditorGUIUtility.isProSkin ? new Color(1f, 1f, 1f, 0.12f) : new Color(0f, 0f, 0f, 0.2f);

            // 表格容器（整体边框）
            var table = new VisualElement();
            table.style.flexDirection = FlexDirection.Column;
            table.style.flexGrow = 1;
            table.style.borderTopWidth = 1;
            table.style.borderBottomWidth = 1;
            table.style.borderLeftWidth = 1;
            table.style.borderRightWidth = 1;
            table.style.borderTopColor = gridColor;
            table.style.borderBottomColor = gridColor;
            table.style.borderLeftColor = gridColor;
            table.style.borderRightColor = gridColor;

            // 表头
            var header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.borderBottomWidth = 1;
            header.style.borderBottomColor = gridColor;

            var hIcon = new Label("Icon");
            hIcon.style.unityFontStyleAndWeight = FontStyle.Bold;
            hIcon.style.flexBasis = new StyleLength(Length.Percent(50));
            hIcon.style.flexGrow = 0;
            hIcon.style.flexShrink = 0;
            hIcon.style.paddingLeft = 8;
            hIcon.style.paddingTop = 4;
            hIcon.style.paddingBottom = 4;
            hIcon.style.borderRightWidth = 1;
            hIcon.style.borderRightColor = gridColor;

            var hName = new Label("Name");
            hName.style.unityFontStyleAndWeight = FontStyle.Bold;
            hName.style.flexBasis = new StyleLength(Length.Percent(50));
            hName.style.flexGrow = 0;
            hName.style.flexShrink = 0;
            hName.style.paddingLeft = 8;
            hName.style.paddingTop = 4;
            hName.style.paddingBottom = 4;

            header.Add(hIcon);
            header.Add(hName);
            table.Add(header);

            foreach (var kv in items.OrderBy(k => k.Key))
            {
                // 行
                var row = new VisualElement();
                row.style.flexDirection = FlexDirection.Row;
                row.style.alignItems = Align.FlexStart;
                row.style.borderBottomWidth = 1;         // 横向网格线
                row.style.borderBottomColor = gridColor;

                // 左列：图片（50%），内容水平居中
                var iconsCol = new VisualElement();
                iconsCol.style.flexBasis = new StyleLength(Length.Percent(50));
                iconsCol.style.flexGrow = 0;
                iconsCol.style.flexShrink = 0;
                iconsCol.style.paddingLeft = 8;
                iconsCol.style.paddingRight = 8;
                iconsCol.style.paddingTop = 6;
                iconsCol.style.paddingBottom = 6;
                iconsCol.style.borderRightWidth = 1;     // 纵向分隔线
                iconsCol.style.borderRightColor = gridColor;

                var iconsContainer = new VisualElement();
                iconsContainer.style.flexDirection = FlexDirection.Row;
                iconsContainer.style.flexWrap = Wrap.Wrap;
                iconsContainer.style.justifyContent = Justify.Center; // 水平居中
                iconsContainer.style.alignItems = Align.Center;       // 垂直居中
                iconsContainer.style.width = Length.Percent(100);     // 让居中生效占满列宽

                foreach (var tex in kv.Value.Textures)
                {
                    if (tex == null) continue;
                    var img = new Image
                    {
                        image = tex,
                        scaleMode = ScaleMode.ScaleToFit,
                        tooltip = tex.name
                    };
                    img.style.maxWidth = 32;
                    img.style.maxHeight = 32;
                    img.style.marginRight = 6;
                    img.style.marginBottom = 6;
                    iconsContainer.Add(img);
                }
                iconsCol.Add(iconsContainer);

                // 右列：名字（50%）
                var namesCol = new VisualElement();
                namesCol.style.flexBasis = new StyleLength(Length.Percent(50));
                namesCol.style.flexGrow = 0;
                namesCol.style.flexShrink = 0;
                namesCol.style.paddingLeft = 8;
                namesCol.style.paddingRight = 8;
                namesCol.style.paddingTop = 8;
                namesCol.style.paddingBottom = 8;

                var names = string.Join("\n", kv.Value.Names);
                var namesLabel = new Label(names);
                namesLabel.style.whiteSpace = WhiteSpace.Normal; // 一行一个
                namesLabel.style.marginTop = 0;
                namesLabel.style.flexGrow = 1;
                namesCol.Add(namesLabel);

                row.Add(iconsCol);
                row.Add(namesCol);

                table.Add(row);
            }

            _scroll.Add(table);
        }

        private static Dictionary<string, IconGroupUI> GetUIData()
        {
            var iconDic = new Dictionary<string, IconGroupUI>();
            var editorAssetBundle = BuildinIconExporter.GetEditorAssetBundle();
            var iconsPath = BuildinIconExporter.GetIconsPath();

            var assetNames = BuildinIconExporter.EnumerateIcons(editorAssetBundle, iconsPath);
            foreach (var assetName in assetNames)
            {
                var icon = editorAssetBundle.LoadAsset<Texture2D>(assetName);
                if (icon == null)
                    continue;

                // 与 README 生成一致：去除 @2x/@4x 等分辨率后缀，进行分组
                var normalIconName = Regex.Replace(icon.name, @"@\d+x$", "");

                if (!iconDic.TryGetValue(normalIconName, out var group))
                {
                    group = new IconGroupUI();
                    iconDic[normalIconName] = group;
                }

                group.Textures.Add(icon);
                group.Names.Add(icon.name);
            }

            // 稳定化展示顺序：组内按名字、整体按组名
            foreach (var g in iconDic.Values)
            {
                var zipped = g.Names.Zip(g.Textures, (n, t) => new { n, t })
                    .OrderBy(x => x.n, StringComparer.OrdinalIgnoreCase)
                    .ToList();
                g.Names = zipped.Select(x => x.n).ToList();
                g.Textures = zipped.Select(x => x.t).ToList();
            }

            return iconDic;
        }
    }
}