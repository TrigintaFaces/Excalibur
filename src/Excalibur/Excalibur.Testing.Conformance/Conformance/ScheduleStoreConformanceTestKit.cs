// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


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
/// completion, and integration scenarios.
/// </para>
/// <para>
/// <strong>IMPORTANT:</strong> CompleteAsync sets Enabled=false on the scheduled message;
/// it does NOT remove the message from the store. Messages remain retrievable via GetAllAsync.
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
	/// connection or client MUST override this with the data-only half — otherwise it disposes the store
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
	/// Verifies that CompleteAsync sets Enabled to false on the message.
	/// </summary>
	public virtual async Task CompleteAsync_ShouldSetEnabledFalse()
	{
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);
		var message = CreateScheduledMessage();
		((TestScheduledMessage)message).Enabled = true;

		await store.StoreAsync(message, CancellationToken.None).ConfigureAwait(false);

		await store.CompleteAsync(message.Id, CancellationToken.None).ConfigureAwait(false);

		var all = await store.GetAllAsync(CancellationToken.None).ConfigureAwait(false);
		var retrieved = all.FirstOrDefault(m => m.Id == message.Id);

		if (retrieved is null)
		{
			throw new TestFixtureAssertionException(
				"Message should remain in store after CompleteAsync");
		}

		if (retrieved.Enabled)
		{
			throw new TestFixtureAssertionException(
				"Enabled should be false after CompleteAsync");
		}
	}

	/// <summary>
	/// Verifies that CompleteAsync is idempotent for non-existent schedules.
	/// </summary>
	public virtual async Task CompleteAsync_NonExistent_ShouldBeIdempotent()
	{
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);
		var nonExistentId = GenerateScheduleId();

		// Should not throw - idempotent operation
		await store.CompleteAsync(nonExistentId, CancellationToken.None).ConfigureAwait(false);

		// Success - no exception thrown
	}

	/// <summary>
	/// Verifies that CompleteAsync is idempotent for already completed schedules.
	/// </summary>
	public virtual async Task CompleteAsync_AlreadyCompleted_ShouldBeIdempotent()
	{
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);
		var message = CreateScheduledMessage();
		((TestScheduledMessage)message).Enabled = true;

		await store.StoreAsync(message, CancellationToken.None).ConfigureAwait(false);

		// Complete once
		await store.CompleteAsync(message.Id, CancellationToken.None).ConfigureAwait(false);

		// Complete again - should not throw
		await store.CompleteAsync(message.Id, CancellationToken.None).ConfigureAwait(false);

		var all = await store.GetAllAsync(CancellationToken.None).ConfigureAwait(false);
		var retrieved = all.FirstOrDefault(m => m.Id == message.Id);

		if (retrieved is null)
		{
			throw new TestFixtureAssertionException(
				"Message should remain in store after double CompleteAsync");
		}

		if (retrieved.Enabled)
		{
			throw new TestFixtureAssertionException(
				"Enabled should still be false after double CompleteAsync");
		}
	}

	#endregion

	#region Integration Tests

	/// <summary>
	/// Verifies that a message remains persisted after completion.
	/// </summary>
	public virtual async Task StoreAsync_ThenComplete_MessageRemainsPersisted()
	{
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);
		var message = CreateScheduledMessage();
		((TestScheduledMessage)message).Enabled = true;

		// Store
		await store.StoreAsync(message, CancellationToken.None).ConfigureAwait(false);

		// Complete
		await store.CompleteAsync(message.Id, CancellationToken.None).ConfigureAwait(false);

		// Verify still retrievable
		var all = await store.GetAllAsync(CancellationToken.None).ConfigureAwait(false);
		var retrieved = all.FirstOrDefault(m => m.Id == message.Id);

		if (retrieved is null)
		{
			throw new TestFixtureAssertionException(
				"Completed message should remain retrievable via GetAllAsync");
		}

		if (retrieved.MessageName != message.MessageName)
		{
			throw new TestFixtureAssertionException(
				$"MessageName mismatch after complete. Expected: {message.MessageName}, Actual: {retrieved.MessageName}");
		}
	}

	/// <summary>
	/// Verifies that completing one message does not affect others.
	/// </summary>
	public virtual async Task MultipleMessages_CompleteOne_OthersUnaffected()
	{
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);

		var message1 = CreateScheduledMessage();
		((TestScheduledMessage)message1).Enabled = true;

		var message2 = CreateScheduledMessage();
		((TestScheduledMessage)message2).Enabled = true;

		await store.StoreAsync(message1, CancellationToken.None).ConfigureAwait(false);
		await store.StoreAsync(message2, CancellationToken.None).ConfigureAwait(false);

		// Complete only message1
		await store.CompleteAsync(message1.Id, CancellationToken.None).ConfigureAwait(false);

		var all = await store.GetAllAsync(CancellationToken.None).ConfigureAwait(false);
		var allList = all.ToList();

		var retrieved1 = allList.FirstOrDefault(m => m.Id == message1.Id);
		var retrieved2 = allList.FirstOrDefault(m => m.Id == message2.Id);

		if (retrieved1 is null || retrieved2 is null)
		{
			throw new TestFixtureAssertionException(
				"Both messages should remain in store after completing one");
		}

		if (retrieved1.Enabled)
		{
			throw new TestFixtureAssertionException(
				"Message1 Enabled should be false after CompleteAsync");
		}

		if (!retrieved2.Enabled)
		{
			throw new TestFixtureAssertionException(
				"Message2 Enabled should still be true (unaffected by completing message1)");
		}
	}

	#endregion

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
