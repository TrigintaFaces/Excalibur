using Microsoft.Extensions.Configuration;

namespace Excalibur.Hosting;

/// <summary>
///     Provides extension methods for building the application configuration.
/// </summary>
public static class ConfigurationBuilderExtensions
{
	/// <summary>
	///     Adds contextual configuration settings based on the current context.
	/// </summary>
	/// <param name="builder"> The <see cref="IConfigurationBuilder" />. </param>
	/// <param name="applicationParameterStoreName"> The application system name (eg. ar-canary). </param>
	/// <param name="localContext"> Used to load appsettings.{localContext}.json. </param>
	/// <returns> <see cref="IConfigurationBuilder" />. </returns>
	public static IConfigurationBuilder AddParameterStoreSettings(
		this IConfigurationBuilder builder,
		string applicationParameterStoreName,
		string localContext)
	{
		var context = Environment.GetEnvironmentVariable("RL_APP_CONTEXT")?.ToLowerInvariant();

		if (context == "remote")
		{
			builder.AddSystemsManager(applicationParameterStoreName, context);
		}
		else if (!string.IsNullOrEmpty(localContext))
		{
			_ = builder.AddJsonFile($"appsettings.{localContext}.json", true);
		}

		// Overlay local secrets and environment variables.
		return builder.AddEnvironmentVariables();
	}

	/// <summary>
	///     Adds contextual configuration settings based on the current context.
	/// </summary>
	/// <param name="builder"> The <see cref="IConfigurationBuilder" />. </param>
	/// <param name="applicationParameterStoreName"> The application system name (eg. ar-canary). </param>
	/// <returns> <see cref="IConfigurationBuilder" />. </returns>
	public static IConfigurationBuilder AddParameterStoreSettings(
		this IConfigurationBuilder builder,
		string applicationParameterStoreName)
		=> builder.AddParameterStoreSettings(applicationParameterStoreName, null);

	/// <summary>
	///     Adds contextual configuration settings based on the current context.
	/// </summary>
	/// <param name="builder"> The <see cref="IConfigurationBuilder" />. </param>
	/// <returns> <see cref="IConfigurationBuilder" />. </returns>
	public static IConfigurationBuilder AddParameterStoreSettings(this IConfigurationBuilder builder)
		=> builder.AddParameterStoreSettings(Environment.GetEnvironmentVariable("RL_APP_NAME"), null);

	private static void AddSystemsManager(
		this IConfigurationBuilder builder,
		string applicationParameterStoreName,
		string context)
	{
		var cluster = Environment.GetEnvironmentVariable("RL_CLUSTER_NAME")?.ToLowerInvariant();

		var commonPath = $"/apps/common/{context}";
		var appPath = $"/apps/{applicationParameterStoreName}/{context}";

		if (!string.IsNullOrEmpty(cluster))
		{
			commonPath = string.Join('/', commonPath, cluster);
			appPath = string.Join('/', appPath, cluster);
		}

		_ = builder.AddSystemsManager(commonPath);
		_ = builder.AddSystemsManager(appPath);
	}
}
