// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Amazon.DynamoDBv2.Model;

using Excalibur.EventSourcing;
using Excalibur.EventSourcing.DynamoDb;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Shouldly;

using Tests.Shared.Conformance.EventStore;

namespace Excalibur.Integration.Tests.Data.EventStore;

/// <summary>
/// Binds the commit-certainty outcome of a DynamoDB append whose response is lost.
/// </summary>
/// <remarks>
/// The last of the eight stores onto the shared commit boundary. As with Cosmos and Firestore, the fix
/// records whether a write was handed to the driver rather than inferring it from a retry
/// classification, which was a proxy for a client-library property nothing here reads.
/// </remarks>
[Collection(DynamoDbEventStoreTestCollection.CollectionName)]
[Trait("Category", "Integration")]
[Trait("Component", "EventStore")]
[Trait("Pattern", "FaultInjection")]
public sealed class DynamoDbEventStoreCommitOutcomeShould(DynamoDbEventStoreContainerFixture fixture)
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
		fixture.DockerAvailable.ShouldBeTrue("Commit-outcome evidence requires real DynamoDB Local; never skip.");

		var id = Guid.NewGuid().ToString("N");
		var store = new DynamoDbEventStore(
			fixture.Client,
			Options.Create(new DynamoDbEventStoreOptions { EventsTableName = fixture.TableName }),
			NullLogger<DynamoDbEventStore>.Instance,
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

				throw new InternalServerErrorException("Injected loss of write response");
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
