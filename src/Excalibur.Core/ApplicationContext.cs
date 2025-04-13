using System.Text.RegularExpressions;

namespace Excalibur.Core;

/// <summary>
///     Provides functionality to manage application context configurations with support for expansion and validation.
/// </summary>
public static partial class ApplicationContext
{
	private static readonly Regex ExpansionPattern = ExpansionRegex();

	private static readonly Dictionary<string, string?> ContextValues = new(StringComparer.OrdinalIgnoreCase);

	/// <summary>
	///     Gets the name of the application.
	/// </summary>
	public static string ApplicationName => Get(nameof(ApplicationName));

	/// <summary>
	///     Gets the system name of the application.
	/// </summary>
	public static string ApplicationSystemName => Get(nameof(ApplicationSystemName));

	/// <summary>
	///     Gets the display name of the application.
	/// </summary>
	public static string ApplicationDisplayName => Get(nameof(ApplicationDisplayName));

	/// <summary>
	///     Gets the audience value for the authentication service.
	/// </summary>
	public static string AuthenticationServiceAudience => Get(nameof(AuthenticationServiceAudience));

	/// <summary>
	///     Gets the endpoint for the authentication service.
	/// </summary>
	public static string AuthenticationServiceEndpoint => Get(nameof(AuthenticationServiceEndpoint));

	/// <summary>
	///     Gets the path to the public key file for the authentication service.
	/// </summary>
	public static string AuthenticationServicePublicKeyPath => Get(nameof(AuthenticationServicePublicKeyPath));

	/// <summary>
	///     Gets the endpoint for the authorization service.
	/// </summary>
	public static string AuthorizationServiceEndpoint => Get(nameof(AuthorizationServiceEndpoint));

	/// <summary>
	///     Gets the current application context, defaulting to "local" if not explicitly set in the environment variables.
	/// </summary>
	public static string Context => Environment.GetEnvironmentVariable(Get("APP_CONTEXT_NAME", "local")) ?? "local";

	/// <summary>
	///     Gets the name of the service account.
	/// </summary>
	public static string ServiceAccountName => Get(nameof(ServiceAccountName));

	/// <summary>
	///     Gets the path to the private key file for the service account.
	/// </summary>
	public static string ServiceAccountPrivateKeyPath => Get(nameof(ServiceAccountPrivateKeyPath));

	/// <summary>
	///     Gets the password for the service account's private key.
	/// </summary>
	public static string ServiceAccountPrivateKeyPassword => Get(nameof(ServiceAccountPrivateKeyPassword));

	/// <summary>
	///     Initializes the application context with the specified key-value pairs.
	/// </summary>
	/// <param name="context"> A dictionary containing context key-value pairs. </param>
	public static void Init(IDictionary<string, string?> context)
	{
		ArgumentNullException.ThrowIfNull(context);

		foreach (var kvp in context)
		{
			ContextValues[kvp.Key] = kvp.Value;
		}
	}

	/// <summary>
	///     Expands placeholders in the specified value using the context and environment variables.
	/// </summary>
	/// <param name="value"> The value to expand, potentially containing placeholders. </param>
	/// <returns> The expanded value, or <c> null </c> if the input value is <c> null </c>. </returns>
	public static string? Expand(string? value) => ExpandInternal(value, []);

	/// <summary>
	///     Retrieves the value associated with the specified key from the context.
	/// </summary>
	/// <param name="key"> The key to retrieve the value for. </param>
	/// <returns>
	///     The value associated with the key. Throws <see cref="Exceptions.InvalidConfigurationException" /> if the key does not exist.
	/// </returns>
	public static string Get(string key) => GetInternal(key, []) ?? throw new Exceptions.InvalidConfigurationException(key);

	/// <summary>
	///     Retrieves the value associated with the specified key from the context, or a default value if the key does not exist.
	/// </summary>
	/// <param name="key"> The key to retrieve the value for. </param>
	/// <param name="defaultValue"> The default value to return if the key is not found. </param>
	/// <returns> The value associated with the key, or the default value if the key does not exist. </returns>
	public static string Get(string key, string defaultValue) => GetInternal(key, []) ?? defaultValue;

	private static string? GetInternal(string key, IList<string> searchPath)
	{
		EnsureNoCircularReferenceExists(key, searchPath);

		searchPath.Add(key);

		// Check environment first
		var envValue = Environment.GetEnvironmentVariable(key);
		if (!string.IsNullOrEmpty(envValue))
		{
			searchPath.RemoveAt(searchPath.Count - 1);
			return envValue;
		}

		// Fallback to context
		if (!ContextValues.TryGetValue(key, out var value) && string.IsNullOrEmpty(Environment.GetEnvironmentVariable(key)))
		{
			searchPath.RemoveAt(searchPath.Count - 1);

			// allow fallback to InvalidConfigurationException outside
			return null;
		}

		var result = ExpandInternal(value, searchPath);
		searchPath.RemoveAt(searchPath.Count - 1);

		return result;
	}

	private static void EnsureNoCircularReferenceExists(string key, ICollection<string> searchPath)
	{
		if (searchPath.Contains(key))
		{
			throw new Exceptions.InvalidConfigurationException(
				key,
				message: $"Circular reference detected in configuration path {string.Join(" > ", searchPath)}");
		}
	}

	private static string? ExpandInternal(string? value, IList<string> searchPath)
	{
		if (string.IsNullOrEmpty(value))
		{
			return value;
		}

		return ExpansionPattern.Replace(
			Environment.ExpandEnvironmentVariables(value),
			match =>
			{
				var placeholderKey = match.Groups[1].Value;
				var expanded = GetInternal(placeholderKey, searchPath);
				if (expanded == null)
				{
					throw new Exceptions.InvalidConfigurationException(
						placeholderKey,
						message: $"Unresolved placeholder: {placeholderKey}");
				}

				return expanded;
			});
	}

	/// <summary>
	///     Compiles the regular expression used for expanding placeholders in context values.
	/// </summary>
	/// <returns> A compiled regular expression. </returns>
	[GeneratedRegex("%([^%]+)%", RegexOptions.IgnoreCase, "en-US")]
	private static partial Regex ExpansionRegex();
}
