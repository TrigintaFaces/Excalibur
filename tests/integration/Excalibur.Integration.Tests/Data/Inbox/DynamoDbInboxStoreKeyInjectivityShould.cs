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
/// Real-infrastructure lock binding the requirement that the DynamoDB inbox sort key is INJECTIVE in
/// (tenant, message) against a live LocalStack DynamoDB container.
/// </summary>
/// <remarks>
/// <para>
/// The sort key was composed as <c>{tenantTerm}:{messageId}</c>. Neither term is validated against any
/// charset -- both are caller data -- so tenant "a:b" with message "c" and tenant "a" with message "b:c"
/// composed the SAME sort key and shared one item within the handler partition.
/// </para>
/// <para>
/// This is the dedup key, so the collision is silent: the second message fails the conditional put, the
/// caller reads that as a duplicate, and the message is dropped -- never processed, never retried.
/// Silent message loss, across a tenant boundary.
/// </para>
/// <para>
/// Exercised through the real store against real DynamoDB rather than by asserting the composed string,
/// so the property under test is whether the second tenant's message is actually delivered. Never
/// skipped: the fixture fails fast when Docker is unavailable.
/// </para>
/// </remarks>
[Collection(DynamoDbInboxStoreTestCollection.CollectionName)]
[Trait("Category", "Integration")]
[Trait("Database", "DynamoDb")]
[Trait("Component", "Inbox")]
public sealed class DynamoDbInboxStoreKeyInjectivityShould
{
	private const string Handler = "OrderPlacedHandler";
	private readonly DynamoDbInboxStoreContainerFixture _fixture;

	public DynamoDbInboxStoreKeyInjectivityShould(DynamoDbInboxStoreContainerFixture fixture) =>
		_fixture = fixture;

	/// <summary>
	/// SAFETY: a colon shifted across the tenant/message boundary must not read as an already-seen message.
	/// </summary>
	[Fact]
	public async Task Not_treat_a_shifted_colon_tuple_as_an_already_seen_message()
	{
		// ONE table, so both stores address the same keyspace -- the collision is only observable when the
		// two tenants share it.
		var tableName = await CreateTableAsync().ConfigureAwait(false);

		try
		{
			// Tenant "a:b" receives message "c". Under the bare join this wrote the sort key "a:b:c".
			var shiftedIntoTenant = CreateStore(tableName, "a:b");
			_ = await shiftedIntoTenant.CreateEntryAsync(
				"c", Handler, "TestMessageType", [1],
				new Dictionary<string, object>(StringComparer.Ordinal), CancellationToken.None)
				.ConfigureAwait(false);

			// A DIFFERENT tenant, "a", receives a DIFFERENT message, "b:c".
			// Under the bare join this composed the identical sort key.
			var shiftedIntoMessage = CreateStore(tableName, "a");
			var seenByOtherTenant = await shiftedIntoMessage
				.GetEntryAsync("b:c", Handler, CancellationToken.None).ConfigureAwait(false);

			seenByOtherTenant.ShouldBeNull(
				"tenant 'a' message 'b:c' is a different message belonging to a different tenant than "
				+ "tenant 'a:b' message 'c'. If the colon shifting across the boundary makes them share a "
				+ "sort key, the second fails the conditional put, is read as a duplicate, and is never "
				+ "processed and never retried -- silent message loss across a tenant boundary.");
		}
		finally
		{
			await _fixture.DeleteTableAsync(tableName, CancellationToken.None).ConfigureAwait(false);
		}
	}

	/// <summary>
	/// LIVENESS: an ordinary message still writes, reads back, and is recognised for its own tenant.
	/// </summary>
	/// <remarks>
	/// Required. Without it the safety arm is satisfied by a store that writes every entry under a unique
	/// key and finds nothing on read -- perfectly non-colliding, deduplicating nothing at all.
	/// </remarks>
	[Fact]
	public async Task Still_store_and_find_an_ordinary_message_for_its_own_tenant()
	{
		var tableName = await CreateTableAsync().ConfigureAwait(false);

		try
		{
			var store = CreateStore(tableName, "tenant-7");

			_ = await store.CreateEntryAsync(
				"order-42", Handler, "TestMessageType", [1],
				new Dictionary<string, object>(StringComparer.Ordinal), CancellationToken.None)
				.ConfigureAwait(false);

			var found = await store.GetEntryAsync("order-42", Handler, CancellationToken.None)
				.ConfigureAwait(false);

			found.ShouldNotBeNull(
				"an ordinary message must still be found by the tenant that received it, or the store "
				+ "recognises no duplicates at all");
		}
		finally
		{
			await _fixture.DeleteTableAsync(tableName, CancellationToken.None).ConfigureAwait(false);
		}
	}

	/// <summary>
	/// OVER-CORRECTION GUARD: terms that legitimately contain a colon must still write and read back.
	/// </summary>
	/// <remarks>
	/// A "fix" that rejected or stripped colons would pass the safety arm while breaking dedup for every
	/// consumer whose ids contain one -- a URN message id, for instance.
	/// </remarks>
	[Fact]
	public async Task Still_find_a_message_whose_terms_contain_a_colon()
	{
		var tableName = await CreateTableAsync().ConfigureAwait(false);

		try
		{
			var store = CreateStore(tableName, "a:b");

			_ = await store.CreateEntryAsync(
				"urn:uuid:9f8c", Handler, "TestMessageType", [1],
				new Dictionary<string, object>(StringComparer.Ordinal), CancellationToken.None)
				.ConfigureAwait(false);

			var found = await store.GetEntryAsync("urn:uuid:9f8c", Handler, CancellationToken.None)
				.ConfigureAwait(false);

			found.ShouldNotBeNull(
				"a colon is legal caller data in both terms. Making the key injective must not cost dedup "
				+ "for the messages whose ids contain one");
		}
		finally
		{
			await _fixture.DeleteTableAsync(tableName, CancellationToken.None).ConfigureAwait(false);
		}
	}

	private async Task<string> CreateTableAsync()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"LocalStack DynamoDB container must be available — real-infra injectivity lock is never "
			+ $"skipped: {_fixture.InitializationError}");

		var tableName = $"{_fixture.TableName}_{Guid.NewGuid():N}";

		// One store creates the table; the per-tenant stores below then share it.
		var bootstrap = CreateStore(tableName, "bootstrap");
		await bootstrap.InitializeAsync(CancellationToken.None).ConfigureAwait(false);

		return tableName;
	}

	private DynamoDbInboxStore CreateStore(string tableName, string tenantId)
	{
		var options = Options.Create(new DynamoDbInboxOptions
		{
			TableName = tableName,
			CreateTableIfNotExists = true,
			DefaultTtlSeconds = 0,
			Connection = new DynamoDbConnectionOptions { ServiceUrl = _fixture.ServiceUrl },
		});

		return new DynamoDbInboxStore(
			_fixture.Client, options, NullLogger<DynamoDbInboxStore>.Instance,
			new FixedTenantContext(tenantId));
	}

	/// <summary>A minimal ambient tenant context pinned to a single tenant id.</summary>
	private sealed class FixedTenantContext(string? tenantId) : ITenantContext
	{
		public string? TenantId { get; } = tenantId;

		public bool HasTenant => !string.IsNullOrEmpty(TenantId);
	}
}
