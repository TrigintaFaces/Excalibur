// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


#pragma warning disable IDE0270 // Null check can be simplified

using Excalibur.Dispatch;
using Excalibur.Dispatch.ErrorHandling;

namespace Excalibur.Testing.Conformance;

/// <summary>
/// Abstract base class for IDeadLetterStore conformance testing.
/// </summary>
/// <remarks>
/// <para>
/// Inherit from this class and implement <see cref="CreateStore(ITenantContext)"/> to verify that
/// your dead letter store implementation conforms to the IDeadLetterStore contract.
/// </para>
/// <para>
/// The test kit verifies core dead letter store operations including store, retrieval,
/// replay marking, deletion, counting, and cleanup scenarios.
/// </para>
/// <para>
/// <strong>IMPORTANT:</strong> IDeadLetterStore uses a two-ID system:
/// <list type="bullet">
/// <item><description><c>Id</c> - Internal unique identifier (used as dictionary key)</description></item>
/// <item><description><c>MessageId</c> - API parameter for GetByIdAsync, DeleteAsync, MarkAsReplayedAsync</description></item>
/// </list>
/// All API methods that accept a messageId parameter search by MessageId, not by Id.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// public class SqlServerDeadLetterStoreConformanceTests : DeadLetterStoreConformanceTestKit
/// {
///     private readonly SqlServerFixture _fixture;
///
///     protected override IDeadLetterStore CreateStore(ITenantContext? ambientTenant) =&gt;
///         new SqlServerDeadLetterStore(_fixture.ConnectionString, ambientTenant);
///
///     protected override async Task CleanupAsync() =&gt;
///         await _fixture.CleanupAsync();
/// }
/// </code>
/// </example>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1707:Identifiers should not contain underscores",
	Justification = "Test method naming convention")]
public abstract class DeadLetterStoreConformanceTestKit
{
	/// <summary>
	/// Creates a dead letter store that resolves its tenant from the supplied ambient context.
	/// </summary>
	/// <param name="ambientTenant">
	/// The ambient tenant context the store must consult on every operation, or <see langword="null"/> for
	/// a single-tenant host that registers none.
	/// </param>
	/// <returns>An IDeadLetterStore implementation to test.</returns>
	/// <remarks>
	/// <para>
	/// The context is a parameter — rather than the tenant itself — because that is how tenancy actually
	/// works in this family: a store resolves the ambient tenant per operation, so the kit can hold
	/// <strong>one</strong> store and change the tenant between calls. Both partitions then address the
	/// <strong>same backing store</strong>, which is what makes the isolation arm falsifiable.
	/// </para>
	/// <para>
	/// A seam that handed out one store per tenant would let an implementation satisfy isolation by
	/// instance separation — two independent stores share no entries, so the arm passes even with the
	/// tenant predicate deleted. The kit is deliberately unable to obtain a second store for that reason.
	/// </para>
	/// <para>
	/// Implementations must consult the context on <em>every</em> operation, including writes and deletes.
	/// The store returned here is reused across a case, so caching the resolved tenant at construction
	/// will fail the isolation arm.
	/// </para>
	/// </remarks>
	protected abstract IDeadLetterStore CreateStore(ITenantContext? ambientTenant);

	/// <summary>
	/// Creates a store for a single-tenant host — one that registers no tenant context at all.
	/// </summary>
	/// <returns>An IDeadLetterStore implementation to test.</returns>
	/// <remarks>
	/// The default for the non-tenancy cases. <see langword="null"/> is the correct model of a
	/// single-tenant host: a context that exists but resolves no tenant means "multi-tenancy is active but
	/// unresolved", which implementations are expected to fail closed on rather than treat as unscoped.
	/// </remarks>
	private IDeadLetterStore CreateStore() => CreateStore(ambientTenant: null);

	/// <summary>
	/// An ambient tenant context whose resolved tenant the kit controls.
	/// </summary>
	/// <remarks>
	/// <para>
	/// This is what makes the isolation arm mean anything. The kit takes <strong>one</strong> store and
	/// changes the ambient tenant between operations, so both partitions address the <strong>same
	/// backing store</strong> — exactly as a singleton store resolving a scoped context does in a real
	/// host.
	/// </para>
	/// <para>
	/// Obtaining a second store per tenant instead would let an implementation satisfy isolation by
	/// <em>instance separation</em>: two independent stores never share an entry, so the arm would pass
	/// with the tenant predicate deleted. That is a test of the fixture, not of the contract.
	/// </para>
	/// </remarks>
	private sealed class SwitchableTenantContext : ITenantContext
	{
		public string? TenantId { get; private set; }

		public bool HasTenant => TenantId is not null;

		/// <summary>Switches the ambient tenant for subsequent operations.</summary>
		/// <param name="tenantId"> The tenant to resolve from now on. </param>
		public void SwitchTo(string tenantId) => TenantId = tenantId;
	}

	/// <summary>
	/// Optional cleanup after each test.
	/// </summary>
	/// <returns>A task representing the cleanup operation.</returns>
	protected virtual Task CleanupAsync() => Task.CompletedTask;

	/// <summary>
	/// Creates a test dead letter message with the given message ID.
	/// </summary>
	/// <param name="messageId">Optional message identifier. If not provided, a new GUID is generated.</param>
	/// <returns>A test dead letter message.</returns>
	protected virtual DeadLetterMessage CreateDeadLetterMessage(string? messageId = null) =>
		new()
		{
			MessageId = messageId ?? GenerateMessageId(),
			MessageType = "TestMessageType",
			MessageBody = "{}",
			MessageMetadata = "{}",
			Reason = "Test reason",
			MovedToDeadLetterAt = DateTimeOffset.UtcNow,
		};

	/// <summary>
	/// Generates a unique message ID for test isolation.
	/// </summary>
	/// <returns>A unique message identifier.</returns>
	protected virtual string GenerateMessageId() => Guid.NewGuid().ToString("N");

	#region Store Tests

	/// <summary>
	/// Verifies that storing a new message persists it successfully.
	/// </summary>
	public virtual async Task StoreAsync_ShouldPersistMessage()
	{
		var store = CreateStore();
		var message = CreateDeadLetterMessage();

		await store.StoreAsync(message, CancellationToken.None).ConfigureAwait(false);

		var retrieved = await store.GetByIdAsync(message.MessageId, CancellationToken.None).ConfigureAwait(false);

		if (retrieved is null)
		{
			throw new TestFixtureAssertionException(
				$"Message with MessageId {message.MessageId} was not found after StoreAsync");
		}

		if (retrieved.MessageBody != message.MessageBody)
		{
			throw new TestFixtureAssertionException(
				$"MessageBody mismatch. Expected: {message.MessageBody}, Actual: {retrieved.MessageBody}");
		}

		if (retrieved.MessageType != message.MessageType)
		{
			throw new TestFixtureAssertionException(
				$"MessageType mismatch. Expected: {message.MessageType}, Actual: {retrieved.MessageType}");
		}
	}

	/// <summary>
	/// Verifies that storing a null message throws ArgumentNullException.
	/// </summary>
	public virtual async Task StoreAsync_WithNullMessage_ShouldThrow()
	{
		var store = CreateStore();

		try
		{
			await store.StoreAsync(null!, CancellationToken.None).ConfigureAwait(false);
			throw new TestFixtureAssertionException(
				"Expected ArgumentNullException but no exception was thrown");
		}
		catch (ArgumentNullException)
		{
			// Expected
		}
	}

	/// <summary>
	/// Verifies that storing multiple messages persists all of them.
	/// </summary>
	public virtual async Task StoreAsync_MultipleMessages_ShouldPersistAll()
	{
		var store = CreateStore();

		var message1 = CreateDeadLetterMessage();
		var message2 = CreateDeadLetterMessage();
		var message3 = CreateDeadLetterMessage();

		await store.StoreAsync(message1, CancellationToken.None).ConfigureAwait(false);
		await store.StoreAsync(message2, CancellationToken.None).ConfigureAwait(false);
		await store.StoreAsync(message3, CancellationToken.None).ConfigureAwait(false);

		var filter = new DeadLetterFilter { MaxResults = 100 };
		var all = await store.GetMessagesAsync(filter, CancellationToken.None).ConfigureAwait(false);
		var allList = all.ToList();

		var messageIds = new[] { message1.MessageId, message2.MessageId, message3.MessageId };
		foreach (var messageId in messageIds)
		{
			if (!allList.Any(m => m.MessageId == messageId))
			{
				throw new TestFixtureAssertionException(
					$"Message with MessageId {messageId} was not found after storing multiple messages");
			}
		}
	}

	#endregion

	#region Retrieval Tests

	/// <summary>
	/// Verifies that GetMessagesAsync returns empty for an empty store with empty filter.
	/// </summary>
	public virtual async Task GetMessagesAsync_EmptyStore_ShouldReturnEmpty()
	{
		var store = CreateStore();
		var filter = new DeadLetterFilter();

		var all = await store.GetMessagesAsync(filter, CancellationToken.None).ConfigureAwait(false);

		if (all.Any())
		{
			throw new TestFixtureAssertionException(
				"Expected empty result from empty store, but got messages");
		}
	}

	/// <summary>
	/// Verifies that GetByIdAsync returns the correct message by MessageId.
	/// </summary>
	public virtual async Task GetByIdAsync_ShouldReturnMessageByMessageId()
	{
		var store = CreateStore();
		var message = CreateDeadLetterMessage();

		await store.StoreAsync(message, CancellationToken.None).ConfigureAwait(false);

		var retrieved = await store.GetByIdAsync(message.MessageId, CancellationToken.None).ConfigureAwait(false);

		if (retrieved is null)
		{
			throw new TestFixtureAssertionException(
				$"GetByIdAsync should return message for MessageId {message.MessageId}");
		}

		if (retrieved.MessageId != message.MessageId)
		{
			throw new TestFixtureAssertionException(
				$"MessageId mismatch. Expected: {message.MessageId}, Actual: {retrieved.MessageId}");
		}
	}

	/// <summary>
	/// Verifies that GetByIdAsync returns null for non-existent MessageId.
	/// </summary>
	public virtual async Task GetByIdAsync_NonExistent_ShouldReturnNull()
	{
		var store = CreateStore();
		var nonExistentId = GenerateMessageId();

		var retrieved = await store.GetByIdAsync(nonExistentId, CancellationToken.None).ConfigureAwait(false);

		if (retrieved is not null)
		{
			throw new TestFixtureAssertionException(
				"GetByIdAsync should return null for non-existent MessageId");
		}
	}

	/// <summary>
	/// Verifies that GetMessagesAsync filters by MessageType correctly.
	/// </summary>
	public virtual async Task GetMessagesAsync_FilterByMessageType_ShouldFilter()
	{
		var store = CreateStore();

		var message1 = CreateDeadLetterMessage();
		message1.MessageType = "TypeA";

		var message2 = CreateDeadLetterMessage();
		message2.MessageType = "TypeB";

		await store.StoreAsync(message1, CancellationToken.None).ConfigureAwait(false);
		await store.StoreAsync(message2, CancellationToken.None).ConfigureAwait(false);

		var filter = new DeadLetterFilter { MessageType = "TypeA" };
		var results = await store.GetMessagesAsync(filter, CancellationToken.None).ConfigureAwait(false);
		var resultsList = results.ToList();

		if (resultsList.Count != 1)
		{
			throw new TestFixtureAssertionException(
				$"Expected 1 message with TypeA, got {resultsList.Count}");
		}

		if (resultsList[0].MessageType != "TypeA")
		{
			throw new TestFixtureAssertionException(
				$"Expected MessageType 'TypeA', got '{resultsList[0].MessageType}'");
		}
	}

	/// <summary>
	/// Verifies that GetMessagesAsync respects MaxResults for pagination.
	/// </summary>
	public virtual async Task GetMessagesAsync_Pagination_ShouldRespectMaxResults()
	{
		var store = CreateStore();

		// Store 5 messages
		for (var i = 0; i < 5; i++)
		{
			var message = CreateDeadLetterMessage();
			await store.StoreAsync(message, CancellationToken.None).ConfigureAwait(false);
		}

		var filter = new DeadLetterFilter { MaxResults = 2 };
		var results = await store.GetMessagesAsync(filter, CancellationToken.None).ConfigureAwait(false);
		var resultsList = results.ToList();

		if (resultsList.Count > 2)
		{
			throw new TestFixtureAssertionException(
				$"Expected at most 2 messages due to MaxResults, got {resultsList.Count}");
		}
	}

	#endregion

	#region Replay Tests

	/// <summary>
	/// Verifies that MarkAsReplayedAsync sets IsReplayed to true.
	/// </summary>
	public virtual async Task MarkAsReplayedAsync_ShouldSetIsReplayedTrue()
	{
		var store = CreateStore();
		var message = CreateDeadLetterMessage();
		message.IsReplayed = false;

		await store.StoreAsync(message, CancellationToken.None).ConfigureAwait(false);

		await store.MarkAsReplayedAsync(message.MessageId, CancellationToken.None).ConfigureAwait(false);

		var retrieved = await store.GetByIdAsync(message.MessageId, CancellationToken.None).ConfigureAwait(false);

		if (retrieved is null)
		{
			throw new TestFixtureAssertionException(
				"Message should remain in store after MarkAsReplayedAsync");
		}

		if (!retrieved.IsReplayed)
		{
			throw new TestFixtureAssertionException(
				"IsReplayed should be true after MarkAsReplayedAsync");
		}

		if (retrieved.ReplayedAt is null)
		{
			throw new TestFixtureAssertionException(
				"ReplayedAt should be set after MarkAsReplayedAsync");
		}
	}

	/// <summary>
	/// Verifies that MarkAsReplayedAsync is idempotent for non-existent messages.
	/// </summary>
	public virtual async Task MarkAsReplayedAsync_NonExistent_ShouldBeIdempotent()
	{
		var store = CreateStore();
		var nonExistentId = GenerateMessageId();

		// Should not throw - idempotent operation
		await store.MarkAsReplayedAsync(nonExistentId, CancellationToken.None).ConfigureAwait(false);

		// Success - no exception thrown
	}

	/// <summary>
	/// Verifies that MarkAsReplayedAsync is idempotent for already replayed messages.
	/// </summary>
	public virtual async Task MarkAsReplayedAsync_AlreadyReplayed_ShouldBeIdempotent()
	{
		var store = CreateStore();
		var message = CreateDeadLetterMessage();

		await store.StoreAsync(message, CancellationToken.None).ConfigureAwait(false);

		// Mark as replayed twice
		await store.MarkAsReplayedAsync(message.MessageId, CancellationToken.None).ConfigureAwait(false);
		await store.MarkAsReplayedAsync(message.MessageId, CancellationToken.None).ConfigureAwait(false);

		var retrieved = await store.GetByIdAsync(message.MessageId, CancellationToken.None).ConfigureAwait(false);

		if (retrieved is null)
		{
			throw new TestFixtureAssertionException(
				"Message should remain in store after double MarkAsReplayedAsync");
		}

		if (!retrieved.IsReplayed)
		{
			throw new TestFixtureAssertionException(
				"IsReplayed should still be true after double MarkAsReplayedAsync");
		}
	}

	#endregion

	#region Delete Tests

	/// <summary>
	/// Verifies that DeleteAsync removes the message and returns true.
	/// </summary>
	public virtual async Task DeleteAsync_ShouldRemoveAndReturnTrue()
	{
		var store = CreateStore();
		var message = CreateDeadLetterMessage();

		await store.StoreAsync(message, CancellationToken.None).ConfigureAwait(false);

		var result = await store.DeleteAsync(message.MessageId, CancellationToken.None).ConfigureAwait(false);

		if (!result)
		{
			throw new TestFixtureAssertionException(
				"DeleteAsync should return true for existing message");
		}

		var retrieved = await store.GetByIdAsync(message.MessageId, CancellationToken.None).ConfigureAwait(false);

		if (retrieved is not null)
		{
			throw new TestFixtureAssertionException(
				"Message should not be retrievable after DeleteAsync");
		}
	}

	/// <summary>
	/// Verifies that DeleteAsync returns false for non-existent messages.
	/// </summary>
	public virtual async Task DeleteAsync_NonExistent_ShouldReturnFalse()
	{
		var store = CreateStore();
		var nonExistentId = GenerateMessageId();

		var result = await store.DeleteAsync(nonExistentId, CancellationToken.None).ConfigureAwait(false);

		if (result)
		{
			throw new TestFixtureAssertionException(
				"DeleteAsync should return false for non-existent message");
		}
	}

	/// <summary>
	/// Verifies that count decreases after DeleteAsync.
	/// </summary>
	public virtual async Task DeleteAsync_ShouldDecreaseCount()
	{
		var store = CreateStore();
		var message = CreateDeadLetterMessage();

		await store.StoreAsync(message, CancellationToken.None).ConfigureAwait(false);

		if (store is not IDeadLetterStoreAdmin adminBefore)
		{
			return; // Store does not implement admin interface; skip count verification
		}

		var countBefore = await adminBefore.GetCountAsync(CancellationToken.None).ConfigureAwait(false);

		_ = await store.DeleteAsync(message.MessageId, CancellationToken.None).ConfigureAwait(false);

		var countAfter = await adminBefore.GetCountAsync(CancellationToken.None).ConfigureAwait(false);

		if (countAfter >= countBefore)
		{
			throw new TestFixtureAssertionException(
				$"Count should decrease after delete. Before: {countBefore}, After: {countAfter}");
		}
	}

	#endregion

	#region Count Tests

	/// <summary>
	/// Verifies that GetCountAsync returns 0 for an empty store.
	/// </summary>
	public virtual async Task GetCountAsync_EmptyStore_ShouldReturnZero()
	{
		var store = CreateStore();

		if (store is not IDeadLetterStoreAdmin admin)
		{
			return; // Store does not implement admin interface; skip count verification
		}

		var count = await admin.GetCountAsync(CancellationToken.None).ConfigureAwait(false);

		if (count != 0)
		{
			throw new TestFixtureAssertionException(
				$"Expected count 0 for empty store, got {count}");
		}
	}

	/// <summary>
	/// Verifies that GetCountAsync returns correct count after storing messages.
	/// </summary>
	public virtual async Task GetCountAsync_AfterStores_ShouldReturnCorrectCount()
	{
		var store = CreateStore();

		await store.StoreAsync(CreateDeadLetterMessage(), CancellationToken.None).ConfigureAwait(false);
		await store.StoreAsync(CreateDeadLetterMessage(), CancellationToken.None).ConfigureAwait(false);
		await store.StoreAsync(CreateDeadLetterMessage(), CancellationToken.None).ConfigureAwait(false);

		if (store is not IDeadLetterStoreAdmin adminCount)
		{
			return; // Store does not implement admin interface; skip count verification
		}

		var count = await adminCount.GetCountAsync(CancellationToken.None).ConfigureAwait(false);

		if (count != 3)
		{
			throw new TestFixtureAssertionException(
				$"Expected count 3 after storing 3 messages, got {count}");
		}
	}

	#endregion

	#region Cleanup Tests

	/// <summary>
	/// Verifies that CleanupOldMessagesAsync removes old messages.
	/// </summary>
	public virtual async Task CleanupOldMessagesAsync_ShouldRemoveOldMessages()
	{
		var store = CreateStore();

		// Create an old message (beyond retention)
		var oldMessage = CreateDeadLetterMessage();
		oldMessage.MovedToDeadLetterAt = DateTimeOffset.UtcNow.AddDays(-10);

		// Create a recent message (within retention)
		var recentMessage = CreateDeadLetterMessage();
		recentMessage.MovedToDeadLetterAt = DateTimeOffset.UtcNow;

		await store.StoreAsync(oldMessage, CancellationToken.None).ConfigureAwait(false);
		await store.StoreAsync(recentMessage, CancellationToken.None).ConfigureAwait(false);

		// Cleanup with 5-day retention (should remove 10-day old message)
		if (store is not IDeadLetterStoreAdmin cleanupAdmin)
		{
			return; // Store does not implement admin interface; skip cleanup verification
		}

		var removedCount = await cleanupAdmin.CleanupOldMessagesAsync(5, CancellationToken.None).ConfigureAwait(false);

		if (removedCount != 1)
		{
			throw new TestFixtureAssertionException(
				$"Expected 1 message removed, got {removedCount}");
		}

		// Verify old message is gone
		var retrievedOld = await store.GetByIdAsync(oldMessage.MessageId, CancellationToken.None).ConfigureAwait(false);
		if (retrievedOld is not null)
		{
			throw new TestFixtureAssertionException(
				"Old message should have been removed by cleanup");
		}

		// Verify recent message remains
		var retrievedRecent = await store.GetByIdAsync(recentMessage.MessageId, CancellationToken.None).ConfigureAwait(false);
		if (retrievedRecent is null)
		{
			throw new TestFixtureAssertionException(
				"Recent message should remain after cleanup");
		}
	}

	/// <summary>
	/// Verifies that CleanupOldMessagesAsync respects retention period.
	/// </summary>
	public virtual async Task CleanupOldMessagesAsync_ShouldRespectRetention()
	{
		var store = CreateStore();

		// Create message at 5 days old
		var message = CreateDeadLetterMessage();
		message.MovedToDeadLetterAt = DateTimeOffset.UtcNow.AddDays(-5);

		await store.StoreAsync(message, CancellationToken.None).ConfigureAwait(false);

		// Cleanup with 7-day retention (should NOT remove 5-day old message)
		if (store is not IDeadLetterStoreAdmin retentionAdmin)
		{
			return; // Store does not implement admin interface; skip retention verification
		}

		var removedCount = await retentionAdmin.CleanupOldMessagesAsync(7, CancellationToken.None).ConfigureAwait(false);

		if (removedCount != 0)
		{
			throw new TestFixtureAssertionException(
				$"Expected 0 messages removed (message within retention), got {removedCount}");
		}

		// Verify message remains
		var retrieved = await store.GetByIdAsync(message.MessageId, CancellationToken.None).ConfigureAwait(false);
		if (retrieved is null)
		{
			throw new TestFixtureAssertionException(
				"Message within retention period should remain after cleanup");
		}
	}

	#endregion

	#region Tenant Isolation Tests

	/// <summary>
	/// SAFETY: an entry written by one tenant must not be observable by another.
	/// </summary>
	/// <remarks>
	/// A dead-letter entry carries the failed message body, so a read that crosses tenants discloses one
	/// tenant's message content to another. This case is mandatory: a store that cannot discriminate
	/// tenants is not a conformant implementation of this contract.
	/// </remarks>
	public virtual async Task TenantScopedRead_MustNotSeeAnotherTenantsEntry()
	{
		// ONE store, ONE backing set, ambient tenant switched between operations. Two stores would let an
		// implementation pass this by instance separation with no tenant predicate at all.
		var ambient = new SwitchableTenantContext();
		var store = CreateStore(ambient);

		ambient.SwitchTo("conformance-tenant-a");
		var message = CreateDeadLetterMessage();
		await store.StoreAsync(message, CancellationToken.None).ConfigureAwait(false);

		ambient.SwitchTo("conformance-tenant-b");

		// Addressed read: B must not resolve A's entry by its identifier.
		var byId = await store.GetByIdAsync(message.MessageId, CancellationToken.None).ConfigureAwait(false);
		if (byId is not null)
		{
			throw new TestFixtureAssertionException(
				$"Tenant isolation violated: tenant B resolved tenant A's entry {message.MessageId} by id, "
				+ "disclosing the failed message body across tenants.");
		}

		// Unfiltered enumeration: the shape most likely to leak, because a filter that specifies nothing
		// must still be scoped rather than returning the estate.
		var listed = await store.GetMessagesAsync(new DeadLetterFilter(), CancellationToken.None)
			.ConfigureAwait(false);
		foreach (var entry in listed)
		{
			if (string.Equals(entry.MessageId, message.MessageId, StringComparison.Ordinal))
			{
				throw new TestFixtureAssertionException(
					"Tenant isolation violated: an unfiltered GetMessagesAsync for tenant B returned tenant "
					+ $"A's entry {message.MessageId}.");
			}
		}
	}

	/// <summary>
	/// LIVENESS: a tenant must still see its own entries.
	/// </summary>
	/// <remarks>
	/// This is the arm that fails when a store is scoped by returning nothing to anybody. Without it the
	/// safety case above is satisfied by a completely inert store — isolation is trivially perfect when no
	/// read ever returns a row — so a provider could pass tenancy conformance while being unusable.
	/// </remarks>
	public virtual async Task TenantScopedRead_MustSeeItsOwnEntry()
	{
		var ambient = new SwitchableTenantContext();
		var store = CreateStore(ambient);
		ambient.SwitchTo("conformance-tenant-a");

		var message = CreateDeadLetterMessage();
		await store.StoreAsync(message, CancellationToken.None).ConfigureAwait(false);

		var byId = await store.GetByIdAsync(message.MessageId, CancellationToken.None).ConfigureAwait(false);
		if (byId is null)
		{
			throw new TestFixtureAssertionException(
				$"Tenant scoping is inert: tenant A stored entry {message.MessageId} and could not read it "
				+ "back. A store that returns nothing to anybody passes every isolation assertion while "
				+ "being unusable.");
		}

		if (byId.MessageBody != message.MessageBody)
		{
			throw new TestFixtureAssertionException(
				$"MessageBody mismatch on a tenant-scoped read. Expected: {message.MessageBody}, "
				+ $"Actual: {byId.MessageBody}");
		}
	}

	/// <summary>
	/// LIVENESS: the untenanted partition is a real partition and must round-trip.
	/// </summary>
	/// <remarks>
	/// A single-tenant host resolves no ambient tenant and therefore operates entirely under the reserved
	/// untenanted partition. If scoping is implemented so that this partition matches nothing, every
	/// consumer who never opted into multi-tenancy loses their dead-letter store outright — and no
	/// isolation assertion would report it.
	/// </remarks>
	public virtual async Task UntenantedPartition_MustRoundTripItsOwnEntry()
	{
		var untenanted = CreateStore(ambientTenant: null);

		var message = CreateDeadLetterMessage();
		await untenanted.StoreAsync(message, CancellationToken.None).ConfigureAwait(false);

		var byId = await untenanted.GetByIdAsync(message.MessageId, CancellationToken.None)
			.ConfigureAwait(false);
		if (byId is null)
		{
			throw new TestFixtureAssertionException(
				$"The untenanted partition did not round-trip entry {message.MessageId}. A single-tenant "
				+ "host operates entirely under this partition, so this breaks every consumer that never "
				+ "opted into multi-tenancy.");
		}
	}

	#endregion
}
