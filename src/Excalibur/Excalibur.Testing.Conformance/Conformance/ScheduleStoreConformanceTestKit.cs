// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0


#pragma warning disable IDE0270 // Null check can be simplified

using Excalibur.Dispatch.Delivery;

namespace Excalibur.Testing.Conformance;

/// <summary>
/// Abstract base class for IScheduleStore conformance testing.
/// </summary>
/// <remarks>
/// <para>
/// Inherit from this class and implement <see cref="CreateStore"/> to verify that
/// your schedule store implementation conforms to the IScheduleStore contract.
/// </para>
/// <para>
/// The test kit verifies core schedule store operations including store, retrieval,
/// completion, and integration scenarios. These legacy arms do not certify atomic execution admission,
/// detached versioned snapshots, fencing, durability or distributed recovery. Providers supporting
/// IScheduleExecutionStore can additionally derive from ScheduleExecutionStoreConformanceTestKit.
/// </para>
/// <para>
/// Completion may retain an inactive record, delete it, or move it to historical storage,
/// as allowed by IScheduleStore. Every retained match must be inactive and preserve its message
/// name and body. Unrelated schedules must preserve their persisted identity, payload and metadata.
/// Historical arm names containing "Persisted" do not impose a retention policy.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // AddDurableScheduleStore registers the store and its durability attestation inseparably,
/// // so resolving through the container certifies the object a consumer actually gets --
/// // including the attestation the boot-time durability gate reads.
/// public class MyScheduleStoreConformanceTests : ScheduleStoreConformanceTestKit
/// {
///     private readonly ServiceProvider _provider;
///
///     public MyScheduleStoreConformanceTests(MyStoreFixture fixture) =>
///         _provider = new ServiceCollection()
///             .AddLogging()
///             .AddSingleton(fixture.DataSource)
///             .AddDurableScheduleStore&lt;MyScheduleStore&gt;()
///             .BuildServiceProvider();
///
///     protected override IScheduleStore CreateStore() =>
///         _provider.GetRequiredService&lt;IScheduleStore&gt;();
/// }
/// </code>
/// </example>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1707:Identifiers should not contain underscores", Justification = "Test method naming convention")]
public abstract class ScheduleStoreConformanceTestKit : ConformanceTestKit
{
	/// <summary>
	/// Creates a fresh schedule store instance for testing.
	/// </summary>
	/// <returns>An IScheduleStore implementation to test.</returns>
	protected abstract IScheduleStore CreateStore();

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
	/// rows, keys or documents. A suite whose <see cref="CleanupAsync"/> <em>also</em> disposes a
	/// connection or client MUST override this with the data-only half â€” otherwise it disposes the store
	/// the arm is about to use, and every arm fails on a disposed handle rather than on the contract.
	/// </para>
	/// <para>
	/// Resetting <em>before</em> an arm is what makes the arm independent; resetting only afterwards makes
	/// every arm's starting state a function of whether its predecessor finished cleanly.
	/// </para>
	/// </remarks>
	protected virtual Task ResetDataAsync() => CleanupAsync();

	/// <summary>
	/// Creates the store for a single arm and clears residual data before the arm runs.
	/// </summary>
	/// <returns>A store ready for one conformance arm.</returns>
	/// <remarks>
	/// Every arm in this kit obtains its store here rather than from <see cref="CreateStore"/> directly.
	/// That is the only thing that causes <see cref="CleanupAsync"/> to run: a cleanup a deriver overrides
	/// but the kit never calls is indistinguishable, from the deriver's side, from one that works.
	/// </remarks>
	protected async Task<IScheduleStore> CreateStoreForArmAsync()
	{
		var store = CreateStore();
		await ResetDataAsync().ConfigureAwait(false);
		return store;
	}

	/// <summary>
	/// Creates a test scheduled message with the given ID.
	/// </summary>
	/// <param name="id">Optional schedule identifier. If not provided, a new GUID is generated.</param>
	/// <returns>A test scheduled message.</returns>
	protected virtual IScheduledMessage CreateScheduledMessage(Guid? id = null) =>
		new TestScheduledMessage { Id = id ?? Guid.NewGuid() };

	/// <summary>
	/// Generates a unique schedule ID for test isolation.
	/// </summary>
	/// <returns>A unique schedule identifier.</returns>
	protected virtual Guid GenerateScheduleId() => Guid.NewGuid();

	#region Store Tests

	/// <summary>
	/// Verifies that storing a new message persists it successfully.
	/// </summary>
	public virtual async Task StoreAsync_ShouldPersistMessage()
	{
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);
		var message = CreateScheduledMessage();

		await store.StoreAsync(message, CancellationToken.None).ConfigureAwait(false);

		var all = await store.GetAllAsync(CancellationToken.None).ConfigureAwait(false);
		var retrieved = all.FirstOrDefault(m => m.Id == message.Id);

		if (retrieved is null)
		{
			throw new TestFixtureAssertionException(
				$"Message with Id {message.Id} was not found after StoreAsync");
		}

		if (retrieved.MessageBody != message.MessageBody)
		{
			throw new TestFixtureAssertionException(
				$"MessageBody mismatch. Expected: {message.MessageBody}, Actual: {retrieved.MessageBody}");
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
	/// Verifies that storing a message with the same ID updates (upserts) the existing entry.
	/// </summary>
	public virtual async Task StoreAsync_SameId_ShouldUpsert()
	{
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);
		var id = GenerateScheduleId();

		var message1 = CreateScheduledMessage(id);
		((TestScheduledMessage)message1).MessageBody = "original-body";

		await store.StoreAsync(message1, CancellationToken.None).ConfigureAwait(false);

		var message2 = CreateScheduledMessage(id);
		((TestScheduledMessage)message2).MessageBody = "updated-body";

		await store.StoreAsync(message2, CancellationToken.None).ConfigureAwait(false);

		var all = await store.GetAllAsync(CancellationToken.None).ConfigureAwait(false);
		var allList = all.ToList();

		var withId = allList.Where(m => m.Id == id).ToList();
		if (withId.Count != 1)
		{
			throw new TestFixtureAssertionException(
				$"Expected exactly 1 message with Id {id}, found {withId.Count}");
		}

		if (withId[0].MessageBody != "updated-body")
		{
			throw new TestFixtureAssertionException(
				$"MessageBody should be 'updated-body' after upsert, got '{withId[0].MessageBody}'");
		}
	}

	/// <summary>
	/// Verifies that storing multiple messages persists all of them.
	/// </summary>
	public virtual async Task StoreAsync_MultipleMessages_ShouldPersistAll()
	{
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);

		var message1 = CreateScheduledMessage();
		var message2 = CreateScheduledMessage();
		var message3 = CreateScheduledMessage();

		await store.StoreAsync(message1, CancellationToken.None).ConfigureAwait(false);
		await store.StoreAsync(message2, CancellationToken.None).ConfigureAwait(false);
		await store.StoreAsync(message3, CancellationToken.None).ConfigureAwait(false);

		var all = await store.GetAllAsync(CancellationToken.None).ConfigureAwait(false);
		var allList = all.ToList();

		var ids = new[] { message1.Id, message2.Id, message3.Id };
		foreach (var id in ids)
		{
			if (!allList.Any(m => m.Id == id))
			{
				throw new TestFixtureAssertionException(
					$"Message with Id {id} was not found after storing multiple messages");
			}
		}
	}

	#endregion

	#region Retrieval Tests

	/// <summary>
	/// Verifies that GetAllAsync returns empty for an empty store.
	/// </summary>
	public virtual async Task GetAllAsync_EmptyStore_ShouldReturnEmpty()
	{
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);

		var all = await store.GetAllAsync(CancellationToken.None).ConfigureAwait(false);

		if (all.Any())
		{
			throw new TestFixtureAssertionException(
				"Expected empty result from empty store, but got messages");
		}
	}

	/// <summary>
	/// Verifies that GetAllAsync returns the stored message after StoreAsync.
	/// </summary>
	public virtual async Task GetAllAsync_AfterStore_ShouldReturnMessage()
	{
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);
		var message = CreateScheduledMessage();

		await store.StoreAsync(message, CancellationToken.None).ConfigureAwait(false);

		var all = await store.GetAllAsync(CancellationToken.None).ConfigureAwait(false);
		var allList = all.ToList();

		if (allList.Count == 0)
		{
			throw new TestFixtureAssertionException(
				"GetAllAsync returned empty after storing a message");
		}

		if (!allList.Any(m => m.Id == message.Id))
		{
			throw new TestFixtureAssertionException(
				$"Stored message with Id {message.Id} was not returned by GetAllAsync");
		}
	}

	/// <summary>
	/// Verifies that GetAllAsync returns all stored messages.
	/// </summary>
	public virtual async Task GetAllAsync_ShouldReturnAllMessages()
	{
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);

		var message1 = CreateScheduledMessage();
		var message2 = CreateScheduledMessage();

		await store.StoreAsync(message1, CancellationToken.None).ConfigureAwait(false);
		await store.StoreAsync(message2, CancellationToken.None).ConfigureAwait(false);

		var all = await store.GetAllAsync(CancellationToken.None).ConfigureAwait(false);
		var allList = all.ToList();

		if (allList.Count < 2)
		{
			throw new TestFixtureAssertionException(
				$"Expected at least 2 messages, got {allList.Count}");
		}

		if (!allList.Any(m => m.Id == message1.Id))
		{
			throw new TestFixtureAssertionException(
				$"Message {message1.Id} not returned by GetAllAsync");
		}

		if (!allList.Any(m => m.Id == message2.Id))
		{
			throw new TestFixtureAssertionException(
				$"Message {message2.Id} not returned by GetAllAsync");
		}
	}

	#endregion

	#region Completion Tests

	/// <summary>
	/// Verifies that completion leaves the schedule absent or inactive and preserves retained name and body.
	/// </summary>
	public virtual async Task CompleteAsync_ShouldSetEnabledFalse()
	{
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);
		var message = CreateScheduledMessage();
		((TestScheduledMessage)message).Enabled = true;
		var expected = ScheduleRecordSnapshot.Capture(message);
		await store.StoreAsync(message, CancellationToken.None).ConfigureAwait(false);
		var unrelated = await CaptureInventoryAsync(store).ConfigureAwait(false);
		AssertStoredIdentity(unrelated, expected);
		await CompleteAndVerifyAsync(store, expected, unrelated).ConfigureAwait(false);
	}

	/// <summary>
	/// Verifies that completing a missing identifier is harmless to existing schedules.
	/// </summary>
	public virtual async Task CompleteAsync_NonExistent_ShouldBeIdempotent()
	{
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);
		var message = CreateScheduledMessage();
		var expected = ScheduleRecordSnapshot.Capture(message);
		var nonExistentId = GenerateScheduleId();
		await store.StoreAsync(message, CancellationToken.None).ConfigureAwait(false);
		var unrelated = await CaptureInventoryAsync(store).ConfigureAwait(false);
		AssertStoredIdentity(unrelated, expected);
		if (unrelated.Any(snapshot => snapshot.Id == nonExistentId))
		{
			throw new TestFixtureAssertionException("The missing schedule identifier must differ from every existing schedule identifier.");
		}
		await store.CompleteAsync(nonExistentId, CancellationToken.None).ConfigureAwait(false);
		var all = (await store.GetAllAsync(CancellationToken.None).ConfigureAwait(false)).ToArray();
		if (all.Any(row => row.Id == nonExistentId && row.Enabled))
		{
			throw new TestFixtureAssertionException("Completing a missing identifier must not introduce an active schedule.");
		}
		AssertUnrelatedUnchanged(all, unrelated, nonExistentId);
	}

	/// <summary>
	/// Verifies that repeated completion leaves the schedule absent or inactive and unrelated schedules unchanged.
	/// </summary>
	public virtual async Task CompleteAsync_AlreadyCompleted_ShouldBeIdempotent()
	{
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);
		var message = CreateScheduledMessage();
		((TestScheduledMessage)message).Enabled = true;
		var expected = ScheduleRecordSnapshot.Capture(message);
		await store.StoreAsync(message, CancellationToken.None).ConfigureAwait(false);
		var unrelated = await CaptureInventoryAsync(store).ConfigureAwait(false);
		AssertStoredIdentity(unrelated, expected);
		await CompleteAndVerifyAsync(store, expected, unrelated).ConfigureAwait(false);
		await CompleteAndVerifyAsync(store, expected, unrelated).ConfigureAwait(false);
	}

	#endregion

	#region Integration Tests

	/// <summary>
	/// Verifies that completion ends execution eligibility and preserves name and body when a record is retained.
	/// </summary>
	/// <remarks>The historical method name does not require completed records to remain in the active inventory.</remarks>
	public virtual async Task StoreAsync_ThenComplete_MessageRemainsPersisted()
	{
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);
		var message = CreateScheduledMessage();
		((TestScheduledMessage)message).Enabled = true;
		var expected = ScheduleRecordSnapshot.Capture(message);
		await store.StoreAsync(message, CancellationToken.None).ConfigureAwait(false);
		var unrelated = await CaptureInventoryAsync(store).ConfigureAwait(false);
		AssertStoredIdentity(unrelated, expected);
		await CompleteAndVerifyAsync(store, expected, unrelated).ConfigureAwait(false);
	}

	/// <summary>
	/// Verifies that completing one message preserves every unrelated schedule's identity, payload and metadata.
	/// </summary>
	public virtual async Task MultipleMessages_CompleteOne_OthersUnaffected()
	{
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);
		var message1 = CreateScheduledMessage();
		((TestScheduledMessage)message1).Enabled = true;
		var message2 = CreateScheduledMessage();
		((TestScheduledMessage)message2).Enabled = true;
		var expected = ScheduleRecordSnapshot.Capture(message1);
		var expectedPeer = ScheduleRecordSnapshot.Capture(message2);
		await store.StoreAsync(message1, CancellationToken.None).ConfigureAwait(false);
		await store.StoreAsync(message2, CancellationToken.None).ConfigureAwait(false);
		var unrelated = await CaptureInventoryAsync(store).ConfigureAwait(false);
		AssertStoredIdentity(unrelated, expected);
		AssertStoredIdentity(unrelated, expectedPeer);
		await CompleteAndVerifyAsync(store, expected, unrelated).ConfigureAwait(false);
	}

	#endregion

	private static async Task<ScheduleRecordSnapshot[]> CaptureInventoryAsync(IScheduleStore store) =>
		(await store.GetAllAsync(CancellationToken.None).ConfigureAwait(false))
			.Select(ScheduleRecordSnapshot.Capture).ToArray();

	private static void AssertStoredIdentity(IEnumerable<ScheduleRecordSnapshot> persisted, ScheduleRecordSnapshot expected)
	{
		var matching = persisted.Where(snapshot => snapshot.Id == expected.Id).ToArray();
		if (matching.Length == 0 || matching.Any(snapshot => snapshot.MessageName != expected.MessageName || snapshot.MessageBody != expected.MessageBody))
		{
			throw new TestFixtureAssertionException($"StoreAsync did not preserve schedule {expected.Id} and its original message name and body.");
		}
	}

	private static async Task CompleteAndVerifyAsync(IScheduleStore store, ScheduleRecordSnapshot expected,
		IEnumerable<ScheduleRecordSnapshot> unrelated)
	{
		await store.CompleteAsync(expected.Id, CancellationToken.None).ConfigureAwait(false);
		var all = (await store.GetAllAsync(CancellationToken.None).ConfigureAwait(false)).ToArray();
		foreach (var row in all.Where(row => row.Id == expected.Id))
		{
			if (row.Enabled)
			{
				throw new TestFixtureAssertionException($"Schedule {expected.Id} remains active after completion.");
			}
			if (row.MessageName != expected.MessageName || row.MessageBody != expected.MessageBody)
			{
				throw new TestFixtureAssertionException($"Retained schedule {expected.Id} changed its message name or body during completion.");
			}
		}
		AssertUnrelatedUnchanged(all, unrelated, expected.Id);
	}

	private static void AssertUnrelatedUnchanged(IEnumerable<IScheduledMessage> inventory,
		IEnumerable<ScheduleRecordSnapshot> expected, Guid completedId)
	{
		var remaining = expected.Where(snapshot => snapshot.Id != completedId)
			.GroupBy(static snapshot => snapshot)
			.ToDictionary(static group => group.Key, static group => group.Count());
		foreach (var row in inventory.Where(row => row.Id != completedId))
		{
			var observed = ScheduleRecordSnapshot.Capture(row);
			if (!remaining.TryGetValue(observed, out var count))
			{
				throw new TestFixtureAssertionException($"Unrelated schedule {row.Id} was introduced or changed during completion.");
			}
			if (count == 1) { _ = remaining.Remove(observed); }
			else { remaining[observed] = count - 1; }
		}
		if (remaining.Count != 0)
		{
			throw new TestFixtureAssertionException("Completion removed one or more unrelated schedules.");
		}
	}

	// Preserve original name/body before storage; capture collateral from persisted inventory before completion.
	// This permits storage normalization while preventing aliases from redefining the completion oracle.
	// Round-trip date text preserves both timestamp precision and offset, not only the UTC instant.
	private sealed record ScheduleRecordSnapshot(Guid Id, string MessageName, string MessageBody, bool Enabled,
		string? CorrelationId, string CronExpression, string? TimeZoneId, TimeSpan? Interval,
		string? NextExecutionUtc, string? TenantId, string? TraceParent, string? UserId,
		string? LastExecutionUtc, MissedExecutionBehavior? MissedExecutionBehavior)
	{
		public static ScheduleRecordSnapshot Capture(IScheduledMessage row) => new(row.Id, row.MessageName, row.MessageBody,
			row.Enabled, row.CorrelationId, row.CronExpression, row.TimeZoneId, row.Interval,
			row.NextExecutionUtc?.ToString("O", System.Globalization.CultureInfo.InvariantCulture),
			row.TenantId, row.TraceParent, row.UserId,
			row.LastExecutionUtc?.ToString("O", System.Globalization.CultureInfo.InvariantCulture), row.MissedExecutionBehavior);
	}

	#region Helper Classes

	/// <summary>
	/// Test implementation of IScheduledMessage for conformance testing.
	/// </summary>
	internal sealed class TestScheduledMessage : IScheduledMessage
	{
		/// <inheritdoc />
		public Guid Id { get; set; } = Guid.NewGuid();

		/// <inheritdoc />
		public string MessageBody { get; set; } = "{}";

		/// <inheritdoc />
		public string MessageName { get; set; } = "TestMessage";

		/// <inheritdoc />
		public bool Enabled { get; set; } = true;

		/// <inheritdoc />
		public string? CorrelationId { get; set; }

		/// <inheritdoc />
		public string CronExpression { get; set; } = "0 * * * * ?";

		/// <inheritdoc />
		public string? TimeZoneId { get; set; }

		/// <inheritdoc />
		public TimeSpan? Interval { get; set; }

		/// <inheritdoc />
		public DateTimeOffset? NextExecutionUtc { get; set; } = DateTimeOffset.UtcNow.AddHours(1);

		/// <inheritdoc />
		public string? TenantId { get; set; }

		/// <inheritdoc />
		public string? TraceParent { get; set; }

		/// <inheritdoc />
		public string? UserId { get; set; }

		/// <inheritdoc />
		public DateTimeOffset? LastExecutionUtc { get; set; }

		/// <inheritdoc />
		public MissedExecutionBehavior? MissedExecutionBehavior { get; set; }
	}

	#endregion

}
