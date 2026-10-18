using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using BepInEx.Configuration;

namespace ModMenu;

public sealed class PluginRef
{
    public string Name, Version;
    public object Instance;
    public Assembly Assembly;
}

public sealed class ModConfig
{
    public string Title, Version;
    public ConfigFile File;
}

public sealed class Discovery
{
    public List<ModConfig> Mods;
    public List<string> OrphanFiles;
}

public static class ConfigSources
{
    const BindingFlags AnyInstance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
    const BindingFlags AnyStatic = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

    public static Discovery Discover(IEnumerable<PluginRef> plugins, ConfigFile core, string configDir, Action<string> warn)
    {
        var ordered = plugins.OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase).ToList();
        var claimed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var found = new Dictionary<PluginRef, List<ConfigFile>>();
        foreach (var plugin in ordered) found[plugin] = new List<ConfigFile>();

        void Claim(PluginRef plugin, ConfigFile file)
        {
            if (file != null && claimed.Add(Path.GetFullPath(file.ConfigFilePath))) found[plugin].Add(file);
        }

        void Scan(PluginRef plugin, Func<List<ConfigFile>> scan)
        {
            try
            {
                foreach (var file in scan()) Claim(plugin, file);
            }
            catch (Exception e)
            {
                warn($"Skipped part of {plugin.Name}: {e.Message}");
            }
        }

        if (core != null) claimed.Add(Path.GetFullPath(core.ConfigFilePath));
        foreach (var plugin in ordered.Where(p => p.Instance != null))
            Scan(plugin, () => InstanceConfigs(plugin.Instance, warn));
        foreach (var plugin in ordered.Where(p => p.Assembly != null))
            Scan(plugin, () => StaticConfigs(plugin.Assembly, warn));

        var mods = new List<ModConfig>();
        foreach (var plugin in ordered)
        {
            var files = found[plugin].Where(f => f.Count > 0).ToList();
            for (var i = 0; i < files.Count; i++)
                mods.Add(new ModConfig
                {
                    Title = i == 0 ? plugin.Name : $"{plugin.Name} ({Path.GetFileName(files[i].ConfigFilePath)})",
                    Version = plugin.Version,
                    File = files[i],
                });
        }
        if (core != null && core.Count > 0)
            mods.Add(new ModConfig { Title = "BepInEx", Version = typeof(ConfigFile).Assembly.GetName().Version?.ToString(), File = core });

        var orphans = Directory.Exists(configDir)
            ? Directory.EnumerateFiles(configDir, "*", SearchOption.AllDirectories)
                .Where(f => !claimed.Contains(Path.GetFullPath(f)))
                .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                .ToList()
            : new List<string>();

        return new Discovery { Mods = mods, OrphanFiles = orphans };
    }

    static List<ConfigFile> InstanceConfigs(object instance, Action<string> warn)
    {
        var result = new List<ConfigFile>();
        for (var type = instance.GetType(); type != null; type = type.BaseType)
        {
            foreach (var field in Guarded(type, () => type.GetFields(AnyInstance), warn))
                if (Is(field, () => field.FieldType == typeof(ConfigFile), warn) && Read(() => field.GetValue(instance), field, warn) is ConfigFile file)
                    result.Add(file);
            foreach (var property in Guarded(type, () => type.GetProperties(AnyInstance), warn))
                if (Is(property, () => property.PropertyType == typeof(ConfigFile) && property.CanRead && property.GetIndexParameters().Length == 0, warn) &&
                    Read(() => property.GetValue(instance), property, warn) is ConfigFile file)
                    result.Add(file);
        }
        return result;
    }

    static List<ConfigFile> StaticConfigs(Assembly assembly, Action<string> warn)
    {
        Type[] types;
        try
        {
            types = assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException e)
        {
            types = e.Types.Where(t => t != null).ToArray();
        }

        var result = new List<ConfigFile>();
        foreach (var type in types)
        {
            if (!Is(type, () => !type.ContainsGenericParameters, warn)) continue;
            foreach (var field in Guarded(type, () => type.GetFields(AnyStatic), warn))
                if (Is(field, () => field.FieldType == typeof(ConfigFile), warn) && Read(() => field.GetValue(null), field, warn) is ConfigFile file)
                    result.Add(file);
        }
        return result;
    }

    static T[] Guarded<T>(Type type, Func<T[]> get, Action<string> warn)
    {
        try
        {
            return get();
        }
        catch (Exception e)
        {
            warn($"Skipped {type.FullName}: {e.Message}");
            return Array.Empty<T>();
        }
    }

    static bool Is(MemberInfo member, Func<bool> check, Action<string> warn)
    {
        try
        {
            return check();
        }
        catch (Exception e)
        {
            warn($"Skipped {member.DeclaringType?.FullName}.{member.Name}: {e.Message}");
            return false;
        }
    }

    static object Read(Func<object> get, MemberInfo member, Action<string> warn)
    {
        try
        {
            return get();
        }
        catch (Exception e)
        {
            warn($"Cannot read {member.DeclaringType?.FullName}.{member.Name}: {(e.InnerException?.InnerException ?? e.InnerException ?? e).Message}");
            return null;
        }
    }
}
