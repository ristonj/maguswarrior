namespace MagusWarrior.UI;

public class InputLock {
    private bool _locked;

    public bool IsLocked => _locked;

    // Returns true if lock acquired (caller may proceed); false if already locked (caller drops action).
    public bool TryAcquire() {
        if (_locked) return false;
        _locked = true;
        return true;
    }

    public void Release() { _locked = false; }
}
