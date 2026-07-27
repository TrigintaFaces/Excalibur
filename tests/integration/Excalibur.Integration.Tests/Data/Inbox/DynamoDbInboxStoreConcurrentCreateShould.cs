// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.DynamoDb;
using Excalibur.Dispatch;
using Excalibur.Inbox.DynamoDb;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Shouldly;

namespace Excalibur.Integration.Tests.Data.Inbox;

/// <summary>
/// Real-infrastructure regression lock for <see cref="DynamoDbInboxStore"/>'s cold-start table
/// auto-create race against a live LocalStack DynamoDB.
/// </summary>
/// <remarks>
/// <para>
/// N store instances cold-start against the SAME not-yet-existing table: each
/// <see cref="DynamoDbInboxStore.InitializeAsync"/> runs <c>EnsureTableExistsAsync</c>, which does
/// <c>DescribeTable</c> → <c>ResourceNotFound</c> → <c>CreateTable</c>. Only the first
/// <c>CreateTableAsync</c> wins; every loser receives a <em>server-side</em>
/// <c>ResourceInUseException</c> that must be swallowed and fall through to the wait-for-active loop.
/// </para>
/// <para>
/// Non-vacuity: the exception is raised by the real DynamoDB service, not a mock — no unit fake
/// reproduces the concurrent-create <c>ResourceInUseException</c>. Pre-fix (no catch around
/// <c>CreateTableAsync</c>) the losing initializer throws and this test goes RED; post-fix all N
/// initializers complete and the table ends ACTIVE. Never skipped: a missing container fails fast.
/// </para>
/// </remarks>
[Collection(DynamoDbInboxStoreTestCollection.CollectionName)]
[Trait("Category", "Integration")]
[Trait("Database", "DynamoDb")]
[Trait("Component", "Inbox")]
public sealed class DynamoDbInboxStoreConcurrentCreateShould : IClassFixture<DynamoDbInboxStoreContainerFixture>
{
	private const int Concurrency = 8;
	private readonly DynamoDbInboxStoreContainerFixture _fixture;

	public DynamoDbInboxStoreConcurrentCreateShould(DynamoDbInboxStoreContainerFixture fixture)
	{
		_fixture = fixture;
	}

	[Fact]
	public async Task Tolerate_concurrent_cold_start_table_creation_without_throwing()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"LocalStack DynamoDB container must be available - real-infra cold-start race is never skipped: "
			+ $"{_fixture.InitializationError}");

		// A single fresh table name shared by every racing instance, so all take the
		// DescribeTable -> ResourceNotFound -> CreateTable cold-start path simultaneously.
		var tableName = $"{_fixture.TableName}_concurrent_{Guid.NewGuid():N}";

		DynamoDbInboxStore CreateStore()
		{
			var options = Options.Create(new DynamoDbInboxOptions
			{
				TableName = tableName,
				CreateTableIfNotExists = true,
				DefaultTtlSeconds = 0,
				Connection = new DynamoDbConnectionOptions { ServiceUrl = _fixture.ServiceUrl },
			});
			return new DynamoDbInboxStore(_fixture.Client, options, NullLogger<DynamoDbInboxStore>.Instance);
		}

		var stores = Enumerable.Range(0, Concurrency).Select(_ => CreateStore()).ToArray();

		try
		{
			// Race all initializers. Pre-fix, the CreateTable losers throw ResourceInUseException
			// (an unhandled AmazonDynamoDBException) → Task.WhenAll faults → RED.
			var initTasks = stores
				.Select(store => Task.Run(() => store.InitializeAsync(CancellationToken.None)))
				.ToArray();

			// Must not throw — the loser's server-side ResourceInUseException is swallowed and
			// every instance converges on the wait-for-active loop.
			await Should.NotThrowAsync(() => Task.WhenAll(initTasks));

			// Sanity: the table is genuinely usable after the race (the winner created it,
			// the losers waited for ACTIVE) — a claim round-trips on the real provider.
			const string messageId = "msg-concurrent-create";
			const string handlerType = "TestHandler";
			(await stores[0].TryClaimAsync(messageId, handlerType, CancellationToken.None)).ShouldBeTrue(
				"after the concurrent cold-start the table must be ACTIVE and usable");
		}
		finally
		{
			foreach (var store in stores)
			{
				await store.DisposeAsync().ConfigureAwait(false);
			}

			await _fixture.DeleteTableAsync(tableName, CancellationToken.None).ConfigureAwait(false);
		}
	}
}
