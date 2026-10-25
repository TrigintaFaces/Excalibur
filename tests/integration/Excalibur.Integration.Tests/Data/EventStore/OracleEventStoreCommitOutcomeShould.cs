// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.EventSourcing;
using Excalibur.EventSourcing.Oracle;

using Microsoft.Extensions.Logging.Abstractions;

using Oracle.ManagedDataAccess.Client;

using Shouldly;

using Tests.Shared.Conformance.EventStore;

namespace Excalibur.Integration.Tests.Data.EventStore;

/// <summary>
/// Binds the commit-certainty outcome of an Oracle append whose acknowledgement is lost.
/// </summary>
/// <remarks>
/// <para>
/// The fourth store onto the shared commit boundary. This one is worth reading for the argument it
/// replaced rather than the code: the handler used to justify reporting <see cref="AppendOutcome.Failed"/>
/// on the grounds that the transaction and connection are scoped to the method that raised, so both are
/// disposed and an uncommitted transaction rolled back while the exception unwinds. The mechanism is
/// real. The conclusion assumed the transaction was still UNCOMMITTED — and if the commit reached the
/// server and its acknowledgement was lost, the rollback does nothing, because there is nothing left to
/// roll back. Scoping proves the cleanup ran, never that the commit did not.
/// </para>
/// </remarks>
[Collection(OracleEventStoreTestCollection.CollectionName)]
[Trait("Category", "Integration")]
[Trait("Component", "EventStore")]
[Trait("Pattern", "FaultInjection")]
public sealed class OracleEventStoreCommitOutcomeShould(OracleEventStoreContainerFixture fixture)
{
	/// <summary>
	/// Verifies a lost commit acknowledgement is never reported as Failed.
	/// </summary>
	/// <param name="committed">Whether the injected fault is raised after the commit really landed.</param>
	/// <returns>A task representing the asynchronous operation.</returns>
	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task NeverReportFailedWhenTheCommitAcknowledgementIsLost(bool committed)
	{
		fixture.DockerAvailable.ShouldBeTrue("Commit-outcome evidence requires real Oracle; never skip.");
		await fixture.EnsureInitializedAsync().ConfigureAwait(true);

		var id = Guid.NewGuid().ToString("N");
		var store = new OracleEventStore(
			() => new OracleConnection(fixture.ConnectionString),
			NullLogger<OracleEventStore>.Instance,
			SingleTenantTestContext.Instance,
			schema: fixture.Schema,
			table: fixture.TableName)
		{
			CommitTransactionAsync = async (transaction, token) =>
			{
				if (committed)
				{
					await transaction.CommitAsync(token).ConfigureAwait(false);
				}

				// A connection reset, a command timeout, or a pause past the client timeout all arrive
				// here, and this handler cannot tell them from a fault that preceded the commit.
				throw CreateInjectedOracleFault();
			},
		};

		TestDomainEvent[] events = [new() { AggregateId = id, Data = "stable-payload" }];
		var result = await store.AppendAsync(id, "CommitOutcome", events, -1, CancellationToken.None)
			.ConfigureAwait(true);

		// THE INVARIANT: never Failed, on either side.
		result.Outcome.ShouldNotBe(
			AppendOutcome.Failed,
			"Failed asserts nothing was written, which this store cannot establish once the commit was sent");
		result.IsConcurrencyConflict.ShouldBeFalse("no other writer took this version");

		// LIVENESS: the rows really are durable when the fault followed a real commit, so Failed would
		// have been a falsehood about state the caller can go and read.
		var loaded = await store.LoadAsync(id, "CommitOutcome", CancellationToken.None).ConfigureAwait(true);
		loaded.Count.ShouldBe(committed ? 1 : 0);
	}

	// OracleException cannot be constructed directly, and the handler filters on it specifically -- a
	// plain Exception would propagate instead of exercising the path under test. Forcing a real one from
	// the driver keeps the fault the same SHAPE the store actually filters on.
	private static Exception CreateInjectedOracleFault()
	{
		try
		{
			using var connection = new OracleConnection(
				"User Id=unused;Password=unused;Data Source=127.0.0.1:1/unused;Connection Timeout=1");
			connection.Open();
		}
		catch (OracleException ex)
		{
			return ex;
		}

		throw new InvalidOperationException("Could not manufacture an OracleException for the injected fault.");
	}
}
