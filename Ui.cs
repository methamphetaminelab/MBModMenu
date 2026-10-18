using System;
using Il2CppInterop.Runtime;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ModMenu;

static class Ui
{
    public static readonly Color Accent = new(1f, 0.8f, 0.32f);
    public static readonly Color PanelBg = new(0.075f, 0.075f, 0.1f, 0.98f);
    public static readonly Color RowBg = new(0.13f, 0.13f, 0.17f);
    public static readonly Color ControlBg = new(0.22f, 0.22f, 0.28f);
    public static readonly Color FieldBg = new(0.05f, 0.05f, 0.07f);
    public static readonly Color ErrorBg = new(0.45f, 0.08f, 0.1f);
    public static readonly Color ErrorText = new(1f, 0.45f, 0.45f);
    public static readonly Color Muted = new(0.62f, 0.62f, 0.68f);
    public static readonly Vector2 Zero = Vector2.zero, One = Vector2.one, Center = new(0.5f, 0.5f);

    static TMP_FontAsset font;

    public static TMP_FontAsset Font
    {
        get
        {
            if (font != null) return font;
            TMP_FontAsset any = null;
            foreach (var o in Object.FindObjectsOfType(Il2CppType.Of<TextMeshProUGUI>()))
            {
                var f = o.Cast<TextMeshProUGUI>().font;
                if (f == null) continue;
                any ??= f;
                if (f.HasCharacter('Ж', true)) return font = f;
            }
            return font = any ?? TMP_Settings.defaultFontAsset;
        }
    }

    public static RectTransform Node(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        var rect = new GameObject(name).AddComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
        return rect;
    }

    public static RectTransform Box(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax, Color color)
    {
        var rect = Node(name, parent, anchorMin, anchorMax, offsetMin, offsetMax);
        rect.gameObject.AddComponent<Image>().color = color;
        return rect;
    }

    public static TextMeshProUGUI Label(RectTransform rect, string text, float size, TextAlignmentOptions align, Color color)
    {
        var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
        if (Font != null) label.font = Font;
        label.text = text;
        label.fontSize = size;
        label.alignment = align;
        label.color = color;
        label.textWrappingMode = TextWrappingModes.Normal;
        label.raycastTarget = false;
        return label;
    }

    public static Button Button(RectTransform rect, Graphic target, Action onClick)
    {
        var button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = target;
        var colors = button.colors;
        colors.normalColor = colors.selectedColor = new Color(0.85f, 0.85f, 0.85f);
        colors.highlightedColor = Color.white;
        colors.pressedColor = new Color(0.65f, 0.65f, 0.65f);
        colors.fadeDuration = 0.08f;
        button.colors = colors;
        button.onClick.AddListener(onClick);
        return button;
    }

    public static TMP_InputField Input(RectTransform rect, string text, bool multiline, Action<string> onEndEdit)
    {
        rect.gameObject.SetActive(false);
        var bg = rect.gameObject.AddComponent<Image>();
        bg.color = FieldBg;
        var area = Node("Text Area", rect, Zero, One, new(8, 4), new(-8, -4));
        area.gameObject.AddComponent<RectMask2D>();
        var label = Label(Node("Text", area, Zero, One, Zero, Zero), "", 17, multiline ? TextAlignmentOptions.TopLeft : TextAlignmentOptions.Left, Color.white);
        label.textWrappingMode = multiline ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
        label.richText = false;

        var input = rect.gameObject.AddComponent<TMP_InputField>();
        input.textViewport = area;
        input.textComponent = label;
        input.targetGraphic = bg;
        if (Font != null) input.fontAsset = Font;
        input.pointSize = 17;
        input.richText = false;
        input.lineType = multiline ? TMP_InputField.LineType.MultiLineNewline : TMP_InputField.LineType.SingleLine;
        input.caretColor = Accent;
        input.selectionColor = new Color(Accent.r, Accent.g, Accent.b, 0.35f);
        input.scrollSensitivity = 40;
        input.text = text ?? "";
        if (onEndEdit != null) input.onEndEdit.AddListener(onEndEdit);
        rect.gameObject.SetActive(true);
        return input;
    }

    public static Slider Slider(RectTransform rect, float min, float max, bool whole, float value, Action<float> onChange)
    {
        rect.gameObject.SetActive(false);
        Box("Background", rect, new(0, 0.4f), new(1, 0.6f), Zero, Zero, ControlBg);
        var fillArea = Node("Fill Area", rect, new(0, 0.4f), new(1, 0.6f), Zero, Zero);
        var fill = Box("Fill", fillArea, Zero, One, Zero, Zero, Accent);
        var handleArea = Node("Handle Slide Area", rect, Zero, One, new(8, 0), new(-8, 0));
        var handle = Box("Handle", handleArea, Zero, new(0, 1), new(-8, 2), new(8, -2), Color.white);

        var slider = rect.gameObject.AddComponent<Slider>();
        slider.fillRect = fill;
        slider.handleRect = handle;
        slider.targetGraphic = handle.GetComponent<Image>();
        slider.direction = UnityEngine.UI.Slider.Direction.LeftToRight;
        slider.minValue = min;
        slider.maxValue = max;
        slider.wholeNumbers = whole;
        slider.SetValueWithoutNotify(value);
        slider.onValueChanged.AddListener(onChange);
        rect.gameObject.SetActive(true);
        return slider;
    }

    public static ScrollRect Scroll(RectTransform area, out RectTransform content)
    {
        var viewport = Box("Viewport", area, Zero, One, Zero, new(-12, 0), new(0, 0, 0, 0.2f));
        viewport.gameObject.AddComponent<RectMask2D>();
        content = Node("Content", viewport, new(0, 1), One, Zero, Zero);
        content.pivot = new Vector2(0.5f, 1);
        var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 6;
        layout.padding.left = layout.padding.right = layout.padding.top = layout.padding.bottom = 6;
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var bar = Box("Scrollbar", area, new(1, 0), One, new(-6, 0), Zero, new(1, 1, 1, 0.06f));
        var handle = Box("Handle", bar, Zero, One, Zero, Zero, new(Accent.r, Accent.g, Accent.b, 0.85f));
        var scrollbar = bar.gameObject.AddComponent<Scrollbar>();
        scrollbar.handleRect = handle;
        scrollbar.targetGraphic = handle.GetComponent<Image>();
        scrollbar.direction = Scrollbar.Direction.BottomToTop;

        var scroll = area.gameObject.AddComponent<ScrollRect>();
        scroll.content = content;
        scroll.viewport = viewport;
        scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 40;
        scroll.verticalScrollbar = scrollbar;
        return scroll;
    }

    public static LayoutElement Height(RectTransform rect, float height)
    {
        var element = rect.gameObject.AddComponent<LayoutElement>();
        element.minHeight = element.preferredHeight = height;
        return element;
    }

    public static void Clear(Transform parent)
    {
        for (var i = parent.childCount - 1; i >= 0; i--) Object.DestroyImmediate(parent.GetChild(i).gameObject);
    }
}
