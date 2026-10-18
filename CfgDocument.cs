using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace ModMenu;

public sealed class CfgEntry
{
    public string Section, Key, Value, Description, TypeName, DefaultValue, AcceptableValues, RangeMin, RangeMax;
    public bool MultipleValues;
}

public sealed class CfgDocument
{
    static readonly Regex Range = new(@"^From (.+) to (.+)$");

    readonly string[] lines;
    readonly List<CfgEntry> entries = new();
    readonly Dictionary<CfgEntry, (int Line, string Prefix)> slots = new();

    public IReadOnlyList<CfgEntry> Entries => entries;
    public bool IsBepInExFormat { get; }

    CfgDocument(string text)
    {
        lines = text.Split('\n');
        string section = null;
        var description = new List<string>();
        var meta = new CfgEntry();
        var valid = true;

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i].TrimEnd('\r');
            var trimmed = line.Trim();

            if (trimmed.Length == 0)
            {
                description.Clear();
                meta = new CfgEntry();
            }
            else if (trimmed.StartsWith("[") && trimmed.EndsWith("]"))
            {
                section = trimmed[1..^1];
                description.Clear();
                meta = new CfgEntry();
            }
            else if (trimmed.StartsWith("##"))
            {
                if (section != null) description.Add(After(trimmed, "##"));
            }
            else if (trimmed.StartsWith("#"))
            {
                ReadMeta(trimmed, meta);
            }
            else if (line.IndexOf('=') is var eq and > 0 && section != null)
            {
                var rest = line[(eq + 1)..];
                var entry = new CfgEntry
                {
                    Section = section,
                    Key = line[..eq].Trim(),
                    Value = rest.Trim(),
                    Description = string.Join("\n", description),
                    TypeName = meta.TypeName,
                    DefaultValue = meta.DefaultValue,
                    AcceptableValues = meta.AcceptableValues,
                    RangeMin = meta.RangeMin,
                    RangeMax = meta.RangeMax,
                    MultipleValues = meta.MultipleValues,
                };
                entries.Add(entry);
                slots[entry] = (i, line[..(line.Length - rest.TrimStart().Length)]);
                description.Clear();
                meta = new CfgEntry();
            }
            else
            {
                valid = false;
            }
        }

        IsBepInExFormat = valid && section != null && entries.Count > 0;
    }

    public static CfgDocument Parse(string text) => new(text);

    public void SetValue(CfgEntry entry, string value)
    {
        var (line, prefix) = slots[entry];
        var cr = lines[line].EndsWith("\r") ? "\r" : "";
        lines[line] = prefix + value + cr;
        entry.Value = value;
    }

    public override string ToString() => string.Join("\n", lines);

    static void ReadMeta(string line, CfgEntry meta)
    {
        if (line.StartsWith("# Setting type:")) meta.TypeName = After(line, "# Setting type:");
        else if (line.StartsWith("# Default value:")) meta.DefaultValue = After(line, "# Default value:");
        else if (line.StartsWith("# Acceptable values:")) meta.AcceptableValues = After(line, "# Acceptable values:");
        else if (line.StartsWith("# Multiple values can be set")) meta.MultipleValues = true;
        else if (line.StartsWith("# Acceptable value range:") && Range.Match(After(line, "# Acceptable value range:")) is { Success: true } m)
        {
            meta.RangeMin = m.Groups[1].Value;
            meta.RangeMax = m.Groups[2].Value;
        }
    }

    static string After(string line, string prefix)
    {
        var rest = line[prefix.Length..];
        return rest.StartsWith(" ") ? rest[1..] : rest;
    }
}
