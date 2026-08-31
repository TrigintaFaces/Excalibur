// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Compliance;
using Excalibur.Compliance.Configuration;
using Excalibur.Compliance.Encryption.Decorators;
using Excalibur.Compliance.Encryption.DependencyInjection;
using Excalibur.Dispatch;

using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Registration extensions that wire the inbox/outbox at-rest encryption decorators into a consumer's
/// dependency-injection container, so message payloads are encrypted at rest.
/// </summary>
/// <remarks>
/// <para>
/// <strong>These decorators encrypt under a single configured context, not per data subject.</strong> The
/// context is built once from the configured purpose and tenant; it carries no data-subject term, and the
/// payload reaching the decorator is already serialized, so no subject identity is available at this seam.
/// </para>
/// <para>
/// <strong>Consumer obligation.</strong> Erasing one data subject's key does <em>not</em> render that
/// subject's inbox or outbox payloads unrecoverable — those surfaces are encrypted under the shared context
/// and are unaffected by destroying an individual subject's key. Where an erasure guarantee must extend to
/// messages in flight, bound retention on these surfaces instead, so the payloads age out.
/// </para>
/// </remarks>
public static class StoreEncryptionServiceCollectionExtensions
{
	/// <summary>
	/// Decorates the registered <see cref="IInboxStore"/> with transparent at-rest payload encryption under the
	/// configured encryption context.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <returns>The service collection for method chaining.</returns>
	/// <remarks>
	/// <para>
	/// Encryption here is <strong>not</strong> keyed per data subject: destroying one subject's key leaves
	/// inbox payloads recoverable. See the type-level remarks for the consumer obligation this creates.
	/// </para>
	/// <para>
	/// Requires an <see cref="IEncryptionProviderRegistry"/> and <see cref="EncryptionOptions"/> to be registered
	/// (typically by the consumer's compliance-encryption setup). Inbox stores are registered under a
	/// provider-specific service key with a <c>"default"</c> forwarding alias; this decorates the terminal
	/// provider-keyed store so both keys resolve the encrypting decorator exactly once.
	/// </para>
	/// </remarks>
	public static IServiceCollection AddInboxEncryption(this IServiceCollection services)
	{
		ArgumentNullException.ThrowIfNull(services);

		// Decorate FIRST, record the marker ONLY if a store was actually decorated. A marker recorded on the
		// mere call would attest at-rest encryption for a container that has no inbox store to encrypt.
		var decorated = services.DecorateKeyedStores<IInboxStore>(
			static (inner, sp) => new EncryptingInboxStoreDecorator(
				inner,
				sp.GetRequiredService<IEncryptionProviderRegistry>(),
				sp.GetRequiredService<IOptions<EncryptionOptions>>()));

		if (decorated)
		{
			services.TryAddSingleton<InboxEncryptionMarker>();
		}

		return services;
	}

	/// <summary>
	/// Decorates the registered <see cref="IOutboxStore"/> with transparent at-rest payload encryption under the
	/// configured encryption context.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <returns>The service collection for method chaining.</returns>
	/// <remarks>
	/// <para>
	/// Encryption here is <strong>not</strong> keyed per data subject, so this decorator does <strong>not</strong>
	/// extend an erasure guarantee to outbox copies: destroying one subject's key leaves outbox payloads
	/// recoverable. See the type-level remarks for the consumer obligation this creates.
	/// </para>
	/// <para>
	/// Requires an <see cref="IEncryptionProviderRegistry"/> and <see cref="EncryptionOptions"/> to be registered.
	/// Outbox stores are registered under a provider-specific service key with a <c>"default"</c> forwarding
	/// alias; this decorates the terminal provider-keyed store so both keys resolve the encrypting decorator
	/// exactly once.
	/// </para>
	/// </remarks>
	public static IServiceCollection AddOutboxEncryption(this IServiceCollection services)
	{
		ArgumentNullException.ThrowIfNull(services);

		var decorated = services.DecorateKeyedStores<IOutboxStore>(
			static (inner, sp) => new EncryptingOutboxStoreDecorator(
				inner,
				sp.GetRequiredService<IEncryptionProviderRegistry>(),
				sp.GetRequiredService<IOptions<EncryptionOptions>>()));

		if (decorated)
		{
			services.TryAddSingleton<OutboxEncryptionMarker>();
		}

		return services;
	}

	/// <summary>
	/// Registers the narrow, fail-closed inbox and outbox at-rest-encryption wiring guards. Each fails host start
	/// only when GDPR crypto-shredding is configured, that store is registered, and that store's encryption
	/// marker is absent — so a consumer who configured crypto-shredding but never wired the store fails closed
	/// instead of persisting plaintext PII. Idempotent; safe to call from an always-on composition root.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <returns>The service collection for method chaining.</returns>
	public static IServiceCollection AddStoreEncryptionWiringGuards(this IServiceCollection services)
	{
		ArgumentNullException.ThrowIfNull(services);

		services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, InboxEncryptionWiringValidator>());
		services.TryAddEnumerable(ServiceDescriptor.Singleton<IStartupPrerequisiteValidator, InboxEncryptionWiringValidator>());
		services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, OutboxEncryptionWiringValidator>());
		services.TryAddEnumerable(ServiceDescriptor.Singleton<IStartupPrerequisiteValidator, OutboxEncryptionWiringValidator>());

		return services;
	}
}
