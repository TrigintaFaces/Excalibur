// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Compliance.CryptoShredding;
using Excalibur.Dispatch;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Excalibur.Compliance.Encryption.DependencyInjection;

/// <summary>
/// Startup guard that fails host start when GDPR crypto-shredding is configured (a per-subject
/// <see cref="SubjectFieldCryptor"/> is registered) while a registered <see cref="IInboxStore"/> is NOT wired
/// for at-rest field encryption — i.e. the consumer configured crypto-shred but never wired the inbox. In that
/// state inbox <c>[PersonalData]</c> payloads persist as plaintext and destroying a subject key erases nothing,
/// so the gap fails closed rather than booting as a silent, inert PII hole.
/// </summary>
/// <remarks>
/// The check inspects service <em>registration</em> via <see cref="IServiceProviderIsService"/> /
/// <see cref="IServiceProviderIsKeyedService"/> and never resolves the probed services: this guard is a
/// singleton holding the root provider, where resolving a scoped service throws or yields a rooted captive.
/// Probing registration returns the same verdict under both scope-validation settings and constructs nothing.
/// AOT-safe: no reflection. Narrow by design — it never widens to the outbox surface (that is a sibling guard).
/// </remarks>
internal sealed partial class InboxEncryptionWiringValidator : IHostedService
{
	private readonly IServiceProvider _services;
	private readonly ILogger<InboxEncryptionWiringValidator> _logger;

	public InboxEncryptionWiringValidator(IServiceProvider services, ILogger<InboxEncryptionWiringValidator> logger)
	{
		_services = services ?? throw new ArgumentNullException(nameof(services));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
	}

	public Task StartAsync(CancellationToken cancellationToken)
	{
		var isService = _services.GetRequiredService<IServiceProviderIsService>();
		var isKeyedService = _services.GetService<IServiceProviderIsKeyedService>();

		var cryptoShredConfigured = isService.IsService(typeof(SubjectFieldCryptor));
		var inboxStorePresent = isKeyedService?.IsKeyedService(typeof(IInboxStore), "default") ?? false;
		var inboxEncryptionWired = isService.IsService(typeof(InboxEncryptionMarker));

		if (cryptoShredConfigured && inboxStorePresent && !inboxEncryptionWired)
		{
			LogCryptoShredConfiguredButInboxUnencrypted();

			throw new InvalidOperationException(
				"GDPR crypto-shredding is configured but the inbox store is not wired for at-rest field "
				+ "encryption. Inbox [PersonalData] payloads would persist as plaintext and destroying a "
				+ "subject key would not erase them. Call AddEventSourcingCryptoShredding() (which wires the "
				+ "inbox) or AddInboxEncryption() to activate at-rest field encryption for the inbox.");
		}

		return Task.CompletedTask;
	}

	public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

	[LoggerMessage(LogLevel.Critical,
		"GDPR crypto-shredding is configured but the inbox store is NOT wired for at-rest field encryption — "
		+ "inbox [PersonalData] payloads will persist as plaintext and destroying a subject key will not erase "
		+ "them. Call AddEventSourcingCryptoShredding() or AddInboxEncryption() to activate it.")]
	private partial void LogCryptoShredConfiguredButInboxUnencrypted();
}

/// <summary>
/// Startup guard that fails host start when GDPR crypto-shredding is configured while a registered
/// <see cref="IOutboxStore"/> is NOT wired for at-rest field encryption. Sibling of
/// <see cref="InboxEncryptionWiringValidator"/>, narrow to the outbox surface.
/// </summary>
internal sealed partial class OutboxEncryptionWiringValidator : IHostedService
{
	private readonly IServiceProvider _services;
	private readonly ILogger<OutboxEncryptionWiringValidator> _logger;

	public OutboxEncryptionWiringValidator(IServiceProvider services, ILogger<OutboxEncryptionWiringValidator> logger)
	{
		_services = services ?? throw new ArgumentNullException(nameof(services));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
	}

	public Task StartAsync(CancellationToken cancellationToken)
	{
		var isService = _services.GetRequiredService<IServiceProviderIsService>();
		var isKeyedService = _services.GetService<IServiceProviderIsKeyedService>();

		var cryptoShredConfigured = isService.IsService(typeof(SubjectFieldCryptor));
		var outboxStorePresent = isKeyedService?.IsKeyedService(typeof(IOutboxStore), "default") ?? false;
		var outboxEncryptionWired = isService.IsService(typeof(OutboxEncryptionMarker));

		if (cryptoShredConfigured && outboxStorePresent && !outboxEncryptionWired)
		{
			LogCryptoShredConfiguredButOutboxUnencrypted();

			throw new InvalidOperationException(
				"GDPR crypto-shredding is configured but the outbox store is not wired for at-rest field "
				+ "encryption. Outbox [PersonalData] payloads would persist as plaintext and destroying a "
				+ "subject key would not erase them. Call AddEventSourcingCryptoShredding() (which wires the "
				+ "outbox) or AddOutboxEncryption() to activate at-rest field encryption for the outbox.");
		}

		return Task.CompletedTask;
	}

	public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

	[LoggerMessage(LogLevel.Critical,
		"GDPR crypto-shredding is configured but the outbox store is NOT wired for at-rest field encryption — "
		+ "outbox [PersonalData] payloads will persist as plaintext and destroying a subject key will not erase "
		+ "them. Call AddEventSourcingCryptoShredding() or AddOutboxEncryption() to activate it.")]
	private partial void LogCryptoShredConfiguredButOutboxUnencrypted();
}
