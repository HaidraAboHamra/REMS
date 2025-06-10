namespace REMS.Abstractions;

/// <summary>
/// Represents the result of an operation, which can be either successful or failed.
/// </summary>
public readonly struct Result
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Result"/> struct.
    /// </summary>
    /// <param name="isSuccess">Indicates whether the result is successful.</param>
    /// <param name="error">The error associated with the result, if any.</param>
    /// <exception cref="ArgumentException">Thrown when the error state is invalid.</exception>
    private Result(bool isSuccess, Error error)
    {
        if (isSuccess && error != Error.None || !isSuccess && error == Error.None)
        {
            throw new ArgumentException("Invalid error", nameof(error));
        }

        IsSuccess = isSuccess;
        Error = error;
    }

    /// <summary>
    /// Indicates whether the result is successful.
    /// </summary>
    public bool IsSuccess { get; }

    /// <summary>
    /// Indicates whether the result is a failure.
    /// </summary>
    public bool IsFailure => !IsSuccess;

    /// <summary>
    /// The error associated with the result, if any.
    /// </summary>
    public Error Error { get; }

    /// <summary>
    /// Creates a successful result.
    /// </summary>
    public static Result Success() => new(true, Error.None);

    /// <summary>
    /// Creates a failed result with the specified error.
    /// </summary>
    /// <param name="error">The error describing the failure.</param>
    public static Result Failure(Error error) => new(false, error);
}

/// <summary>
/// Represents the result of an operation, which can be either successful or failed, with a value.
/// </summary>
/// <typeparam name="T">The type of the value.</typeparam>
public readonly struct Result<T>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Result{T}"/> struct.
    /// </summary>
    /// <param name="isSuccess">Indicates whether the result is successful.</param>
    /// <param name="error">The error associated with the result, if any.</param>
    /// <param name="value">The value associated with the result, if any.</param>
    /// <exception cref="ArgumentException">Thrown when the error state is invalid.</exception>
    private Result(bool isSuccess, Error error, T? value)
    {
        if (isSuccess && error != Error.None || !isSuccess && error == Error.None)
        {
            throw new ArgumentException("Invalid error", nameof(error));
        }

        IsSuccess = isSuccess;
        Error = error;
        Value = value;
    }

    /// <summary>
    /// Indicates whether the result is successful.
    /// </summary>
    public bool IsSuccess { get; }

    /// <summary>
    /// Indicates whether the result is a failure.
    /// </summary>
    public bool IsFailure => !IsSuccess;

    /// <summary>
    /// The value associated with the result, if any.
    /// </summary>
    public T? Value { get; }

    /// <summary>
    /// The error associated with the result, if any.
    /// </summary>
    public Error Error { get; }

    /// <summary>
    /// Creates a successful result with the specified value.
    /// </summary>
    /// <param name="value">The value of the successful result.</param>
    public static Result<T> Success(T value) => new(true, Error.None, value);

    /// <summary>
    /// Creates a failed result with the specified error.
    /// </summary>
    /// <param name="error">The error describing the failure.</param>
    public static Result<T> Failure(Error error) => new(false, error, default);


    public static implicit operator Result(Result<T> result)
    {
        if (result.IsSuccess)
        {
            return Result.Success();
        }

        return Result.Failure(result.Error);
    }
}
