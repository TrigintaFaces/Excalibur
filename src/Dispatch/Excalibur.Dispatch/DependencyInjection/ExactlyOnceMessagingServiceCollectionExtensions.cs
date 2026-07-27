// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch;
using Excalibur.Dispatch.Options.Delivery;

using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering turnkey exactly-once messaging — the composed outbox, inbox, and
/// deduplication primitives behind a single call with a documented delivery-guarantee boundary.
/// </summary>
public static class ExactlyOnceMessagingServiceCollectionExtensions
{
	/// <summary>
	/// Registers turnkey exactly-once messaging by composing the outbox store, the inbox store, and the
	/// deduplication window into one registration with a documented delivery-guarantee boundary (see
	/// <see cref="ExactlyOnceOptions"/>).
	/// </summary>
	/// <remarks>
	/// <para>
	/// This composes the existing <see cref="DeliveryServiceCollectionExtensions.AddOutbox{TStore}"/> and
	/// <see cref="DeliveryServiceCollectionExtensions.AddInbox{TStore}"/> registrations (inheriting their
	/// startup capability guards) and ties the deduplication time-to-live to the inbox message time-to-live
	/// unless overridden (<see cref="ExactlyOnceOptions.DeduplicationTimeToLive"/>).
	/// </para>
	/// <para>
	/// <b>Guarantee boundary:</b> exactly-once when the inbox store implements
	/// <see cref="ITransactionalInboxStore"/>; otherwise at-least-once with durable deduplication (handlers must
	/// be idempotent). Set <see cref="ExactlyOnceOptions.RequireTransactionalExactlyOnce"/> to fail fast at
	/// startup unless the transactional guarantee is available.
	/// </para>
	/// </remarks>
	/// <typeparam name="TOutboxStore">The outbox store implementation type.</typeparam>
	/// <typeparam name="TInboxStore">The inbox store implementation type.</typeparam>
	/// <param name="services">The service collection to register into.</param>
	/// <param name="configure">An optional delegate to configure <see cref="ExactlyOnceOptions"/>.</param>
	/// <returns>The same <see cref="IServiceCollection"/> for chaining.</returns>
	public static IServiceCollection AddExactlyOnceMessaging<
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TOutboxStore,
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TInboxStore>(
		this IServiceCollection services,
		Action<ExactlyOnceOptions>? configure = null)
		where TOutboxStore : class, IOutboxStore
		where TInboxStore : class, IInboxStore
	{
		ArgumentNullException.ThrowIfNull(services);

		// Compose the hardened outbox + inbox registrations (each brings its own ValidateOnStart capability guards).
		_ = services.AddOutbox<TOutboxStore>();
		_ = services.AddInbox<TInboxStore>();

		var builder = services.AddOptions<ExactlyOnceOptions>();
		if (configure is not null)
		{
			_ = builder.Configure(configure);
		}

		// Tie the dedup TTL to the inbox message TTL unless explicitly overridden, so a message can never be
		// re-admitted by the inbox after its dedup record has expired (which would defeat suppression).
		// Reuse the SAME OptionsBuilder (one builder per options type) so the PostConfigure shares the
		// Validate + ValidateOnStart chain below — a second AddOptions<T>() here registers an unvalidated
		// configuration path the ValidateOnStart guard flags.
		_ = builder
			.PostConfigure<IOptions<InboxOptions>>(static (exactlyOnce, inbox) =>
			{
				exactlyOnce.DeduplicationTimeToLive ??= inbox.Value.DefaultMessageTimeToLive;
			});

		_ = builder.Validate(
				static options => ExactlyOnceOptions.Validate(options) is null,
				"ExactlyOnceOptions failed validation.")
			.ValidateOnStart();

		// Structural guarantee-boundary guard: an explicit transactional-exactly-once request fails fast at
		// startup unless the inbox store can honor it, rather than silently degrading to at-least-once.
		services.TryAddEnumerable(
			ServiceDescriptor.Singleton<IValidateOptions<ExactlyOnceOptions>, TransactionalExactlyOnceCapabilityValidator>());

		return services;
	}
}
