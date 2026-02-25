// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Security.Aws;

using Microsoft.Extensions.Configuration;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for configuring AWS security services in the dependency injection container.
/// </summary>
public static class DispatchSecurityAwsServiceCollectionExtensions
{
	/// <summary>
	/// Adds AWS Secrets Manager credential store services.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configuration">The configuration containing AWS settings.</param>
	/// <returns>The service collection for chaining.</returns>
	/// <remarks>
	/// Requires the following configuration:
	/// <code>
	/// {
	///   "AWS": {
	///     "Region": "us-east-1",
	///     "SecretsManager": {
	///       "Secrets": {
	///         "my-secret-key": "secret-value"
	///       }
	///     }
	///   }
	/// }
	/// </code>
	/// </remarks>
	public static IServiceCollection AddAwsSecretsManagerCredentialStore(
		this IServiceCollection services,
		IConfiguration configuration)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configuration);

		var awsRegion = configuration["AWS:Region"];
		if (!string.IsNullOrEmpty(awsRegion))
		{
			_ = services.AddSingleton<ICredentialStore, AwsSecretsManagerCredentialStore>();
			_ = services.AddSingleton<IWritableCredentialStore, AwsSecretsManagerCredentialStore>();
		}

		return services;
	}

	/// <summary>
	/// Adds all AWS security services including Secrets Manager credential store.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configuration">The configuration containing AWS settings.</param>
	/// <returns>The service collection for chaining.</returns>
	public static IServiceCollection AddDispatchSecurityAws(
		this IServiceCollection services,
		IConfiguration configuration)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configuration);

		_ = services.AddAwsSecretsManagerCredentialStore(configuration);

		return services;
	}
}
