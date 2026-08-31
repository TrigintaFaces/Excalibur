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
/// // The store is resolved from a container built by its own registration extension, with the
/// // arm's ambient tenant context seated into that container -- so the arms certify the object
/// // a consumer actually gets rather than one you assembled by hand.
/// public class SqlServerDeadLetterStoreConformanceTests : DeadLetterStoreConformanceTestKit
/// {
///     private readonly SqlServerFixture _fixture;
///
///     public SqlServerDeadLetterStoreConformanceTests(SqlServerFixture fixture) =&gt; _fixture = fixture;
///
///     protected override IDeadLetterStore CreateStore(ITenantContext ambientTenant) =&gt;
///         new ServiceCollection()
///             .AddLogging()
///             .AddSingleton(ambientTenant)
///             .AddSqlServerDeadLetterStore(_fixture.ConnectionString)
///             .BuildServiceProvider()
///             .GetRequiredService&lt;IDeadLetterStore&gt;();
///
///     protected override async Task CleanupAsync() =&gt;
///         await _fixture.CleanupAsync();
/// }
/// </code>
/// </example>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1707:Identifiers should not contain underscores",
	Justification = "Test method naming convention")]
public abstract class DeadLetterStoreConformanceTestKit : ConformanceTestKit
{
	/// <summary>
	/// Creates a dead letter store that resolves its tenant from the supplied ambient context.
	/// </summary>
	/// <param name="ambientTenant">
	/// The ambient tenant context the store must consult on every operation. Always supplied: a store that
	/// partitions entries by tenant resolves that partition from here, so there is no state in which the
	/// partition is undecided.
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
	protected abstract IDeadLetterStore CreateStore(ITenantContext ambientTenant);

	/// <summary>
	/// Creates a store for a host with no tenancy established — the untenanted partition.
	/// </summary>
	/// <returns>An IDeadLetterStore implementation to test.</returns>
	/// <remarks>
	/// The default for the non-tenancy cases. The reserved untenanted term is the correct model here, and it
	/// is what a store with no ambient context resolved to before the context became required — so these
	/// cases address exactly the partition they always did. It is distinct from a context that resolves no
	/// tenant at all, which means "multi-tenancy is active but unresolved" and which implementations are
	/// expected to fail closed on rather than treat as unscoped.
	/// </remarks>
	private IDeadLetterStore CreateStore() => CreateStore(new UntenantedContext());

	/// <summary>Resolves the reserved untenanted partition — a concrete term, never an absent one.</summary>
	private sealed class UntenantedContext : ITenantContext
	{
		public string? TenantId => TenantScope.UntenantedSentinel;

		public bool HasTenant => true;
	}

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

		public bool HasTenant => !string.IsNullOrEmpty(TenantId);

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
	/// Clears residual data before an arm runs. Defaults to <see cref="CleanupAsync"/>.
	/// </summary>
	/// <returns>A task that completes when the store holds no data from a previous arm.</returns>
	/// <remarks>
	/// <para>
	/// Defaults to <see cref="CleanupAsync"/>, which is correct for any suite whose teardown only deletes
	/// rows or documents. A suite whose <see cref="CleanupAsync"/> <em>also</em> disposes a connection or
	/// client MUST override this with the data-only half — otherwise it tears down the store the arm is
	/// about to use, and every arm fails on a disposed handle rather than on the contract.
	/// </para>
	/// <para>
	/// Resetting <em>before</em> an arm is what makes the arm independent; resetting only afterwards makes
	/// every arm's starting state a function of whether its predecessor finished cleanly.
	/// </para>
	/// </remarks>
	protected virtual Task ResetDataAsync() => CleanupAsync();

	/// <summary>
	/// Resolves the optional administrative facet of the store under test, or <see langword="null"/> when the
	/// store does not provide it.
	/// </summary>
	/// <param name="store"> The store under test. </param>
	/// <returns> The administrative facet, or <see langword="null"/> when the store does not provide one. </returns>
	/// <remarks>
	/// <para>
	/// The default asks the store, not the type system: <see cref="IDeadLetterStore"/> discovers optional
	/// capabilities through <see cref="IServiceProvider.GetService(Type)" />, and a decorator that forwards
	/// unknown capabilities to what it wraps therefore surfaces the facet through a wrapper. A cast
	/// (<c>store as IDeadLetterStoreAdmin</c>) sees only the outermost type, so it reports a capability a
	/// decorated store genuinely has as one it lacks, and every arm needing the facet is skipped -- a store
	/// certified through the kit's default while its administrative surface was never exercised.
	/// </para>
	/// <para>
	/// Override this only when your store surfaces the facet by some route other than its own
	/// <see cref="IServiceProvider.GetService(Type)" />.
	/// </para>
	/// </remarks>
	protected virtual IDeadLetterStoreAdmin? ResolveAdminFacet(IDeadLetterStore store)
	{
		ArgumentNullException.ThrowIfNull(store);

		return store.GetService(typeof(IDeadLetterStoreAdmin)) as IDeadLetterStoreAdmin;
	}

	/// <summary>Resolves the untenanted store for one arm, clearing residual data first.</summary>
	/// <returns>A store ready for one conformance arm.</returns>
	/// <remarks>
	/// Every arm obtains its store here. That is the only thing that causes <see cref="CleanupAsync"/> to
	/// run: a cleanup a deriver overrides but the kit never calls is indistinguishable, from the deriver's
	/// side, from one that works.
	/// </remarks>
	private async Task<IDeadLetterStore> CreateStoreForArmAsync()
	{
		var store = CreateStore();
		await ResetDataAsync().ConfigureAwait(false);
		return store;
	}

	/// <summary>Resolves the store for one arm under a supplied ambient tenant, clearing data first.</summary>
	/// <param name="ambientTenant">The ambient tenant context the arm controls.</param>
	/// <returns>A store ready for one conformance arm.</returns>
	private async Task<IDeadLetterStore> CreateStoreForArmAsync(ITenantContext ambientTenant)
	{
		var store = CreateStore(ambientTenant);
		await ResetDataAsync().ConfigureAwait(false);
		return store;
	}

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
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);
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
	/// Verifies that a non-empty message property bag survives a store-and-fetch round trip intact.
	/// </summary>
	/// <remarks>
	/// The bag is the only part of a dead-letter row a provider serializes rather than maps column-wise,
	/// so it is the only part a serializer change can silently corrupt. The arm asserts the bag is
	/// non-empty BEFORE comparing it -- the default test message carries no properties, so an arm that
	/// skipped that assertion would pass on an empty bag and prove nothing. The values deliberately
	/// include embedded quotes, a newline, a non-ASCII key and an empty string, because those are what a
	/// mis-configured serialization contract mangles first.
	/// </remarks>
	public virtual async Task StoreAsync_ShouldRoundTripPropertyBag()
	{
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);

		var message = CreateDeadLetterMessage();
		message.Properties = new Dictionary<string, string>(StringComparer.Ordinal)
		{
			["plain"] = "value",
			["embedded-json"] = """{"nested":{"n":1},"arr":[1,2]}""",
			["quotes-and-newline"] = "he said \"hi\"\nsecond line",
			["ünïcode-key"] = "값",
			["empty"] = string.Empty,
		};

		if (message.Properties.Count == 0)
		{
			throw new TestFixtureAssertionException(
				"The property bag under test is empty, so the round trip would assert nothing.");
		}

		await store.StoreAsync(message, CancellationToken.None).ConfigureAwait(false);

		var retrieved = await store.GetByIdAsync(message.MessageId, CancellationToken.None).ConfigureAwait(false)
			?? throw new TestFixtureAssertionException(
				$"Message with MessageId {message.MessageId} was not found after StoreAsync");

		if (retrieved.Properties.Count != message.Properties.Count)
		{
			throw new TestFixtureAssertionException(
				$"Property count mismatch. Expected {message.Properties.Count}, got {retrieved.Properties.Count} "
				+ $"(keys: {string.Join(", ", retrieved.Properties.Keys)})");
		}

		foreach (var (key, expected) in message.Properties)
		{
			if (!retrieved.Properties.TryGetValue(key, out var actual))
			{
				throw new TestFixtureAssertionException(
					$"Property key '{key}' is missing after the round trip "
					+ $"(keys present: {string.Join(", ", retrieved.Properties.Keys)})");
			}

			if (!string.Equals(actual, expected, StringComparison.Ordinal))
			{
				throw new TestFixtureAssertionException(
					$"Property '{key}' mismatch. Expected: {expected}, Actual: {actual}");
			}
		}
	}

	/// <summary>
	/// Verifies that storing a null message throws ArgumentNullException.
	/// </summary>
	public virtual async Task StoreAsync_WithNullMessage_ShouldThrow()
	{
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);

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
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);

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
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);
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
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);
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
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);
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
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);

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
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);

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
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);
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
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);
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
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);
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
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);
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
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);
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
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);
		var message = CreateDeadLetterMessage();

		await store.StoreAsync(message, CancellationToken.None).ConfigureAwait(false);

		if (ResolveAdminFacet(store) is not { } adminBefore)
		{
			SkipArm(nameof(DeleteAsync_ShouldDecreaseCount), typeof(IDeadLetterStoreAdmin), "Store does not implement admin interface; skip count verification");
			return;
		}

		RecordArmExecuted(nameof(DeleteAsync_ShouldDecreaseCount));
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
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);

		if (ResolveAdminFacet(store) is not { } admin)
		{
			SkipArm(nameof(GetCountAsync_EmptyStore_ShouldReturnZero), typeof(IDeadLetterStoreAdmin), "Store does not implement admin interface; skip count verification");
			return;
		}

		RecordArmExecuted(nameof(GetCountAsync_EmptyStore_ShouldReturnZero));
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
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);

		await store.StoreAsync(CreateDeadLetterMessage(), CancellationToken.None).ConfigureAwait(false);
		await store.StoreAsync(CreateDeadLetterMessage(), CancellationToken.None).ConfigureAwait(false);
		await store.StoreAsync(CreateDeadLetterMessage(), CancellationToken.None).ConfigureAwait(false);

		if (ResolveAdminFacet(store) is not { } adminCount)
		{
			SkipArm(nameof(GetCountAsync_AfterStores_ShouldReturnCorrectCount), typeof(IDeadLetterStoreAdmin), "Store does not implement admin interface; skip count verification");
			return;
		}

		RecordArmExecuted(nameof(GetCountAsync_AfterStores_ShouldReturnCorrectCount));
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
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);

		// Create an old message (beyond retention)
		var oldMessage = CreateDeadLetterMessage();
		oldMessage.MovedToDeadLetterAt = DateTimeOffset.UtcNow.AddDays(-10);

		// Create a recent message (within retention)
		var recentMessage = CreateDeadLetterMessage();
		recentMessage.MovedToDeadLetterAt = DateTimeOffset.UtcNow;

		await store.StoreAsync(oldMessage, CancellationToken.None).ConfigureAwait(false);
		await store.StoreAsync(recentMessage, CancellationToken.None).ConfigureAwait(false);

		// Cleanup with 5-day retention (should remove 10-day old message)
		if (ResolveAdminFacet(store) is not { } cleanupAdmin)
		{
			SkipArm(nameof(CleanupOldMessagesAsync_ShouldRemoveOldMessages), typeof(IDeadLetterStoreAdmin), "Store does not implement admin interface; skip cleanup verification");
			return;
		}

		RecordArmExecuted(nameof(CleanupOldMessagesAsync_ShouldRemoveOldMessages));
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
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);

		// Create message at 5 days old
		var message = CreateDeadLetterMessage();
		message.MovedToDeadLetterAt = DateTimeOffset.UtcNow.AddDays(-5);

		await store.StoreAsync(message, CancellationToken.None).ConfigureAwait(false);

		// Cleanup with 7-day retention (should NOT remove 5-day old message)
		if (ResolveAdminFacet(store) is not { } retentionAdmin)
		{
			SkipArm(nameof(CleanupOldMessagesAsync_ShouldRespectRetention), typeof(IDeadLetterStoreAdmin), "Store does not implement admin interface; skip retention verification");
			return;
		}

		RecordArmExecuted(nameof(CleanupOldMessagesAsync_ShouldRespectRetention));
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
		var store = await CreateStoreForArmAsync(ambient).ConfigureAwait(false);

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
		var store = await CreateStoreForArmAsync(ambient).ConfigureAwait(false);
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
		var untenanted = CreateStore(new UntenantedContext());

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

	#region Concurrency

	/// <summary>Callers released into one race, together.</summary>
	private const int RaceParticipants = 8;

	/// <summary>Independent races the arm runs, each on messages of its own.</summary>
	private const int RaceIterations = 48;

	/// <summary>The operation each participant in a race attempts.</summary>
	private enum RaceOperation
	{
		/// <summary>Delete the contested message. The only operation that reports a winner.</summary>
		Delete,

		/// <summary>Mark the contested message replayed, so a second writer straddles the delete.</summary>
		MarkReplayed,

		/// <summary>Store a fresh message of its own, so an insert is in flight during the deletes.</summary>
		StoreBystander,
	}

	/// <summary>What one participant in a race was asked to do, and what happened to it.</summary>
	private readonly record struct RaceOutcome(RaceOperation Operation, bool Won, Exception? Failure);

	/// <summary>
	/// SAFETY: concurrent callers racing over one dead letter must elect exactly one deleter, must not
	/// resurrect the message they removed, and must not lose a message stored alongside them.
	/// </summary>
	/// <remarks>
	/// <para>
	/// A dead letter is the record of a message that already failed. Losing one is silent and terminal:
	/// nothing redelivers it, no handler retries it, and no operator can replay what the store no longer
	/// has. Every other arm in this kit calls a method, waits for it, then calls the next — the single
	/// condition under which no store can drop a write, because there is never a second writer to be
	/// overwritten by.
	/// </para>
	/// <para>
	/// The race deliberately MIXES the operations rather than repeating one call, because the pair that
	/// destroys state here is <em>remove against insert</em>. A store that keeps its dead letters in one
	/// container it rewrites — a per-partition document, a load-filter-save cycle, a map without its own
	/// concurrency control — takes a snapshot to perform a delete and writes that snapshot back. Any
	/// message stored after the snapshot was taken and before it was written back is gone, and it is a
	/// message nobody will ever look for again. N identical deletes cannot reach that fault: the
	/// destruction needs a delete's rewrite straddling a store's insert, so the participants here are drawn
	/// across delete, mark-replayed, and store.
	/// </para>
	/// <para>
	/// The mark-replayed participant tests the other direction of the same window. A store whose
	/// mark-replayed writes unconditionally rather than updating in place will re-create the contested
	/// message when it lands after the delete — a dead letter that an operator watched disappear and that
	/// then returns, with its replayed flag set and no message body anyone verified. That is why the arm
	/// asserts the contested message is absent afterwards rather than merely that someone deleted it: the
	/// assertion holds under both orderings, because a mark against a message that is not there is required
	/// elsewhere in this kit to be a no-op.
	/// </para>
	/// <para>
	/// <b>Exactly one winner</b> is asserted rather than at most one. At-most-one is satisfied perfectly by
	/// a store that reports every delete as a miss, which would leave an operator unable to tell a removed
	/// message from one that was never there. The two uncontested operations taken before the race — a
	/// store then a delete, on a message nobody is competing for — fail such a store deterministically and
	/// by a message that names its own cause, so a store that simply never confirms a delete is never
	/// reported as a concurrency failure.
	/// </para>
	/// <para>
	/// <b>What this arm cannot prove.</b> It is sound but not complete. A correct store passes it under
	/// every interleaving, so a failure here is always a real defect and never a scheduling artefact. The
	/// converse does not hold: the arm cannot force an interleaving from outside the store, so a store
	/// carrying this fault passes on any run where the delete and the insert happen not to overlap inside
	/// the offending window. A barrier releasing every participant at one instant, and many independent
	/// races, make that outcome progressively less likely — they do not make it impossible. Read a pass
	/// here as evidence, not proof; read a failure as conclusive.
	/// </para>
	/// </remarks>
	/// <returns>A task representing the asynchronous test operation.</returns>
	public virtual async Task ConcurrentDeleteAndStore_MustElectExactlyOneDeleter_AndLoseNoStoredMessage()
	{
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);

		await AssertStoreAndDeleteAreLiveAsync(store).ConfigureAwait(false);

		for (var iteration = 1; iteration <= RaceIterations; iteration++)
		{
			var contested = CreateDeadLetterMessage();
			await store.StoreAsync(contested, CancellationToken.None).ConfigureAwait(false);

			var bystanders = new DeadLetterMessage[RaceParticipants];
			for (var slot = 0; slot < RaceParticipants; slot++)
			{
				bystanders[slot] = CreateDeadLetterMessage();
			}

			var outcomes = await RunOneRaceAsync(store, contested.MessageId, bystanders).ConfigureAwait(false);

			AssertNoParticipantThrew(outcomes, iteration, contested.MessageId);
			AssertExactlyOneDeleter(outcomes, iteration, contested.MessageId);

			await AssertContestedMessageIsGoneAsync(store, outcomes, iteration, contested.MessageId)
				.ConfigureAwait(false);

			await AssertBystandersSurvivedAsync(store, outcomes, bystanders, iteration).ConfigureAwait(false);
		}
	}

	/// <summary>
	/// Verifies an uncontested store round-trips and an uncontested delete reports success, before any race
	/// is run.
	/// </summary>
	/// <param name="store"> The store under test. </param>
	/// <returns> A task that completes when both operations have been shown live. </returns>
	/// <remarks>
	/// A store that reports every delete as a miss satisfies "at most one deleter" perfectly, because a
	/// caller that never wins can never win twice. A store that drops every write satisfies the
	/// absent-afterwards assertion for the same kind of reason. Both are caught here, uncontested, so
	/// neither verdict depends on a schedule and neither is misread as contention.
	/// </remarks>
	private async Task AssertStoreAndDeleteAreLiveAsync(IDeadLetterStore store)
	{
		var probe = CreateDeadLetterMessage();

		await store.StoreAsync(probe, CancellationToken.None).ConfigureAwait(false);

		var stored = await store.GetByIdAsync(probe.MessageId, CancellationToken.None).ConfigureAwait(false);

		if (stored is null)
		{
			throw new TestFixtureAssertionException(
				$"StoreAsync accepted message '{probe.MessageId}' and GetByIdAsync then returned nothing, "
				+ "with no competing caller. Every dead letter would be discarded on arrival. This is "
				+ "reported before the concurrency assertions because a store that keeps nothing can never "
				+ "lose a message to a race, and would otherwise satisfy them without storing anything.");
		}

		var deleted = await store.DeleteAsync(probe.MessageId, CancellationToken.None).ConfigureAwait(false);

		if (!deleted)
		{
			throw new TestFixtureAssertionException(
				$"DeleteAsync reported false for message '{probe.MessageId}', which it had just returned "
				+ "from GetByIdAsync, with no competing caller. An operator could not distinguish a message "
				+ "this store removed from one that was never present. Reported separately from the "
				+ "concurrency assertions because a store that confirms no delete cannot elect two "
				+ "deleters, and would pass a race by never electing anyone.");
		}
	}

	/// <summary>
	/// Runs one race: every participant released together over one contested message and fresh bystanders.
	/// </summary>
	/// <param name="store"> The store under test. </param>
	/// <param name="contestedId"> The message every delete participant competes for. </param>
	/// <param name="bystanders"> A fresh message per slot, for the participants that store rather than delete. </param>
	/// <returns> What each participant was asked to do and what happened to it, in slot order. </returns>
	private static async Task<RaceOutcome[]> RunOneRaceAsync(
		IDeadLetterStore store,
		string contestedId,
		DeadLetterMessage[] bystanders)
	{
		var outcomes = new RaceOutcome[RaceParticipants];

		// Participants run on dedicated threads rather than pooled ones. A pool that injects threads on a
		// delay would release the participants seconds apart, and participants that do not overlap are a
		// sequence wearing the shape of a race.
		using var gate = new Barrier(RaceParticipants + 1);

		var racers = new Task[RaceParticipants];

		for (var slot = 0; slot < RaceParticipants; slot++)
		{
			var index = slot;
			var operation = SelectRaceOperation(index);

			racers[index] = Task.Factory.StartNew(
				async () =>
				{
					// Parked until the last participant arrives, so the calls are issued at one instant
					// instead of in whatever order the threads happened to start.
					gate.SignalAndWait();

					try
					{
						var won = false;

						switch (operation)
						{
							case RaceOperation.Delete:
								won = await store.DeleteAsync(contestedId, CancellationToken.None)
									.ConfigureAwait(false);
								break;

							case RaceOperation.MarkReplayed:
								await store.MarkAsReplayedAsync(contestedId, CancellationToken.None)
									.ConfigureAwait(false);
								break;

							default:
								await store.StoreAsync(bystanders[index], CancellationToken.None)
									.ConfigureAwait(false);
								break;
						}

						outcomes[index] = new RaceOutcome(operation, won, null);
					}
					catch (Exception ex)
					{
						// Recorded rather than rethrown. Awaiting the set surfaces one exception and
						// discards the others, and the discarded ones are the other half of the
						// interleaving — the half that says which operations were in flight together.
						outcomes[index] = new RaceOutcome(operation, false, ex);
					}
				},
				CancellationToken.None,
				TaskCreationOptions.LongRunning,
				TaskScheduler.Default).Unwrap();
		}

		gate.SignalAndWait();

		await Task.WhenAll(racers).ConfigureAwait(false);

		return outcomes;
	}

	/// <summary>
	/// Spreads the participants across delete, mark-replayed, and store.
	/// </summary>
	/// <param name="slot"> The participant's position in the race. </param>
	/// <returns> The operation this participant attempts. </returns>
	private static RaceOperation SelectRaceOperation(int slot) => (slot % 4) switch
	{
		0 or 1 => RaceOperation.Delete,
		2 => RaceOperation.MarkReplayed,
		_ => RaceOperation.StoreBystander,
	};

	/// <summary>
	/// Fails when any participant threw.
	/// </summary>
	/// <param name="outcomes"> Every participant's result, in slot order. </param>
	/// <param name="iteration"> Which race this was. </param>
	/// <param name="contestedId"> The contested message identifier. </param>
	private static void AssertNoParticipantThrew(RaceOutcome[] outcomes, int iteration, string contestedId)
	{
		var thrown = Array.FindAll(outcomes, static o => o.Failure is not null);

		if (thrown.Length > 0)
		{
			throw new TestFixtureAssertionException(
				$"Race {iteration} of {RaceIterations} on message '{contestedId}': {thrown.Length} of "
				+ $"{RaceParticipants} concurrent callers threw. Losing a delete is a return value, not an "
				+ "exception, and storing a dead letter while another is being removed is ordinary "
				+ "operation: a throwing caller is indistinguishable to the host from a store outage and is "
				+ "retried as one, which for StoreAsync means the same dead letter written twice. "
				+ DescribeRace(outcomes));
		}
	}

	/// <summary>
	/// Fails unless exactly one participant was told it removed the message.
	/// </summary>
	/// <param name="outcomes"> Every participant's result, in slot order. </param>
	/// <param name="iteration"> Which race this was. </param>
	/// <param name="contestedId"> The contested message identifier. </param>
	private static void AssertExactlyOneDeleter(RaceOutcome[] outcomes, int iteration, string contestedId)
	{
		var deleters = Array.FindAll(outcomes, static o => o.Operation == RaceOperation.Delete).Length;
		var winners = Array.FindAll(outcomes, static o => o.Won).Length;

		if (winners > 1)
		{
			throw new TestFixtureAssertionException(
				$"Race {iteration} of {RaceIterations} told {winners} of {deleters} concurrent deleters "
				+ $"that each of them removed message '{contestedId}', which existed once. This kit already "
				+ "requires DeleteAsync to report false for a message that is not there, so at most one of "
				+ "these callers can truthfully be told true. Every operator tool that counts what it "
				+ "purged, or logs a removal, now reports work that did not happen. The read, the decision, "
				+ "and the removal are three steps here with nothing holding the record still in between: "
				+ "make the removal itself report whether it matched, rather than a preceding existence "
				+ "check. " + DescribeRace(outcomes));
		}

		if (winners == 0)
		{
			throw new TestFixtureAssertionException(
				$"Race {iteration} of {RaceIterations} told all {deleters} concurrent deleters that message "
				+ $"'{contestedId}' was not there to remove, although it was stored immediately before the "
				+ "race and no other participant deletes. An operator draining this queue is told every "
				+ "message is already gone while the queue does not drain. Note the uncontested delete "
				+ "taken before this race reported true, so the store does confirm deletes — it loses the "
				+ "winner specifically under contention. " + DescribeRace(outcomes));
		}
	}

	/// <summary>
	/// Fails when the contested message is still present after a caller was told it removed it.
	/// </summary>
	/// <param name="store"> The store under test. </param>
	/// <param name="outcomes"> Every participant's result, in slot order. </param>
	/// <param name="iteration"> Which race this was. </param>
	/// <param name="contestedId"> The contested message identifier. </param>
	/// <returns> A task that completes when the message has been shown absent. </returns>
	private static async Task AssertContestedMessageIsGoneAsync(
		IDeadLetterStore store,
		RaceOutcome[] outcomes,
		int iteration,
		string contestedId)
	{
		var survivor = await store.GetByIdAsync(contestedId, CancellationToken.None).ConfigureAwait(false);

		if (survivor is not null)
		{
			throw new TestFixtureAssertionException(
				$"Race {iteration} of {RaceIterations}: one caller was told it removed message "
				+ $"'{contestedId}', and GetByIdAsync still returns it. The concurrent MarkAsReplayedAsync "
				+ "wrote unconditionally instead of updating in place, so it re-created the row after the "
				+ "delete had removed it — this kit requires a mark against a message that is not present "
				+ "to be a no-op, precisely so this cannot happen. The result is a dead letter an operator "
				+ "watched disappear and that has now returned, carrying a replayed flag nobody set against "
				+ "a live message. " + DescribeRace(outcomes));
		}
	}

	/// <summary>
	/// Fails when any message stored during the race is missing afterwards.
	/// </summary>
	/// <param name="store"> The store under test. </param>
	/// <param name="outcomes"> Every participant's result, in slot order. </param>
	/// <param name="bystanders"> The per-slot messages, of which the storing slots' were written. </param>
	/// <param name="iteration"> Which race this was. </param>
	/// <returns> A task that completes when every stored message has been shown to survive. </returns>
	private static async Task AssertBystandersSurvivedAsync(
		IDeadLetterStore store,
		RaceOutcome[] outcomes,
		DeadLetterMessage[] bystanders,
		int iteration)
	{
		for (var slot = 0; slot < outcomes.Length; slot++)
		{
			if (outcomes[slot].Operation != RaceOperation.StoreBystander)
			{
				continue;
			}

			var messageId = bystanders[slot].MessageId;

			var stored = await store.GetByIdAsync(messageId, CancellationToken.None).ConfigureAwait(false);

			if (stored is null)
			{
				throw new TestFixtureAssertionException(
					$"Race {iteration} of {RaceIterations}: message '{messageId}' was stored during the "
					+ "race, StoreAsync returned without error, and GetByIdAsync now returns nothing. A "
					+ "dead letter has been lost. Nothing redelivers it and no operator can replay what the "
					+ "store does not have, so the failure that produced it is now unrecoverable and "
					+ "silent. A concurrent delete rewrote the container from a snapshot taken before this "
					+ "insert landed: remove the one record atomically rather than rewriting the set that "
					+ "contains it. " + DescribeRace(outcomes));
			}
		}
	}

	/// <summary>
	/// Renders what every participant was asked to do and what it was told.
	/// </summary>
	/// <param name="outcomes"> Every participant's result, in slot order. </param>
	/// <returns> A one-line account of the race. </returns>
	/// <remarks>
	/// A count alone names the symptom and discards the evidence. Which operations were in flight together,
	/// and which of them were told they had won, is the whole of what distinguishes an unguarded delete
	/// from a resurrecting mark, and it cannot be recovered by re-running: the next run interleaves
	/// differently.
	/// </remarks>
	private static string DescribeRace(RaceOutcome[] outcomes) =>
		"Every caller in this race, in slot order: " + string.Join("; ", outcomes.Select(DescribeOutcome)) + ".";

	/// <summary>
	/// Renders one participant's operation and result.
	/// </summary>
	/// <param name="outcome"> The participant's result. </param>
	/// <param name="slot"> The participant's position in the race. </param>
	/// <returns> A short account of that participant. </returns>
	private static string DescribeOutcome(RaceOutcome outcome, int slot)
	{
		var result = outcome.Failure is not null
			? $"THREW {outcome.Failure.GetType().Name}: {outcome.Failure.Message}"
			: outcome.Operation == RaceOperation.Delete ? outcome.Won ? "WON" : "lost" : "done";

		return $"slot {slot} {outcome.Operation} -> {result}";
	}

	#endregion Concurrency

}
