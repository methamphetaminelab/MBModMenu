using System;
using System.Globalization;
using System.Linq;
using BepInEx.Configuration;

namespace ModMenu;

public abstract class Setting
{
    public abstract string Section { get; }
    public abstract string Key { get; }
    public abstract string Description { get; }
    public abstract Type ValueType { get; }
    public abstract string Value { get; }
    public abstract string DefaultValue { get; }
    public abstract (double Min, double Max)? Range { get; }
    public abstract string[] Options { get; }
    public bool IsDefault => DefaultValue == null || Value == DefaultValue;
    public abstract bool TrySet(string text, out string error);
    public bool Reset() => DefaultValue != null && TrySet(DefaultValue, out _);

    protected const string OutOfRange = "Value is out of the allowed range";
    protected const string NotInList = "Value is not in the allowed list";
}

public sealed class LiveSetting : Setting
{
    readonly ConfigEntryBase entry;

    public LiveSetting(ConfigEntryBase entry) => this.entry = entry;

    public override string Section => entry.Definition.Section;
    public override string Key => entry.Definition.Key;
    public override string Description => entry.Description?.Description ?? "";
    public override Type ValueType => entry.SettingType;
    public override string Value => entry.GetSerializedValue();
    public override string DefaultValue => TomlTypeConverter.ConvertToString(entry.DefaultValue, entry.SettingType);

    public override (double Min, double Max)? Range
    {
        get
        {
            var acceptable = entry.Description?.AcceptableValues;
            if (!IsGeneric(acceptable, typeof(AcceptableValueRange<>))) return null;
            try
            {
                var type = acceptable.GetType();
                return (Convert.ToDouble(type.GetProperty("MinValue").GetValue(acceptable), CultureInfo.InvariantCulture),
                        Convert.ToDouble(type.GetProperty("MaxValue").GetValue(acceptable), CultureInfo.InvariantCulture));
            }
            catch (Exception e) when (e is InvalidCastException or FormatException)
            {
                return null;
            }
        }
    }

    public override string[] Options
    {
        get
        {
            var acceptable = entry.Description?.AcceptableValues;
            if (IsGeneric(acceptable, typeof(AcceptableValueList<>)))
                return ((Array)acceptable.GetType().GetProperty("AcceptableValues").GetValue(acceptable))
                    .Cast<object>().Select(v => TomlTypeConverter.ConvertToString(v, entry.SettingType)).ToArray();
            var type = entry.SettingType;
            if (type.IsEnum && !type.IsDefined(typeof(FlagsAttribute), false)) return Enum.GetNames(type);
            return null;
        }
    }

    public override bool TrySet(string text, out string error)
    {
        object value;
        try
        {
            value = TomlTypeConverter.ConvertToValue(ValueText.Normalize(text, entry.SettingType), entry.SettingType);
        }
        catch (Exception)
        {
            error = $"Not a valid {entry.SettingType.Name}";
            return false;
        }

        var acceptable = entry.Description?.AcceptableValues;
        if (acceptable != null && !acceptable.IsValid(value))
        {
            error = IsGeneric(acceptable, typeof(AcceptableValueRange<>)) ? OutOfRange : NotInList;
            return false;
        }

        entry.BoxedValue = value;
        if (!entry.ConfigFile.SaveOnConfigSet) entry.ConfigFile.Save();
        error = null;
        return true;
    }

    static bool IsGeneric(object o, Type definition) =>
        o != null && o.GetType().IsGenericType && o.GetType().GetGenericTypeDefinition() == definition;
}

public sealed class FileSetting : Setting
{
    readonly CfgDocument doc;
    readonly CfgEntry entry;
    readonly Action<string> save;

    public FileSetting(CfgDocument doc, CfgEntry entry, Action<string> save) => (this.doc, this.entry, this.save) = (doc, entry, save);

    public override string Section => entry.Section;
    public override string Key => entry.Key;
    public override string Description => entry.Description ?? "";
    public override string Value => entry.Value;
    public override string DefaultValue => entry.DefaultValue;

    public override Type ValueType => entry.TypeName switch
    {
        "Boolean" => typeof(bool),
        "Int32" => typeof(int),
        "Int64" => typeof(long),
        "Single" => typeof(float),
        "Double" => typeof(double),
        "String" => typeof(string),
        _ => null,
    };

    public override (double Min, double Max)? Range =>
        double.TryParse(ValueText.Normalize(entry.RangeMin, typeof(double)), NumberStyles.Float, CultureInfo.InvariantCulture, out var min) &&
        double.TryParse(ValueText.Normalize(entry.RangeMax, typeof(double)), NumberStyles.Float, CultureInfo.InvariantCulture, out var max)
            ? (min, max)
            : null;

    public override string[] Options => entry.MultipleValues ? null : Allowed;

    string[] Allowed => entry.AcceptableValues?.Split(", ");

    public override bool TrySet(string text, out string error)
    {
        var value = ValueText.Normalize(text, ValueType);
        if (!Parses(value, ValueType)) error = $"Not a valid {entry.TypeName}";
        else if (Allowed is { } allowed && value.Split(',').Select(v => v.Trim()).Any(v => !allowed.Contains(v))) error = NotInList;
        else if (Range is var (min, max) && double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) && (d < min || d > max)) error = OutOfRange;
        else error = null;

        if (error != null) return false;
        doc.SetValue(entry, value);
        save(doc.ToString());
        return true;
    }

    static bool Parses(string value, Type type)
    {
        if (type == typeof(bool)) return bool.TryParse(value, out _);
        if (type == typeof(int)) return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out _);
        if (type == typeof(long)) return long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out _);
        if (type == typeof(float) || type == typeof(double)) return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out _);
        return true;
    }
}

public static class ValueText
{
    public static string Normalize(string text, Type type)
    {
        var value = (text ?? "").Trim();
        return type == typeof(float) || type == typeof(double) || type == typeof(decimal) ? value.Replace(',', '.') : value;
    }
}
