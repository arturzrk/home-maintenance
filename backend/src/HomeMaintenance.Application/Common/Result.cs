namespace HomeMaintenance.Application.Common;

/// <summary>
/// A success/failure return shape that carries either a value of type
/// <typeparamref name="T"/> or an <see cref="Common.Error"/>. Handlers
/// return <c>Result&lt;T&gt;</c> instead of throwing for expected failure
/// modes so the API layer can map outcomes to HTTP uniformly.
/// </summary>
public readonly record struct Result<T>
{
    public T? Value { get; }
    public Error? Error { get; }

    public bool IsSuccess => Error is null;
    public bool IsFailure => Error is not null;

    private Result(T? value, Error? error)
    {
        Value = value;
        Error = error;
    }

    public static Result<T> Success(T value) => new(value, null);
    public static Result<T> Failure(Error error) => new(default, error);
}
