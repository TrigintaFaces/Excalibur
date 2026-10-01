// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Compliance;
using Excalibur.Compliance.Erasure;

using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for declaring the aggregate types an erasure must not destroy.
/// </summary>
public static class ErasureRetentionServiceCollectionExtensions
{
	/// <summary>
	/// Declares the aggregate types this deployment is legally obliged to keep through an erasure.
	/// </summary>
	/// <param name="services"> The service collection. </param>
	/// <param name="retentions"> The aggregate types that must survive an erasure. </param>
	/// <returns> The service collection for chaining. </returns>
	/// <remarks>
	/// <para>
	/// <b>Call this only where the law requires the data kept.</b> Every aggregate type named here survives
	/// an erasure whole and readable, including the personal data inside it, and the erasure record names it
	/// with the justification given. An aggregate type not named here is erased exactly as it is without
	/// this call.
	/// </para>
	/// <para>
	/// <b>Declare it before the data is written.</b> Naming a type here moves where its personal fields are
	/// encrypted to, which is what keeps the record readable afterwards. Events written before the
	/// declaration were encrypted under the subject's shared key, which the erasure destroys — they survive
	/// the tombstone with their personal fields no longer decryptable.
	/// </para>
	/// <para>
	/// The declarations are validated at host start: an aggregate type must be named, a justification must
	/// say more than which Article 17(3) ground was picked, and the period must be greater than zero.
	/// </para>
	/// </remarks>
	/// <example>
	/// <code>
	/// services.AddErasureRetention(new ErasureRetention
	/// {
	///     AggregateType = "SalesRecord",
	///     TenantId = TenantScope.UntenantedSentinel,
	///     Basis = LegalHoldBasis.LegalObligation,
	///     Justification = "Vehicle sales records are kept for six years under the tax code's record-keeping "
	///                   + "requirement and for product-recall traceability.",
	///     RetentionPeriod = TimeSpan.FromDays(365 * 6),
	/// });
	/// </code>
	/// </example>
	public static IServiceCollection AddErasureRetention(
		this IServiceCollection services,
		params ErasureRetention[] retentions)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(retentions);

		// Taken as values rather than through a mutable options bag. The declarations are snapshotted at
		// startup, so a bag handed back to a consumer accepts additions afterwards that have no effect and
		// raise no error -- and a retention that did not take effect is a legally-required record
		// destroyed. Calling this more than once accumulates; each call adds what it was given.
		_ = services.AddOptions<AggregateRetentionOptions>()
			.Configure(options =>
			{
				foreach (var retention in retentions)
				{
					options.Aggregates.Add(retention);
				}
			})
			.ValidateOnStart();

		services.TryAddEnumerable(ServiceDescriptor
			.Singleton<IValidateOptions<AggregateRetentionOptions>, ErasureRetentionDeclarationValidator>());

		// Singleton and not scoped: the declarations are a startup-time snapshot, and both consumers -- the
		// event-store contributor (a singleton) and the encryption path (scoped) -- must read the same set
		// within one erasure.
		services.TryAddSingleton<IErasureRetentionRegistry, ErasureRetentionRegistry>();

		return services;
	}
}
