using Xunit;
using MagusWarrior.Core;

namespace MagusWarrior.Tests.Unit;

public class ResultTest {
    [Fact]
    public void OkResult_IsSuccess() {
        var result = Result<int>.Ok(42);
        Assert.True(result.IsSuccess);
        Assert.Equal(42, result.Value);
        Assert.Null(result.Error);
    }

    [Fact]
    public void FailResult_HasError() {
        var result = Result<int>.Fail("bad input");
        Assert.False(result.IsSuccess);
        Assert.Equal("bad input", result.Error);
        Assert.Equal(0, result.Value);
    }

    [Fact]
    public void OkResult_String_HasValue() {
        var result = Result<string>.Ok("hello");
        Assert.True(result.IsSuccess);
        Assert.Equal("hello", result.Value);
    }

    [Fact]
    public void FailResult_String_ValueIsNull() {
        var result = Result<string>.Fail("missing");
        Assert.False(result.IsSuccess);
        Assert.Null(result.Value);
    }
}
