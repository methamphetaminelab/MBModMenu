using System;
using System.IO;

namespace ModMenu;

public static class SafeFile
{
    const long MaxBytes = 256 * 1024;

    public static void Write(string path, string text)
    {
        var temp = path + ".modmenu-tmp";
        try
        {
            File.WriteAllText(temp, text);
            File.Move(temp, path, true);
        }
        finally
        {
            if (File.Exists(temp)) File.Delete(temp);
        }
    }

    public static bool IsEditableText(string path, out string reason)
    {
        var length = new FileInfo(path).Length;
        if (length > MaxBytes) reason = $"The file is too large to edit here ({length / 1024} KB)";
        else if (Array.IndexOf(File.ReadAllBytes(path), (byte)0) >= 0) reason = "The file looks binary and cannot be edited here";
        else reason = null;
        return reason == null;
    }
}
