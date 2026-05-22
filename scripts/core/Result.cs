namespace MagusWarrior.Core;

public readonly struct Result<T> {
    public bool IsSuccess { get; }
    public T? Value { get; }
    public string? Error { get; }
    private Result(bool ok, T? value, string? error) =>
        (IsSuccess, Value, Error) = (ok, value, error);
    public static Result<T> Ok(T value) => new(true, value, null);
    public static Result<T> Fail(string error) => new(false, default, error);
}
