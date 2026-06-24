// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Amazon;
using Amazon.SecretsManager;

using Excalibur.Security;
using Excalibur.Security.Aws;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for configuring AWS security services in the dependency injection container.
/// </summary>
public static class DispatchSecurityAwsServiceCollectionExtensions
{
	/// <summary>
	/// Adds AWS security services (Secrets Manager credential store) to the service collection.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configure">Configuration action for the AWS security builder.</param>
	/// <returns>The service collection for chaining.</returns>
	/// <exception cref="ArgumentNullException">Thrown when services or configure is null.</exception>
	/// <example>
	/// <code>
	/// services.AddDispatchSecurityAws(aws =&gt;
	/// {
	///     aws.Region("us-east-1");
	/// });
	/// </code>
	/// </example>
	public static IServiceCollection AddDispatchSecurityAws(
		this IServiceCollection services,
		Action<ISecurityAwsBuilder> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configure);

		var builder = new SecurityAwsBuilder();
		configure(builder);

		if (builder.Region is not null)
		{
			var region = builder.Region;

			// Register the concrete store once with a region-configured AWS client, then forward both
			// interfaces to the same instance so a single AmazonSecretsManagerClient is shared (and the
			// configured region is actually honored — it was previously captured by the builder but ignored).
			_ = services.AddSingleton(sp => new AwsSecretsManagerCredentialStore(
				sp.GetRequiredService<ILogger<AwsSecretsManagerCredentialStore>>(),
				sp.GetRequiredService<IConfiguration>(),
				new AmazonSecretsManagerClient(RegionEndpoint.GetBySystemName(region))));
			_ = services.AddSingleton<ICredentialStore>(sp => sp.GetRequiredService<AwsSecretsManagerCredentialStore>());
			_ = services.AddSingleton<IWritableCredentialStore>(sp => sp.GetRequiredService<AwsSecretsManagerCredentialStore>());
		}

		return services;
	}
}
