// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Compliance.CryptoShredding;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Excalibur.EventSourcing.DependencyInjection;

/// <summary>
/// Marker registered by <c>AddEventStoreEncryption()</c> to record that the event store is wired for at-rest
/// per-subject field encryption. Its presence (or absence) lets the always-on wiring guard detect a
/// crypto-shred-configured-but-event-store-unencrypted state without walking the decorator chain.
/// </summary>
internal sealed class EventStoreEncryptionMarker;

/// <summary>
/// Startup-time guard that runs on every <c>AddEventSourcing()</c> (not only the crypto-shred opt-in) and
/// fails host start if GDPR crypto-shredding is configured (a per-subject <see cref="SubjectFieldCryptor"/> is
/// registered) while the event store is NOT wired for at-rest field encryption — i.e. the consumer configured
/// crypto-shred but never called <c>AddEventSourcingCryptoShredding()</c>. In that state
/// <c>[PersonalData]</c> event fields persist as plaintext and destroying a subject key erases nothing, so the
/// gap fails closed rather than booting as a silent, inert PII hole.
/// </summary>
/// <remarks>
/// <para>
/// The host does not start in this state. A misconfigured PII-at-rest control fails at startup rather than
/// serving traffic while silently defeating crypto-shredding. The predicate is event-store-only, so the
/// deferred projection/outbox/inbox surfaces do not widen it and cannot be broken by it.
/// </para>
/// <para>
/// Presence of the registration is not proof that encryption happens. This guard is the door; the
/// ciphertext-at-rest lock is the lock. Ship only this and we have shipped a marker.
/// </para>
/// <para>
/// The check inspects service <em>registration</em> via <see cref="IServiceProviderIsService"/> and never
/// resolves the probed services. This matters: the validator is a singleton and so holds the root provider,
/// where resolving a scoped service throws when scope validation is enabled and yields a rooted captive
/// instance when it is not. Probing registration returns the same verdict under both settings, and
/// constructs nothing. AOT-safe: no reflection.
/// </para>
/// </remarks>
internal sealed partial class CryptoShreddingWiringValidator : IHostedService
{
	private readonly IServiceProvider _services;
	private readonly ILogger<CryptoShreddingWiringValidator> _logger;

	public CryptoShreddingWiringValidator(IServiceProvider services, ILogger<CryptoShreddingWiringValidator> logger)
	{
		_services = services ?? throw new ArgumentNullException(nameof(services));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
	}

	public Task StartAsync(CancellationToken cancellationToken)
	{
		// Probe REGISTRATION, never RESOLUTION. This service is a singleton and therefore holds the ROOT
		// provider: resolving the scoped SubjectFieldCryptor from it throws InvalidOperationException
		// ("Cannot resolve scoped service ... from root provider") whenever scope validation is enabled —
		// which is the default for the Development environment. That fires on the CORRECTLY-wired consumer
		// too, because the probe runs before any verdict is computed. Where scope validation is disabled the
		// same resolve silently succeeds as a rooted captive instance, so the verdict was only ever right by
		// coincidence. IServiceProviderIsService answers the question actually being asked — "is this type
		// registered?" — without constructing anything, and returns the same verdict under both settings.
		var isService = _services.GetRequiredService<IServiceProviderIsService>();

		var cryptoShredConfigured = isService.IsService(typeof(SubjectFieldCryptor));
		var eventStoreEncryptionWired = isService.IsService(typeof(EventStoreEncryptionMarker));

		if (cryptoShredConfigured && !eventStoreEncryptionWired)
		{
			// Fail fast. A misconfigured PII-at-rest control must not boot: in this state [PersonalData]
			// event fields persist as plaintext and destroying a subject key erases nothing, so the host
			// would serve traffic while silently defeating crypto-shredding. Booting with a warning is a
			// documented limitation wearing a startup-validation badge. AddEventSourcingCryptoShredding()
			// is the one-line remedy, so no consumer is stranded by this throw.
			LogCryptoShredConfiguredButEventStoreUnencrypted();

			throw new InvalidOperationException(
				"GDPR crypto-shredding is configured but the event store is not wired for at-rest field "
				+ "encryption. [PersonalData] event fields would persist as plaintext and destroying a "
				+ "subject key would not erase them. Call AddEventSourcingCryptoShredding() to activate "
				+ "at-rest field encryption for the event store.");
		}

		return Task.CompletedTask;
	}

	public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

	[LoggerMessage(LogLevel.Critical,
		"GDPR crypto-shredding is configured but the event store is NOT wired for at-rest field encryption — "
		+ "[PersonalData] event fields will persist as plaintext and destroying a subject key will not erase them. "
		+ "Call AddEventSourcingCryptoShredding() to activate at-rest field encryption for the event store.")]
	private partial void LogCryptoShredConfiguredButEventStoreUnencrypted();
}
