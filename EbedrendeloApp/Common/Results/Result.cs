namespace EbedrendeloApp.Common.Results;

public class Result
{
    public bool IsSuccess { get; }
    public string? ErrorCode { get; }
    public string? ErrorMessage { get; }

    protected Result(bool isSuccess, string? errorCode, string? errorMessage)
    {
        IsSuccess = isSuccess;
        ErrorCode = errorCode;
        ErrorMessage = errorMessage;
    }

    public static Result Success() => new(true, null, null);

    public static Result Failure(string errorCode, string errorMessage) => new(false, errorCode, errorMessage);

    public static Result<T> Success<T>(T value) => Result<T>.Success(value);

    public static Result<T> Failure<T>(string errorCode, string errorMessage) => Result<T>.Failure(errorCode, errorMessage);

    /// <summary>Egy nem generikus hibát tovább ad egy <see cref="Result{T}"/>-et váró hívónak
    /// (hibakód és üzenet változatlanul) — így a közös, érték nélküli ellenőrzések (pl.
    /// <c>ALaCarteOrderingGate</c>) értéket visszaadó handlerekben is használhatók.</summary>
    public Result<T> ToFailure<T>()
    {
        if (IsSuccess)
        {
            throw new InvalidOperationException("Sikeres Result nem alakítható hibává.");
        }

        return Result<T>.Failure(ErrorCode!, ErrorMessage!);
    }
}

public sealed class Result<T> : Result
{
    public T? Value { get; }

    private Result(bool isSuccess, T? value, string? errorCode, string? errorMessage)
        : base(isSuccess, errorCode, errorMessage)
    {
        Value = value;
    }

    public static Result<T> Success(T value) => new(true, value, null, null);

    public static new Result<T> Failure(string errorCode, string errorMessage) => new(false, default, errorCode, errorMessage);
}
