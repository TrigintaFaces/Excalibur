// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Compliance.Diagnostics;
using Excalibur.Dispatch;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Excalibur.Compliance.CryptoShredding;

/// <summary>
/// Refuses to start a crypto-shredding composition whose key-destruction ledger is missing, or whose ledger
/// contradicts the erasure wiring beside it.
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
/// <b>An always-negative ledger exists, and is deliberately not a default.</b> It can never report a field as
/// erased, which is the safe direction and exactly why defaulting to it would be wrong: a consumer whose
/// erasure wiring slipped would get a composition that silently never reports an erasure, with nothing
/// anywhere to notice. So it is reachable only by naming it --
/// <c>AddCryptoShreddingWithoutErasure()</c> -- and an absence still produces a loud refusal here.
/// </para>
/// <para>
/// The second arm catches the other half of that choice: the no-erasure ledger registered <em>beside</em> an
/// erasure store. Every ledger registration in this framework is a <c>TryAdd</c>, so the two would resolve by
/// registration order, and one of the two orders puts the always-false ledger in front of a real erasure
/// store -- a deployment that erases, never tombstones, and reports nothing. Order-dependence on this
/// question is refused rather than documented.
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
			if (isService.IsService(typeof(NoErasureKeyDestructionLedger))
				&& isService.IsService(typeof(IErasureStore)))
			{
				LogLedgerContradictsErasureWiring();

				throw new InvalidOperationException(
					"This composition registers BOTH the no-erasure key-destruction ledger "
					+ "(AddCryptoShreddingWithoutErasure) and an erasure store, which answer the same question "
					+ "differently. Ledger registrations are TryAdd, so which one wins depends on the order the "
					+ "two calls were made in -- and if the no-erasure one won, this deployment would perform "
					+ "erasures that never tombstone anything, and read its own erased subjects back in the "
					+ "clear with nothing to report it. Remove one: keep the erasure store and call "
					+ "AddCryptoShredding() if this deployment erases, or drop the erasure store if it does "
					+ "not.");
			}

			ValidateMintLedger(isService);

			return;
		}

		LogLedgerNotRegistered();

		throw new InvalidOperationException(
			"Crypto-shredding is configured but no key-destruction ledger (IKeyDestructionLedger) is "
			+ "registered, so IFieldEncryptor cannot be constructed and every read of an encrypted personal "
			+ "field would fail. "
			+ RemedyGuidance);
	}

	/// <summary>
	/// The same two arms, for the MINT ledger — and the failure it prevents is the inverse one.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The destruction ledger decides whether a field reports as erased; a missing one makes every
	/// crypto-shredded READ fail, which is loud. The mint ledger decides which handles an erasure can
	/// ENUMERATE, and its failure is silent in the other direction: an erasure that enumerates nothing
	/// destroys only the handles it can derive from the declared retentions and the data inventory, and then
	/// signs a completion certificate over whatever it missed. Nothing downstream ever learns otherwise.
	/// </para>
	/// <para>
	/// That is why the no-erasure mint ledger THROWS on enumeration rather than answering with an empty
	/// list — and why the contradiction arm below matters more here than it does above. Ledger registrations
	/// are <c>TryAdd</c>, so a composition carrying both resolves by registration order; in one of the two
	/// orders a real erasure's enumeration is answered by a type holding no rows. The throw makes that loud
	/// at the first erasure, and this guard makes it loud at start-up instead.
	/// </para>
	/// </remarks>
	/// <param name="isService">The registration probe.</param>
	private void ValidateMintLedger(IServiceProviderIsService isService)
	{
		if (isService.IsService(typeof(IKeyMintLedger)))
		{
			if (isService.IsService(typeof(NoErasureKeyMintLedger))
				&& isService.IsService(typeof(IErasureStore)))
			{
				LogMintLedgerContradictsErasureWiring();

				throw new InvalidOperationException(
					"This composition registers BOTH the no-erasure key-mint ledger "
					+ "(AddCryptoShreddingWithoutErasure) and an erasure store, which answer the same "
					+ "question differently. Ledger registrations are TryAdd, so which one wins depends on "
					+ "the order the two calls were made in -- and if the no-erasure one won, an erasure "
					+ "would be unable to enumerate the handles holding this subject's personal fields. It "
					+ "refuses rather than returning an empty list, so the erasure fails loudly instead of "
					+ "destroying only what it can derive and certifying; this check moves that discovery to "
					+ "start-up. Remove one: keep the erasure store and call AddCryptoShredding() if this "
					+ "deployment erases, or drop the erasure store if it does not.");
			}

			// A VOLATILE MINT LEDGER IS REFUSED WHEN THIS DEPLOYMENT ERASES, because its empty enumeration
			// is a CONFORMING answer and therefore undetectable downstream. The contract documents two
			// meanings for an empty list -- no fields were ever written, and the fields predate this
			// ledger -- and "the rows were discarded" is indistinguishable from both. So an erasure run
			// after a restart enumerates nothing, destroys only the handles it can derive from declared
			// retentions, and signs a completion certificate over everything else.
			//
			// RESOLVED RATHER THAN PROBED, which is the one place this guard departs from registration-only
			// inspection, and it is safe for a stated reason: every mint-ledger registration this framework
			// makes is a singleton, so resolving it from the root provider captures nothing and throws
			// nothing under scope validation. The property cannot be read any other way -- that is the
			// point of it being on the implementation rather than a separately registrable token.
			var mintLedger = _services.GetService<IKeyMintLedger>();

			if (mintLedger is not null && !mintLedger.IsDurable && isService.IsService(typeof(IErasureStore)))
			{
				// LOGGED EVEN WHEN ACKNOWLEDGED, AND AT Critical, which is the half that does the work.
				//
				// The refusal below cannot fire for the in-memory store, because registering a call named
				// AddInMemoryErasureStore() IS the consumer stating the choice by name -- that is the shape
				// Microsoft uses for the same situation (UseInMemoryDatabase,
				// AddDeveloperSigningCredential): name it unmistakably rather than try to detect
				// production, which a framework cannot do. So for THAT composition the remaining protection
				// is this line, and it must not be conditional on the acknowledgement: a host running
				// erasures against a ledger that forgets them emits a Critical record naming the
				// consequence at every single startup. That removes the SILENCE, which is the whole
				// discriminator -- a loud recoverable condition instead of a signed false completion
				// nothing reports.
				LogMintLedgerNotDurable(mintLedger.GetType().Name);
			}

			if (mintLedger is not null
				&& !mintLedger.IsDurable
				&& isService.IsService(typeof(IErasureStore))
				&& !isService.IsService(typeof(NonDurableErasureLedgerAcknowledged)))
			{
				// REFUSED only when the volatility was NOT announced. This is the case the log above cannot
				// reach: a consumer's own IKeyMintLedger, or a third-party store, whose registration name
				// says nothing about losing its rows. There the consumer has no way to know, so startup
				// stops rather than emitting a record they were never told to look for.
				throw new InvalidOperationException(
					$"The registered key-mint ledger ({mintLedger.GetType().Name}) does not survive a "
					+ "process restart, and this composition registers an erasure store, so its erasures "
					+ "are relied upon. After a restart an erasure would enumerate NO handles for a data "
					+ "subject, destroy only the handles it can derive from declared retentions, and sign "
					+ "a completion certificate over every personal field still readable under the rest -- "
					+ "and nothing downstream could tell, because an empty enumeration is also what a "
					+ "subject with no personal fields looks like. Register a durable erasure store "
					+ "instead: AddPostgresErasureStore() or AddSqlServerErasureStore(). If this is a "
					+ "development or test host and you accept that erasures here attest nothing, say so "
					+ "by name with AcknowledgeNonDurableErasureLedger() -- it is deliberately a separate "
					+ "call, because a deployment that silently accepted this would be the failure itself.");
			}

			return;
		}

		LogMintLedgerNotRegistered();

		throw new InvalidOperationException(
			"Crypto-shredding is configured but no key-mint ledger (IKeyMintLedger) is registered. The write "
			+ "path records every key handle it is about to mint, BEFORE minting it, so that an erasure can "
			+ "enumerate the handles holding a data subject's personal fields -- the declared retentions and "
			+ "the data inventory can only answer which handles SHOULD exist, and a handle neither of them "
			+ "implies is a key no erasure destroys. Without this registration ISubjectKeyManager cannot be "
			+ "constructed and no personal field can be written at all. "
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
		+ "instance it registers the erasure store from."
		+ " IF THIS DEPLOYMENT DOES NOT ERASE -- it encrypts personal data at rest and never destroys a "
		+ "subject key -- then an erasure store is the wrong remedy and you want "
		+ "AddCryptoShreddingWithoutErasure() instead of AddCryptoShredding(). That registers a ledger "
		+ "holding no rows, so every generation reports as not destroyed, which is the true answer in that "
		+ "deployment rather than a stand-in. It is a separate call and never a default, because a ledger "
		+ "that always answers \"not destroyed\" cannot fabricate an erasure -- and that safety is exactly "
		+ "why defaulting to one would be wrong: a deployment that DOES erase but never registered its "
		+ "store would then silently never tombstone anything and read its own erased subjects back in the "
		+ "clear, with nothing to report it.";

	[LoggerMessage(
 ComplianceEventId.CryptoShreddingLedgerNotRegistered,
 LogLevel.Critical,
 "Crypto-shredding is configured but no IKeyDestructionLedger is registered. IFieldEncryptor cannot be "
 + "constructed, so every read of an encrypted personal field will fail. Register an erasure store "
 + "(AddInMemoryErasureStore, AddPostgresErasureStore or AddSqlServerErasureStore) -- it supplies the ledger")]
	private partial void LogLedgerNotRegistered();

	[LoggerMessage(
 ComplianceEventId.CryptoShreddingLedgerContradictsErasureWiring,
 LogLevel.Critical,
 "Crypto-shredding registers both the no-erasure key-destruction ledger and an erasure store. Which one "
 + "answers a destruction query depends on registration order, and if the no-erasure one wins this "
 + "deployment erases without ever tombstoning. Startup is refused")]
	private partial void LogLedgerContradictsErasureWiring();

	[LoggerMessage(
 ComplianceEventId.CryptoShreddingLedgerWiringUnverifiable,
 LogLevel.Critical,
 "Crypto-shredding could not determine whether an IKeyDestructionLedger is registered, because this "
 + "container supplies no IServiceProviderIsService. Startup is refused rather than assuming it is wired")]
	private partial void LogLedgerWiringUnverifiable();

	[LoggerMessage(
 ComplianceEventId.CryptoShreddingMintLedgerNotRegistered,
 LogLevel.Critical,
 "Crypto-shredding is configured but no IKeyMintLedger is registered. ISubjectKeyManager cannot be "
 + "constructed, so no personal field can be written -- and an erasure would have no enumeration of the "
 + "handles holding a subject's data. Register an erasure store (AddInMemoryErasureStore, "
 + "AddPostgresErasureStore or AddSqlServerErasureStore) -- it supplies the ledger")]
	private partial void LogMintLedgerNotRegistered();

	[LoggerMessage(
 ComplianceEventId.CryptoShreddingMintLedgerContradictsErasureWiring,
 LogLevel.Critical,
 "Crypto-shredding registers both the no-erasure key-mint ledger and an erasure store. Which one answers "
 + "an enumeration depends on registration order, and if the no-erasure one wins no erasure in this "
 + "deployment can enumerate the handles holding a subject's personal fields. Startup is refused")]
	private partial void LogMintLedgerContradictsErasureWiring();

	[LoggerMessage(
 ComplianceEventId.CryptoShreddingMintLedgerNotDurable,
 LogLevel.Critical,
 "The registered key-mint ledger {LedgerType} does not survive a process restart, and this composition "
 + "registers an erasure store. After a restart an erasure would enumerate no handles for a data subject "
 + "and certify a completion over whatever it could not derive. Startup is refused; register a durable "
 + "erasure store, or state the choice with AcknowledgeNonDurableErasureLedger()")]
	private partial void LogMintLedgerNotDurable(string ledgerType);
}

/// <summary>
/// A consumer's explicit statement that this host's erasures are not relied upon, so a mint ledger that
/// loses its rows on restart is acceptable here.
/// </summary>
/// <remarks>
/// <para>
/// <b>A marker type, and it attests the CONSUMER'S intent rather than an implementation's property —
/// which is why a standalone registrable token is the right shape here and the wrong shape for
/// durability itself.</b> Durability is a fact about an implementation, so a token that could be
/// registered apart from it could lie; it lives on <see cref="IKeyMintLedger.IsDurable"/> for that
/// reason. Whether a deployment relies on its erasures is a fact about the deployment, which nothing but
/// the consumer can state.
/// </para>
/// <para>
/// It carries no members. Its registration IS the statement.
/// </para>
/// </remarks>
internal sealed class NonDurableErasureLedgerAcknowledged;
