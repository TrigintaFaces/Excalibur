// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Compliance.Diagnostics;
using Excalibur.Dispatch;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Excalibur.Compliance.CryptoShredding;

/// <summary>
/// Refuses to start a crypto-shredding composition that has registered no key-destruction ledger.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="FieldEncryptor"/> requires <see cref="IKeyDestructionLedger"/>, and every registration of one
/// hangs off an erasure store. So a consumer who wires crypto-shredding and no erasure store has a container
/// that cannot construct the field encryptor at all — and because the encryptor is registered scoped, the
/// failure surfaces on the first REQUEST that resolves it, as a dependency-injection error naming a type the
/// consumer never asked for. This guard moves that discovery to start-up and replaces the message with one
/// that names the remedy.
/// </para>
/// <para>
/// <b>There is deliberately no empty-ledger default to fall back on.</b> An always-negative ledger would be
/// safe in the narrow sense that it can never report a field as erased, which is exactly why it is tempting
/// and exactly why it is refused: a consumer whose wiring slipped would get a composition that silently never
/// reports an erasure, with nothing anywhere to notice. A loud refusal is the only honest option.
/// </para>
/// <para>
/// The check inspects service <em>registration</em> through <see cref="IServiceProviderIsService"/> and never
/// resolves the probed service: this guard is a singleton holding the root provider, where resolving a scoped
/// service throws or yields a rooted captive. Probing registration returns the same verdict under either
/// scope-validation setting, constructs nothing, and uses no reflection.
/// </para>
/// <para>
/// Registered as both an <see cref="IHostedService"/> and an <see cref="IStartupPrerequisiteValidator"/>, so
/// it fires for a consumer who starts a host AND for one who builds a provider and calls
/// <c>ValidateStartupGates</c>. **A consumer who does neither still gets the dependency-injection error on
/// first resolve** — that path is unchanged and is not something this guard can reach.
/// </para>
/// </remarks>
internal sealed partial class CryptoShreddingLedgerWiringValidator : IHostedService, IStartupPrerequisiteValidator
{
	private readonly IServiceProvider _services;
	private readonly ILogger<CryptoShreddingLedgerWiringValidator> _logger;

	/// <summary>
	/// Initializes a new instance of the <see cref="CryptoShreddingLedgerWiringValidator"/> class.
	/// </summary>
	/// <param name="services">The root service provider, used to probe registration.</param>
	/// <param name="logger">The logger.</param>
	/// <exception cref="ArgumentNullException">Thrown when any argument is null.</exception>
	public CryptoShreddingLedgerWiringValidator(
		IServiceProvider services,
		ILogger<CryptoShreddingLedgerWiringValidator> logger)
	{
		_services = services ?? throw new ArgumentNullException(nameof(services));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
	}

	/// <inheritdoc/>
	public Task StartAsync(CancellationToken cancellationToken)
	{
		Validate();
		return Task.CompletedTask;
	}

	/// <inheritdoc/>
	public void Validate()
	{
		var isService = _services.GetService<IServiceProviderIsService>();

		if (isService is null)
		{
			// Registration cannot be probed on this container, so the question was not answered. Reporting
			// success here would turn "not measured" into "verified", which is the shape this guard exists to
			// prevent -- and the consequence of a false pass is a composition whose every crypto-shredded read
			// fails on the first request.
			LogLedgerWiringUnverifiable();

			throw new InvalidOperationException(
				"Crypto-shredding could not verify that a key-destruction ledger is registered, because this "
				+ "container does not supply IServiceProviderIsService. It will not start rather than assume "
				+ "the ledger is wired. "
				+ RemedyGuidance);
		}

		if (isService.IsService(typeof(IKeyDestructionLedger)))
		{
			return;
		}

		LogLedgerNotRegistered();

		throw new InvalidOperationException(
			"Crypto-shredding is configured but no key-destruction ledger (IKeyDestructionLedger) is "
			+ "registered, so IFieldEncryptor cannot be constructed and every read of an encrypted personal "
			+ "field would fail. "
			+ RemedyGuidance);
	}

	/// <inheritdoc/>
	public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

	/// <summary>
	/// The remedy, naming registrations this framework actually ships.
	/// </summary>
	/// <remarks>
	/// It explains WHY an erasure store is the answer, because "register an erasure store" is baffling advice
	/// to a consumer who only wanted field encryption and never mentioned erasure. A message that names a
	/// remedy without connecting it to the symptom reads as a non-sequitur and sends the reader to the source.
	/// </remarks>
	private const string RemedyGuidance =
		"A crypto-shredded field reports as erased only on the strength of a durable destruction record, and "
		+ "the erasure store is what holds that record -- which is why registering one is the remedy here. "
		+ "Call AddInMemoryErasureStore() for development, or AddPostgresErasureStore() / "
		+ "AddSqlServerErasureStore() for a durable one; each registers the ledger from the same store "
		+ "instance it registers the erasure store from.";

	[LoggerMessage(
 ComplianceEventId.CryptoShreddingLedgerNotRegistered,
 LogLevel.Critical,
 "Crypto-shredding is configured but no IKeyDestructionLedger is registered. IFieldEncryptor cannot be "
 + "constructed, so every read of an encrypted personal field will fail. Register an erasure store "
 + "(AddInMemoryErasureStore, AddPostgresErasureStore or AddSqlServerErasureStore) -- it supplies the ledger")]
	private partial void LogLedgerNotRegistered();

	[LoggerMessage(
 ComplianceEventId.CryptoShreddingLedgerWiringUnverifiable,
 LogLevel.Critical,
 "Crypto-shredding could not determine whether an IKeyDestructionLedger is registered, because this "
 + "container supplies no IServiceProviderIsService. Startup is refused rather than assuming it is wired")]
	private partial void LogLedgerWiringUnverifiable();
}
