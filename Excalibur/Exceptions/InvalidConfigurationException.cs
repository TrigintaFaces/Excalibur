namespace Excalibur.Exceptions;

/// <summary>
///     Represents an exception that occurs when a configuration setting is invalid or missing.
/// </summary>
[Serializable]
public class InvalidConfigurationException : ApiException
{
	/// <summary>
	///     Initializes a new instance of the <see cref="InvalidConfigurationException" /> class with the name of the problematic setting,
	///     an optional status code, an optional error message, and an optional inner exception.
	/// </summary>
	/// <param name="setting"> The name of the configuration setting that caused the exception. </param>
	/// <param name="statusCode"> The HTTP status code associated with the exception. If not provided, defaults to 500. </param>
	/// <param name="message">
	///     The error message describing the exception. If not provided, a default message is constructed using the <paramref name="setting" />.
	/// </param>
	/// <param name="innerException"> The exception that caused the current exception. </param>
	public InvalidConfigurationException(string setting, int? statusCode = null, string? message = null, Exception? innerException = null)
		: base(statusCode ?? 500, message ?? $"The '{setting}' setting is missing or invalid.", innerException) => Setting = setting;

	/// <summary>
	///     Gets or sets the name of the configuration setting that caused the exception.
	/// </summary>
	public string Setting { get; protected set; }
}
