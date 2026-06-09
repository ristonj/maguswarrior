using MagusWarrior.UI;
using Xunit;

namespace MagusWarrior.Tests;

public class InputLockTest {
    [Fact]
    public void TryAcquire_WhenFree_ReturnsTrue() {
        var lock_ = new InputLock();
        Assert.True(lock_.TryAcquire());
    }

    [Fact]
    public void TryAcquire_WhenFree_SetsIsLocked() {
        var lock_ = new InputLock();
        lock_.TryAcquire();
        Assert.True(lock_.IsLocked);
    }

    [Fact]
    public void TryAcquire_WhenLocked_ReturnsFalse() {
        var lock_ = new InputLock();
        lock_.TryAcquire();
        Assert.False(lock_.TryAcquire());
    }

    [Fact]
    public void Release_AfterAcquire_ClearsIsLocked() {
        var lock_ = new InputLock();
        lock_.TryAcquire();
        lock_.Release();
        Assert.False(lock_.IsLocked);
    }

    [Fact]
    public void TryAcquire_AfterRelease_CanAcquireAgain() {
        var lock_ = new InputLock();
        lock_.TryAcquire();
        lock_.Release();
        Assert.True(lock_.TryAcquire());
    }
}
