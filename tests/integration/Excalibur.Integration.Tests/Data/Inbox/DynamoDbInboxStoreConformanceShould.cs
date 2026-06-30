// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.DynamoDb;
using Excalibur.Dispatch;
using Excalibur.Inbox.DynamoDb;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Shouldly;

using Tests.Shared.Conformance.Inbox;

namespace Excalibur.Integration.Tests.Data.Inbox;

/// <summary>
/// Real-infrastructure conformance tests for <see cref="DynamoDbInboxStore"/> using the Inbox Conformance
/// Test Kit against a LocalStack DynamoDB container.
/// </summary>
/// <remarks>
/// <para>
/// The store is constructed with the fixture's <em>default-config</em> DynamoDB client (no custom
/// serializers), so the suite exercises the real wire behaviour a default consumer client produces — the
/// conditional-write first-writer-wins dedup (<c>attribute_not_exists</c>), the
/// <c>ConditionalCheckFailedException</c> status transitions, and the item -> attribute-map round-trip a
/// mocked client could never reproduce. The real-infra suite is never skipped.
/// </para>
/// <para>
/// The store auto-creates its table: the injected-client constructor preserves the supplied client and
/// <see cref="DynamoDbInboxStore.InitializeAsync"/> runs the <c>CreateTableIfNotExists</c> path, so this
/// deriver exercises the real consumer-supplied-client auto-create flow (no fixture table pre-create);
/// cleanup deletes the per-test table.
/// </para>
/// </remarks>
[Collection(DynamoDbInboxStoreTestCollection.CollectionName)]
[Trait("Category", "Integration")]
[Trait("Component", "Core")]
[Trait("Database", "DynamoDb")]
public sealed class DynamoDbInboxStoreConformanceShould : InboxStoreConformanceTestBase, IClassFixture<DynamoDbInboxStoreContainerFixture>
{
	private readonly DynamoDbInboxStoreContainerFixture _fixture;
	private string? _tableName;

	/// <summary>
	/// Initializes a new instance of the <see cref="DynamoDbInboxStoreConformanceShould"/> class.
	/// </summary>
	/// <param name="fixture">The DynamoDb container fixture.</param>
	public DynamoDbInboxStoreConformanceShould(DynamoDbInboxStoreContainerFixture fixture)
	{
		_fixture = fixture;
	}

	/// <inheritdoc/>
	protected override async Task<IInboxStore> CreateStoreAsync()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"LocalStack DynamoDB container must be available for real-infra conformance (never skipped): " +
			$"{_fixture.InitializationError}");

		// Unique table per test for isolation. The store auto-creates it via InitializeAsync below.
		_tableName = $"{_fixture.TableName}_{Guid.NewGuid():N}";

		var options = Options.Create(new DynamoDbInboxOptions
		{
			TableName = _tableName,

			// Exercise the real consumer-supplied-client auto-create path.
			CreateTableIfNotExists = true,

			// Keep TTL auto-reap off so it never races the explicit-cleanup conformance tests.
			DefaultTtlSeconds = 0,
			Connection = new DynamoDbConnectionOptions { ServiceUrl = _fixture.ServiceUrl },
		});

		// Injected-client ctor preserves the supplied client; InitializeAsync runs CreateTableIfNotExists,
		// so the store creates its own table on the real consumer path (no fixture pre-create).
		var store = new DynamoDbInboxStore(_fixture.Client, options, NullLogger<DynamoDbInboxStore>.Instance);
		await store.InitializeAsync(CancellationToken.None).ConfigureAwait(false);

		return store;
	}

	/// <inheritdoc/>
	protected override async Task CleanupAsync()
	{
		if (_tableName is not null)
		{
			await _fixture.DeleteTableAsync(_tableName, CancellationToken.None).ConfigureAwait(false);
			_tableName = null;
		}
	}
}
