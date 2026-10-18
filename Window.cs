using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx;
using BepInEx.Configuration;
using Il2CppInterop.Runtime.Attributes;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using static ModMenu.Ui;

namespace ModMenu;

public class Window : MonoBehaviour
{
    public Window(IntPtr ptr) : base(ptr) { }

    const string RestartNote = "Some settings may require a game restart to take effect.";
    const string FilesNote = "These files are not loaded by any running mod right now. Changes take effect after a restart, and a mod that keeps the file open may overwrite them.";
    const string FilesKey = "\u0001files";

    GameObject root;
    RectTransform list, pane;
    Discovery discovery;
    string selected;
    Action<KeyCode> capture;
    readonly List<(string Key, Image Bg, TextMeshProUGUI Text)> items = new();

    public bool IsOpen => root != null && root.activeSelf;

    public void Open()
    {
        if (root == null) Build();
        root.SetActive(true);
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        Refresh();
    }

    public void Close()
    {
        capture = null;
        if (root != null) root.SetActive(false);
    }

    [HideFromIl2Cpp]
    public void CaptureKey(Action<KeyCode> done) => capture = done;

    void OnGUI()
    {
        if (!IsOpen) return;
        var e = Event.current;
        if (e.type != EventType.KeyDown || e.keyCode == KeyCode.None) return;
        if (capture == null && EditingText()) return;
        e.Use();
        if (capture != null)
        {
            var done = capture;
            capture = null;
            done(e.keyCode == KeyCode.Escape ? KeyCode.None : e.keyCode);
        }
        else if (e.keyCode == KeyCode.Escape)
        {
            Close();
        }
    }

    static bool EditingText()
    {
        var focused = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        var input = focused != null ? focused.GetComponent<TMP_InputField>() : null;
        return input != null && input.isFocused;
    }

    void Build()
    {
        root = new GameObject("ModMenuWindow");
        DontDestroyOnLoad(root);
        var canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32000;
        var scaler = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        root.AddComponent<GraphicRaycaster>();

        var backdrop = Box("Backdrop", root.transform, Zero, One, Zero, Zero, new Color(0, 0, 0, 0.72f));
        var border = Box("Border", backdrop, Center, Center, new(-650, -410), new(650, 410), Accent);
        var panel = Box("Panel", border, Zero, One, new(2, 2), new(-2, -2), PanelBg);

        Label(Node("Title", panel, new(0, 1), One, new(28, -66), new(-240, -18)), "Mods", 38, TextAlignmentOptions.Left, Accent)
            .fontStyle = FontStyles.Bold;
        Label(Node("Note", panel, new(0, 1), One, new(28, -100), new(-240, -66)), RestartNote, 18, TextAlignmentOptions.Left, Muted);

        var close = Box("Close", panel, One, One, new(-200, -64), new(-28, -20), ControlBg);
        Label(Node("Text", close, Zero, One, Zero, Zero), "Close  [Esc]", 20, TextAlignmentOptions.Center, Color.white);
        Button(close, close.GetComponent<Image>(), Close);

        Box("Divider", panel, new(0, 1), One, new(28, -114), new(-28, -112), new(1, 1, 1, 0.08f));

        Scroll(Node("Mods", panel, Zero, new(0, 1), new(28, 24), new(328, -126)), out list);
        pane = Node("Pane", panel, Zero, One, new(348, 24), new(-28, -126));
    }

    void Refresh()
    {
        Clear(list);
        items.Clear();
        try
        {
            discovery = ConfigSources.Discover(Plugin.LoadedPlugins(), ConfigFile.CoreConfig, Paths.ConfigPath, w => Plugin.Logger.LogWarning(w));
        }
        catch (Exception e)
        {
            Plugin.Logger.LogError($"Cannot list mod configs: {e}");
            discovery = new Discovery { Mods = new List<ModConfig>(), OrphanFiles = new List<string>() };
            Clear(pane);
            Label(Node("Error", pane, Zero, One, Zero, Zero), $"Cannot list mod configs: {e.Message}\nSee BepInEx/LogOutput.log for details.", 20, TextAlignmentOptions.Center, ErrorText);
            return;
        }
        Plugin.Logger.LogDebug($"Configs: {string.Join(", ", discovery.Mods.Select(m => $"{m.Title} [{m.File.ConfigFilePath}]"))}; other files: {discovery.OrphanFiles.Count}");

        foreach (var mod in discovery.Mods)
            AddItem(mod.Title, string.IsNullOrEmpty(mod.Version) ? mod.Title : $"{mod.Title}  <color=#9a9aa6><size=75%>v{mod.Version}</size></color>");
        if (discovery.OrphanFiles.Count > 0) AddItem(FilesKey, "Files…");

        if (selected == FilesKey && discovery.OrphanFiles.Count > 0) ShowFiles();
        else if (Find(discovery, selected) is { } mod) ShowMod(mod);
        else ShowEmpty();
    }

    void AddItem(string key, string text)
    {
        var item = Box("Item", list, Zero, Zero, Zero, Zero, ControlBg);
        Height(item, 44);
        var label = Label(Node("Text", item, Zero, One, new(14, 0), new(-10, 0)), text, 20, TextAlignmentOptions.Left, Color.white);
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.overflowMode = TextOverflowModes.Ellipsis;
        var bg = item.GetComponent<Image>();
        Button(item, bg, () => Select(key));
        items.Add((key, bg, label));
    }

    void Select(string key)
    {
        if (key == FilesKey) ShowFiles();
        else if (Find(discovery, key) is { } mod) ShowMod(mod);
    }

    void Highlight(string key)
    {
        selected = key;
        foreach (var (k, bg, text) in items)
        {
            bg.color = k == key ? Accent : ControlBg;
            text.color = k == key ? PanelBg : Color.white;
        }
    }

    RectTransform PaneHeader(string title)
    {
        capture = null;
        Clear(pane);
        Label(Node("Header", pane, new(0, 1), One, new(4, -40), new(0, 0)), title, 26, TextAlignmentOptions.Left, Color.white)
            .fontStyle = FontStyles.Bold;
        return Node("Body", pane, Zero, One, Zero, new(0, -48));
    }

    void ShowEmpty()
    {
        Highlight(null);
        Clear(pane);
        Label(Node("Empty", pane, Zero, One, Zero, Zero), "Select a mod on the left", 22, TextAlignmentOptions.Center, Muted);
    }

    [HideFromIl2Cpp]
    public void ShowMod(ModConfig mod)
    {
        Highlight(mod.Title);
        var settings = mod.File.Values
            .Select(e => (Setting)new LiveSetting(e))
            .OrderBy(s => s.Section, StringComparer.Ordinal);
        ShowSettings(PaneHeader(mod.Title), settings);
    }

    [HideFromIl2Cpp]
    void ShowSettings(RectTransform body, IEnumerable<Setting> settings)
    {
        Scroll(body, out var content);
        string section = null;
        foreach (var setting in settings)
        {
            if (setting.Section != section)
            {
                section = setting.Section;
                var title = Label(Node("Section", content, Zero, Zero, Zero, Zero), $"[{section}]", 18, TextAlignmentOptions.BottomLeft, Accent);
                title.fontStyle = FontStyles.Bold;
                Height(title.rectTransform, 34);
            }
            try
            {
                SettingRows.Build(content, setting, this);
            }
            catch (Exception e)
            {
                Plugin.Logger.LogError($"Cannot show {setting.Section}.{setting.Key}: {e}");
                Label(Node("Broken", content, Zero, Zero, Zero, Zero), $"{setting.Key}: cannot be shown here ({e.GetType().Name})", 16, TextAlignmentOptions.Left, ErrorText);
            }
        }
    }

    public void ShowFiles()
    {
        Highlight(FilesKey);
        Scroll(PaneHeader("Files…"), out var content);
        Label(Node("Note", content, Zero, Zero, Zero, Zero), FilesNote, 17, TextAlignmentOptions.TopLeft, Muted);
        foreach (var path in discovery.OrphanFiles)
        {
            var item = Box("File", content, Zero, Zero, Zero, Zero, ControlBg);
            Height(item, 44);
            Label(Node("Text", item, Zero, One, new(14, 0), new(-10, 0)), Relative(path), 19, TextAlignmentOptions.Left, Color.white)
                .textWrappingMode = TextWrappingModes.NoWrap;
            Button(item, item.GetComponent<Image>(), () => ShowFile(path));
        }
    }

    public void ShowFile(string path)
    {
        var body = PaneHeader(Relative(path));
        var back = Box("Back", body, new(0, 1), new(0, 1), new(0, -40), new(120, 0), ControlBg);
        Label(Node("Text", back, Zero, One, Zero, Zero), "< Back", 18, TextAlignmentOptions.Center, Color.white);
        Button(back, back.GetComponent<Image>(), ShowFiles);
        var area = Node("Area", body, Zero, One, Zero, new(0, -52));

        string text;
        try
        {
            if (!SafeFile.IsEditableText(path, out var reason))
            {
                Label(Node("Error", area, Zero, One, Zero, Zero), reason, 18, TextAlignmentOptions.TopLeft, ErrorText);
                return;
            }
            text = File.ReadAllText(path);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Plugin.Logger.LogError($"Cannot read {path}: {e.Message}");
            Label(Node("Error", area, Zero, One, Zero, Zero), $"Cannot read the file: {e.Message}", 18, TextAlignmentOptions.TopLeft, ErrorText);
            return;
        }

        var doc = CfgDocument.Parse(text);
        if (doc.IsBepInExFormat)
        {
            ShowSettings(area, doc.Entries.Select(e => (Setting)new FileSetting(doc, e, t => SafeFile.Write(path, t))));
            return;
        }

        var editor = Input(Node("Editor", area, Zero, One, new(0, 56), Zero), text, true, null);
        editor.restoreOriginalTextOnEscape = false;
        var save = Box("Save", area, Zero, Zero, Zero, new(140, 44), Accent);
        Label(Node("Text", save, Zero, One, Zero, Zero), "Save", 20, TextAlignmentOptions.Center, PanelBg).fontStyle = FontStyles.Bold;
        var status = Label(Node("Status", area, Zero, new(1, 0), new(156, 0), new(0, 44)), "", 17, TextAlignmentOptions.Left, Muted);
        Button(save, save.GetComponent<Image>(), () =>
        {
            try
            {
                SafeFile.Write(path, editor.text);
                status.color = Muted;
                status.text = $"Saved {DateTime.Now:HH:mm:ss}";
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                Plugin.Logger.LogError($"Cannot save {path}: {e.Message}");
                status.color = ErrorText;
                status.text = $"Cannot save: {e.Message}";
            }
        });
    }

    static ModConfig Find(Discovery d, string title) => d.Mods.FirstOrDefault(m => m.Title == title);

    static string Relative(string path) => System.IO.Path.GetRelativePath(Paths.ConfigPath, path);
}
