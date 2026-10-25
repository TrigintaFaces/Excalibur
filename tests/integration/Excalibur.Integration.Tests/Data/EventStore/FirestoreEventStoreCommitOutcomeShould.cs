// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.EventSourcing;
using Excalibur.EventSourcing.Firestore;

using Grpc.Core;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Shouldly;

using Tests.Shared.Conformance.EventStore;

namespace Excalibur.Integration.Tests.Data.EventStore;

/// <summary>
/// Binds the commit-certainty outcome of a Firestore append whose acknowledgement is lost.
/// </summary>
/// <remarks>
/// <para>
/// This is the store that started the whole line of work, and the one where "dispatched" is least
/// visible: <c>RunTransactionAsync</c> commits INSIDE the driver, so there is no commit call of ours to
/// mark around. The moment the driver is invoked, a commit may have reached the server.
/// </para>
/// <para>
/// It also replaced an earlier, weaker fix. That one keyed the outcome on whether our own transient
/// classification matched the fault — a proxy for "the driver retried this code", which is a property of
/// the client library that nothing here reads, and which left a permanent fault arriving after a landed
/// commit still reporting <see cref="AppendOutcome.Failed"/>. The store now records whether a commit was
/// handed to the driver at all, which is the fact that proxy was standing in for.
/// </para>
/// </remarks>
[Collection(FirestoreEventStoreTestCollection.CollectionName)]
[Trait("Category", "Integration")]
[Trait("Component", "EventStore")]
[Trait("Pattern", "FaultInjection")]
public sealed class FirestoreEventStoreCommitOutcomeShould(FirestoreEventStoreContainerFixture fixture)
{
	/// <summary>
	/// Verifies a lost transaction acknowledgement is never reported as Failed.
	/// </summary>
	/// <param name="committed">Whether the injected fault is raised after the transaction really ran.</param>
	/// <returns>A task representing the asynchronous operation.</returns>
	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task NeverReportFailedWhenTheTransactionAcknowledgementIsLost(bool committed)
	{
		fixture.DockerAvailable.ShouldBeTrue("Commit-outcome evidence requires the real Firestore emulator; never skip.");

		var id = Guid.NewGuid().ToString("N");
		var store = new FirestoreEventStore(
			fixture.Db,
			Options.Create(new FirestoreEventStoreOptions
			{
				ProjectId = fixture.ProjectId,
				EventsCollectionName = fixture.CollectionName,
			}),
			NullLogger<FirestoreEventStore>.Instance,
			SingleTenantTestContext.Instance)
		{
			RunAppendTransactionInterceptor = async (runTransaction, token) =>
			{
				if (committed)
				{
					// The REAL transaction, so the committed case is genuinely durable and a Failed
					// report would be a falsehood about state the caller can go and read.
					await runTransaction(token).ConfigureAwait(false);
				}

				// A channel reset or a deadline after the driver has already committed arrives exactly
				// here, and the terminal handler cannot tell it from a fault raised before.
				throw new RpcException(new Status(StatusCode.Unavailable, "Injected loss of commit acknowledgement"));
			},
		};

		// Through IEventStore: the cloud-native overloads are the ones bound to the type, and the
		// consumer-facing contract under test here is the explicitly implemented interface.
		IEventStore eventStore = store;

		TestDomainEvent[] events = [new() { AggregateId = id, Data = "stable-payload" }];
		var result = await eventStore.AppendAsync(id, "CommitOutcome", events, -1, CancellationToken.None)
			.ConfigureAwait(true);

		// THE INVARIANT: never Failed, on either side.
		result.Outcome.ShouldNotBe(
			AppendOutcome.Failed,
			"Failed asserts nothing was written, which this store cannot establish once the driver was invoked");
		result.IsConcurrencyConflict.ShouldBeFalse("no other writer took this version");

		// LIVENESS: the events really are durable when the fault followed a real transaction.
		var loaded = await eventStore.LoadAsync(id, "CommitOutcome", CancellationToken.None).ConfigureAwait(true);
		loaded.Count.ShouldBe(committed ? 1 : 0);
	}
}
