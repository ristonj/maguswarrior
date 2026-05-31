#if GODOT_ANDROID
using System;
using Godot;
using MagusWarrior.UI;

// Godot's ScriptPathAttributeGenerator fails during Android export (CS8785,
// 'GodotProjectDir' is null or empty), so it emits neither the per-class
// [ScriptPath] attributes nor this assembly-level manifest that maps script
// paths to their C# classes. Without the manifest, Godot finds the script path
// but cannot resolve it to a class ("associated class could not be found").
//
// We supply both manually, guarded by GODOT_ANDROID so they apply only to the
// Android export (where the generator fails) and never collide with the working
// generator in editor/desktop builds. Every node script (partial class deriving
// from a Godot type) must carry a [ScriptPath] attribute AND be listed here.
[assembly: AssemblyHasScripts(new Type[] {
    typeof(PlaceholderMainMenu),
    typeof(EffectEventLogPanel),
    typeof(HandView),
    typeof(CardCompact),
    typeof(CardExpanded),
})]
#endif
