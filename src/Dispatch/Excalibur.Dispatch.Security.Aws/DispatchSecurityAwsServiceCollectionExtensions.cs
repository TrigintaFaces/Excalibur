// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Security.Aws;

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
			_ = services.AddSingleton<ICredentialStore, AwsSecretsManagerCredentialStore>();
			_ = services.AddSingleton<IWritableCredentialStore, AwsSecretsManagerCredentialStore>();
		}

		return services;
	}
}
