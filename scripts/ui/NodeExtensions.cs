using Godot;

namespace MagusWarrior.UI;

public static class NodeExtensions {
    // Detach and free every child of `parent`.
    //
    // The RemoveChild-BEFORE-QueueFree ordering is the entire point of this helper, and it is not
    // optional. QueueFree only *queues* deletion for the end of the frame, so the bare pattern
    //
    //     foreach (Node c in container.GetChildren()) c.QueueFree();   // WRONG
    //     ... rebuild and AddChild the new rows ...
    //
    // leaves the stale children parented, laid out, visible, and signal-connected alongside the new
    // ones for the rest of that frame. Two ways that bit us on device (2026-07-12):
    //
    //   LAYOUT  — BlockTargetingPanel rebuilt its rows on every card played. One enemy with one
    //             attack produced rows=2, the row rect grew 567px → 2002px on a 1920px viewport,
    //             and the overflow visibly misaligned the Pass button.
    //   INPUT   — a stale Button is still tappable. In DamageAssignmentPanel the resolver's
    //             continuation resumes INLINE inside a button's Pressed handler, so the rebuild
    //             happens mid-frame and the old rows get re-armed against the NEW
    //             TaskCompletionSource; a tap on one would hand the resolver an already-assigned
    //             unit and trip its ineligible-unit assert. (HandView's "ignoring stale tap" guard
    //             in OnCardTapped is scar tissue from the same cause.)
    //
    // Always use this instead of a bare QueueFree loop when rebuilding a container's children.
    public static void ClearChildren(this Node parent) {
        foreach (Node child in parent.GetChildren()) {
            parent.RemoveChild(child);
            child.QueueFree();
        }
    }
}
