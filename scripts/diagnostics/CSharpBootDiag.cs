using Godot;

namespace MagusWarrior.Diagnostics;

public partial class CSharpBootDiag : Node {
    public override void _Ready() {
        var parentName = GetParent()?.Name ?? "none";
        string path;
        if (parentName == "root")
            path = "user://csharp_proof.txt";          // autoload
        else if (parentName == "BootDiagnostic")
            path = "user://csharp_runtime_proof.txt";  // runtime-instantiated via GDScript
        else
            path = "user://csharp_child_proof.txt";    // scene child

        using var f = FileAccess.Open(path, FileAccess.ModeFlags.Write);
        f?.StoreString("C# _Ready ran, parent=" + parentName);
        GD.Print("[CS_DIAG] C# _Ready reached, path=" + path + " parent=" + parentName);
    }
}
