using Microsoft.Extensions.Configuration;

namespace Excalibur.Core.Extensions;

/// <summary>
///     Provides extension methods for the <see cref="IConfiguration" /> interface to simplify application configuration retrieval.
/// </summary>
public static class ConfigurationExtensions
{
	/// <summary>
	///     Retrieves a dictionary of key-value pairs representing the configuration settings for the application context from the specified <see cref="IConfiguration" />.
	/// </summary>
	/// <param name="configuration"> The <see cref="IConfiguration" /> instance to retrieve the application context settings from. </param>
	/// <returns> A dictionary containing configuration key-value pairs from the "ApplicationContext" section. </returns>
	/// <exception cref="ArgumentNullException"> Thrown if the <paramref name="configuration" /> parameter is <c> null </c>. </exception>
	public static Dictionary<string, string?> GetApplicationContextConfiguration(this IConfiguration configuration)
	{
		ArgumentNullException.ThrowIfNull(configuration);

		return configuration
			.GetSection(nameof(ApplicationContext))
			.GetChildren()
			.ToDictionary(c => c.Key, c => c.Value);
	}
}
