// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.EventSourcing;
using Excalibur.EventSourcing.MongoDB;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using MongoDB.Driver;

using Shouldly;

using Tests.Shared.Conformance.EventStore;

namespace Excalibur.Integration.Tests.Data.EventStore;

/// <summary>
/// Binds the commit-certainty outcome of a MongoDB append whose acknowledgement is lost.
/// </summary>
/// <remarks>
/// <para>
/// The third store onto the shared commit boundary, after Postgres and SQL Server. What it asserts is
/// that the store does not claim <see cref="AppendOutcome.Failed"/> — which states that nothing was
/// written — for an append it cannot prove did not commit.
/// </para>
/// <para>
/// This store lands on the boundary's FLOOR rather than on a measurement, and the difference from SQL
/// Server is worth stating because it is the next improvement rather than a defect. Mongo does carry an
/// identity probe, but it runs on the duplicate-key and version-mismatch paths — the shapes a losing
/// racer arrives in. A generic fault after a successful write reaches neither, so there is nothing to
/// measure with and <see cref="AppendOutcome.Unknown"/> is the honest answer on both sides. SQL Server
/// can do better because its probe also sits in the after-failure position. Moving Mongo's there would
/// turn the committed case below into <see cref="AppendOutcome.AlreadyCommitted"/>.
/// </para>
/// </remarks>
[Collection(MongoDbEventStoreTestCollection.CollectionName)]
[Trait("Category", "Integration")]
[Trait("Component", "EventStore")]
[Trait("Pattern", "FaultInjection")]
public sealed class MongoDbEventStoreCommitOutcomeShould(MongoDbEventStoreContainerFixture fixture)
{
	/// <summary>
	/// Verifies a lost write acknowledgement is reported as Unknown, never as Failed.
	/// </summary>
	/// <param name="committed">Whether the injected fault is raised after the write really landed.</param>
	/// <returns>A task representing the asynchronous operation.</returns>
	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task ReportUnknownWhenTheWriteAcknowledgementIsLost(bool committed)
	{
		// No explicit schema step: the store creates its own collections on first append. The Docker
		// assertion is a hard requirement rather than a skip -- an arm that passes by not running is the
		// gap this whole line of work exists to close.
		fixture.DockerAvailable.ShouldBeTrue("Commit-outcome evidence requires real MongoDB; never skip.");

		var id = Guid.NewGuid().ToString("N");
		var store = new MongoDbEventStore(
			Options.Create(new MongoDbEventStoreOptions
			{
				ConnectionString = fixture.ConnectionString,
				DatabaseName = fixture.DatabaseName,
			}),
			NullLogger<MongoDbEventStore>.Instance,
			SingleTenantTestContext.Instance)
		{
			WriteEventDocumentsInterceptor = async (write, token) =>
			{
				if (committed)
				{
					// The REAL write, so the committed case is genuinely durable and a Failed report
					// would be a falsehood about state the caller can go and read.
					await write(token).ConfigureAwait(false);
				}

				// A MongoException specifically: the store's terminal handler is an allow-list on that type,
				// so a plain Exception would propagate instead of exercising the path under test.
				throw new MongoException("Injected loss of write acknowledgement");
			},
		};

		TestDomainEvent[] events = [new() { AggregateId = id, Data = "stable-payload" }];
		var result = await store.AppendAsync(id, "CommitOutcome", events, -1, CancellationToken.None)
			.ConfigureAwait(true);

		// THE INVARIANT: never Failed. The terminal handler cannot separate a fault that preceded the
		// write from one that followed a write which landed, so it has no grounds for asserting that
		// nothing was written.
		result.Outcome.ShouldNotBe(
			AppendOutcome.Failed,
			"Failed asserts nothing was written, which this store cannot establish once a write was sent");
		result.Outcome.ShouldBe(AppendOutcome.Unknown, "no probe reaches a generic post-write fault here");
		result.IsConcurrencyConflict.ShouldBeFalse("no other writer took this version");
		result.NextExpectedVersion.ShouldBeNull("an indeterminate append reached no known version");

		// LIVENESS, and the half that makes the committed case worth asserting: the event really is
		// durable when the fault followed a real write.
		var loaded = await store.LoadAsync(id, "CommitOutcome", CancellationToken.None).ConfigureAwait(true);
		loaded.Count.ShouldBe(committed ? 1 : 0);
	}
}
