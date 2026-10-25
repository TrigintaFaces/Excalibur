// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Net;

using Excalibur.EventSourcing;
using Excalibur.EventSourcing.CosmosDb;

using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Shouldly;

using Tests.Shared.Conformance.EventStore;

namespace Excalibur.Integration.Tests.Data.EventStore;

/// <summary>
/// Binds the commit-certainty outcome of a Cosmos DB append whose response is lost.
/// </summary>
/// <remarks>
/// Like the other two cloud stores, this one replaced a weaker fix that keyed the outcome on whether our
/// own transient classification matched the fault. That was a proxy for a property of the client library
/// which nothing in this repository reads, and it left a permanent fault arriving after a landed write
/// still reporting <see cref="AppendOutcome.Failed"/>. The store now records whether a write was handed
/// to the driver at all.
/// </remarks>
[Collection(CosmosDbEventStoreTestCollection.CollectionName)]
[Trait("Category", "Integration")]
[Trait("Component", "EventStore")]
[Trait("Pattern", "FaultInjection")]
public sealed class CosmosDbEventStoreCommitOutcomeShould(CosmosDbEventStoreContainerFixture fixture)
{
	/// <summary>
	/// Verifies a lost write response is never reported as Failed.
	/// </summary>
	/// <param name="written">Whether the injected fault is raised after the write really landed.</param>
	/// <returns>A task representing the asynchronous operation.</returns>
	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task NeverReportFailedWhenTheWriteResponseIsLost(bool written)
	{
		fixture.DockerAvailable.ShouldBeTrue("Commit-outcome evidence requires the real Cosmos emulator; never skip.");

		var id = Guid.NewGuid().ToString("N");
		var store = new CosmosDbEventStore(
			fixture.Client,
			Options.Create(new CosmosDbEventStoreOptions
			{
				DatabaseName = fixture.DatabaseName,
				EventsContainerName = "commit_outcome_events",
			}),
			NullLogger<CosmosDbEventStore>.Instance,
			SingleTenantTestContext.Instance)
		{
			AppendWriteInterceptor = async (write, token) =>
			{
				if (written)
				{
					// The REAL write, so the written case is genuinely durable and a Failed report would
					// be a falsehood about state the caller can go and read.
					_ = await write(token).ConfigureAwait(false);
				}

				throw new CosmosException(
					"Injected loss of write response", HttpStatusCode.ServiceUnavailable, 0, activityId: string.Empty, requestCharge: 0d);
			},
		};

		IEventStore eventStore = store;
		TestDomainEvent[] events = [new() { AggregateId = id, Data = "stable-payload" }];
		var result = await eventStore.AppendAsync(id, "CommitOutcome", events, -1, CancellationToken.None)
			.ConfigureAwait(true);

		result.Outcome.ShouldNotBe(
			AppendOutcome.Failed,
			"Failed asserts nothing was written, which this store cannot establish once the driver was invoked");
		result.IsConcurrencyConflict.ShouldBeFalse("no other writer took this version");

		var loaded = await eventStore.LoadAsync(id, "CommitOutcome", CancellationToken.None).ConfigureAwait(true);
		loaded.Count.ShouldBe(written ? 1 : 0);
	}
}
