using BepInEx.Configuration;
using ModMenu;

if (args.Length == 2 && args[0] == "--scan")
{
    foreach (var path in Directory.EnumerateFiles(args[1], "*", SearchOption.AllDirectories))
    {
        var text = File.ReadAllText(path);
        var doc = CfgDocument.Parse(text);
        Console.WriteLine($"{Path.GetFileName(path)}: bepinex={doc.IsBepInExFormat} entries={doc.Entries.Count} roundtrip={doc.ToString() == text}");
    }
    return 0;
}

var failed = 0;

void Check(bool ok, string name)
{
    if (ok) return;
    failed++;
    Console.WriteLine($"FAIL {name}");
}

void Run(string name, Action test)
{
    try { test(); }
    catch (Exception e) { Check(false, $"{name}: {e.GetType().Name}: {e.Message}"); }
}

const string SpeedCfg =
    "## Settings file was created by plugin SpeedHack v1.0.0\n" +
    "## Plugin GUID: megabonk.speedhack\n" +
    "\n" +
    "[Speed]\n" +
    "\n" +
    "## Fastest speed (Unity caps time scale at 100)\n" +
    "# Setting type: Single\n" +
    "# Default value: 10\n" +
    "# Acceptable value range: From 1 to 100\n" +
    "Max = 10\n";

Run("ParsesBepInExEntry", () =>
{
    var doc = CfgDocument.Parse(SpeedCfg);
    Check(doc.Entries.Count == 1, "ParsesBepInExEntry count");
    var e = doc.Entries[0];
    Check(e.Section == "Speed", "ParsesBepInExEntry section");
    Check(e.Key == "Max", "ParsesBepInExEntry key");
    Check(e.Value == "10", "ParsesBepInExEntry value");
    Check(e.TypeName == "Single", "ParsesBepInExEntry type");
    Check(e.DefaultValue == "10", "ParsesBepInExEntry default");
    Check(e.RangeMin == "1" && e.RangeMax == "100", "ParsesBepInExEntry range");
    Check(e.Description == "Fastest speed (Unity caps time scale at 100)", "ParsesBepInExEntry description");
    Check(doc.IsBepInExFormat, "ParsesBepInExEntry format");
});

Run("SetValueChangesOnlyThatValue", () =>
{
    var doc = CfgDocument.Parse(SpeedCfg);
    doc.SetValue(doc.Entries[0], "20");
    Check(doc.ToString() == SpeedCfg.Replace("Max = 10", "Max = 20"), "SetValueChangesOnlyThatValue");
});

Run("KeepsCrlfEqualsAndSpaces", () =>
{
    const string text = "[Main]\r\n\r\n## Two\r\n## lines\r\n# Setting type: String\r\n# Default value: \r\nSome Key = a=b\r\n";
    var doc = CfgDocument.Parse(text);
    var e = doc.Entries[0];
    Check(e.Key == "Some Key", "KeepsCrlfEqualsAndSpaces key");
    Check(e.Value == "a=b", "KeepsCrlfEqualsAndSpaces value");
    Check(e.Description == "Two\nlines", "KeepsCrlfEqualsAndSpaces description");
    Check(e.DefaultValue == "", "KeepsCrlfEqualsAndSpaces empty default");
    doc.SetValue(e, "c");
    Check(doc.ToString() == text.Replace("a=b", "c"), "KeepsCrlfEqualsAndSpaces roundtrip");
});

Run("HeaderIsNotDescription", () =>
{
    var doc = CfgDocument.Parse("## Settings file was created by plugin X\n\n[A]\nKey = 1\n");
    Check(doc.Entries[0].Description == "", "HeaderIsNotDescription");
});

Run("ReadsAcceptableValues", () =>
{
    var doc = CfgDocument.Parse("[A]\n# Acceptable values: Fast, Slow\nMode = Fast\n");
    Check(doc.Entries[0].AcceptableValues == "Fast, Slow", "ReadsAcceptableValues");
});

Run("RejectsOtherFormats", () =>
{
    Check(!CfgDocument.Parse("{\"a\": 1}\n").IsBepInExFormat, "RejectsOtherFormats json");
    Check(!CfgDocument.Parse("foo: bar\n").IsBepInExFormat, "RejectsOtherFormats yaml");
    Check(!CfgDocument.Parse("").IsBepInExFormat, "RejectsOtherFormats empty");
});

Run("ReadsMultipleValuesFlag", () =>
{
    var doc = CfgDocument.Parse("[A]\n# Acceptable values: None, Fatal, Error\n# Multiple values can be set at the same time by separating them with , (e.g. Debug, Warning)\nLevels = Fatal, Error\n");
    Check(doc.Entries[0].MultipleValues, "ReadsMultipleValuesFlag");
    Check(!CfgDocument.Parse(SpeedCfg).Entries[0].MultipleValues, "ReadsMultipleValuesFlag off");
});

ConfigFile NewConfig(out string path)
{
    path = Path.Combine(Directory.CreateTempSubdirectory("modmenu").FullName, "s.cfg");
    return new ConfigFile(path, false);
}

ConfigEntry<float> BindMax(ConfigFile cfg) =>
    cfg.Bind("Speed", "Max", 10f, new ConfigDescription("Fastest", new AcceptableValueRange<float>(1f, 100f)));

Run("LiveFloatAcceptsComma", () =>
{
    var cfg = NewConfig(out var path);
    var max = BindMax(cfg);
    var s = new LiveSetting(max);
    Check(s.Range == (1d, 100d), "LiveFloatAcceptsComma range");
    Check(s.Options == null, "LiveFloatAcceptsComma options");
    Check(s.TrySet("20,5", out _), "LiveFloatAcceptsComma set");
    Check(max.Value == 20.5f, "LiveFloatAcceptsComma value");
    Check(File.ReadAllText(path).Contains("Max = 20.5"), "LiveFloatAcceptsComma file");
});

Run("LiveRejectsOutOfRange", () =>
{
    var max = BindMax(NewConfig(out _));
    var s = new LiveSetting(max);
    Check(!s.TrySet("500", out var error), "LiveRejectsOutOfRange result");
    Check(!string.IsNullOrEmpty(error), "LiveRejectsOutOfRange error");
    Check(max.Value == 10f, "LiveRejectsOutOfRange value");
});

Run("LiveRejectsGarbageInt", () =>
{
    var count = NewConfig(out _).Bind("A", "Count", 5, "");
    var s = new LiveSetting(count);
    Check(!s.TrySet("abc", out var error) && !string.IsNullOrEmpty(error), "LiveRejectsGarbageInt result");
    Check(count.Value == 5, "LiveRejectsGarbageInt value");
});

Run("LiveEnumOptions", () =>
{
    var mode = NewConfig(out _).Bind("A", "Mode", TestMode.A, "");
    var s = new LiveSetting(mode);
    Check(s.Options != null && s.Options.SequenceEqual(new[] { "A", "B", "C" }), "LiveEnumOptions options");
    Check(s.TrySet("B", out _) && mode.Value == TestMode.B, "LiveEnumOptions set");
});

Run("LiveFlagsEnumIsText", () =>
{
    var flags = NewConfig(out _).Bind("A", "Flags", TestFlags.X, "");
    var s = new LiveSetting(flags);
    Check(s.Options == null, "LiveFlagsEnumIsText options");
    Check(s.TrySet("X, Y", out _) && flags.Value == (TestFlags.X | TestFlags.Y), "LiveFlagsEnumIsText set");
});

Run("LiveListOptions", () =>
{
    var pick = NewConfig(out _).Bind("A", "Pick", "x", new ConfigDescription("", new AcceptableValueList<string>("x", "y")));
    var s = new LiveSetting(pick);
    Check(s.Options != null && s.Options.SequenceEqual(new[] { "x", "y" }), "LiveListOptions options");
    Check(!s.TrySet("z", out _) && pick.Value == "x", "LiveListOptions reject");
});

Run("LiveSavesWhenAutoSaveOff", () =>
{
    var cfg = NewConfig(out var path);
    cfg.SaveOnConfigSet = false;
    var s = new LiveSetting(BindMax(cfg));
    Check(s.TrySet("30", out _), "LiveSavesWhenAutoSaveOff set");
    Check(File.ReadAllText(path).Contains("Max = 30"), "LiveSavesWhenAutoSaveOff file");
});

Run("ResetRestoresDefault", () =>
{
    var max = BindMax(NewConfig(out _));
    var s = new LiveSetting(max);
    s.TrySet("30", out _);
    Check(!s.IsDefault, "ResetRestoresDefault changed");
    Check(s.Reset() && max.Value == 10f && s.IsDefault, "ResetRestoresDefault reset");
});

Run("FileSettingWritesThrough", () =>
{
    var doc = CfgDocument.Parse(SpeedCfg);
    string saved = null;
    var s = new FileSetting(doc, doc.Entries[0], t => saved = t);
    Check(s.ValueType == typeof(float), "FileSettingWritesThrough type");
    Check(s.Range == (1d, 100d), "FileSettingWritesThrough range");
    Check(s.TrySet("20,5", out _), "FileSettingWritesThrough set");
    Check(saved == SpeedCfg.Replace("Max = 10", "Max = 20.5"), "FileSettingWritesThrough saved");
    Check(s.Value == "20.5", "FileSettingWritesThrough value");
});

Run("FileSettingRejectsOutOfRange", () =>
{
    var doc = CfgDocument.Parse(SpeedCfg);
    string saved = null;
    var s = new FileSetting(doc, doc.Entries[0], t => saved = t);
    Check(!s.TrySet("0", out var error) && !string.IsNullOrEmpty(error), "FileSettingRejectsOutOfRange result");
    Check(!s.TrySet("abc", out _), "FileSettingRejectsOutOfRange garbage");
    Check(saved == null && s.Value == "10", "FileSettingRejectsOutOfRange untouched");
});

Run("FileSettingOptions", () =>
{
    var doc = CfgDocument.Parse("[A]\n# Setting type: Mode\n# Acceptable values: Fast, Slow\nMode = Fast\n");
    var s = new FileSetting(doc, doc.Entries[0], _ => { });
    Check(s.ValueType == null, "FileSettingOptions unknown type");
    Check(s.Options != null && s.Options.SequenceEqual(new[] { "Fast", "Slow" }), "FileSettingOptions options");
    Check(!s.TrySet("Medium", out _) && s.TrySet("Slow", out _), "FileSettingOptions validation");
});

Run("FileSettingMultipleValues", () =>
{
    var doc = CfgDocument.Parse("[A]\n# Acceptable values: None, Fatal, Error\n# Multiple values can be set at the same time by separating them with , (e.g. Debug, Warning)\nLevels = Fatal\n");
    var s = new FileSetting(doc, doc.Entries[0], _ => { });
    Check(s.Options == null, "FileSettingMultipleValues options");
    Check(s.TrySet("Fatal, Error", out _) && !s.TrySet("Fatal, Nope", out _), "FileSettingMultipleValues validation");
});

ConfigFile Cfg(string dir, string name, bool withEntry)
{
    var cfg = new ConfigFile(Path.Combine(dir, name), false);
    if (withEntry) cfg.Bind("A", "Value", 1, "");
    return cfg;
}

PluginRef Fake(ConfigFile config) =>
    new() { Name = "Fake", Version = "1.0.0", Instance = new FakePlugin { Config = config }, Assembly = typeof(FakePlugin).Assembly };

string[] Titles(Discovery d) => d.Mods.Select(m => m.Title).ToArray();

Run("FindsInstanceAndStaticConfigs", () =>
{
    var dir = Directory.CreateTempSubdirectory("modmenu").FullName;
    HiddenCfg.Set(Cfg(dir, "hidden.cfg", true));
    var d = ConfigSources.Discover(new[] { Fake(Cfg(dir, "a.cfg", true)) }, Cfg(dir, "core.cfg", true), dir, _ => { });
    Check(Titles(d).SequenceEqual(new[] { "Fake", "Fake (hidden.cfg)", "BepInEx" }), $"FindsInstanceAndStaticConfigs titles: {string.Join(" | ", Titles(d))}");
    Check(d.Mods[0].Version == "1.0.0", "FindsInstanceAndStaticConfigs version");
});

Run("SingleHiddenConfigGetsPlainName", () =>
{
    var dir = Directory.CreateTempSubdirectory("modmenu").FullName;
    var hidden = Cfg(dir, "hidden.cfg", true);
    HiddenCfg.Set(hidden);
    var d = ConfigSources.Discover(new[] { Fake(Cfg(dir, "a.cfg", false)) }, Cfg(dir, "core.cfg", true), dir, _ => { });
    Check(Titles(d).SequenceEqual(new[] { "Fake", "BepInEx" }) && d.Mods[0].File == hidden, $"SingleHiddenConfigGetsPlainName: {string.Join(" | ", Titles(d))}");
});

Run("OrphansAreUnclaimedFiles", () =>
{
    var dir = Directory.CreateTempSubdirectory("modmenu").FullName;
    HiddenCfg.Set(Cfg(dir, "hidden.cfg", true));
    File.WriteAllText(Path.Combine(dir, "leftover.cfg"), "[A]\nB = 1\n");
    File.WriteAllText(Path.Combine(dir, "notes.txt"), "hello");
    var d = ConfigSources.Discover(new[] { Fake(Cfg(dir, "a.cfg", true)) }, Cfg(dir, "core.cfg", true), dir, _ => { });
    var expected = new[] { Path.Combine(dir, "leftover.cfg"), Path.Combine(dir, "notes.txt") };
    Check(d.OrphanFiles.SequenceEqual(expected), $"OrphansAreUnclaimedFiles: {string.Join(" | ", d.OrphanFiles)}");
});

Run("SkipsEmptyConfigs", () =>
{
    var dir = Directory.CreateTempSubdirectory("modmenu").FullName;
    HiddenCfg.Set(null);
    var d = ConfigSources.Discover(new[] { Fake(Cfg(dir, "a.cfg", false)) }, Cfg(dir, "core.cfg", true), dir, _ => { });
    Check(Titles(d).SequenceEqual(new[] { "BepInEx" }), $"SkipsEmptyConfigs: {string.Join(" | ", Titles(d))}");
});

Run("SurvivesThrowingStaticCtor", () =>
{
    var dir = Directory.CreateTempSubdirectory("modmenu").FullName;
    HiddenCfg.Set(null);
    var warnings = new List<string>();
    var d = ConfigSources.Discover(new[] { Fake(Cfg(dir, "a.cfg", true)) }, Cfg(dir, "core.cfg", true), dir, warnings.Add);
    Check(warnings.Any(w => w.Contains("boom")), $"SurvivesThrowingStaticCtor warn: {string.Join(" | ", warnings)}");
    Check(Titles(d).SequenceEqual(new[] { "Fake", "BepInEx" }), "SurvivesThrowingStaticCtor mods");
});

Run("SurvivesMissingDependency", () =>
{
    var dir = Directory.CreateTempSubdirectory("modmenu").FullName;
    HiddenCfg.Set(null);
    var holder = new PluginRef { Name = "Holder", Version = "1.0.0", Instance = Holder.Factory.Create(), Assembly = typeof(Holder.Factory).Assembly };
    var warnings = new List<string>();
    Discovery d = null;
    try
    {
        d = ConfigSources.Discover(new[] { holder, Fake(Cfg(dir, "a.cfg", true)) }, Cfg(dir, "core.cfg", true), dir, warnings.Add);
    }
    catch (Exception e)
    {
        Check(false, $"SurvivesMissingDependency threw {e.GetType().Name}");
        return;
    }
    Check(Titles(d).SequenceEqual(new[] { "Fake", "BepInEx" }), $"SurvivesMissingDependency mods: {string.Join(" | ", Titles(d))}");
    Check(warnings.Count > 0, "SurvivesMissingDependency warned");
});

Run("FileSettingCultureRange", () =>
{
    var culture = System.Globalization.CultureInfo.CurrentCulture;
    System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo("ru-RU");
    try
    {
        var doc = CfgDocument.Parse("[Speed]\n# Setting type: Single\n# Default value: 0.25\n# Acceptable value range: From 0,05 to 1\nMin = 0.25\n");
        string saved = null;
        var s = new FileSetting(doc, doc.Entries[0], t => saved = t);
        Check(s.Range == (0.05d, 1d), $"FileSettingCultureRange range {s.Range}");
        Check(!s.TrySet("500", out _) && saved == null, "FileSettingCultureRange rejects");
        Check(s.TrySet("0,5", out _) && saved != null && saved.Contains("Min = 0.5"), "FileSettingCultureRange accepts comma");
    }
    finally
    {
        System.Globalization.CultureInfo.CurrentCulture = culture;
    }
});

Run("SafeFileWritesAndLeavesNoTemp", () =>
{
    var dir = Directory.CreateTempSubdirectory("modmenu").FullName;
    var path = Path.Combine(dir, "x.cfg");
    File.WriteAllText(path, "old");
    SafeFile.Write(path, "new");
    Check(File.ReadAllText(path) == "new", "SafeFileWritesAndLeavesNoTemp content");
    Check(Directory.GetFiles(dir).Length == 1, "SafeFileWritesAndLeavesNoTemp no temp");
});

Run("SafeFileEditableText", () =>
{
    var dir = Directory.CreateTempSubdirectory("modmenu").FullName;
    var text = Path.Combine(dir, "a.txt");
    var binary = Path.Combine(dir, "b.bin");
    var huge = Path.Combine(dir, "c.txt");
    File.WriteAllText(text, "hello");
    File.WriteAllBytes(binary, new byte[] { 1, 0, 2 });
    File.WriteAllText(huge, new string('x', 300 * 1024));
    Check(SafeFile.IsEditableText(text, out _), "SafeFileEditableText text");
    Check(!SafeFile.IsEditableText(binary, out var r1) && !string.IsNullOrEmpty(r1), "SafeFileEditableText binary");
    Check(!SafeFile.IsEditableText(huge, out var r2) && !string.IsNullOrEmpty(r2), "SafeFileEditableText huge");
});

Console.WriteLine(failed == 0 ? "all tests passed" : $"{failed} failed");
return failed == 0 ? 0 : 1;

class FakePlugin
{
    public ConfigFile Config { get; init; }
}

static class HiddenCfg
{
    static ConfigFile cfg;
    public static void Set(ConfigFile value) => cfg = value;
}

static class ThrowingStatic
{
    public static ConfigFile Cfg = Boom();
    static ConfigFile Boom() => throw new InvalidOperationException("boom");
}

enum TestMode { A, B, C }

[Flags]
enum TestFlags { X = 1, Y = 2 }
