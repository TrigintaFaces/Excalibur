using System.Text.Json;
using System.Text.Json.Serialization;

namespace Excalibur.Data.Serialization;

/// <summary>
///     Provides predefined <see cref="JsonSerializerOptions" /> configurations for use in the application.
/// </summary>
/// <remarks>
///     This class offers centralized configurations for JSON serialization, enabling consistent settings across the application. These
///     options include settings for general web-based JSON serialization and custom configurations for specific use cases such as OPA (Open
///     Policy Agent).
/// </remarks>
public static class ExcaliburJsonSerializerOptions
{
	/// <summary>
	///     A lazily initialized default <see cref="JsonSerializerOptions" /> instance configured with <see cref="JsonSerializerDefaults.General" />.
	/// </summary>
	private static readonly Lazy<JsonSerializerOptions> DefaultSettings =
		new(() => new JsonSerializerOptions(JsonSerializerDefaults.General));

	/// <summary>
	///     A lazily initialized web <see cref="JsonSerializerOptions" /> instance configured with <see cref="JsonSerializerDefaults.Web" />.
	/// </summary>
	private static readonly Lazy<JsonSerializerOptions> WebSettings = new(() =>
		ApplyDefaults(new JsonSerializerOptions(JsonSerializerDefaults.Web)));

	/// <summary>
	///     Gets the default <see cref="JsonSerializerOptions" /> configured for JSON serialization.
	/// </summary>
	/// <value> A <see cref="JsonSerializerOptions" /> instance using <see cref="JsonSerializerDefaults.Web" />. </value>
	public static JsonSerializerOptions Default => DefaultSettings.Value;

	/// <summary>
	///     Gets the OPA (Open Policy Agent) specific <see cref="JsonSerializerOptions" /> configuration.
	/// </summary>
	/// <value> An alias to the <see cref="Default" /> settings. </value>
	/// <remarks> This property can be extended in the future to provide custom OPA-specific serialization configurations. </remarks>
	public static JsonSerializerOptions Opa => DefaultSettings.Value;

	/// <summary>
	///     Gets the default <see cref="JsonSerializerOptions" /> configured for web-based JSON serialization.
	/// </summary>
	/// <value> A <see cref="JsonSerializerOptions" /> instance using <see cref="JsonSerializerDefaults.Web" />. </value>
	public static JsonSerializerOptions Web => WebSettings.Value;

	/// <summary>
	///     Applies standard settings to the provided <see cref="JsonSerializerOptions" /> instance.
	/// </summary>
	/// <param name="options"> The <see cref="JsonSerializerOptions" /> instance to configure. </param>
	/// <returns> The configured <see cref="JsonSerializerOptions" /> instance. </returns>
	/// <exception cref="ArgumentNullException"> Thrown if <paramref name="options" /> is <c> null </c>. </exception>
	public static JsonSerializerOptions ApplyDefaults(JsonSerializerOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		// Configure property naming and handling settings.
		options.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
		options.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
		options.WriteIndented = true;

		// Add custom converters for enums and other data types.
		options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));

		return options;
	}
}
