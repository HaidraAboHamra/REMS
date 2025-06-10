namespace REMS.Abstractions;

/// <summary>
/// Represents an error with a code and an optional description.
/// </summary>
public sealed record Error(string? Description = null)
{
    /// <summary>
    /// Represents no error.
    /// </summary>
    public static readonly Error None = new();

	/// <summary>
    /// Converts an exception into an error
    /// </summary>
	public static explicit operator Error(Exception? exception) =>
	    new(exception?.Message);

    public static implicit operator string(Error? error) =>
        error?.Description ?? string.Empty;
}