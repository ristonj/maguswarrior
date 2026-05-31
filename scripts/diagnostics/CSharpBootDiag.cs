using Godot;

namespace MagusWarrior.Diagnostics;

public partial class CSharpBootDiag : Node {
    public override void _Ready() {
        using var f = FileAccess.Open("user://csharp_proof.txt", FileAccess.ModeFlags.Write);
        f?.StoreString("C# autoload _Ready ran");
        GD.Print("[CS_DIAG] C# autoload _Ready reached");
    }
}
