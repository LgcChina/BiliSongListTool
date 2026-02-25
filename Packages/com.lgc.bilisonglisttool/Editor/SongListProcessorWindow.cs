#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Text;
using System.IO;

public class SongListProcessorWindow : EditorWindow
{
    private string playlistName = "歌曲合集名称";
    private string rawText = "";
    private string baseUrl = "https://www.bilibili.com/video/BV1X84y1v7x8";
    private string urlPrefix = "https://biliplayer.91vrchat.com/player/?url=";
    private string generatedJson = "";
    private bool autoRemoveTimestamps = true;
    private bool removeLeadingNumbers = false;
    private bool parseWebSource = false;
    private bool bookmarkFormat = false;

    private Vector2 songListScroll;
    private Vector2 resultScroll;

    [MenuItem("LGC/歌曲列表处理器")]
    public static void ShowWindow()
    {
        var window = GetWindow<SongListProcessorWindow>("歌曲列表处理");
        window.minSize = new Vector2(800, 650);
    }

    void OnGUI()
    {
        // 播放列表设置区域
        EditorGUILayout.BeginVertical(GUI.skin.box);
        GUILayout.Label("播放列表设置", EditorStyles.boldLabel);
        playlistName = EditorGUILayout.TextField("播放列表名称", playlistName);
        EditorGUILayout.EndVertical();

        GUILayout.Space(10);

        // URL设置区域
        EditorGUILayout.BeginVertical(GUI.skin.box);
        GUILayout.Label("URL设置", EditorStyles.boldLabel);
        urlPrefix = EditorGUILayout.TextField("自动添加解析前缀", urlPrefix);
        baseUrl = EditorGUILayout.TextField("合集的第一个视频链接", baseUrl);
        EditorGUILayout.EndVertical();

        GUILayout.Space(10);

        // 处理选项区域
        EditorGUILayout.BeginVertical(GUI.skin.box);
        GUILayout.Label("处理选项", EditorStyles.boldLabel);

        // 未启用源码解析模式时的提示
        if (!parseWebSource)
        {
            EditorGUILayout.HelpBox("当前状态将处理同作者且视频都在合集里的情况，脚本会自动给链接加p=2,3,4...", MessageType.Info);
        }

        EditorGUI.BeginDisabledGroup(parseWebSource);
        {
            // 时间戳选项 - 更新提示信息
            autoRemoveTimestamps = EditorGUILayout.Toggle("自动移除时间戳 (04:00 或 01:23:45)", autoRemoveTimestamps);
            if (autoRemoveTimestamps)
            {
                EditorGUILayout.HelpBox("自动移除歌曲名称前后的时间戳格式（如04:00 或 01:23:45）", MessageType.Info);
            }

            GUILayout.Space(5);

            // 行首序号选项
            removeLeadingNumbers = EditorGUILayout.Toggle("移除行首序号 (001)", removeLeadingNumbers);
            if (removeLeadingNumbers)
            {
                EditorGUILayout.HelpBox("自动移除歌曲名称前的序号（如001）", MessageType.Info);
            }
        }
        EditorGUI.EndDisabledGroup();

        GUILayout.Space(10);
        parseWebSource = EditorGUILayout.Toggle("网页源码解析模式", parseWebSource);

        EditorGUI.BeginDisabledGroup(!parseWebSource);
        {
            bookmarkFormat = EditorGUILayout.Toggle("收藏夹合集格式（不同作者）", bookmarkFormat);

            // 网页源码解析模式的提示
            if (parseWebSource)
            {
                if (bookmarkFormat)
                {
                    EditorGUILayout.HelpBox("此模式用于解析收藏夹合集源码，其中每个视频来自不同作者。", MessageType.Info);
                }
                else
                {
                    EditorGUILayout.HelpBox("此模式用于解析合集源码，每个视频同作者但分开发后组成的合集。", MessageType.Info);
                }
            }
        }
        EditorGUI.EndDisabledGroup();

        EditorGUILayout.EndVertical();

        GUILayout.Space(10);

        // 生成按钮区域
        GUILayout.Label("生成格式", EditorStyles.boldLabel);

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("生成YamaPlayer的JSON", GUILayout.Height(35)))
        {
            GenerateYamaPlayerJson();
        }
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("生成VizVid的JSON", GUILayout.Height(35)))
        {
            GenerateVizVidJson();
        }
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("生成ProTv的文本", GUILayout.Height(35)))
        {
            GenerateProTvText();
        }
        GUILayout.EndHorizontal();

        GUILayout.Space(10);

        // 操作按钮区域 - 调整顺序：清空（左）、复制（中）、保存（右）
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("清空所有", GUILayout.Height(30)))
        {
            ClearAll();
        }

        if (GUILayout.Button("复制到剪贴板", GUILayout.Height(30)))
        {
            CopyToClipboard();
        }

        if (GUILayout.Button("保存到文件", GUILayout.Height(30)))
        {
            SaveToFile();
        }
        EditorGUILayout.EndHorizontal();

        GUILayout.Space(15);

        // 并排显示区域
        EditorGUILayout.BeginHorizontal(GUILayout.ExpandHeight(true));

        // 左侧：输入区域
        EditorGUILayout.BeginVertical(GUILayout.Width(position.width * 0.48f));
        GUILayout.Label(parseWebSource ? "粘贴网页源代码" : "粘贴歌曲列表 (每行一首)", EditorStyles.boldLabel);
        songListScroll = EditorGUILayout.BeginScrollView(songListScroll, GUILayout.ExpandHeight(true));
        rawText = EditorGUILayout.TextArea(rawText, GUILayout.ExpandHeight(true));
        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();

        // 分隔线
        EditorGUILayout.BeginVertical();
        GUILayout.Label("", GUILayout.ExpandHeight(true));
        EditorGUILayout.EndVertical();

        // 右侧：处理结果
        EditorGUILayout.BeginVertical(GUILayout.Width(position.width * 0.48f));
        GUILayout.Label("处理结果", EditorStyles.boldLabel);
        resultScroll = EditorGUILayout.BeginScrollView(resultScroll, GUILayout.ExpandHeight(true));
        generatedJson = EditorGUILayout.TextArea(generatedJson, GUILayout.ExpandHeight(true));
        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();

        EditorGUILayout.EndHorizontal();
    }

    // 清空所有内容的方法
    private void ClearAll()
    {
        // 重置URL设置
        urlPrefix = "https://biliplayer.91vrchat.com/player/?url=";
        baseUrl = "https://www.bilibili.com/video/BV1X84y1v7x8";

        // 清空输入和输出文本
        rawText = "";
        generatedJson = "";

        // 重置通知
        this.RemoveNotification();

        ShowNotification(new GUIContent("所有内容已清空"));
    }

    // 单独的复制到剪贴板方法
    private void CopyToClipboard()
    {
        if (string.IsNullOrEmpty(generatedJson))
        {
            ShowNotification(new GUIContent("没有内容可复制"));
            return;
        }

        GUIUtility.systemCopyBuffer = generatedJson;
        ShowNotification(new GUIContent("内容已复制到剪贴板"));
    }

    private List<SongEntry> ProcessEntries()
    {
        if (string.IsNullOrEmpty(rawText))
        {
            ShowNotification(new GUIContent("没有输入任何内容"));
            return new List<SongEntry>();
        }

        if (parseWebSource)
        {
            return bookmarkFormat ? ParseBookmarkList() : ParseCollectionList();
        }
        else
        {
            return ParseSongTitles();
        }
    }

    private List<SongEntry> ParseSongTitles()
    {
        string[] lines = rawText.Split(new[] { '\n' }, System.StringSplitOptions.RemoveEmptyEntries);
        List<SongEntry> entries = new List<SongEntry>();

        foreach (string line in lines)
        {
            string processedLine = line.Trim();

            if (autoRemoveTimestamps)
            {
                // 增强时间戳处理：支持 mm:ss 和 hh:mm:ss 格式
                // 开头的时间戳（如 "04:00 歌曲名" 或 "01:23:45 歌曲名"）
                processedLine = Regex.Replace(processedLine, @"^\d{1,2}:\d{2}(:\d{2})?\s*", "");

                // 结尾的时间戳（如 "歌曲名 04:00" 或 "歌曲名 01:23:45"）
                processedLine = Regex.Replace(processedLine, @"\s+\d{1,2}:\d{2}(:\d{2})?$", "");

                // 检查整行是否只是一个时间戳（如 "04:00" 或 "01:23:45"）
                if (Regex.IsMatch(processedLine, @"^\d{1,2}:\d{2}(:\d{2})?$"))
                {
                    continue;
                }
            }

            if (removeLeadingNumbers)
            {
                // 移除行首序号（如 "001. 歌曲名"）
                processedLine = Regex.Replace(processedLine, @"^\d{1,4}[.\s]*", "");
            }

            if (!string.IsNullOrWhiteSpace(processedLine))
            {
                entries.Add(new SongEntry
                {
                    Title = processedLine,
                    Url = ""
                });
            }
        }

        if (entries.Count == 0)
        {
            ShowNotification(new GUIContent("未找到有效歌曲标题"));
        }

        return entries;
    }

    private List<SongEntry> ParseCollectionList()
    {
        List<SongEntry> entries = new List<SongEntry>();

        // 正则表达式匹配普通合集格式
        string bvPattern = @"data-key=""(BV[\w\d]{10})""";
        string titlePattern = @"class=""title""[^>]*?title=""([^""]+)""|class=""title-txt""[^>]*>([^<]+)</div>";

        // 提取所有BV号
        MatchCollection bvMatches = Regex.Matches(rawText, bvPattern, RegexOptions.Singleline);

        foreach (Match bvMatch in bvMatches)
        {
            if (!bvMatch.Success) continue;

            string bvId = bvMatch.Groups[1].Value;
            string title = "";

            // 在BV号附近区域查找标题
            int searchStart = bvMatch.Index;
            int searchEnd = Mathf.Min(rawText.Length, searchStart + 500);
            string searchArea = rawText.Substring(searchStart, searchEnd - searchStart);

            // 匹配标题
            Match titleMatch = Regex.Match(searchArea, titlePattern, RegexOptions.Singleline);
            if (titleMatch.Success)
            {
                // 优先使用title属性值
                if (!string.IsNullOrEmpty(titleMatch.Groups[1].Value))
                {
                    title = titleMatch.Groups[1].Value;
                }
                else if (!string.IsNullOrEmpty(titleMatch.Groups[2].Value))
                {
                    title = titleMatch.Groups[2].Value;
                }
            }

            if (string.IsNullOrWhiteSpace(title))
            {
                continue;
            }

            // 清理HTML实体
            title = System.Net.WebUtility.HtmlDecode(title);

            // 构建完整URL
            string url = urlPrefix + "https://www.bilibili.com/video/" + bvId;

            entries.Add(new SongEntry
            {
                Title = title.Trim(),
                Url = url
            });
        }

        if (entries.Count > 0)
        {
            ShowNotification(new GUIContent($"提取了 {entries.Count} 首歌曲 (合集格式)"));
        }
        else
        {
            ShowNotification(new GUIContent("未找到有效视频条目"));
        }

        return entries;
    }

    private List<SongEntry> ParseBookmarkList()
    {
        List<SongEntry> entries = new List<SongEntry>();

        // 修改后的正则表达式，更准确地匹配收藏夹格式
        // 匹配包含data-key的div，然后查找其中的title属性
        string pattern = @"<div[^>]*data-key=""(BV[\w\d]+)""[^>]*>.*?<div[^>]*class=""title""[^>]*title=""([^""]*)""";

        // 使用单行模式匹配跨行内容
        MatchCollection matches = Regex.Matches(rawText, pattern, RegexOptions.Singleline | RegexOptions.IgnoreCase);

        foreach (Match match in matches)
        {
            if (match.Groups.Count >= 3)
            {
                string bvId = match.Groups[1].Value;
                string title = match.Groups[2].Value;

                if (!string.IsNullOrWhiteSpace(title))
                {
                    // 清理HTML实体
                    title = System.Net.WebUtility.HtmlDecode(title);

                    // 构建完整URL
                    string url = urlPrefix + "https://www.bilibili.com/video/" + bvId;

                    entries.Add(new SongEntry
                    {
                        Title = title.Trim(),
                        Url = url
                    });
                }
            }
        }

        // 如果没有匹配到任何内容，尝试备用匹配方式
        if (entries.Count == 0)
        {
            // 备用匹配方式：直接查找BV号和标题
            string fallbackPattern = @"data-key=""(BV[\w\d]+)"".*?title=""([^""]*)""";
            MatchCollection fallbackMatches = Regex.Matches(rawText, fallbackPattern, RegexOptions.Singleline | RegexOptions.IgnoreCase);

            foreach (Match match in fallbackMatches)
            {
                if (match.Groups.Count >= 3)
                {
                    string bvId = match.Groups[1].Value;
                    string title = match.Groups[2].Value;

                    if (!string.IsNullOrWhiteSpace(title) && title.Length > 2) // 确保标题不是太短
                    {
                        title = System.Net.WebUtility.HtmlDecode(title);
                        string url = urlPrefix + "https://www.bilibili.com/video/" + bvId;
                        entries.Add(new SongEntry
                        {
                            Title = title.Trim(),
                            Url = url
                        });
                    }
                }
            }
        }

        if (entries.Count > 0)
        {
            ShowNotification(new GUIContent($"提取了 {entries.Count} 首歌曲 (收藏夹格式)"));
        }
        else
        {
            ShowNotification(new GUIContent("未找到有效视频条目"));
        }

        return entries;
    }

    private void GenerateYamaPlayerJson()
    {
        List<SongEntry> entries = ProcessEntries();
        if (entries.Count == 0) return;

        // 创建YamaPlayer数据结构
        Playlist playlist = new Playlist();
        playlist.Active = true;
        playlist.Name = playlistName;
        playlist.Tracks = new SongData[entries.Count];
        playlist.YoutubeListId = "";
        playlist.IsEdit = false;

        for (int i = 0; i < entries.Count; i++)
        {
            string url = !string.IsNullOrEmpty(entries[i].Url) ?
                entries[i].Url :
                (i == 0 ?
                    urlPrefix + baseUrl :
                    urlPrefix + baseUrl + $"&p={i + 1}");

            playlist.Tracks[i] = new SongData();
            playlist.Tracks[i].Mode = 1;
            playlist.Tracks[i].Title = entries[i].Title;
            playlist.Tracks[i].Url = url;
        }

        // 构建JSON字符串
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("{");
        sb.AppendLine("  \"playlists\": [");
        sb.AppendLine("    {");
        sb.AppendLine($"      \"Active\": {playlist.Active.ToString().ToLower()},");
        sb.AppendLine($"      \"Name\": \"{EscapeJsonString(playlist.Name)}\",");
        sb.AppendLine("      \"Tracks\": [");

        for (int i = 0; i < playlist.Tracks.Length; i++)
        {
            var track = playlist.Tracks[i];
            sb.AppendLine("        {");
            sb.AppendLine($"          \"Mode\": {track.Mode},");
            sb.AppendLine($"          \"Title\": \"{EscapeJsonString(track.Title)}\",");
            sb.AppendLine($"          \"Url\": \"{EscapeJsonString(track.Url)}\"");

            if (i < playlist.Tracks.Length - 1)
                sb.AppendLine("        },");
            else
                sb.AppendLine("        }");
        }

        sb.AppendLine("      ],");
        sb.AppendLine("      \"YoutubeListId\": \"\",");
        sb.AppendLine("      \"IsEdit\": false");
        sb.AppendLine("    }");
        sb.AppendLine("  ]");
        sb.Append("}");

        generatedJson = sb.ToString();
        ShowNotification(new GUIContent($"已生成YamaPlayer格式的{entries.Count}首歌曲"));
    }

    private void GenerateVizVidJson()
    {
        List<SongEntry> entries = ProcessEntries();
        if (entries.Count == 0) return;

        // 创建VizVid数据结构
        VizVidPlaylist playlist = new VizVidPlaylist();
        playlist.title = playlistName;
        playlist.entries = new VizVidEntry[entries.Count];

        for (int i = 0; i < entries.Count; i++)
        {
            string url = !string.IsNullOrEmpty(entries[i].Url) ?
                entries[i].Url :
                (i == 0 ?
                    urlPrefix + baseUrl :
                    urlPrefix + baseUrl + $"&p={i + 1}");

            playlist.entries[i] = new VizVidEntry();
            playlist.entries[i].title = entries[i].Title;
            playlist.entries[i].url = url;
            playlist.entries[i].urlForQuest = "";
            playlist.entries[i].playerIndex = 0;
        }

        // 构建JSON字符串
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("{");
        sb.AppendLine($"  \"title\": \"{EscapeJsonString(playlist.title)}\",");
        sb.AppendLine("  \"entries\": [");

        for (int i = 0; i < playlist.entries.Length; i++)
        {
            var entry = playlist.entries[i];
            sb.AppendLine("    {");
            sb.AppendLine($"      \"title\": \"{EscapeJsonString(entry.title)}\",");
            sb.AppendLine($"      \"url\": \"{EscapeJsonString(entry.url)}\",");
            sb.AppendLine("      \"urlForQuest\": \"\",");
            sb.AppendLine($"      \"playerIndex\": {entry.playerIndex}");

            if (i < playlist.entries.Length - 1)
                sb.AppendLine("    },");
            else
                sb.AppendLine("    }");
        }

        sb.AppendLine("  ]");
        sb.Append("}");

        generatedJson = sb.ToString();
        ShowNotification(new GUIContent($"已生成VizVid格式的{entries.Count}首歌曲"));
    }

    private void GenerateProTvText()
    {
        List<SongEntry> entries = ProcessEntries();
        if (entries.Count == 0) return;

        // 构建ProTv格式文本
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("?@^/#~");
        sb.AppendLine();
        sb.AppendLine(playlistName);
        sb.AppendLine();

        for (int i = 0; i < entries.Count; i++)
        {
            string url = !string.IsNullOrEmpty(entries[i].Url) ?
                entries[i].Url :
                (i == 0 ?
                    urlPrefix + baseUrl :
                    urlPrefix + baseUrl + $"&p={i + 1}");

            sb.AppendLine("@" + url);
            sb.AppendLine("~" + entries[i].Title);
            sb.AppendLine();
        }

        generatedJson = sb.ToString();
        ShowNotification(new GUIContent($"已生成ProTv格式的{entries.Count}首歌曲"));
    }

    private string EscapeJsonString(string input)
    {
        if (string.IsNullOrEmpty(input)) return "";
        return input.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }

    private void SaveToFile()
    {
        if (string.IsNullOrEmpty(generatedJson)) return;

        string extension, filter;

        if (generatedJson.StartsWith("{")) // JSON
        {
            extension = ".json";
            filter = "JSON文件|json";
        }
        else if (generatedJson.StartsWith("?@^/#~")) // ProTv格式
        {
            extension = ".txt"; // 修改为.txt
            filter = "文本文件|txt";
        }
        else // 其他情况
        {
            extension = ".txt";
            filter = "文本文件|txt";
        }

        string path = EditorUtility.SaveFilePanel(
            "保存播放列表",
            Application.dataPath,
            $"{playlistName.Replace("/", "-").Replace("\\", "-").Replace(":", "-")}{extension}",
            filter);

        if (!string.IsNullOrEmpty(path))
        {
            File.WriteAllText(path, generatedJson);
            AssetDatabase.Refresh();
            ShowNotification(new GUIContent($"播放列表已保存:\n{path}"));
        }
    }

    private void ShowNotification(GUIContent message)
    {
        ShowNotification(message, 2);
    }

    // 歌曲条目结构
    private struct SongEntry
    {
        public string Title;
        public string Url;
    }

    // YamaPlayer 数据模型
    [System.Serializable]
    public class SongData
    {
        public int Mode = 1;
        public string Title;
        public string Url;
    }

    [System.Serializable]
    public class Playlist
    {
        public bool Active = true;
        public string Name;
        public SongData[] Tracks;
        public string YoutubeListId = "";
        public bool IsEdit = false;
    }

    // VizVid 数据模型
    [System.Serializable]
    public class VizVidEntry
    {
        public string title;
        public string url;
        public string urlForQuest;
        public int playerIndex;
    }

    [System.Serializable]
    public class VizVidPlaylist
    {
        public string title;
        public VizVidEntry[] entries;
    }
}
#endif
