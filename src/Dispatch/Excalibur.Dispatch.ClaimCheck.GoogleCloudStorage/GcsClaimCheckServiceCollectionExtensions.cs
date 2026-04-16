// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch.ClaimCheck.GoogleCloudStorage;
using Excalibur.Dispatch.Patterns.ClaimCheck;

using Google.Apis.Auth.OAuth2;
using Google.Cloud.Storage.V1;

using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering the Google Cloud Storage Claim Check provider.
/// </summary>
public static class GcsClaimCheckServiceCollectionExtensions
{
	/// <summary>
	/// Adds the Google Cloud Storage Claim Check provider using a fluent builder.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configure">The configuration action for the GCS claim check builder.</param>
	/// <returns>The service collection for chaining.</returns>
	/// <example>
	/// <code>
	/// services.AddGcsClaimCheck(gcs =&gt;
	/// {
	///     gcs.ProjectId("my-project")
	///        .BucketName("claim-check-payloads");
	/// });
	/// </code>
	/// </example>
	[UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode",
		Justification = "Options binding uses reflection by design.")]
	[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
		Justification = "Configuration binding uses reflection by design.")]
	public static IServiceCollection AddGcsClaimCheck(
		this IServiceCollection services,
		Action<IClaimCheckGcsBuilder> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configure);

		var gcsBuilder = new ClaimCheckGcsBuilder();
		configure(gcsBuilder);

		RegisterOptionsAndServices(services, gcsBuilder);

		return services;
	}

	[UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode",
		Justification = "Options binding uses reflection by design.")]
	[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
		Justification = "Configuration binding uses reflection by design.")]
	private static void RegisterOptionsAndServices(
		IServiceCollection services,
		ClaimCheckGcsBuilder gcsBuilder)
	{
		// Configure options from builder state
		_ = services.AddOptions<GcsClaimCheckOptions>()
			.Configure(opt =>
			{
				if (gcsBuilder.BucketNameValue is not null)
				{
					opt.BucketName = gcsBuilder.BucketNameValue;
				}

				if (gcsBuilder.ProjectIdValue is not null)
				{
					opt.ProjectId = gcsBuilder.ProjectIdValue;
				}

				if (gcsBuilder.PrefixValue is not null)
				{
					opt.Prefix = gcsBuilder.PrefixValue;
				}
			})
			.ValidateOnStart();

		// Register BindConfiguration if set
		if (gcsBuilder.BindConfigurationPath is not null)
		{
			services.AddOptions<GcsClaimCheckOptions>()
				.BindConfiguration(gcsBuilder.BindConfigurationPath)
				.ValidateOnStart();
		}

		services.AddOptions<ClaimCheckOptions>().ValidateOnStart();

		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<GcsClaimCheckOptions>, GcsClaimCheckOptionsValidator>());

		// Register StorageClient based on connection path
		var hasBuilderClient = gcsBuilder.ClientInstance is not null
			|| gcsBuilder.ClientFactoryFunc is not null;

		if (hasBuilderClient)
		{
			RegisterBuilderManagedClient(services, gcsBuilder);
		}
		else if (gcsBuilder.CredentialsPathValue is not null)
		{
			var credPath = gcsBuilder.CredentialsPathValue;
			services.TryAddSingleton(_ =>
			{
				var credential = GoogleCredential.FromFile(credPath);
				return StorageClient.Create(credential);
			});
		}
		else if (gcsBuilder.CredentialsJsonValue is not null)
		{
			var credJson = gcsBuilder.CredentialsJsonValue;
			services.TryAddSingleton(_ =>
			{
				var credential = GoogleCredential.FromJson(credJson);
				return StorageClient.Create(credential);
			});
		}

		services.TryAddSingleton<IClaimCheckProvider, GcsClaimCheckStore>();
	}

	private static void RegisterBuilderManagedClient(
		IServiceCollection services,
		ClaimCheckGcsBuilder gcsBuilder)
	{
		if (gcsBuilder.ClientInstance is not null)
		{
			var client = gcsBuilder.ClientInstance;
			services.TryAddSingleton(client);
		}
		else if (gcsBuilder.ClientFactoryFunc is not null)
		{
			var factory = gcsBuilder.ClientFactoryFunc;
			services.TryAddSingleton(factory);
		}
	}
}
