using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;

namespace Excalibur.Data.Serialization;

/// <summary>
///     Provides predefined <see cref="JsonSerializerSettings" /> configurations for use with Newtonsoft.Json.
/// </summary>
/// <remarks>
///     This class centralizes JSON serialization settings, offering consistency and reducing redundancy across the application. The
///     provided configurations include settings for ignoring streams during serialization and applying default formatting conventions.
/// </remarks>
public static class ExcaliburNewtonsoftSerializerSettings
{
	/// <summary>
	///     Gets a <see cref="JsonSerializerSettings" /> instance configured to ignore stream properties during serialization.
	/// </summary>
	/// <value>
	///     A <see cref="JsonSerializerSettings" /> instance that applies the <see cref="IgnoreStreamContractResolver" /> to exclude stream
	///     properties from serialization.
	/// </value>
	public static JsonSerializerSettings IgnoreStream { get; } = new() { ContractResolver = new IgnoreStreamContractResolver() };

	/// <summary>
	///     Gets the default <see cref="JsonSerializerSettings" /> instance configured with standard serialization conventions.
	/// </summary>
	/// <value>
	///     A <see cref="JsonSerializerSettings" /> instance with applied default settings, including camelCase property names, ignoring
	///     null values, and using indented formatting.
	/// </value>
	public static JsonSerializerSettings Default { get; } = ApplyDefaults(new JsonSerializerSettings());

	/// <summary>
	///     Applies default serialization settings to the given <see cref="JsonSerializerSettings" /> instance.
	/// </summary>
	/// <param name="jsonSerializerSettings"> The <see cref="JsonSerializerSettings" /> to configure. </param>
	/// <returns> The configured <see cref="JsonSerializerSettings" /> instance. </returns>
	/// <remarks>
	///     The default settings include:
	///     <list type="bullet">
	///         <item> Indented formatting for readability. </item>
	///         <item> Allowing non-public default constructors. </item>
	///         <item> Replacing object values during deserialization. </item>
	///         <item> Ignoring null values during serialization. </item>
	///         <item> Using UTC for date/time values. </item>
	///         <item> Formatting dates in ISO 8601 format. </item>
	///         <item> Applying camelCase naming to properties and dictionary keys, while honoring explicitly specified names. </item>
	///         <item> Using a <see cref="StringEnumConverter" /> for serializing enums as strings. </item>
	///     </list>
	/// </remarks>
	private static JsonSerializerSettings ApplyDefaults(JsonSerializerSettings jsonSerializerSettings)
	{
		jsonSerializerSettings.Formatting = Formatting.Indented;
		jsonSerializerSettings.ConstructorHandling = ConstructorHandling.AllowNonPublicDefaultConstructor;
		jsonSerializerSettings.ObjectCreationHandling = ObjectCreationHandling.Replace;
		jsonSerializerSettings.NullValueHandling = NullValueHandling.Ignore;
		jsonSerializerSettings.DateTimeZoneHandling = DateTimeZoneHandling.Utc;
		jsonSerializerSettings.DateFormatHandling = DateFormatHandling.IsoDateFormat;
		jsonSerializerSettings.ContractResolver = new CamelCasePropertyNamesContractResolver()
		{
			NamingStrategy = new CamelCaseNamingStrategy { ProcessDictionaryKeys = false, OverrideSpecifiedNames = true }
		};
		jsonSerializerSettings.Converters = new List<JsonConverter> { new StringEnumConverter() };

		return jsonSerializerSettings;
	}
}
