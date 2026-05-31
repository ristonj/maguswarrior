using Godot;

namespace MagusWarrior.Diagnostics;

public partial class CSharpBootDiag : Node {
    public override void _Ready() {
        var path = IsInsideTree() && GetParent() is not null && GetParent().Name != "root"
            ? "user://csharp_child_proof.txt"
            : "user://csharp_proof.txt";
        using var f = FileAccess.Open(path, FileAccess.ModeFlags.Write);
        f?.StoreString("C# Node _Ready ran, parent=" + (GetParent()?.Name ?? "none"));
        GD.Print("[CS_DIAG] C# _Ready reached, path=" + path);
    }
}
