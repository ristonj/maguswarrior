using System.Diagnostics;
using Godot;

namespace MagusWarrior.Core;

public static class Log {
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
        // TODO: ring-buffer write to user://errors.log (50 KB max) — Story 0.2
    }
}
