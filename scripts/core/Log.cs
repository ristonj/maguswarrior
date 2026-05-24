using System.Diagnostics;
using System.Linq;
using Godot;

namespace MagusWarrior.Core;

public static class Log {
    private const string ErrorLogPath = "user://errors.log";
    private const long MaxBytes = 50_000;

    public static void Error(string tag, string msg) {
        var line = $"[ERROR] {tag} {msg}";
        GD.PrintErr(line);
        AppendToErrorLog(line);
    }

    [Conditional("DEBUG")]
    public static void Warn(string tag, string msg) =>
        GD.Print($"[WARN] {tag} {msg}");

    [Conditional("DEBUG")]
    public static void Debug(string tag, string msg) =>
        GD.Print($"[DEBUG] {tag} {msg}");

    private static void AppendToErrorLog(string line) {
        try {
            if (FileAccess.FileExists(ErrorLogPath)) {
                ulong length;
                using (var reader = FileAccess.Open(ErrorLogPath, FileAccess.ModeFlags.Read)) {
                    if (reader == null) return;
                    length = reader.GetLength();
                    if (length > MaxBytes) {
                        var content = reader.GetAsText();
                        var lines = content.Split('\n');
                        var trimmed = string.Join('\n', lines.Skip(lines.Length / 2));
                        // reader is disposed before rewriter opens (avoids sharing conflict)
                        using var rewriter = FileAccess.Open(ErrorLogPath, FileAccess.ModeFlags.Write);
                        rewriter?.StoreString(trimmed);
                    }
                }
                // File exists: use ReadWrite (rb+ / keep-existing) to seek-and-append
                using var file = FileAccess.Open(ErrorLogPath, FileAccess.ModeFlags.ReadWrite);
                if (file != null) {
                    file.SeekEnd(0);
                    file.StoreString(line + "\n");
                }
            } else {
                // File absent: Write creates it
                using var file = FileAccess.Open(ErrorLogPath, FileAccess.ModeFlags.Write);
                file?.StoreString(line + "\n");
            }
        } catch { /* swallow — logging must not throw */ }
    }
}
