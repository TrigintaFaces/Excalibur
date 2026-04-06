// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;

using Excalibur.A3.Governance;
using Excalibur.A3.Governance.AccessReviews;
using Excalibur.A3.Governance.Stores.InMemory;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering access review services on <see cref="IGovernanceBuilder"/>.
/// </summary>
public static class AccessReviewGovernanceBuilderExtensions
{
	/// <summary>
	/// Adds access review services to the governance builder.
	/// </summary>
	/// <param name="builder">The governance builder.</param>
	/// <param name="configure">Optional delegate to configure <see cref="AccessReviewOptions"/>.</param>
	/// <returns>The <see cref="IGovernanceBuilder"/> for fluent chaining.</returns>
	/// <remarks>
	/// <para>
	/// Registers <see cref="IAccessReviewStore"/> with an in-memory fallback store.
	/// Override with a persistent store implementation via DI replacement.
	/// </para>
	/// <code>
	/// services.AddExcaliburA3Core()
	///     .AddGovernance(g => g
	///         .AddAccessReviews(opts =>
	///         {
	///             opts.DefaultCampaignDuration = TimeSpan.FromDays(14);
	///             opts.DefaultExpiryPolicy = AccessReviewExpiryPolicy.RevokeUnreviewed;
	///         }));
	/// </code>
	/// </remarks>
	public static IGovernanceBuilder AddAccessReviews(
		this IGovernanceBuilder builder,
		Action<AccessReviewOptions>? configure = null)
	{
		ArgumentNullException.ThrowIfNull(builder);

		// Register options with validation
		var optionsBuilder = builder.Services.AddOptions<AccessReviewOptions>();
		if (configure is not null)
		{
			optionsBuilder.Configure(configure);
		}

		optionsBuilder.ValidateOnStart();

		// Register AOT-safe validator
		builder.Services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<AccessReviewOptions>, AccessReviewOptionsValidator>());

		return builder.AddAccessReviewsCore();
	}

	/// <summary>
	/// Adds access review services using an <see cref="IConfiguration"/> section.
	/// </summary>
	/// <param name="builder">The governance builder.</param>
	/// <param name="configuration">The configuration section to bind to <see cref="AccessReviewOptions"/>.</param>
	/// <returns>The <see cref="IGovernanceBuilder"/> for fluent chaining.</returns>
	[UnconditionalSuppressMessage("AOT", "IL2026:RequiresUnreferencedCode",
		Justification = "Configuration binding uses reflection by design. AOT consumers should use source-generated binding.")]
	[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
		Justification = "Configuration binding uses reflection by design. AOT consumers should use source-generated binding.")]
	public static IGovernanceBuilder AddAccessReviews(
		this IGovernanceBuilder builder,
		IConfiguration configuration)
	{
		ArgumentNullException.ThrowIfNull(builder);
		ArgumentNullException.ThrowIfNull(configuration);

		_ = builder.Services.AddOptions<AccessReviewOptions>()
			.Bind(configuration)
			.ValidateOnStart();

		// Register AOT-safe validator
		builder.Services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<AccessReviewOptions>, AccessReviewOptionsValidator>());

		return builder.AddAccessReviewsCore();
	}

	private static IGovernanceBuilder AddAccessReviewsCore(this IGovernanceBuilder builder)
	{
		// Fallback in-memory store (overridable)
		builder.Services.TryAddSingleton<IAccessReviewStore, InMemoryAccessReviewStore>();

		// Background service for expired campaign processing
		builder.Services.TryAddEnumerable(
			ServiceDescriptor.Singleton<Microsoft.Extensions.Hosting.IHostedService, AccessReviewExpiryService>());

		// Null notifier fallback (overridable)
		builder.Services.TryAddSingleton<IAccessReviewNotifier, NullAccessReviewNotifier>();

		return builder;
	}
}
