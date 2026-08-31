// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;

using Amazon;
using Amazon.SecretsManager;

using Excalibur.Security;
using Excalibur.Security.Aws;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

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
	[RequiresUnreferencedCode("AWS Secrets Manager integration depends on AWSSDK.SecretsManager (v3), whose reflection-based serialization cannot be statically analyzed by the trimmer; this registration is not trim-compatible.")]
	[RequiresDynamicCode("AWS Secrets Manager integration depends on AWSSDK.SecretsManager (v3), which uses runtime code generation; this registration is not compatible with Native AOT.")]
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

	/// <summary>
	/// Registers an AWS Secrets Manager-backed <see cref="IKeyProvider"/> for message-signing keys.
	/// Key material is stored as the secret's binary payload, retrieved keys are cached with a bounded
	/// TTL, and an unknown key fails closed (a <see cref="SigningException"/> is thrown — no key is minted
	/// on the retrieval path).
	/// <para>
	/// This is an explicit provider selection, so it takes precedence over any <see cref="IKeyProvider"/>
	/// already registered. The framework registers none of its own — it never mints signing keys — so a
	/// host that calls this has named the only provider in play. To supply a different one instead,
	/// register it after this call.
	/// </para>
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configure">Optional configuration for the provider options.</param>
	/// <returns>The service collection for chaining.</returns>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="services"/> is null.</exception>
	[RequiresUnreferencedCode("AWS Secrets Manager integration depends on AWSSDK.SecretsManager (v3), whose reflection-based serialization cannot be statically analyzed by the trimmer; this registration is not trim-compatible.")]
	[RequiresDynamicCode("AWS Secrets Manager integration depends on AWSSDK.SecretsManager (v3), which uses runtime code generation; this registration is not compatible with Native AOT.")]
	public static IServiceCollection AddAwsSecretsManagerKeyProvider(
		this IServiceCollection services,
		Action<AwsSecretsManagerKeyProviderOptions>? configure = null)
	{
		ArgumentNullException.ThrowIfNull(services);

		var optionsBuilder = services.AddOptions<AwsSecretsManagerKeyProviderOptions>();
		if (configure is not null)
		{
			_ = optionsBuilder.Configure(configure);
		}

		_ = optionsBuilder.ValidateOnStart();
		services.TryAddEnumerable(ServiceDescriptor.Singleton<
			IValidateOptions<AwsSecretsManagerKeyProviderOptions>,
			AwsSecretsManagerKeyProviderOptionsValidator>());

		services.TryAddSingleton(TimeProvider.System);
		services.AddSingleton<IKeyProvider>(sp => new AwsSecretsManagerKeyProvider(
			sp.GetRequiredService<ILogger<AwsSecretsManagerKeyProvider>>(),
			sp.GetRequiredService<IOptions<AwsSecretsManagerKeyProviderOptions>>(),
			sp.GetRequiredService<TimeProvider>()));

		return services;
	}
}
