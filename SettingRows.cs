using System;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static ModMenu.Ui;

namespace ModMenu;

static class SettingRows
{
    static readonly Type[] WholeTypes = { typeof(int), typeof(long), typeof(short), typeof(byte), typeof(sbyte), typeof(uint), typeof(ulong), typeof(ushort) };

    public static RectTransform Build(Transform parent, Setting setting, Window window)
    {
        var row = Box("Row", parent, Zero, Zero, Zero, Zero, RowBg);
        var layout = row.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.padding.left = layout.padding.right = 12;
        layout.padding.top = layout.padding.bottom = 8;
        layout.spacing = 4;
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        var header = Node("Header", row, Zero, Zero, Zero, Zero);
        var multiline = (setting.ValueType == null || setting.ValueType == typeof(string)) && setting.Value.Contains('\n');
        Height(header, multiline ? 120 : 40);
        var key = Label(Node("Key", header, Zero, new(0.38f, 1), Zero, new(-8, 0)), setting.Key, 20, TextAlignmentOptions.Left, Color.white);
        key.textWrappingMode = TextWrappingModes.NoWrap;
        key.overflowMode = TextOverflowModes.Ellipsis;
        var control = Node("Control", header, new(0.38f, 0), One, Zero, new(-86, 0));
        var reset = Box("Reset", header, new(1, 0.5f), new(1, 0.5f), new(-76, -16), new(0, 16), ControlBg);
        Label(Node("Text", reset, Zero, One, Zero, Zero), "Reset", 15, TextAlignmentOptions.Center, Color.white);

        var error = Label(Node("Error", row, Zero, Zero, Zero, Zero), "", 15, TextAlignmentOptions.Left, ErrorText);
        error.gameObject.SetActive(false);
        if (!string.IsNullOrWhiteSpace(setting.Description))
            Label(Node("Description", row, Zero, Zero, Zero, Zero), setting.Description, 16, TextAlignmentOptions.TopLeft, Muted).richText = false;

        Image field = null;
        Action show = null;

        void Refresh()
        {
            show();
            reset.gameObject.SetActive(!setting.IsDefault);
        }

        void Apply(string text)
        {
            bool ok;
            string message;
            try
            {
                ok = setting.TrySet(text, out message);
            }
            catch (Exception e)
            {
                Plugin.Logger.LogError($"Cannot save {setting.Section}.{setting.Key}: {e}");
                ok = false;
                message = $"Cannot save: {e.Message}";
            }
            error.text = message ?? "";
            error.gameObject.SetActive(!ok);
            if (field != null) field.color = ok ? FieldBg : ErrorBg;
            Refresh();
        }

        show = BuildControl(control, setting, window, multiline, Apply, out field);
        Button(reset, reset.GetComponent<Image>(), () => Apply(setting.DefaultValue));
        Refresh();
        return row;
    }

    static Action BuildControl(RectTransform control, Setting setting, Window window, bool multiline, Action<string> apply, out Image field)
    {
        field = null;
        var type = setting.ValueType;

        if (type == typeof(bool))
        {
            var box = Box("Toggle", control, new(0, 0.5f), new(0, 0.5f), new(0, -16), new(110, 16), ControlBg);
            var label = Label(Node("Text", box, Zero, One, Zero, Zero), "", 18, TextAlignmentOptions.Center, Color.white);
            var bg = box.GetComponent<Image>();
            Button(box, bg, () => apply(IsOn(setting) ? "false" : "true"));
            return () =>
            {
                var on = IsOn(setting);
                label.text = on ? "On" : "Off";
                label.color = on ? PanelBg : Color.white;
                bg.color = on ? Accent : ControlBg;
            };
        }

        if (type == typeof(KeyCode))
        {
            var box = Box("Key", control, new(0, 0.5f), new(0, 0.5f), new(0, -16), new(220, 16), ControlBg);
            var label = Label(Node("Text", box, Zero, One, Zero, Zero), "", 18, TextAlignmentOptions.Center, Accent);
            Action show = () => label.text = setting.Value;
            Button(box, box.GetComponent<Image>(), () =>
            {
                label.text = "Press a key…";
                window.CaptureKey(k =>
                {
                    if (k != KeyCode.None) apply(k.ToString());
                    else show();
                });
            });
            return show;
        }

        if (setting.Options is { Length: > 0 } options)
        {
            var prev = Box("Prev", control, new(0, 0.5f), new(0, 0.5f), new(0, -16), new(36, 16), ControlBg);
            var next = Box("Next", control, new(1, 0.5f), new(1, 0.5f), new(-36, -16), new(0, 16), ControlBg);
            Label(Node("Text", prev, Zero, One, Zero, Zero), "<", 20, TextAlignmentOptions.Center, Color.white);
            Label(Node("Text", next, Zero, One, Zero, Zero), ">", 20, TextAlignmentOptions.Center, Color.white);
            var label = Label(Node("Value", control, Zero, One, new(44, 0), new(-44, 0)), "", 18, TextAlignmentOptions.Center, Accent);
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Ellipsis;

            void Step(int delta)
            {
                var i = Array.IndexOf(options, setting.Value);
                apply(options[((i < 0 ? 0 : i + delta) % options.Length + options.Length) % options.Length]);
            }

            Button(prev, prev.GetComponent<Image>(), () => Step(-1));
            Button(next, next.GetComponent<Image>(), () => Step(1));
            return () => label.text = setting.Value;
        }

        if (setting.Range is var (min, max))
        {
            var whole = Array.IndexOf(WholeTypes, type) >= 0;
            var input = Input(Node("Input", control, new(1, 0.5f), new(1, 0.5f), new(-120, -16), new(0, 16)), setting.Value, false, apply);
            var slider = Slider(Node("Slider", control, Zero, One, new(0, 6), new(-132, -6)), (float)min, (float)max, whole, Number(setting.Value), v =>
                apply(whole ? Math.Round(v).ToString(CultureInfo.InvariantCulture) : Math.Round(v, 2).ToString(CultureInfo.InvariantCulture)));
            field = input.GetComponent<Image>();
            return () =>
            {
                input.SetTextWithoutNotify(setting.Value);
                slider.SetValueWithoutNotify(Number(setting.Value));
            };
        }

        var text = Input(Node("Input", control, Zero, One, new(0, 4), new(0, -4)), setting.Value, multiline, apply);
        field = text.GetComponent<Image>();
        return () => text.SetTextWithoutNotify(setting.Value);
    }

    static bool IsOn(Setting setting) => string.Equals(setting.Value, "true", StringComparison.OrdinalIgnoreCase);

    static float Number(string value) =>
        float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var f) ? f : 0;
}
