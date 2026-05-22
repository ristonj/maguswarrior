using System.Diagnostics;

namespace MagusWarrior.Core;

public static class GameDebug {
    [Conditional("DEBUG")]
    public static void Inspect(string label, object? value) {
        // TODO: dump to on-screen overlay — Story 1a+
    }
}
