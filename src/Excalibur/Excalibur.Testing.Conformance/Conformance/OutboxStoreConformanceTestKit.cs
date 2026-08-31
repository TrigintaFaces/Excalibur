// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

#pragma warning disable IDE0270 // Null check can be simplified

using System.Text;

using Excalibur.Dispatch;

namespace Excalibur.Testing.Conformance;

/// <summary>
/// Abstract base class for IOutboxStore conformance testing.
/// </summary>
/// <remarks>
/// <para>
/// Inherit from this class and implement <see cref="CreateStoreAsync"/> to verify that
/// your outbox store implementation conforms to the IOutboxStore contract.
/// </para>
/// <para>
/// The test kit verifies core outbox operations including stage, mark sent, mark failed,
/// retrieval, cleanup, and statistics behavior.
/// </para>
/// <para>
/// Note: EnqueueAsync is intentionally excluded as it requires DispatchJsonSerializer
/// dependency. Use StageMessageAsync for conformance testing.
/// </para>
/// <para>
/// <b>This kit is trim-excluded, not trim-safe, and that is a statement about the store contract
/// rather than about the kit.</b> The arms read staged messages back through the store under test,
/// and a conformant store deserializes the message payload reflectively. No annotation on this kit
/// can reach the payload types, so a deriving suite must itself carry
/// <see cref="System.Diagnostics.CodeAnalysis.RequiresUnreferencedCodeAttribute"/> and
/// <see cref="System.Diagnostics.CodeAnalysis.RequiresDynamicCodeAttribute"/> — or suppress the
/// warnings deliberately — when it is compiled with the trim or ahead-of-time analyzer enabled.
/// Overriding an arm rather than wrapping it requires the same annotations on the override. Neither
/// a trimmed nor an ahead-of-time-compiled test host is a supported configuration for this kit.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// public class SqlServerOutboxStoreConformanceTests : OutboxStoreConformanceTestKit
/// {
///     private readonly SqlServerFixture _fixture;
///
///     protected override async Task&lt;IOutboxStore&gt; CreateStoreAsync()
///     {
///         var services = new ServiceCollection();
///         services.AddSqlServerOutboxStore(o =&gt; o.ConnectionString = _fixture.ConnectionString);
///         await _fixture.EnsureInitializedAsync();
///         return services.BuildServiceProvider().GetRequiredService&lt;IOutboxStore&gt;();
///     }
///
///     protected override async Task CleanupAsync() =>
///         await _fixture.CleanupAsync();
/// }
/// </code>
/// </example>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1707:Identifiers should not contain underscores", Justification = "Test method naming convention")]
[System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode(
	"Outbox conformance arms read staged messages back through the store, which deserializes the payload reflectively. A trimmed test host is not a supported configuration for this kit.")]
[System.Diagnostics.CodeAnalysis.RequiresDynamicCode(
	"Outbox conformance arms read staged messages back through the store, which deserializes the payload reflectively. An ahead-of-time-compiled test host is not a supported configuration for this kit.")]
public abstract class OutboxStoreConformanceTestKit : ConformanceTestKit
{
	/// <summary>
	/// Creates a fresh outbox store instance for testing.
	/// </summary>
	/// <returns>An IOutboxStore implementation to test.</returns>
	/// <remarks>
	/// <para>
	/// The seam is asynchronous because a real provider cannot be constructed synchronously: starting a
	/// container, waiting for the engine to accept connections and creating the schema are all awaits, and
	/// a synchronous seam forces every such provider into sync-over-async to derive this kit at all. That
	/// is a plausible reason a suite like this ends up forked rather than derived.
	/// </para>
	/// <para>
	/// The seam takes no tenant context, and that is deliberate rather than an omission. No method on
	/// <c>IOutboxStore</c> accepts a tenant argument and a conformant store consults no ambient tenant
	/// context: ownership travels on the message. The tenancy arms in this kit therefore vary the
	/// <em>message</em>, not the host, and a store that resolved a tenant from ambient state would be
	/// answering a question this contract never asks it.
	/// </para>
	/// </remarks>
	protected abstract Task<IOutboxStore> CreateStoreAsync();

	/// <summary>
	/// Optional cleanup after each test.
	/// </summary>
	/// <returns>A task representing the cleanup operation.</returns>
	protected virtual Task CleanupAsync() => Task.CompletedTask;

	/// <summary>
	/// Generates a unique message ID for test isolation.
	/// </summary>
	/// <returns>A unique message identifier.</returns>
	protected virtual string GenerateMessageId() => Guid.NewGuid().ToString();

	/// <summary>
	/// Creates a payload from the given content string.
	/// </summary>
	/// <param name="content">The content to encode.</param>
	/// <returns>The encoded payload bytes.</returns>
	protected virtual byte[] CreatePayload(string content) =>
		Encoding.UTF8.GetBytes(content);

	/// <summary>
	/// Creates a test outbound message with default values.
	/// </summary>
	/// <returns>A new OutboundMessage for testing.</returns>
	protected virtual OutboundMessage CreateTestMessage()
	{
		return new OutboundMessage(
			messageType: "TestMessageType",
			payload: CreatePayload("Test payload content"),
			destination: "test-destination")
		{
			Id = GenerateMessageId()
		};
	}

	/// <summary>
	/// Creates a test outbound message owned by the supplied tenant.
	/// </summary>
	/// <param name="tenantId">
	/// The owning tenant, or <see langword="null"/> for the untenanted partition a single-tenant host uses.
	/// </param>
	/// <returns>A new OutboundMessage carrying the requested tenant.</returns>
	/// <remarks>
	/// <para>
	/// Cases that mean to exercise tenancy must stage through this factory rather than the no-argument
	/// one. The no-argument factory sets no tenant, so every row it produces lands in the untenanted
	/// partition — and a tenant predicate written as a comparison against an unresolved ambient tenant
	/// matches exactly that partition. A store whose aggregates or reads silently drop every tenanted row
	/// still returns the expected answer for that fixture, so a suite staged entirely through the
	/// no-argument factory exercises the one input for which such a defect cannot be observed.
	/// </para>
	/// </remarks>
	protected virtual OutboundMessage CreateTenantedTestMessage(string? tenantId)
	{
		return new OutboundMessage(
			messageType: "TestMessageType",
			payload: CreatePayload("Test payload content"),
			destination: "test-destination")
		{
			Id = GenerateMessageId(),
			TenantId = tenantId
		};
	}

	/// <summary>
	/// Creates a test outbound message with specified message ID.
	/// </summary>
	/// <param name="messageId">The message ID to use.</param>
	/// <returns>A new OutboundMessage for testing.</returns>
	protected virtual OutboundMessage CreateTestMessage(string messageId)
	{
		return new OutboundMessage(
			messageType: "TestMessageType",
			payload: CreatePayload("Test payload content"),
			destination: "test-destination")
		{
			Id = messageId
		};
	}

	/// <summary>
	/// Creates a test outbound message with scheduled delivery time.
	/// </summary>
	/// <param name="scheduledAt">The scheduled delivery time.</param>
	/// <returns>A new OutboundMessage scheduled for future delivery.</returns>
	protected virtual OutboundMessage CreateScheduledMessage(DateTimeOffset scheduledAt)
	{
		return new OutboundMessage(
			messageType: "ScheduledTestMessage",
			payload: CreatePayload("Scheduled payload"),
			destination: "test-destination")
		{
			Id = GenerateMessageId(),
			ScheduledAt = scheduledAt
		};
	}

	#region Stage Tests

	/// <summary>
	/// Verifies that staging a new message succeeds.
	/// </summary>
	public virtual async Task StageMessageAsync_NewMessage_ShouldSucceed()
	{
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);
		var message = CreateTestMessage();

		await store.StageMessageAsync(message, CancellationToken.None).ConfigureAwait(false);

		var unsent = await store.GetUnsentMessagesAsync(10, CancellationToken.None).ConfigureAwait(false);
		var unsentList = unsent.ToList();

		var found = unsentList.Any(m => m.Id == message.Id);
		if (!found)
		{
			throw new TestFixtureAssertionException(
				$"Expected to find staged message with ID '{message.Id}' in unsent messages");
		}
	}

	/// <summary>
	/// Verifies that staging a duplicate message ID throws.
	/// </summary>
	public virtual async Task StageMessageAsync_DuplicateId_ShouldThrowInvalidOperationException()
	{
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);
		var messageId = GenerateMessageId();
		var message1 = CreateTestMessage(messageId);
		var message2 = CreateTestMessage(messageId);

		await store.StageMessageAsync(message1, CancellationToken.None).ConfigureAwait(false);

		var threw = false;
		try
		{
			await store.StageMessageAsync(message2, CancellationToken.None).ConfigureAwait(false);
		}
		catch (InvalidOperationException)
		{
			threw = true;
		}

		if (!threw)
		{
			throw new TestFixtureAssertionException(
				$"Expected duplicate stage for message '{messageId}' to throw InvalidOperationException.");
		}
	}

	/// <summary>
	/// Verifies that staging a scheduled message stores it correctly.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Two properties, and the second is the load-bearing one. A scheduled message appears in
	/// <c>GetAllTenantsScheduledMessagesAsync</c> with its due time preserved, and it does NOT appear in
	/// <c>GetUnsentMessagesAsync</c> until that due time has arrived.
	/// </para>
	/// <para>
	/// <c>GetUnsentMessagesAsync</c> is the dispatcher's claim query: everything it returns is delivered
	/// now. A store that offers a not-yet-due message there sends it early, which for a scheduled message
	/// is the whole defect — the schedule is the contract, and a delivery before the due time breaks it
	/// with no error anywhere. There is no second acceptable behaviour: a store that includes the message
	/// and a store that excludes it differ in whether they honour the schedule at all.
	/// </para>
	/// </remarks>
	public virtual async Task StageMessageAsync_WithScheduledAt_ShouldStoreCorrectly()
	{
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);
		if (store.GetService(typeof(IOutboxStoreAdmin)) is not IOutboxStoreAdmin admin)
		{
			SkipArm(nameof(StageMessageAsync_WithScheduledAt_ShouldStoreCorrectly), typeof(IOutboxStoreAdmin), "Admin interface not supported");
			return;
		}

		RecordArmExecuted(nameof(StageMessageAsync_WithScheduledAt_ShouldStoreCorrectly));
		var futureTime = DateTimeOffset.UtcNow.AddHours(1);
		var message = CreateScheduledMessage(futureTime);

		await store.StageMessageAsync(message, CancellationToken.None).ConfigureAwait(false);

		// A message whose due time has not arrived must not be offered to the dispatcher. This is asserted
		// before the listing half because it is the property a consumer depends on: everything
		// GetUnsentMessagesAsync returns is delivered now.
		var unsent = await store.GetUnsentMessagesAsync(100, CancellationToken.None).ConfigureAwait(false);

		if (unsent.Any(m => string.Equals(m.Id, message.Id, StringComparison.Ordinal)))
		{
			throw new TestFixtureAssertionException(
				$"Message '{message.Id}' is scheduled for {futureTime:O}, which has not arrived, and the store "
				+ "offered it to GetUnsentMessagesAsync anyway. Everything that query returns is dispatched "
				+ "immediately, so this store sends scheduled messages early and no error is raised anywhere.");
		}

		// Scheduled messages SHOULD appear in GetAllTenantsScheduledMessagesAsync
		var scheduled = await admin.GetAllTenantsScheduledMessagesAsync(
			DateTimeOffset.UtcNow.AddHours(2),
			10,
			CancellationToken.None).ConfigureAwait(false);
		var inScheduled = scheduled.Any(m => m.Id == message.Id);

		if (!inScheduled)
		{
			throw new TestFixtureAssertionException(
				"Scheduled message should appear in GetAllTenantsScheduledMessagesAsync results");
		}

		// Verify the ScheduledAt property was preserved
		var foundMessage = scheduled.First(m => m.Id == message.Id);
		if (foundMessage.ScheduledAt is null)
		{
			throw new TestFixtureAssertionException(
				"Scheduled message should have ScheduledAt property set");
		}
	}

	#endregion

	#region Retrieval Tests

	/// <summary>
	/// Verifies that GetUnsentMessagesAsync returns staged messages.
	/// </summary>
	public virtual async Task GetUnsentMessagesAsync_ShouldReturnStagedMessages()
	{
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);
		var message1 = CreateTestMessage();
		var message2 = CreateTestMessage();

		await store.StageMessageAsync(message1, CancellationToken.None).ConfigureAwait(false);
		await store.StageMessageAsync(message2, CancellationToken.None).ConfigureAwait(false);

		var unsent = await store.GetUnsentMessagesAsync(10, CancellationToken.None).ConfigureAwait(false);
		var unsentList = unsent.ToList();

		var hasMessage1 = unsentList.Any(m => m.Id == message1.Id);
		var hasMessage2 = unsentList.Any(m => m.Id == message2.Id);

		if (!hasMessage1 || !hasMessage2)
		{
			throw new TestFixtureAssertionException(
				$"Expected both staged messages to be returned. Found message1: {hasMessage1}, message2: {hasMessage2}");
		}
	}

	/// <summary>
	/// Verifies that GetUnsentMessagesAsync respects batch size.
	/// </summary>
	public virtual async Task GetUnsentMessagesAsync_ShouldRespectBatchSize()
	{
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);

		// Stage 5 messages
		for (var i = 0; i < 5; i++)
		{
			await store.StageMessageAsync(CreateTestMessage(), CancellationToken.None).ConfigureAwait(false);
		}

		var unsent = await store.GetUnsentMessagesAsync(2, CancellationToken.None).ConfigureAwait(false);
		var unsentList = unsent.ToList();

		if (unsentList.Count > 2)
		{
			throw new TestFixtureAssertionException(
				$"Expected at most 2 messages due to batch size limit but got {unsentList.Count}");
		}
	}

	#endregion

	#region Sent Tests

	/// <summary>
	/// Verifies that MarkSentAsync sets SentAt timestamp.
	/// </summary>
	public virtual async Task MarkSentAsync_ExistingMessage_ShouldSetSentAt()
	{
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);
		var message = CreateTestMessage();

		await store.StageMessageAsync(message, CancellationToken.None).ConfigureAwait(false);
		await store.MarkSentAsync(message.Id, CancellationToken.None).ConfigureAwait(false);

		// Sent messages should not appear in unsent
		var unsent = await store.GetUnsentMessagesAsync(10, CancellationToken.None).ConfigureAwait(false);
		var stillUnsent = unsent.Any(m => m.Id == message.Id);

		if (stillUnsent)
		{
			throw new TestFixtureAssertionException(
				"Message marked as sent should not appear in unsent messages");
		}
	}

	/// <summary>
	/// Verifies that marking a sent message excludes it from unsent.
	/// </summary>
	public virtual async Task MarkSentAsync_ShouldExcludeFromUnsent()
	{
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);
		var message1 = CreateTestMessage();
		var message2 = CreateTestMessage();

		await store.StageMessageAsync(message1, CancellationToken.None).ConfigureAwait(false);
		await store.StageMessageAsync(message2, CancellationToken.None).ConfigureAwait(false);

		// Mark only message1 as sent
		await store.MarkSentAsync(message1.Id, CancellationToken.None).ConfigureAwait(false);

		var unsent = await store.GetUnsentMessagesAsync(10, CancellationToken.None).ConfigureAwait(false);
		var unsentList = unsent.ToList();

		var hasMessage1 = unsentList.Any(m => m.Id == message1.Id);
		var hasMessage2 = unsentList.Any(m => m.Id == message2.Id);

		if (hasMessage1)
		{
			throw new TestFixtureAssertionException(
				"Sent message should not appear in unsent list");
		}

		if (!hasMessage2)
		{
			throw new TestFixtureAssertionException(
				"Unsent message2 should still be in unsent list");
		}
	}

	/// <summary>
	/// Verifies that MarkSentAsync for non-existent message throws.
	/// </summary>
	public virtual async Task MarkSentAsync_NonExistent_ShouldThrowInvalidOperationException()
	{
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);
		var nonExistentId = GenerateMessageId();

		var threw = false;
		try
		{
			await store.MarkSentAsync(nonExistentId, CancellationToken.None).ConfigureAwait(false);
		}
		catch (InvalidOperationException)
		{
			threw = true;
		}

		if (!threw)
		{
			throw new TestFixtureAssertionException(
				$"Expected MarkSentAsync to throw InvalidOperationException for message '{nonExistentId}'.");
		}
	}

	#endregion

	#region Failure Tests

	/// <summary>
	/// Verifies that MarkFailedAsync sets error message.
	/// </summary>
	public virtual async Task MarkFailedAsync_ShouldSetErrorMessage()
	{
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);
		if (store.GetService(typeof(IOutboxStoreAdmin)) is not IOutboxStoreAdmin admin)
		{
			SkipArm(nameof(MarkFailedAsync_ShouldSetErrorMessage), typeof(IOutboxStoreAdmin), "Admin interface not supported");
			return;
		}

		RecordArmExecuted(nameof(MarkFailedAsync_ShouldSetErrorMessage));
		var message = CreateTestMessage();
		var errorMessage = "Test error message";

		await store.StageMessageAsync(message, CancellationToken.None).ConfigureAwait(false);
		await store.MarkFailedAsync(message.Id, errorMessage, 1, CancellationToken.None).ConfigureAwait(false);

		var failed = await admin.GetAllTenantsFailedMessagesAsync(10, null, 10, CancellationToken.None).ConfigureAwait(false);
		var failedMessage = failed.FirstOrDefault(m => m.Id == message.Id);

		if (failedMessage is null)
		{
			throw new TestFixtureAssertionException(
				"Expected failed message in GetAllTenantsFailedMessagesAsync results");
		}

		if (failedMessage.LastError != errorMessage)
		{
			throw new TestFixtureAssertionException(
				$"Expected LastError '{errorMessage}' but got '{failedMessage.LastError}'");
		}
	}

	/// <summary>
	/// Verifies that MarkFailedAsync sets retry count.
	/// </summary>
	public virtual async Task MarkFailedAsync_ShouldSetRetryCount()
	{
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);
		if (store.GetService(typeof(IOutboxStoreAdmin)) is not IOutboxStoreAdmin admin)
		{
			SkipArm(nameof(MarkFailedAsync_ShouldSetRetryCount), typeof(IOutboxStoreAdmin), "Admin interface not supported");
			return;
		}

		RecordArmExecuted(nameof(MarkFailedAsync_ShouldSetRetryCount));
		var message = CreateTestMessage();

		await store.StageMessageAsync(message, CancellationToken.None).ConfigureAwait(false);
		await store.MarkFailedAsync(message.Id, "Error 1", 1, CancellationToken.None).ConfigureAwait(false);

		var failed = await admin.GetAllTenantsFailedMessagesAsync(10, null, 10, CancellationToken.None).ConfigureAwait(false);
		var failedMessage = failed.FirstOrDefault(m => m.Id == message.Id);

		if (failedMessage is null)
		{
			throw new TestFixtureAssertionException("Expected failed message");
		}

		if (failedMessage.RetryCount != 1)
		{
			throw new TestFixtureAssertionException(
				$"Expected RetryCount 1 but got {failedMessage.RetryCount}");
		}
	}

	/// <summary>
	/// Verifies that GetAllTenantsFailedMessagesAsync respects maxRetries filter.
	/// </summary>
	public virtual async Task GetAllTenantsFailedMessagesAsync_ShouldRespectMaxRetries()
	{
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);
		if (store.GetService(typeof(IOutboxStoreAdmin)) is not IOutboxStoreAdmin admin)
		{
			SkipArm(nameof(GetAllTenantsFailedMessagesAsync_ShouldRespectMaxRetries), typeof(IOutboxStoreAdmin), "Admin interface not supported");
			return;
		}

		RecordArmExecuted(nameof(GetAllTenantsFailedMessagesAsync_ShouldRespectMaxRetries));
		var message1 = CreateTestMessage();
		var message2 = CreateTestMessage();

		await store.StageMessageAsync(message1, CancellationToken.None).ConfigureAwait(false);
		await store.StageMessageAsync(message2, CancellationToken.None).ConfigureAwait(false);

		// Fail message1 with 2 retries, message2 with 5 retries
		await store.MarkFailedAsync(message1.Id, "Error", 2, CancellationToken.None).ConfigureAwait(false);
		await store.MarkFailedAsync(message2.Id, "Error", 5, CancellationToken.None).ConfigureAwait(false);

		// Query with maxRetries=3 - should only return message1
		var failed = await admin.GetAllTenantsFailedMessagesAsync(3, null, 10, CancellationToken.None).ConfigureAwait(false);
		var failedList = failed.ToList();

		var hasExcessiveRetries = failedList.Any(m => m.RetryCount > 3);
		if (hasExcessiveRetries)
		{
			throw new TestFixtureAssertionException(
				"GetAllTenantsFailedMessagesAsync should not return messages exceeding maxRetries");
		}
	}

	/// <summary>
	/// Verifies that GetAllTenantsFailedMessagesAsync respects olderThan filter.
	/// </summary>
	public virtual async Task GetAllTenantsFailedMessagesAsync_ShouldRespectOlderThan()
	{
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);
		if (store.GetService(typeof(IOutboxStoreAdmin)) is not IOutboxStoreAdmin admin)
		{
			SkipArm(nameof(GetAllTenantsFailedMessagesAsync_ShouldRespectOlderThan), typeof(IOutboxStoreAdmin), "Admin interface not supported");
			return;
		}

		RecordArmExecuted(nameof(GetAllTenantsFailedMessagesAsync_ShouldRespectOlderThan));
		var message = CreateTestMessage();

		await store.StageMessageAsync(message, CancellationToken.None).ConfigureAwait(false);
		await store.MarkFailedAsync(message.Id, "Error", 1, CancellationToken.None).ConfigureAwait(false);

		// Query for messages older than 1 minute ago - our just-failed message should NOT match
		var pastThreshold = DateTimeOffset.UtcNow.AddMinutes(-1);
		var failed = await admin.GetAllTenantsFailedMessagesAsync(10, pastThreshold, 10, CancellationToken.None).ConfigureAwait(false);
		var hasRecentMessage = failed.Any(m => m.Id == message.Id);

		if (hasRecentMessage)
		{
			throw new TestFixtureAssertionException(
				"Recently failed message should not appear when olderThan is in the past");
		}
	}

	#endregion

	#region Scheduled Tests

	/// <summary>
	/// Verifies that GetAllTenantsScheduledMessagesAsync returns scheduled messages before threshold.
	/// </summary>
	public virtual async Task GetAllTenantsScheduledMessagesAsync_ShouldReturnScheduledBeforeThreshold()
	{
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);
		if (store.GetService(typeof(IOutboxStoreAdmin)) is not IOutboxStoreAdmin admin)
		{
			SkipArm(nameof(GetAllTenantsScheduledMessagesAsync_ShouldReturnScheduledBeforeThreshold), typeof(IOutboxStoreAdmin), "Admin interface not supported");
			return;
		}

		RecordArmExecuted(nameof(GetAllTenantsScheduledMessagesAsync_ShouldReturnScheduledBeforeThreshold));
		// Schedule message for 30 minutes from now
		var scheduledTime = DateTimeOffset.UtcNow.AddMinutes(30);
		var message = CreateScheduledMessage(scheduledTime);

		await store.StageMessageAsync(message, CancellationToken.None).ConfigureAwait(false);

		// Query for messages scheduled before 1 hour from now - should include our message
		var scheduled = await admin.GetAllTenantsScheduledMessagesAsync(
			DateTimeOffset.UtcNow.AddHours(1),
			10,
			CancellationToken.None).ConfigureAwait(false);

		var found = scheduled.Any(m => m.Id == message.Id);
		if (!found)
		{
			throw new TestFixtureAssertionException(
				"Scheduled message should be returned when its schedule time is before the threshold");
		}
	}

	/// <summary>
	/// Verifies that GetAllTenantsScheduledMessagesAsync does not return immediate messages.
	/// </summary>
	public virtual async Task GetAllTenantsScheduledMessagesAsync_ShouldNotReturnImmediateMessages()
	{
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);
		if (store.GetService(typeof(IOutboxStoreAdmin)) is not IOutboxStoreAdmin admin)
		{
			SkipArm(nameof(GetAllTenantsScheduledMessagesAsync_ShouldNotReturnImmediateMessages), typeof(IOutboxStoreAdmin), "Admin interface not supported");
			return;
		}

		RecordArmExecuted(nameof(GetAllTenantsScheduledMessagesAsync_ShouldNotReturnImmediateMessages));
		// Stage an immediate message (no ScheduledAt)
		var immediateMessage = CreateTestMessage();

		await store.StageMessageAsync(immediateMessage, CancellationToken.None).ConfigureAwait(false);

		var scheduled = await admin.GetAllTenantsScheduledMessagesAsync(
			DateTimeOffset.UtcNow.AddHours(1),
			10,
			CancellationToken.None).ConfigureAwait(false);

		var hasImmediate = scheduled.Any(m => m.Id == immediateMessage.Id);
		if (hasImmediate)
		{
			throw new TestFixtureAssertionException(
				"Immediate messages (no ScheduledAt) should not appear in scheduled messages");
		}
	}

	#endregion

	#region Cleanup Tests

	/// <summary>
	/// Verifies that CleanupAllTenantsSentMessagesAsync removes old sent messages.
	/// </summary>
	public virtual async Task CleanupAllTenantsSentMessagesAsync_ShouldRemoveOldMessages()
	{
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);
		if (store.GetService(typeof(IOutboxStoreAdmin)) is not IOutboxStoreAdmin admin)
		{
			SkipArm(nameof(CleanupAllTenantsSentMessagesAsync_ShouldRemoveOldMessages), typeof(IOutboxStoreAdmin), "Admin interface not supported");
			return;
		}

		RecordArmExecuted(nameof(CleanupAllTenantsSentMessagesAsync_ShouldRemoveOldMessages));
		var message = CreateTestMessage();

		await store.StageMessageAsync(message, CancellationToken.None).ConfigureAwait(false);
		await store.MarkSentAsync(message.Id, CancellationToken.None).ConfigureAwait(false);

		// Cleanup with future threshold - should remove our just-sent message
		var futureThreshold = DateTimeOffset.UtcNow.AddHours(1);
		var removed = await admin.CleanupAllTenantsSentMessagesAsync(futureThreshold, 100, CancellationToken.None)
			.ConfigureAwait(false);

		// Both retention policies are conformant and each is asserted against its own expected outcome
		// rather than one being skipped. A store that retains sent messages must have one to remove here;
		// a store that deletes them at mark-sent has none left, and demanding a removal from it would
		// reject a correct store. Neither branch is vacuous: whichever policy a store declares, the other
		// outcome fails it.
		if (SupportsSentTracking(store))
		{
			if (removed < 1)
			{
				throw new TestFixtureAssertionException(
					"This store retains messages after they are sent, so a cleanup whose threshold is in the "
					+ $"future had one to remove — and removed {removed}.");
			}
		}
		else if (removed != 0)
		{
			throw new TestFixtureAssertionException(
				"This store deletes messages at mark-sent, so a later cleanup of sent messages has nothing "
				+ $"left to remove — and it reported removing {removed}.");
		}
	}

	/// <summary>
	/// Verifies that CleanupAllTenantsSentMessagesAsync respects batch size.
	/// </summary>
	public virtual async Task CleanupAllTenantsSentMessagesAsync_ShouldRespectBatchSize()
	{
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);
		if (store.GetService(typeof(IOutboxStoreAdmin)) is not IOutboxStoreAdmin admin)
		{
			SkipArm(nameof(CleanupAllTenantsSentMessagesAsync_ShouldRespectBatchSize), typeof(IOutboxStoreAdmin), "Admin interface not supported");
			return;
		}

		RecordArmExecuted(nameof(CleanupAllTenantsSentMessagesAsync_ShouldRespectBatchSize));
		// Stage and send 5 messages
		for (var i = 0; i < 5; i++)
		{
			var message = CreateTestMessage();
			await store.StageMessageAsync(message, CancellationToken.None).ConfigureAwait(false);
			await store.MarkSentAsync(message.Id, CancellationToken.None).ConfigureAwait(false);
		}

		// Cleanup with batch size of 2
		var futureThreshold = DateTimeOffset.UtcNow.AddHours(1);
		var removed = await admin.CleanupAllTenantsSentMessagesAsync(futureThreshold, 2, CancellationToken.None)
			.ConfigureAwait(false);

		if (removed > 2)
		{
			throw new TestFixtureAssertionException(
				$"Expected at most 2 removed due to batch size but got {removed}");
		}
	}

	#endregion

	#region Statistics Tests

	/// <summary>
	/// Verifies that GetAllTenantsStatisticsAsync reflects message counts.
	/// </summary>
	public virtual async Task GetAllTenantsStatisticsAsync_ShouldReflectMessageCounts()
	{
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);
		if (store.GetService(typeof(IOutboxStoreAdmin)) is not IOutboxStoreAdmin admin)
		{
			SkipArm(nameof(GetAllTenantsStatisticsAsync_ShouldReflectMessageCounts), typeof(IOutboxStoreAdmin), "Admin interface not supported");
			return;
		}

		RecordArmExecuted(nameof(GetAllTenantsStatisticsAsync_ShouldReflectMessageCounts));
		// Stage a message
		var stagedMessage = CreateTestMessage();
		await store.StageMessageAsync(stagedMessage, CancellationToken.None).ConfigureAwait(false);

		// Stage and send a message
		var sentMessage = CreateTestMessage();
		await store.StageMessageAsync(sentMessage, CancellationToken.None).ConfigureAwait(false);
		await store.MarkSentAsync(sentMessage.Id, CancellationToken.None).ConfigureAwait(false);

		// Stage and fail a message
		var failedMessage = CreateTestMessage();
		await store.StageMessageAsync(failedMessage, CancellationToken.None).ConfigureAwait(false);
		await store.MarkFailedAsync(failedMessage.Id, "Error", 1, CancellationToken.None).ConfigureAwait(false);

		var stats = await admin.GetAllTenantsStatisticsAsync(CancellationToken.None).ConfigureAwait(false);

		if (stats is null)
		{
			throw new TestFixtureAssertionException("Expected statistics but got null");
		}

		if (stats.StagedMessageCount < 1)
		{
			throw new TestFixtureAssertionException(
				$"Expected at least 1 staged message but got {stats.StagedMessageCount}");
		}

		// Sent-message counting depends on the store's retention policy, and both are conformant. A store
		// that deletes at mark-sent legitimately reports none, so demanding a non-zero count here would
		// reject a correct store. The matching outcome is asserted for each policy, so neither is vacuous.
		if (SupportsSentTracking(store))
		{
			if (stats.SentMessageCount < 1)
			{
				throw new TestFixtureAssertionException(
					"This store retains messages after they are sent, so the sent count must include the "
					+ $"message marked sent above — and it reported {stats.SentMessageCount}.");
			}
		}
		else if (stats.SentMessageCount != 0)
		{
			throw new TestFixtureAssertionException(
				"This store deletes messages at mark-sent, so the sent count must be zero — and it reported "
				+ $"{stats.SentMessageCount}.");
		}

		if (stats.FailedMessageCount < 1)
		{
			throw new TestFixtureAssertionException(
				$"Expected at least 1 failed message but got {stats.FailedMessageCount}");
		}
	}

	/// <summary>
	/// Verifies that GetAllTenantsStatisticsAsync updates accurately after operations.
	/// </summary>
	public virtual async Task GetAllTenantsStatisticsAsync_AfterOperations_ShouldUpdateAccurately()
	{
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);
		if (store.GetService(typeof(IOutboxStoreAdmin)) is not IOutboxStoreAdmin admin)
		{
			SkipArm(nameof(GetAllTenantsStatisticsAsync_AfterOperations_ShouldUpdateAccurately), typeof(IOutboxStoreAdmin), "Admin interface not supported");
			return;
		}

		RecordArmExecuted(nameof(GetAllTenantsStatisticsAsync_AfterOperations_ShouldUpdateAccurately));
		// Get initial stats
		var initialStats = await admin.GetAllTenantsStatisticsAsync(CancellationToken.None).ConfigureAwait(false);
		var initialTotal = initialStats?.TotalMessageCount ?? 0;

		// Stage a message
		var message = CreateTestMessage();
		await store.StageMessageAsync(message, CancellationToken.None).ConfigureAwait(false);

		// Get stats after staging
		var afterStageStats = await admin.GetAllTenantsStatisticsAsync(CancellationToken.None).ConfigureAwait(false);

		if (afterStageStats is null)
		{
			throw new TestFixtureAssertionException("Expected statistics after staging but got null");
		}

		if (afterStageStats.TotalMessageCount <= initialTotal)
		{
			throw new TestFixtureAssertionException(
				$"Expected total count to increase after staging. Initial: {initialTotal}, After: {afterStageStats.TotalMessageCount}");
		}
	}

	#endregion

	#region Tenant Attribution Tests

	/// <summary>
	/// ENABLING PROPERTY: a message staged under a tenant must be drained still carrying that tenant.
	/// </summary>
	/// <remarks>
	/// <para>
	/// This is the arm the whole tenancy contract of <c>IOutboxStore</c> rests on. No method on the
	/// interface takes a tenant argument and no conformant store consults an ambient tenant context, so
	/// the <em>only</em> carrier of ownership is the field on the message itself. The drain is deliberately
	/// cross-tenant — one dispatcher serves every tenant — and a handler re-establishes the owning
	/// partition per message from the value returned here.
	/// </para>
	/// <para>
	/// If a store drops the tenant on the round trip, delivery does not fail and no isolation assertion
	/// fires. The message is simply published having forgotten whose it was, and the handler re-establishes
	/// the wrong partition, or none. That is why this is asserted directly rather than inferred from a
	/// scoped read.
	/// </para>
	/// </remarks>
	public virtual async Task StageMessage_TenantAttribution_SurvivesTheDrain()
	{
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);
		const string tenantId = "conformance-tenant-a";

		var message = CreateTenantedTestMessage(tenantId: tenantId);
		await store.StageMessageAsync(message, CancellationToken.None).ConfigureAwait(false);

		var drained = await store.GetUnsentMessagesAsync(100, CancellationToken.None).ConfigureAwait(false);
		var own = drained.FirstOrDefault(m => string.Equals(m.Id, message.Id, StringComparison.Ordinal));

		if (own is null)
		{
			throw new TestFixtureAssertionException(
				$"Tenant attribution could not be observed: message {message.Id} was staged carrying tenant "
				+ $"'{tenantId}' and the drain did not return it at all.");
		}

		if (!string.Equals(own.TenantId, tenantId, StringComparison.Ordinal))
		{
			throw new TestFixtureAssertionException(
				$"Tenant attribution lost on the drain: message {message.Id} was staged carrying tenant "
				+ $"'{tenantId}' and was drained carrying '{own.TenantId ?? "<null>"}'. The message is the "
				+ "only carrier of ownership on this contract, so a handler cannot re-establish the owning "
				+ "partition and will process it under the wrong tenant, or none.");
		}
	}

	/// <summary>
	/// SAFETY: the drain must not be confined to one tenant — every tenant's message must be reachable.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The safety property for an outbox drain is the opposite of the one a reader may expect. Confinement
	/// is the fault here, not the goal: one dispatcher serves the whole estate, so a store that scopes this
	/// read to some ambient tenant stalls delivery permanently for every other tenant. The staged rows are
	/// still there, the drain simply stops returning them, and nothing else in the contract notices.
	/// </para>
	/// <para>
	/// This arm exists to fail on the plausible wrong fix. A reviewer who reads "tenant isolation" and adds
	/// a tenant predicate to the drain will pass every other case in this kit and break delivery for the
	/// estate; that change turns this arm red immediately.
	/// </para>
	/// </remarks>
	public virtual async Task Drain_MustReturnMessagesFromEveryTenant()
	{
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);

		var first = CreateTenantedTestMessage(tenantId: "conformance-tenant-a");
		var second = CreateTenantedTestMessage(tenantId: "conformance-tenant-b");
		await store.StageMessageAsync(first, CancellationToken.None).ConfigureAwait(false);
		await store.StageMessageAsync(second, CancellationToken.None).ConfigureAwait(false);

		var drained = (await store.GetUnsentMessagesAsync(100, CancellationToken.None).ConfigureAwait(false))
			.ToList();

		var sawFirst = drained.Exists(m => string.Equals(m.Id, first.Id, StringComparison.Ordinal));
		var sawSecond = drained.Exists(m => string.Equals(m.Id, second.Id, StringComparison.Ordinal));

		if (!sawFirst || !sawSecond)
		{
			throw new TestFixtureAssertionException(
				"The drain is confined to a subset of tenants. Messages were staged under two tenants and the "
				+ $"drain returned tenant-a={sawFirst}, tenant-b={sawSecond}. One dispatcher serves every "
				+ "tenant on this contract, so a scoped drain stalls delivery permanently for every tenant it "
				+ "excludes while the rows remain staged and no other assertion fails.");
		}
	}

	/// <summary>
	/// LIVENESS: the untenanted partition is a real partition and must round-trip.
	/// </summary>
	/// <remarks>
	/// A single-tenant host stages messages carrying no tenant at all. If a store implements tenancy so
	/// that a null tenant matches nothing, every message a single-tenant host stages becomes undrainable —
	/// the most common deployment, broken by the tenancy feature. This arm is what keeps a tenant predicate
	/// from being written as a plain equality that a null can never satisfy.
	/// </remarks>
	public virtual async Task UntenantedPartition_MustRoundTripItsOwnMessage()
	{
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);

		var message = CreateTenantedTestMessage(tenantId: null);
		await store.StageMessageAsync(message, CancellationToken.None).ConfigureAwait(false);

		var drained = await store.GetUnsentMessagesAsync(100, CancellationToken.None).ConfigureAwait(false);
		var own = drained.FirstOrDefault(m => string.Equals(m.Id, message.Id, StringComparison.Ordinal));

		if (own is null)
		{
			throw new TestFixtureAssertionException(
				$"The untenanted partition does not round-trip: message {message.Id} was staged with no "
				+ "tenant and the drain did not return it. A single-tenant host stages every message this "
				+ "way, so this store would never deliver anything for one.");
		}

		// The contract fixes the REPRESENTATION as well as the partition: an untenanted message is drained
		// carrying the reserved sentinel, never a null, an empty string, or whitespace. "Untenanted is a
		// value, not an absence" — when absence has two spellings something has to fold between them, and
		// every fold is a place the two can disagree. A consumer handler written as `msg.TenantId is null`
		// must not re-establish a different partition depending on which store is underneath it, and a
		// consumer writing their own store needs one answer to implement against. Fold a caller-supplied
		// null through KeyedTenantPartition.FromStoredValue when you persist it, and again when you read it
		// back, and this arm passes.
		if (!string.Equals(own.TenantId, TenantScope.UntenantedSentinel, StringComparison.Ordinal))
		{
			var carried = own.TenantId is null ? "<null>" : $"'{own.TenantId}'";

			throw new TestFixtureAssertionException(
				$"A message staged with no tenant was drained carrying tenant {carried}, but the contract "
				+ $"requires the reserved untenanted partition '{TenantScope.UntenantedSentinel}'. "
				+ (own.TenantId is null || string.IsNullOrWhiteSpace(own.TenantId)
					? "The store round-trips absence as an absence, so a handler cannot tell an untenanted "
					+ "message from one whose tenant this store simply does not carry, and the same handler "
					+ "behaves differently against a provider that stores the sentinel."
					: "The store invented an owner the caller never supplied, and a handler will "
					+ "re-establish that fabricated tenant."));
		}
	}

	/// <summary>
	/// SAFETY: estate-wide statistics must count a tenanted message, not only an untenanted one.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The statistics call takes no tenant argument and returns flat counts with no tenant field, so its
	/// answer is estate-wide by construction — it cannot describe which partition it counted. A store that
	/// filters these aggregates on any tenant therefore under-reports, and it under-reports
	/// <em>silently</em>: the call succeeds and returns a smaller number.
	/// </para>
	/// <para>
	/// <strong>This arm exists because the rest of the kit cannot fail on it.</strong> Every other
	/// statistics case stages through the no-argument message factory, which sets no tenant, so every row
	/// lands in the untenanted partition. A predicate comparing a row's tenant against an unresolved
	/// ambient one matches exactly those rows and no others — so an aggregate that drops every tenanted
	/// message still returns the expected count for that fixture. The suite exercises the single input for
	/// which the statement is incapable of being wrong. Staging a row that carries a real tenant is what
	/// makes the assertion falsifiable.
	/// </para>
	/// </remarks>
	public virtual async Task GetStatistics_MustCountTenantedMessages()
	{
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);
		if (store.GetService(typeof(IOutboxStoreAdmin)) is not IOutboxStoreAdmin admin)
		{
			SkipArm(nameof(GetStatistics_MustCountTenantedMessages), typeof(IOutboxStoreAdmin), "Admin interface not supported by this store.");
			return;
		}

		RecordArmExecuted(nameof(GetStatistics_MustCountTenantedMessages));
		var before = await admin.GetAllTenantsStatisticsAsync(CancellationToken.None).ConfigureAwait(false);
		var baseline = before?.StagedMessageCount ?? 0;

		var tenanted = CreateTenantedTestMessage(tenantId: "conformance-tenant-a");
		await store.StageMessageAsync(tenanted, CancellationToken.None).ConfigureAwait(false);

		var after = await admin.GetAllTenantsStatisticsAsync(CancellationToken.None).ConfigureAwait(false);
		if (after is null)
		{
			throw new TestFixtureAssertionException("Expected statistics but got null.");
		}

		if (after.StagedMessageCount <= baseline)
		{
			throw new TestFixtureAssertionException(
				$"A tenanted message is not counted by estate-wide statistics: staged count was {baseline} "
				+ $"before staging a message carrying a tenant and {after.StagedMessageCount} after. These "
				+ "aggregates take no tenant argument and carry no tenant field, so they describe the whole "
				+ "table; filtering them on a tenant the store never receives drops every tenanted row and "
				+ "reports a smaller number to the operator without failing.");
		}
	}

	#endregion

	#region Isolation And Capability Seams

	/// <summary>
	/// Clears residual data before each arm, leaving the store returned by <see cref="CreateStoreAsync"/>
	/// fully usable.
	/// </summary>
	/// <returns>A task that completes when residual data has been cleared.</returns>
	/// <remarks>
	/// <para>
	/// Defaults to <see cref="CleanupAsync"/>, which is correct for any suite whose teardown only deletes
	/// rows, keys or documents. A suite whose <see cref="CleanupAsync"/> <em>also</em> disposes a
	/// connection or client MUST override this with the data-only half — otherwise it disposes the store
	/// the arm is about to use, and every arm fails on a disposed handle rather than on the contract.
	/// </para>
	/// <para>
	/// Resetting <em>before</em> an arm is what makes the arm independent; resetting only afterwards makes
	/// every arm's starting state a function of whether its predecessor finished cleanly. An arm that
	/// fails partway, a store whose delete lags its commit, or a cleanup that silently misses rows all
	/// leave residue, and the arm that reports the failure is then not the arm that caused it.
	/// </para>
	/// <para>
	/// Implementing this seam is a consumer obligation, not an optional extra. The arms in this kit are
	/// written to tolerate residue wherever the property allows it — counts are asserted as deltas from a
	/// baseline measured inside the arm, and membership is asserted for the specific identifiers the arm
	/// staged. Two arms cannot be written that way, because "the store is empty" is not expressible
	/// relative to a baseline; those two say so in their own failure messages.
	/// </para>
	/// </remarks>
	protected virtual Task ResetDataAsync() => CleanupAsync();

	/// <summary>
	/// Creates a store for a single conformance arm and clears residual data from it.
	/// </summary>
	/// <returns>A store whose residual data has been cleared.</returns>
	protected async Task<IOutboxStore> CreateStoreForArmAsync()
	{
		var store = await CreateStoreAsync().ConfigureAwait(false);
		await ResetDataAsync().ConfigureAwait(false);
		return store;
	}

	/// <summary>
	/// Gets a value indicating whether the store under test retains messages after they are marked sent.
	/// </summary>
	/// <value>
	/// <see langword="true"/> when the store keeps sent rows for later inspection and cleanup;
	/// <see langword="false"/> when it deletes them at mark-sent.
	/// </value>
	/// <remarks>
	/// <para>
	/// Both retention policies are conformant, and the arms below assert the <em>matching</em> outcome for
	/// whichever a store declares rather than skipping. Neither branch is vacuous: a retaining store fails
	/// the delete-on-sent expectation and a delete-on-sent store fails the retaining one.
	/// </para>
	/// <para>
	/// A store that declares nothing is treated as retaining. That is not this kit's guess: it is the
	/// default <see cref="IOutboxStoreCapabilities"/> itself states, under which only delete-on-sent stores
	/// need declare anything. Reading the declaration through <see cref="IServiceProvider.GetService(Type)"/>
	/// rather than a cast is what makes the default safe to apply -- a cast sees only the outermost type, so
	/// a delete-on-sent store behind any decorator would silently fall through to the retaining default and
	/// be held to a contract it never claimed.
	/// </para>
	/// </remarks>
	protected static bool SupportsSentTracking(IOutboxStore store)
	{
		ArgumentNullException.ThrowIfNull(store);

		return store.GetService(typeof(IOutboxStoreCapabilities)) is not IOutboxStoreCapabilities capabilities
			|| capabilities.SupportsSentTracking;
	}

	/// <summary>
	/// Creates a store configured with the given failure-anchored re-claim floor, in seconds.
	/// </summary>
	/// <param name="floorSeconds">The failure-anchored re-claim floor, in seconds.</param>
	/// <returns>
	/// A store configured with the floor, or <see langword="null"/> when the store under test does not
	/// implement a failure-anchored re-claim floor.
	/// </returns>
	/// <remarks>
	/// <para>
	/// A store that reports a delivery attempt as failed must not make that message immediately claimable
	/// again — an immediate re-claim is a zero-backoff hot loop that saturates the transport against a
	/// persistently failing destination. The floor is the interval, anchored at the recorded failure, for
	/// which the message stays unclaimable.
	/// </para>
	/// <para>
	/// Returning <see langword="null"/> means the arms in the re-claim floor region do not apply to this
	/// store and return without asserting. Overriding this seam and then returning <see langword="null"/>
	/// for every floor is a different thing, and the liveness arm in that region fails on it: a suite that
	/// declares the capability and then exercises it zero times reads as coverage while providing none.
	/// </para>
	/// </remarks>
	protected virtual Task<IOutboxStore?> CreateStoreWithReclaimFloorAsync(int floorSeconds) =>
		Task.FromResult<IOutboxStore?>(null);

	/// <summary>
	/// Reserves an already-staged message under a dispatcher identity foreign to the given store.
	/// </summary>
	/// <param name="store">The store holding the staged message.</param>
	/// <param name="messageId">The identifier of the message to reserve.</param>
	/// <returns>
	/// <see langword="true"/> when the message was reserved under a foreign dispatcher identity;
	/// <see langword="false"/> when the store exposes no way to do so.
	/// </returns>
	/// <remarks>
	/// The reservation-ownership arm needs the row to be owned by somebody <em>other</em> than the caller
	/// of <c>MarkFailedAsync</c>. A store whose dispatcher identity is fixed per process cannot produce
	/// that state from two in-process instances — they share the identity — so the arm needs an explicit
	/// entry point that names the owner. Returning <see langword="false"/> means the arm cannot be staged
	/// against this store and it returns without asserting.
	/// </remarks>
	protected virtual Task<bool> TryReserveMessageUnderForeignDispatcherAsync(
		IOutboxStore store,
		string messageId) => Task.FromResult(false);

	/// <summary>
	/// Produces a fencing token that is strictly greater than any token this suite has used before.
	/// </summary>
	/// <returns>A monotonically increasing fencing token.</returns>
	/// <remarks>
	/// <para>
	/// The fencing high-water mark is durable and shared by every arm that runs against the same store, so
	/// a fixed constant is only safe for whichever arm happens to run first. An arm establishing a
	/// high-water of 100 leaves every later arm that presents 10 correctly fenced off and reported as a
	/// failure of the store rather than of the fixture.
	/// </para>
	/// <para>
	/// Anchoring on the wall clock removes the ordering dependency without needing the store to expose its
	/// current high-water: the value rises across arms within a run and across runs, so each arm can
	/// establish its own baseline and reason relative to it. The arms below never compare against an
	/// absolute token.
	/// </para>
	/// </remarks>
	protected static long NextFencingToken() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

	#endregion

	#region Argument Validation

	/// <summary>
	/// Verifies that staging a null message is rejected rather than persisted.
	/// </summary>
	public virtual async Task StageMessageAsync_NullMessage_ShouldThrowArgumentNullException()
	{
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);

		var threw = false;
		try
		{
			await store.StageMessageAsync(null!, CancellationToken.None).ConfigureAwait(false);
		}
		catch (ArgumentNullException)
		{
			threw = true;
		}

		if (!threw)
		{
			throw new TestFixtureAssertionException(
				"Staging a null message must throw ArgumentNullException. A store that accepts it either "
				+ "persists a row with no payload or fails later on a path the caller cannot attribute.");
		}
	}

	/// <summary>
	/// Verifies that a non-positive batch size is rejected rather than silently reinterpreted.
	/// </summary>
	/// <remarks>
	/// A store that reads zero as "no limit" drains its whole backlog into memory on a call the caller
	/// believed asked for nothing, and one that reads it as "none" stalls a poller that will never report
	/// an error. Both are worse than rejecting the argument.
	/// </remarks>
	public virtual async Task GetUnsentMessagesAsync_NonPositiveBatchSize_ShouldThrowArgumentOutOfRangeException()
	{
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);

		var threw = false;
		try
		{
			_ = await store.GetUnsentMessagesAsync(0, CancellationToken.None).ConfigureAwait(false);
		}
		catch (ArgumentOutOfRangeException)
		{
			threw = true;
		}

		if (!threw)
		{
			throw new TestFixtureAssertionException(
				"GetUnsentMessagesAsync(0) must throw ArgumentOutOfRangeException rather than reinterpret a "
				+ "non-positive batch size as 'no limit' or 'none'.");
		}
	}

	/// <summary>
	/// Verifies that marking a null message identifier as sent is rejected.
	/// </summary>
	public virtual async Task MarkSentAsync_NullMessageId_ShouldThrowArgumentException()
	{
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);

		var threw = false;
		try
		{
			await store.MarkSentAsync(null!, CancellationToken.None).ConfigureAwait(false);
		}
		catch (ArgumentException)
		{
			threw = true;
		}

		if (!threw)
		{
			throw new TestFixtureAssertionException("MarkSentAsync(null) must throw ArgumentException.");
		}
	}

	/// <summary>
	/// Verifies that marking an empty message identifier as sent is rejected.
	/// </summary>
	/// <remarks>
	/// The empty string is the case a null guard alone misses, and it is the one a caller reaches by
	/// propagating an unset field rather than an explicit null.
	/// </remarks>
	public virtual async Task MarkSentAsync_EmptyMessageId_ShouldThrowArgumentException()
	{
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);

		var threw = false;
		try
		{
			await store.MarkSentAsync(string.Empty, CancellationToken.None).ConfigureAwait(false);
		}
		catch (ArgumentException)
		{
			threw = true;
		}

		if (!threw)
		{
			throw new TestFixtureAssertionException(
				"MarkSentAsync(string.Empty) must throw ArgumentException. A null guard alone does not cover "
				+ "the empty identifier a caller reaches by propagating an unset field.");
		}
	}

	/// <summary>
	/// Verifies that marking a null message identifier as failed is rejected.
	/// </summary>
	public virtual async Task MarkFailedAsync_NullMessageId_ShouldThrowArgumentException()
	{
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);

		var threw = false;
		try
		{
			await store.MarkFailedAsync(null!, "error", 1, CancellationToken.None).ConfigureAwait(false);
		}
		catch (ArgumentException)
		{
			threw = true;
		}

		if (!threw)
		{
			throw new TestFixtureAssertionException("MarkFailedAsync(null, ...) must throw ArgumentException.");
		}
	}

	/// <summary>
	/// Verifies that recording a failure with a null error message is rejected.
	/// </summary>
	/// <remarks>
	/// The recorded error is the only account an operator gets of why a message stopped moving. A store
	/// that accepts a null leaves a failed row with no diagnosis attached to it.
	/// </remarks>
	public virtual async Task MarkFailedAsync_NullErrorMessage_ShouldThrowArgumentNullException()
	{
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);
		var message = CreateTestMessage();
		await store.StageMessageAsync(message, CancellationToken.None).ConfigureAwait(false);

		var threw = false;
		try
		{
			await store.MarkFailedAsync(message.Id, null!, 1, CancellationToken.None).ConfigureAwait(false);
		}
		catch (ArgumentNullException)
		{
			threw = true;
		}

		if (!threw)
		{
			throw new TestFixtureAssertionException(
				"MarkFailedAsync with a null error message must throw ArgumentNullException. The recorded "
				+ "error is the only account of why the message stopped moving.");
		}
	}

	/// <summary>
	/// Verifies that marking an already-sent message as sent again is rejected.
	/// </summary>
	/// <remarks>
	/// A second successful mark-sent means two dispatchers each believe they own the delivery, which is
	/// the duplicate the outbox exists to bound.
	/// </remarks>
	public virtual async Task MarkSentAsync_AlreadySent_ShouldThrowInvalidOperationException()
	{
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);
		var message = CreateTestMessage();

		await store.StageMessageAsync(message, CancellationToken.None).ConfigureAwait(false);
		await store.MarkSentAsync(message.Id, CancellationToken.None).ConfigureAwait(false);

		var threw = false;
		try
		{
			await store.MarkSentAsync(message.Id, CancellationToken.None).ConfigureAwait(false);
		}
		catch (InvalidOperationException)
		{
			threw = true;
		}

		if (!threw)
		{
			throw new TestFixtureAssertionException(
				$"Marking message '{message.Id}' sent a second time must throw InvalidOperationException. A "
				+ "store that accepts it lets two dispatchers each believe they own the delivery.");
		}
	}

	#endregion

	#region Retrieval Semantics

	/// <summary>
	/// Verifies that a store with nothing staged returns no unsent messages.
	/// </summary>
	/// <remarks>
	/// This arm and its statistics twin are the only two in the kit that cannot be written relative to a
	/// baseline, because emptiness has no baseline. They therefore depend on <see cref="ResetDataAsync"/>
	/// actually clearing the store, and the failure message says so — a suite that has not implemented
	/// that seam is told what to fix rather than told its store is broken.
	/// </remarks>
	public virtual async Task GetUnsentMessagesAsync_EmptyStore_ShouldReturnNothing()
	{
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);

		var unsent = (await store.GetUnsentMessagesAsync(10, CancellationToken.None).ConfigureAwait(false))
			.ToList();

		if (unsent.Count > 0)
		{
			throw new TestFixtureAssertionException(
				$"A store with nothing staged returned {unsent.Count} unsent message(s). Either ResetDataAsync "
				+ "does not clear this store — implement it, or override it with the data-only half of your "
				+ "cleanup if yours also disposes a client — or the store returns rows that were never staged.");
		}
	}

	/// <summary>
	/// Verifies that CreatedAt rises with staging order, so a drain can be ordered oldest-first.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The assertion is on the timestamp, not on the order the batch is returned in. Draining is a claim
	/// rather than a plain read — implementations issue an updating select, or select for update skipping
	/// locked rows — and none of those guarantee the returned row order matches the order used to choose
	/// the batch. Asserting iteration order would fail conformant stores for a property the contract does
	/// not promise.
	/// </para>
	/// <para>
	/// What the contract does promise is that CreatedAt is populated monotonically as messages are staged,
	/// which is what makes oldest-first delivery expressible at all. A store that stamps every row with
	/// the same timestamp, or with the read time rather than the write time, fails here.
	/// </para>
	/// </remarks>
	public virtual async Task GetUnsentMessagesAsync_CreatedAt_ShouldRiseWithStagingOrder()
	{
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);

		var staged = new List<OutboundMessage>();
		for (var i = 0; i < 3; i++)
		{
			var message = CreateTestMessage();
			staged.Add(message);
			await store.StageMessageAsync(message, CancellationToken.None).ConfigureAwait(false);

			// A real clock tick between writes: a store with coarse timestamp resolution would otherwise
			// stamp all three identically and the ordering would be untestable rather than wrong.
			await Task.Delay(25, CancellationToken.None).ConfigureAwait(false);
		}

		var drained = (await store.GetUnsentMessagesAsync(100, CancellationToken.None).ConfigureAwait(false))
			.ToList();

		// Consider only the messages this arm staged, so residue from another arm cannot change the answer.
		var stagedIds = staged.Select(static m => m.Id).ToHashSet(StringComparer.Ordinal);
		var ours = drained.Where(m => stagedIds.Contains(m.Id)).ToList();

		if (ours.Count != staged.Count)
		{
			throw new TestFixtureAssertionException(
				$"The drain returned {ours.Count} of the {staged.Count} messages this arm staged, so their "
				+ "relative order cannot be checked. A claim must hand out every claimable message.");
		}

		var byCreatedAt = ours.OrderBy(static m => m.CreatedAt).Select(static m => m.Id).ToList();
		for (var i = 0; i < byCreatedAt.Count; i++)
		{
			if (!string.Equals(byCreatedAt[i], staged[i].Id, StringComparison.Ordinal))
			{
				throw new TestFixtureAssertionException(
					"Ordering the drained messages by CreatedAt does not reproduce the order they were staged "
					+ $"in: position {i} holds '{byCreatedAt[i]}' where '{staged[i].Id}' was staged. CreatedAt "
					+ "must rise with staging order, or oldest-first delivery cannot be expressed.");
			}
		}
	}

	/// <summary>
	/// SAFETY and LIVENESS: two concurrent claimers must receive disjoint, non-empty sets.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Draining is a claim, not a read. Two pollers running against one store must never be handed the
	/// same message, or both send it — the duplicate the outbox exists to bound. Implementations realise
	/// this as an atomic lease: an updating select that outputs the rows it locked, a per-document
	/// find-and-modify, or a select-for-update that skips locked rows.
	/// </para>
	/// <para>
	/// The liveness half is what stops the safety half being satisfied by doing nothing. A store that
	/// returned the empty set to every caller would never hand the same message to two claimers, and would
	/// never deliver anything either. Disjointness alone cannot tell those two stores apart.
	/// </para>
	/// </remarks>
	public virtual async Task GetUnsentMessagesAsync_ConcurrentClaimers_ShouldReceiveDisjointSets()
	{
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);

		const int perBatch = 5;
		for (var i = 0; i < perBatch * 2; i++)
		{
			await store.StageMessageAsync(CreateTestMessage(), CancellationToken.None).ConfigureAwait(false);
		}

		var first = store.GetUnsentMessagesAsync(perBatch, CancellationToken.None).AsTask();
		var second = store.GetUnsentMessagesAsync(perBatch, CancellationToken.None).AsTask();
		var results = await Task.WhenAll(first, second).ConfigureAwait(false);

		var claimA = results[0].Select(static m => m.Id).ToList();
		var claimB = results[1].Select(static m => m.Id).ToList();

		var overlap = claimA.Intersect(claimB, StringComparer.Ordinal).ToList();
		if (overlap.Count > 0)
		{
			throw new TestFixtureAssertionException(
				$"Two concurrent claimers were handed {overlap.Count} of the same message(s), starting with "
				+ $"'{overlap[0]}'. The claim is not atomic, so two pollers can drain and send one message at "
				+ "once. A claim must lease the rows it returns.");
		}

		var union = claimA.Union(claimB, StringComparer.Ordinal).ToList();
		if (union.Count == 0)
		{
			throw new TestFixtureAssertionException(
				$"Neither claimer received any of the {perBatch * 2} staged, claimable messages. Returning "
				+ "nothing to everybody is 'safe' only by doing no work — the outbox would never drain.");
		}

		if (union.Count != claimA.Count + claimB.Count)
		{
			throw new TestFixtureAssertionException(
				$"The union of the two claims holds {union.Count} identifiers where the claims together "
				+ $"returned {claimA.Count + claimB.Count}, so a message was claimed twice under a duplicate "
				+ "the intersection check did not surface.");
		}
	}

	/// <summary>
	/// Verifies that only failed messages are returned by the failed-retrieval path.
	/// </summary>
	public virtual async Task GetAllTenantsFailedMessagesAsync_ShouldReturnOnlyFailedMessages()
	{
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);
		if (store.GetService(typeof(IOutboxStoreAdmin)) is not IOutboxStoreAdmin admin)
		{
			SkipArm(nameof(GetAllTenantsFailedMessagesAsync_ShouldReturnOnlyFailedMessages), typeof(IOutboxStoreAdmin), "Admin interface not supported");
			return;
		}

		RecordArmExecuted(nameof(GetAllTenantsFailedMessagesAsync_ShouldReturnOnlyFailedMessages));
		var stagedMessage = CreateTestMessage();
		var sentMessage = CreateTestMessage();
		var failedMessage = CreateTestMessage();

		await store.StageMessageAsync(stagedMessage, CancellationToken.None).ConfigureAwait(false);
		await store.StageMessageAsync(sentMessage, CancellationToken.None).ConfigureAwait(false);
		await store.StageMessageAsync(failedMessage, CancellationToken.None).ConfigureAwait(false);

		await store.MarkSentAsync(sentMessage.Id, CancellationToken.None).ConfigureAwait(false);
		await store.MarkFailedAsync(failedMessage.Id, "Error", 1, CancellationToken.None).ConfigureAwait(false);

		var failed = (await admin.GetAllTenantsFailedMessagesAsync(5, null, 100, CancellationToken.None)
			.ConfigureAwait(false)).ToList();

		// LIVENESS first: the failed message is actually returned. Without this the two exclusions below
		// are satisfied by a path that returns nothing at all.
		if (!failed.Exists(m => string.Equals(m.Id, failedMessage.Id, StringComparison.Ordinal)))
		{
			throw new TestFixtureAssertionException(
				$"The message marked failed ('{failedMessage.Id}') was not returned by GetAllTenantsFailedMessagesAsync.");
		}

		if (failed.Exists(m => string.Equals(m.Id, stagedMessage.Id, StringComparison.Ordinal)))
		{
			throw new TestFixtureAssertionException(
				"A staged message that has never failed was returned by the failed-retrieval path.");
		}

		if (failed.Exists(m => string.Equals(m.Id, sentMessage.Id, StringComparison.Ordinal)))
		{
			throw new TestFixtureAssertionException(
				"A message that was marked sent was returned by the failed-retrieval path.");
		}
	}

	/// <summary>
	/// Verifies that the failed-retrieval path honours its batch size without withholding available rows.
	/// </summary>
	public virtual async Task GetAllTenantsFailedMessagesAsync_ShouldRespectBatchSize()
	{
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);
		if (store.GetService(typeof(IOutboxStoreAdmin)) is not IOutboxStoreAdmin admin)
		{
			SkipArm(nameof(GetAllTenantsFailedMessagesAsync_ShouldRespectBatchSize), typeof(IOutboxStoreAdmin), "Admin interface not supported");
			return;
		}

		RecordArmExecuted(nameof(GetAllTenantsFailedMessagesAsync_ShouldRespectBatchSize));
		for (var i = 0; i < 5; i++)
		{
			var message = CreateTestMessage();
			await store.StageMessageAsync(message, CancellationToken.None).ConfigureAwait(false);
			await store.MarkFailedAsync(message.Id, "Error", 1, CancellationToken.None).ConfigureAwait(false);
		}

		var failed = (await admin.GetAllTenantsFailedMessagesAsync(10, null, 2, CancellationToken.None)
			.ConfigureAwait(false)).ToList();

		// Both bounds, because each alone is satisfied by a broken store: the upper bound alone passes for
		// a path that returns nothing, and the lower bound alone passes for one that ignores the limit.
		if (failed.Count != 2)
		{
			throw new TestFixtureAssertionException(
				$"Five messages were marked failed and a batch of 2 was requested, but {failed.Count} came "
				+ "back. A batch size caps the result; it does not withhold available rows.");
		}
	}

	/// <summary>
	/// Verifies that the scheduled-retrieval path honours its batch size without withholding due rows.
	/// </summary>
	public virtual async Task GetAllTenantsScheduledMessagesAsync_ShouldRespectBatchSize()
	{
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);
		if (store.GetService(typeof(IOutboxStoreAdmin)) is not IOutboxStoreAdmin admin)
		{
			SkipArm(nameof(GetAllTenantsScheduledMessagesAsync_ShouldRespectBatchSize), typeof(IOutboxStoreAdmin), "Admin interface not supported");
			return;
		}

		RecordArmExecuted(nameof(GetAllTenantsScheduledMessagesAsync_ShouldRespectBatchSize));
		for (var i = 0; i < 5; i++)
		{
			var message = CreateScheduledMessage(DateTimeOffset.UtcNow.AddMinutes(-10 + i));
			await store.StageMessageAsync(message, CancellationToken.None).ConfigureAwait(false);
		}

		var scheduled = (await admin.GetAllTenantsScheduledMessagesAsync(
			DateTimeOffset.UtcNow.AddHours(1),
			2,
			CancellationToken.None).ConfigureAwait(false)).ToList();

		if (scheduled.Count != 2)
		{
			throw new TestFixtureAssertionException(
				"Five due scheduled messages were staged and a batch of 2 was requested, but "
				+ $"{scheduled.Count} came back.");
		}
	}

	#endregion

	#region Round-Trip Fidelity

	/// <summary>
	/// Verifies that every field the caller set on a message survives the store and comes back unchanged.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The commonly-checked fields are the message type and destination, and a store that drops one of the
	/// others fails no other arm in this kit. The routing and ordering fields are the dangerous ones:
	/// losing the partition key silently removes the ordering guarantee a caller relies on, losing the
	/// target transports sends the message to the default destination instead of the ones requested, and
	/// losing the headers strips context a downstream handler reads. None of those surface as a delivery
	/// failure — the message is delivered, wrongly.
	/// </para>
	/// <para>
	/// Fields the store owns rather than the caller — status, sent time, retry count, sequence number — are
	/// deliberately excluded. They are the store's to assign, not the caller's to round-trip.
	/// </para>
	/// <para>
	/// The scheduled time is set in the past so the message is due, and therefore comes back from the
	/// drain rather than from the scheduled path.
	/// </para>
	/// </remarks>
	public virtual async Task StageMessageAsync_ShouldRoundTripEveryCallerSuppliedField()
	{
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);

		var scheduledAt = DateTimeOffset.UtcNow.AddMinutes(-5);
		var headers = new Dictionary<string, object>(StringComparer.Ordinal)
		{
			["x-custom-header"] = "header-value",
		};

		var message = new OutboundMessage(
			messageType: "Test.FullFieldRoundTrip",
			payload: CreatePayload("full-field-payload"),
			destination: "orders.topic",
			headers: headers)
		{
			Id = GenerateMessageId(),
			CorrelationId = "corr-777",
			CausationId = "cause-888",
			TenantId = "conformance-tenant-roundtrip",
			Priority = 9,
			ScheduledAt = scheduledAt,
			PartitionKey = "partition-A",
			GroupKey = "group-B",
			TargetTransports = "kafka,rabbitmq",
			IsMultiTransport = true,
		};

		await store.StageMessageAsync(message, CancellationToken.None).ConfigureAwait(false);

		var drained = await store.GetUnsentMessagesAsync(100, CancellationToken.None).ConfigureAwait(false);
		var reloaded = drained.FirstOrDefault(m => string.Equals(m.Id, message.Id, StringComparison.Ordinal));

		if (reloaded is null)
		{
			throw new TestFixtureAssertionException(
				$"The staged message '{message.Id}' could not be reloaded from the store, so no field could "
				+ "be checked. Its scheduled time is in the past, so it is due and must appear in the drain.");
		}

		var divergences = new List<string>();
		AddIfDifferent(divergences, nameof(OutboundMessage.MessageType), message.MessageType, reloaded.MessageType);
		AddIfDifferent(divergences, nameof(OutboundMessage.Destination), message.Destination, reloaded.Destination);
		AddIfDifferent(divergences, nameof(OutboundMessage.CorrelationId), message.CorrelationId, reloaded.CorrelationId);
		AddIfDifferent(divergences, nameof(OutboundMessage.CausationId), message.CausationId, reloaded.CausationId);
		AddIfDifferent(divergences, nameof(OutboundMessage.TenantId), message.TenantId, reloaded.TenantId);
		AddIfDifferent(divergences, nameof(OutboundMessage.PartitionKey), message.PartitionKey, reloaded.PartitionKey);
		AddIfDifferent(divergences, nameof(OutboundMessage.GroupKey), message.GroupKey, reloaded.GroupKey);
		AddIfDifferent(divergences, nameof(OutboundMessage.TargetTransports), message.TargetTransports, reloaded.TargetTransports);

		if (reloaded.Priority != message.Priority)
		{
			divergences.Add($"{nameof(OutboundMessage.Priority)}: staged {message.Priority}, reloaded {reloaded.Priority}");
		}

		if (reloaded.IsMultiTransport != message.IsMultiTransport)
		{
			divergences.Add(
				$"{nameof(OutboundMessage.IsMultiTransport)}: staged {message.IsMultiTransport}, "
				+ $"reloaded {reloaded.IsMultiTransport}");
		}

		if (!reloaded.Payload.AsSpan().SequenceEqual(message.Payload.AsSpan()))
		{
			divergences.Add($"{nameof(OutboundMessage.Payload)}: the reloaded bytes differ from the staged bytes");
		}

		if (reloaded.ScheduledAt is null)
		{
			divergences.Add($"{nameof(OutboundMessage.ScheduledAt)}: staged {scheduledAt:O}, reloaded null");
		}
		else if ((reloaded.ScheduledAt.Value - scheduledAt).Duration() > TimeSpan.FromSeconds(1))
		{
			divergences.Add(
				$"{nameof(OutboundMessage.ScheduledAt)}: staged {scheduledAt:O}, "
				+ $"reloaded {reloaded.ScheduledAt.Value:O}");
		}

		if (!reloaded.Headers.TryGetValue("x-custom-header", out var headerValue)
			|| !string.Equals(headerValue?.ToString(), "header-value", StringComparison.Ordinal))
		{
			divergences.Add(
				$"{nameof(OutboundMessage.Headers)}: 'x-custom-header' was staged as 'header-value' and came "
				+ $"back as '{headerValue?.ToString() ?? "<absent>"}'");
		}

		if (divergences.Count > 0)
		{
			throw new TestFixtureAssertionException(
				"The store did not round-trip every caller-supplied field. A dropped routing or context field "
				+ "does not fail delivery — the message is delivered to the wrong place, in the wrong order, "
				+ "or without the context a handler reads. Divergences: "
				+ string.Join("; ", divergences));
		}
	}

	private static void AddIfDifferent(List<string> divergences, string field, string? staged, string? reloaded)
	{
		if (!string.Equals(staged, reloaded, StringComparison.Ordinal))
		{
			divergences.Add($"{field}: staged '{staged ?? "<null>"}', reloaded '{reloaded ?? "<null>"}'");
		}
	}

	#endregion

	#region Cleanup Preservation

	/// <summary>
	/// Verifies that cleanup with a past threshold leaves a recently sent message in place.
	/// </summary>
	/// <remarks>
	/// The assertion names the specific message rather than counting removals, so residue from another arm
	/// cannot decide the outcome. A store that ignores the threshold deletes evidence an operator is still
	/// relying on.
	/// </remarks>
	public virtual async Task CleanupAllTenantsSentMessagesAsync_ShouldPreserveRecentlySentMessages()
	{
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);
		if (store.GetService(typeof(IOutboxStoreAdmin)) is not IOutboxStoreAdmin admin)
		{
			SkipArm(nameof(CleanupAllTenantsSentMessagesAsync_ShouldPreserveRecentlySentMessages), typeof(IOutboxStoreAdmin), "Admin interface not supported");
			return;
		}

		RecordArmExecuted(nameof(CleanupAllTenantsSentMessagesAsync_ShouldPreserveRecentlySentMessages));
		var message = CreateTestMessage();
		await store.StageMessageAsync(message, CancellationToken.None).ConfigureAwait(false);
		await store.MarkSentAsync(message.Id, CancellationToken.None).ConfigureAwait(false);

		// A threshold an hour in the past cannot match a message sent moments ago.
		_ = await admin.CleanupAllTenantsSentMessagesAsync(
			DateTimeOffset.UtcNow.AddHours(-1),
			100,
			CancellationToken.None).ConfigureAwait(false);

		// A store that deletes rows at mark-sent has nothing left to preserve, and that is conformant; the
		// property under test is that cleanup honours its threshold, which such a store never reaches.
		if (!SupportsSentTracking(store))
		{
			return;
		}

		var stillSent = await admin.CleanupAllTenantsSentMessagesAsync(
			DateTimeOffset.UtcNow.AddHours(1),
			100,
			CancellationToken.None).ConfigureAwait(false);

		if (stillSent < 1)
		{
			throw new TestFixtureAssertionException(
				"A message sent moments ago was already gone after a cleanup whose threshold was an hour in "
				+ "the past, so the threshold was not honoured. A later cleanup with a future threshold found "
				+ "nothing to remove, which means the first call deleted a row it should have preserved.");
		}
	}

	/// <summary>
	/// Verifies that cleanup of sent messages does not remove messages still awaiting delivery.
	/// </summary>
	/// <remarks>
	/// This is the arm that fails on a cleanup whose predicate omits the status filter. Such a store passes
	/// every other cleanup case — it removes old sent messages, and it honours the batch size — while
	/// deleting undelivered messages the caller committed. Nothing else in the contract notices.
	/// </remarks>
	public virtual async Task CleanupAllTenantsSentMessagesAsync_ShouldPreservePendingMessages()
	{
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);
		if (store.GetService(typeof(IOutboxStoreAdmin)) is not IOutboxStoreAdmin admin)
		{
			SkipArm(nameof(CleanupAllTenantsSentMessagesAsync_ShouldPreservePendingMessages), typeof(IOutboxStoreAdmin), "Admin interface not supported");
			return;
		}

		RecordArmExecuted(nameof(CleanupAllTenantsSentMessagesAsync_ShouldPreservePendingMessages));
		var pending = CreateTestMessage();
		await store.StageMessageAsync(pending, CancellationToken.None).ConfigureAwait(false);

		_ = await admin.CleanupAllTenantsSentMessagesAsync(
			DateTimeOffset.UtcNow.AddHours(1),
			100,
			CancellationToken.None).ConfigureAwait(false);

		var unsent = await store.GetUnsentMessagesAsync(100, CancellationToken.None).ConfigureAwait(false);
		if (!unsent.Any(m => string.Equals(m.Id, pending.Id, StringComparison.Ordinal)))
		{
			throw new TestFixtureAssertionException(
				$"Message '{pending.Id}' was staged and never sent, and a cleanup of SENT messages removed it. "
				+ "The cleanup predicate is not filtering on status, so it deletes undelivered messages the "
				+ "caller committed — silently, and without failing any other case in this kit.");
		}
	}

	/// <summary>
	/// Verifies that cleanup of sent messages does not remove failed messages awaiting retry.
	/// </summary>
	public virtual async Task CleanupAllTenantsSentMessagesAsync_ShouldPreserveFailedMessages()
	{
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);
		if (store.GetService(typeof(IOutboxStoreAdmin)) is not IOutboxStoreAdmin admin)
		{
			SkipArm(nameof(CleanupAllTenantsSentMessagesAsync_ShouldPreserveFailedMessages), typeof(IOutboxStoreAdmin), "Admin interface not supported");
			return;
		}

		RecordArmExecuted(nameof(CleanupAllTenantsSentMessagesAsync_ShouldPreserveFailedMessages));
		var message = CreateTestMessage();
		await store.StageMessageAsync(message, CancellationToken.None).ConfigureAwait(false);
		await store.MarkFailedAsync(message.Id, "Error", 1, CancellationToken.None).ConfigureAwait(false);

		_ = await admin.CleanupAllTenantsSentMessagesAsync(
			DateTimeOffset.UtcNow.AddHours(1),
			100,
			CancellationToken.None).ConfigureAwait(false);

		var failed = await admin.GetAllTenantsFailedMessagesAsync(100, null, 100, CancellationToken.None)
			.ConfigureAwait(false);

		if (!failed.Any(m => string.Equals(m.Id, message.Id, StringComparison.Ordinal)))
		{
			throw new TestFixtureAssertionException(
				$"Message '{message.Id}' failed and was awaiting retry, and a cleanup of SENT messages removed "
				+ "it. A failed message is not a delivered one; deleting it drops the delivery entirely.");
		}
	}

	#endregion

	#region Statistics

	/// <summary>
	/// Verifies that a store with nothing in it reports zero across every count.
	/// </summary>
	/// <remarks>
	/// Like its retrieval twin, this cannot be written relative to a baseline and so depends on
	/// <see cref="ResetDataAsync"/> clearing the store. The failure message distinguishes the two causes.
	/// </remarks>
	public virtual async Task GetAllTenantsStatisticsAsync_EmptyStore_ShouldReportZeroCounts()
	{
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);
		if (store.GetService(typeof(IOutboxStoreAdmin)) is not IOutboxStoreAdmin admin)
		{
			SkipArm(nameof(GetAllTenantsStatisticsAsync_EmptyStore_ShouldReportZeroCounts), typeof(IOutboxStoreAdmin), "Admin interface not supported");
			return;
		}

		RecordArmExecuted(nameof(GetAllTenantsStatisticsAsync_EmptyStore_ShouldReportZeroCounts));
		var stats = await admin.GetAllTenantsStatisticsAsync(CancellationToken.None).ConfigureAwait(false);
		if (stats is null)
		{
			throw new TestFixtureAssertionException("Expected statistics but got null.");
		}

		if (stats.TotalMessageCount != 0
			|| stats.StagedMessageCount != 0
			|| stats.SentMessageCount != 0
			|| stats.FailedMessageCount != 0)
		{
			throw new TestFixtureAssertionException(
				$"A cleared store reported total={stats.TotalMessageCount}, staged={stats.StagedMessageCount}, "
				+ $"sent={stats.SentMessageCount}, failed={stats.FailedMessageCount}. Either ResetDataAsync "
				+ "does not clear this store — implement it — or the statistics count rows that are not there.");
		}
	}

	/// <summary>
	/// Verifies that staging messages moves the staged count by exactly that many.
	/// </summary>
	/// <remarks>
	/// Asserted as a delta from a baseline read inside the arm rather than as an absolute count, so the
	/// assertion is exact without requiring the store to be empty.
	/// </remarks>
	public virtual async Task GetAllTenantsStatisticsAsync_ShouldTrackStagedMessages()
	{
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);
		if (store.GetService(typeof(IOutboxStoreAdmin)) is not IOutboxStoreAdmin admin)
		{
			SkipArm(nameof(GetAllTenantsStatisticsAsync_ShouldTrackStagedMessages), typeof(IOutboxStoreAdmin), "Admin interface not supported");
			return;
		}

		RecordArmExecuted(nameof(GetAllTenantsStatisticsAsync_ShouldTrackStagedMessages));
		var before = await admin.GetAllTenantsStatisticsAsync(CancellationToken.None).ConfigureAwait(false);
		var baseline = before?.StagedMessageCount ?? 0;

		const int staged = 3;
		for (var i = 0; i < staged; i++)
		{
			await store.StageMessageAsync(CreateTestMessage(), CancellationToken.None).ConfigureAwait(false);
		}

		var after = await admin.GetAllTenantsStatisticsAsync(CancellationToken.None).ConfigureAwait(false);
		if (after is null)
		{
			throw new TestFixtureAssertionException("Expected statistics but got null.");
		}

		var delta = after.StagedMessageCount - baseline;
		if (delta != staged)
		{
			throw new TestFixtureAssertionException(
				$"Staging {staged} messages moved the staged count by {delta} (from {baseline} to "
				+ $"{after.StagedMessageCount}).");
		}
	}

	/// <summary>
	/// Verifies that marking a message sent moves it out of the staged count.
	/// </summary>
	/// <remarks>
	/// Both retention policies are asserted rather than one being skipped. A store that retains sent rows
	/// must show the sent count rise; a store that deletes them at mark-sent must not. Whichever a store
	/// declares, the other outcome fails, so neither branch is vacuous.
	/// </remarks>
	public virtual async Task GetAllTenantsStatisticsAsync_ShouldTrackSentMessages()
	{
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);
		if (store.GetService(typeof(IOutboxStoreAdmin)) is not IOutboxStoreAdmin admin)
		{
			SkipArm(nameof(GetAllTenantsStatisticsAsync_ShouldTrackSentMessages), typeof(IOutboxStoreAdmin), "Admin interface not supported");
			return;
		}

		RecordArmExecuted(nameof(GetAllTenantsStatisticsAsync_ShouldTrackSentMessages));
		var before = await admin.GetAllTenantsStatisticsAsync(CancellationToken.None).ConfigureAwait(false);
		var stagedBaseline = before?.StagedMessageCount ?? 0;
		var sentBaseline = before?.SentMessageCount ?? 0;

		var message = CreateTestMessage();
		await store.StageMessageAsync(message, CancellationToken.None).ConfigureAwait(false);
		await store.MarkSentAsync(message.Id, CancellationToken.None).ConfigureAwait(false);

		var after = await admin.GetAllTenantsStatisticsAsync(CancellationToken.None).ConfigureAwait(false);
		if (after is null)
		{
			throw new TestFixtureAssertionException("Expected statistics but got null.");
		}

		var stagedDelta = after.StagedMessageCount - stagedBaseline;
		if (stagedDelta != 0)
		{
			throw new TestFixtureAssertionException(
				$"A message that was staged and then marked sent moved the staged count by {stagedDelta}. It "
				+ "must not still be counted as awaiting delivery.");
		}

		var sentDelta = after.SentMessageCount - sentBaseline;
		var expectedSentDelta = SupportsSentTracking(store) ? 1 : 0;
		if (sentDelta != expectedSentDelta)
		{
			throw new TestFixtureAssertionException(
				$"The sent count moved by {sentDelta} where {expectedSentDelta} was expected. This store "
				+ (SupportsSentTracking(store)
					? "retains messages after they are sent, so the sent count must rise."
					: "deletes messages at mark-sent, so the sent count must not rise."));
		}
	}

	/// <summary>
	/// Verifies that recording a failure moves the failed count.
	/// </summary>
	public virtual async Task GetAllTenantsStatisticsAsync_ShouldTrackFailedMessages()
	{
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);
		if (store.GetService(typeof(IOutboxStoreAdmin)) is not IOutboxStoreAdmin admin)
		{
			SkipArm(nameof(GetAllTenantsStatisticsAsync_ShouldTrackFailedMessages), typeof(IOutboxStoreAdmin), "Admin interface not supported");
			return;
		}

		RecordArmExecuted(nameof(GetAllTenantsStatisticsAsync_ShouldTrackFailedMessages));
		var before = await admin.GetAllTenantsStatisticsAsync(CancellationToken.None).ConfigureAwait(false);
		var baseline = before?.FailedMessageCount ?? 0;

		var message = CreateTestMessage();
		await store.StageMessageAsync(message, CancellationToken.None).ConfigureAwait(false);
		await store.MarkFailedAsync(message.Id, "Error", 1, CancellationToken.None).ConfigureAwait(false);

		var after = await admin.GetAllTenantsStatisticsAsync(CancellationToken.None).ConfigureAwait(false);
		if (after is null)
		{
			throw new TestFixtureAssertionException("Expected statistics but got null.");
		}

		var delta = after.FailedMessageCount - baseline;
		if (delta != 1)
		{
			throw new TestFixtureAssertionException(
				$"Recording one failure moved the failed count by {delta} (from {baseline} to "
				+ $"{after.FailedMessageCount}).");
		}
	}

	/// <summary>
	/// Verifies that the counts stay consistent with each other when all three states are present at once.
	/// </summary>
	/// <remarks>
	/// The per-state arms above each move one count in isolation, which a store can satisfy with three
	/// independent queries that disagree about the same row. Exercising all three states in one arm is
	/// what binds them together.
	/// </remarks>
	public virtual async Task GetAllTenantsStatisticsAsync_ShouldTrackAllStatesTogether()
	{
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);
		if (store.GetService(typeof(IOutboxStoreAdmin)) is not IOutboxStoreAdmin admin)
		{
			SkipArm(nameof(GetAllTenantsStatisticsAsync_ShouldTrackAllStatesTogether), typeof(IOutboxStoreAdmin), "Admin interface not supported");
			return;
		}

		RecordArmExecuted(nameof(GetAllTenantsStatisticsAsync_ShouldTrackAllStatesTogether));
		var before = await admin.GetAllTenantsStatisticsAsync(CancellationToken.None).ConfigureAwait(false);
		var totalBaseline = before?.TotalMessageCount ?? 0;
		var stagedBaseline = before?.StagedMessageCount ?? 0;
		var sentBaseline = before?.SentMessageCount ?? 0;
		var failedBaseline = before?.FailedMessageCount ?? 0;

		var staged = CreateTestMessage();
		var sent = CreateTestMessage();
		var failed = CreateTestMessage();

		await store.StageMessageAsync(staged, CancellationToken.None).ConfigureAwait(false);
		await store.StageMessageAsync(sent, CancellationToken.None).ConfigureAwait(false);
		await store.StageMessageAsync(failed, CancellationToken.None).ConfigureAwait(false);

		await store.MarkSentAsync(sent.Id, CancellationToken.None).ConfigureAwait(false);
		await store.MarkFailedAsync(failed.Id, "Error", 1, CancellationToken.None).ConfigureAwait(false);

		var after = await admin.GetAllTenantsStatisticsAsync(CancellationToken.None).ConfigureAwait(false);
		if (after is null)
		{
			throw new TestFixtureAssertionException("Expected statistics but got null.");
		}

		var retainsSent = SupportsSentTracking(store);
		var divergences = new List<string>();

		if (after.StagedMessageCount - stagedBaseline != 1)
		{
			divergences.Add($"staged moved by {after.StagedMessageCount - stagedBaseline}, expected 1");
		}

		if (after.FailedMessageCount - failedBaseline != 1)
		{
			divergences.Add($"failed moved by {after.FailedMessageCount - failedBaseline}, expected 1");
		}

		var expectedSent = retainsSent ? 1 : 0;
		if (after.SentMessageCount - sentBaseline != expectedSent)
		{
			divergences.Add($"sent moved by {after.SentMessageCount - sentBaseline}, expected {expectedSent}");
		}

		var expectedTotal = retainsSent ? 3 : 2;
		if (after.TotalMessageCount - totalBaseline != expectedTotal)
		{
			divergences.Add($"total moved by {after.TotalMessageCount - totalBaseline}, expected {expectedTotal}");
		}

		if (divergences.Count > 0)
		{
			throw new TestFixtureAssertionException(
				"With one message in each state the counts do not agree with each other. This store "
				+ (retainsSent ? "retains" : "deletes")
				+ " messages at mark-sent, so one staged, one failed and "
				+ (retainsSent ? "one sent were expected, totalling three" : "no sent were expected, totalling two")
				+ ". Divergences: " + string.Join("; ", divergences));
		}
	}

	#endregion

	#region Concurrency

	/// <summary>
	/// Verifies that many staged messages are all persisted when staged concurrently.
	/// </summary>
	public virtual async Task StageMessageAsync_ConcurrentDistinctMessages_ShouldAllSucceed()
	{
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);

		const int count = 20;
		var messages = Enumerable.Range(0, count).Select(_ => CreateTestMessage()).ToList();

		await Task.WhenAll(messages.Select(m =>
			store.StageMessageAsync(m, CancellationToken.None).AsTask())).ConfigureAwait(false);

		var drained = (await store.GetUnsentMessagesAsync(count * 2, CancellationToken.None)
			.ConfigureAwait(false)).Select(static m => m.Id).ToHashSet(StringComparer.Ordinal);

		var missing = messages.Where(m => !drained.Contains(m.Id)).Select(static m => m.Id).ToList();
		if (missing.Count > 0)
		{
			throw new TestFixtureAssertionException(
				$"{missing.Count} of {count} concurrently staged messages were not retrievable afterwards, "
				+ $"starting with '{missing[0]}'. Concurrent stages of distinct messages must not lose writes.");
		}
	}

	/// <summary>
	/// Verifies that exactly one of many concurrent attempts to mark a message sent succeeds.
	/// </summary>
	/// <remarks>
	/// Every attempt succeeding means the store applies no transition guard, so two dispatchers can both
	/// conclude they delivered the message. Every attempt failing means it can never be marked sent at all.
	/// The count is asserted exactly, so both failures are caught.
	/// </remarks>
	public virtual async Task MarkSentAsync_ConcurrentAttempts_ShouldSucceedExactlyOnce()
	{
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);
		var message = CreateTestMessage();
		await store.StageMessageAsync(message, CancellationToken.None).ConfigureAwait(false);

		const int attempts = 10;
		var tasks = new List<Task<bool>>(attempts);
		for (var i = 0; i < attempts; i++)
		{
			// Invoke MarkSentAsync directly and collect the tasks so they overlap (Task.Run is banned in
			// this shipped library, RS0030); each call starts before the WhenAll await, so async stores
			// genuinely race. A store that applies no transition guard returns success from every attempt
			// whether or not they truly interleave, so the exactly-one assertion below still catches it.
			tasks.Add(MarkSentOnceAsync(store, message.Id));
		}

		var outcomes = await Task.WhenAll(tasks).ConfigureAwait(false);

		static async Task<bool> MarkSentOnceAsync(IOutboxStore store, string messageId)
		{
			try
			{
				await store.MarkSentAsync(messageId, CancellationToken.None).ConfigureAwait(false);
				return true;
			}
			catch (InvalidOperationException)
			{
				return false;
			}
		}

		var succeeded = outcomes.Count(static ok => ok);
		if (succeeded != 1)
		{
			throw new TestFixtureAssertionException(
				$"{succeeded} of {attempts} concurrent attempts to mark message '{message.Id}' sent succeeded, "
				+ "where exactly one must. More than one means two dispatchers each concluded they delivered "
				+ "it; none means the message can never be marked sent.");
		}
	}

	/// <summary>
	/// Verifies that interleaved sent and failed transitions leave the counts consistent.
	/// </summary>
	public virtual async Task ConcurrentMixedOperations_ShouldLeaveStatisticsConsistent()
	{
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);
		if (store.GetService(typeof(IOutboxStoreAdmin)) is not IOutboxStoreAdmin admin)
		{
			SkipArm(nameof(ConcurrentMixedOperations_ShouldLeaveStatisticsConsistent), typeof(IOutboxStoreAdmin), "Admin interface not supported");
			return;
		}

		RecordArmExecuted(nameof(ConcurrentMixedOperations_ShouldLeaveStatisticsConsistent));
		var before = await admin.GetAllTenantsStatisticsAsync(CancellationToken.None).ConfigureAwait(false);
		var stagedBaseline = before?.StagedMessageCount ?? 0;
		var sentBaseline = before?.SentMessageCount ?? 0;
		var failedBaseline = before?.FailedMessageCount ?? 0;

		const int count = 10;
		var messages = Enumerable.Range(0, count).Select(_ => CreateTestMessage()).ToList();
		foreach (var message in messages)
		{
			await store.StageMessageAsync(message, CancellationToken.None).ConfigureAwait(false);
		}

		var transitions = messages.Select((m, i) => i % 2 == 0
			? store.MarkSentAsync(m.Id, CancellationToken.None).AsTask()
			: store.MarkFailedAsync(m.Id, "Error", 1, CancellationToken.None).AsTask());
		await Task.WhenAll(transitions).ConfigureAwait(false);

		var after = await admin.GetAllTenantsStatisticsAsync(CancellationToken.None).ConfigureAwait(false);
		if (after is null)
		{
			throw new TestFixtureAssertionException("Expected statistics but got null.");
		}

		var divergences = new List<string>();

		if (after.StagedMessageCount - stagedBaseline != 0)
		{
			divergences.Add(
				$"staged moved by {after.StagedMessageCount - stagedBaseline}, expected 0 — every message was "
				+ "transitioned out of the staged state");
		}

		if (after.FailedMessageCount - failedBaseline != count / 2)
		{
			divergences.Add($"failed moved by {after.FailedMessageCount - failedBaseline}, expected {count / 2}");
		}

		var expectedSent = SupportsSentTracking(store) ? count / 2 : 0;
		if (after.SentMessageCount - sentBaseline != expectedSent)
		{
			divergences.Add($"sent moved by {after.SentMessageCount - sentBaseline}, expected {expectedSent}");
		}

		if (divergences.Count > 0)
		{
			throw new TestFixtureAssertionException(
				$"After {count} messages were concurrently transitioned — half sent, half failed — the counts "
				+ "do not add up. Divergences: " + string.Join("; ", divergences));
		}
	}

	/// <summary>
	/// Verifies that many callers arriving at once do not fault the store.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The operation is a read of something that does not exist, so nothing is mutated and any exception at
	/// all is the finding. The callers are released together rather than started in sequence, so they
	/// genuinely overlap.
	/// </para>
	/// <para>
	/// SCOPE, stated because an arm that reads as broader than it is would be worse than none. This is a
	/// guard against gross concurrency faults — an operation that throws, deadlocks or corrupts shared
	/// state when entered many times at once. It is NOT a reliable detector of a narrow initialisation
	/// race: a store the suite hands back already initialised has passed through that window before this
	/// arm runs, and no number of concurrent callers can re-enter it.
	/// </para>
	/// </remarks>
	public virtual async Task Store_ShouldNotFaultWhenManyCallersArriveAtOnce()
	{
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);

		const int callers = 24;

		// RunContinuationsAsynchronously: without it the caller that completes the barrier runs the first
		// continuation inline on its own thread, which serialises the very overlap being exercised.
		var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var faults = new System.Collections.Concurrent.ConcurrentBag<Exception>();

		var racers = Enumerable.Range(0, callers).Select(async caller =>
		{
			await release.Task.ConfigureAwait(false);

			try
			{
				_ = await store.GetUnsentMessagesAsync(1, CancellationToken.None).ConfigureAwait(false);
			}
			catch (Exception ex)
			{
				faults.Add(ex);
			}
		}).ToArray();

		release.SetResult();
		await Task.WhenAll(racers).ConfigureAwait(false);

		if (!faults.IsEmpty)
		{
			var first = faults.First();
			throw new TestFixtureAssertionException(
				$"{faults.Count} of {callers} concurrent first callers faulted against the outbox store. The "
				+ $"first was {first.GetType().Name}: {first.Message}");
		}
	}

	#endregion

	#region Leadership Fencing

	/// <summary>
	/// SAFETY: a superseded leader presenting a stale token is refused, and its mutation is not applied.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Fencing exists because a paused leader does not know it has been superseded. It resumes and issues
	/// the mutation it was about to issue before it paused. The store is the only party that can tell it
	/// is stale, and it does so by comparing the presented token against the highest it has seen.
	/// </para>
	/// <para>
	/// Refusing is not enough on its own. An implementation that checks the fence and then issues the
	/// mutation as a separate round trip has a window in which the check passes and the mutation lands
	/// anyway, so this arm asserts the outcome — the message must still be there afterwards — rather than
	/// only that an exception was thrown. A throw that happened to occur does not prove the write did not.
	/// </para>
	/// <para>
	/// The set-based claim is deliberately held to a different standard from the mutation: presenting a
	/// stale token to it must yield no rows rather than throw, because a claim is a query for work and a
	/// superseded leader simply has none.
	/// </para>
	/// </remarks>
	public virtual async Task Fencing_StaleToken_ShouldBeRefusedWithoutApplyingTheMutation()
	{
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);
		if (store.GetService(typeof(IFencedOutboxStore)) is not IFencedOutboxStore fenced)
		{
			SkipArm(nameof(Fencing_StaleToken_ShouldBeRefusedWithoutApplyingTheMutation), typeof(IFencedOutboxStore), "This store does not participate in leadership fencing.");
			return;
		}

		RecordArmExecuted(nameof(Fencing_StaleToken_ShouldBeRefusedWithoutApplyingTheMutation));
		var current = NextFencingToken();
		var stale = current - 1000;

		var decoy = CreateTestMessage();
		var survivor = CreateTestMessage();
		await store.StageMessageAsync(decoy, CancellationToken.None).ConfigureAwait(false);
		await store.StageMessageAsync(survivor, CancellationToken.None).ConfigureAwait(false);

		// A valid tenure marks one message sent, which advances the high-water mark to its token.
		await fenced.MarkSentAsync(decoy.Id, current, CancellationToken.None).ConfigureAwait(false);

		StaleOutboxFencingTokenException? refusal = null;
		try
		{
			await fenced.MarkSentAsync(survivor.Id, stale, CancellationToken.None).ConfigureAwait(false);
		}
		catch (StaleOutboxFencingTokenException ex)
		{
			refusal = ex;
		}

		if (refusal is null)
		{
			throw new TestFixtureAssertionException(
				$"A mark-sent presenting token {stale} was accepted after the high-water mark had advanced to "
				+ $"{current}. A superseded leader can still mutate, so two leaders act on the same message.");
		}

		if (refusal.PresentedToken != stale)
		{
			throw new TestFixtureAssertionException(
				$"The refusal reported PresentedToken {refusal.PresentedToken?.ToString() ?? "<null>"} where "
				+ $"{stale} was presented.");
		}

		var staleClaim = await fenced.GetUnsentMessagesAsync(10, stale, CancellationToken.None)
			.ConfigureAwait(false);
		if (staleClaim.Any())
		{
			throw new TestFixtureAssertionException(
				"A stale fencing token claimed rows. A superseded leader must be handed no work.");
		}

		// The mutation must not have been applied. This is the half a throw alone does not establish.
		var survivors = await store.GetUnsentMessagesAsync(100, CancellationToken.None).ConfigureAwait(false);
		if (!survivors.Any(m => string.Equals(m.Id, survivor.Id, StringComparison.Ordinal)))
		{
			throw new TestFixtureAssertionException(
				$"The refused mark-sent still removed message '{survivor.Id}'. The fence was checked and the "
				+ "mutation applied anyway, so the refusal is decorative and the message is lost.");
		}
	}

	/// <summary>
	/// Verifies that a fencing refusal reports the high-water mark it was refused against.
	/// </summary>
	/// <remarks>
	/// Isolated from the safety arm above on purpose. This field is a diagnostic — an operator reading it
	/// learns which tenure won — and a gap in it must never cause the safety assertions to be skipped
	/// alongside it.
	/// </remarks>
	public virtual async Task Fencing_Refusal_ShouldReportTheHighWaterMark()
	{
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);
		if (store.GetService(typeof(IFencedOutboxStore)) is not IFencedOutboxStore fenced)
		{
			SkipArm(nameof(Fencing_Refusal_ShouldReportTheHighWaterMark), typeof(IFencedOutboxStore), "This store does not participate in leadership fencing.");
			return;
		}

		RecordArmExecuted(nameof(Fencing_Refusal_ShouldReportTheHighWaterMark));
		var current = NextFencingToken();
		var stale = current - 1000;

		var decoy = CreateTestMessage();
		var target = CreateTestMessage();
		await store.StageMessageAsync(decoy, CancellationToken.None).ConfigureAwait(false);
		await store.StageMessageAsync(target, CancellationToken.None).ConfigureAwait(false);

		await fenced.MarkSentAsync(decoy.Id, current, CancellationToken.None).ConfigureAwait(false);

		StaleOutboxFencingTokenException? refusal = null;
		try
		{
			await fenced.MarkSentAsync(target.Id, stale, CancellationToken.None).ConfigureAwait(false);
		}
		catch (StaleOutboxFencingTokenException ex)
		{
			refusal = ex;
		}

		if (refusal is null)
		{
			throw new TestFixtureAssertionException(
				$"A mark-sent presenting the stale token {stale} was accepted after the high-water mark had "
				+ $"advanced to {current}.");
		}

		if (refusal.HighWaterToken != current)
		{
			throw new TestFixtureAssertionException(
				$"The refusal reported HighWaterToken {refusal.HighWaterToken?.ToString() ?? "<null>"} where "
				+ $"{current} was the mark it was refused against. An operator reading this field cannot tell "
				+ "which tenure won.");
		}
	}

	/// <summary>
	/// LIVENESS: a token at or above the high-water mark claims work and completes it.
	/// </summary>
	/// <remarks>
	/// A store that refused every token would satisfy every safety arm above while never delivering
	/// anything. This is the arm it fails. An equal token is the same tenure and must be honoured, not
	/// refused — the comparison is strictly-less-than.
	/// </remarks>
	public virtual async Task Fencing_CurrentLeaderToken_ShouldClaimAndComplete()
	{
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);
		if (store.GetService(typeof(IFencedOutboxStore)) is not IFencedOutboxStore fenced)
		{
			SkipArm(nameof(Fencing_CurrentLeaderToken_ShouldClaimAndComplete), typeof(IFencedOutboxStore), "This store does not participate in leadership fencing.");
			return;
		}

		RecordArmExecuted(nameof(Fencing_CurrentLeaderToken_ShouldClaimAndComplete));
		var current = NextFencingToken();

		var message = CreateTestMessage();
		await store.StageMessageAsync(message, CancellationToken.None).ConfigureAwait(false);

		var claimed = await fenced.GetUnsentMessagesAsync(50, current, CancellationToken.None)
			.ConfigureAwait(false);
		if (!claimed.Any(m => string.Equals(m.Id, message.Id, StringComparison.Ordinal)))
		{
			throw new TestFixtureAssertionException(
				$"A valid fencing token ({current}) could not claim the staged message '{message.Id}'. A store "
				+ "that refuses every token is 'safe' by never delivering anything.");
		}

		// The same token again: an equal token is the same tenure and must be honoured.
		try
		{
			await fenced.MarkSentAsync(message.Id, current, CancellationToken.None).ConfigureAwait(false);
		}
		catch (StaleOutboxFencingTokenException ex)
		{
			throw new TestFixtureAssertionException(
				$"A mark-sent presenting the same token that claimed the message ({current}) was refused "
				+ $"against a high-water of {ex.HighWaterToken?.ToString() ?? "<null>"}. An equal token is the "
				+ "same tenure; the comparison must be strictly-less-than or a leader fences itself off.");
		}

		var remaining = await store.GetUnsentMessagesAsync(100, CancellationToken.None).ConfigureAwait(false);
		if (remaining.Any(m => string.Equals(m.Id, message.Id, StringComparison.Ordinal)))
		{
			throw new TestFixtureAssertionException(
				$"Message '{message.Id}' was marked sent under a valid token and is still claimable.");
		}
	}

	/// <summary>
	/// SAFETY: the high-water mark survives cleanup of the messages that carried it.
	/// </summary>
	/// <remarks>
	/// A store that derives the high-water mark from a column on the sent rows forgets it the moment a
	/// routine cleanup deletes them. The mark collapses to nothing, a paused leader's stale token then
	/// compares favourably against it, and the fence silently stops fencing. Nothing announces this: the
	/// cleanup succeeds, and the next stale mutation is accepted. The mark has to be durable independently
	/// of the messages.
	/// </remarks>
	public virtual async Task Fencing_HighWaterMark_ShouldSurviveCleanup()
	{
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);
		if (store.GetService(typeof(IFencedOutboxStore)) is not IFencedOutboxStore fenced)
		{
			SkipArm(nameof(Fencing_HighWaterMark_ShouldSurviveCleanup), typeof(IFencedOutboxStore), "This store does not participate in leadership fencing.");
			return;
		}

		if (store.GetService(typeof(IOutboxStoreAdmin)) is not IOutboxStoreAdmin admin)
		{
			SkipArm(nameof(Fencing_HighWaterMark_ShouldSurviveCleanup), typeof(IOutboxStoreAdmin), "Admin interface not supported");
			return;
		}

		RecordArmExecuted(nameof(Fencing_HighWaterMark_ShouldSurviveCleanup));
		var current = NextFencingToken();
		var stale = current - 1000;

		var carrier = CreateTestMessage();
		await store.StageMessageAsync(carrier, CancellationToken.None).ConfigureAwait(false);
		await fenced.MarkSentAsync(carrier.Id, current, CancellationToken.None).ConfigureAwait(false);

		// Routine cleanup removes the sent rows that carried the token.
		_ = await admin.CleanupAllTenantsSentMessagesAsync(
			DateTimeOffset.UtcNow.AddHours(1),
			100,
			CancellationToken.None).ConfigureAwait(false);

		var target = CreateTestMessage();
		await store.StageMessageAsync(target, CancellationToken.None).ConfigureAwait(false);

		StaleOutboxFencingTokenException? refusal = null;
		try
		{
			await fenced.MarkSentAsync(target.Id, stale, CancellationToken.None).ConfigureAwait(false);
		}
		catch (StaleOutboxFencingTokenException ex)
		{
			refusal = ex;
		}

		if (refusal is null)
		{
			throw new TestFixtureAssertionException(
				$"After cleanup removed the rows carrying token {current}, a mark-sent presenting the stale "
				+ $"token {stale} was accepted. The high-water mark was derived from the message rows and was "
				+ "deleted with them, so a routine cleanup silently disabled the fence.");
		}

		// LIVENESS: the fence is durable, not stuck — the current leader still works after cleanup.
		try
		{
			await fenced.MarkSentAsync(target.Id, current, CancellationToken.None).ConfigureAwait(false);
		}
		catch (StaleOutboxFencingTokenException ex)
		{
			throw new TestFixtureAssertionException(
				$"After cleanup the current leader's token ({current}) was itself refused against a high-water "
				+ $"of {ex.HighWaterToken?.ToString() ?? "<null>"}. The fence is not merely durable, it is "
				+ "stuck, and delivery stops for everyone.");
		}
	}

	/// <summary>
	/// SAFETY and LIVENESS: after a handover the superseded leader can neither mutate nor lose the message.
	/// </summary>
	/// <remarks>
	/// The message is deliberately left staged and unclaimed while another message carries the handover, so
	/// what is asserted afterwards is that the row still exists rather than that some claim state was
	/// preserved. The liveness half — the new leader can still complete it — is what separates a store that
	/// protected the message from one that stranded it.
	/// </remarks>
	public virtual async Task Fencing_SupersededLeader_ShouldNeitherMutateNorLoseTheMessage()
	{
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);
		if (store.GetService(typeof(IFencedOutboxStore)) is not IFencedOutboxStore fenced)
		{
			SkipArm(nameof(Fencing_SupersededLeader_ShouldNeitherMutateNorLoseTheMessage), typeof(IFencedOutboxStore), "This store does not participate in leadership fencing.");
			return;
		}

		RecordArmExecuted(nameof(Fencing_SupersededLeader_ShouldNeitherMutateNorLoseTheMessage));
		var superseded = NextFencingToken();
		var fresher = superseded + 1000;

		var message = CreateTestMessage();
		var handoverCarrier = CreateTestMessage();
		await store.StageMessageAsync(message, CancellationToken.None).ConfigureAwait(false);
		await store.StageMessageAsync(handoverCarrier, CancellationToken.None).ConfigureAwait(false);

		// The fresher leader takes over by completing an unrelated message, which advances the mark without
		// touching or claiming the one under test.
		await fenced.MarkSentAsync(handoverCarrier.Id, fresher, CancellationToken.None).ConfigureAwait(false);

		var refused = false;
		try
		{
			await fenced.MarkSentAsync(message.Id, superseded, CancellationToken.None).ConfigureAwait(false);
		}
		catch (StaleOutboxFencingTokenException)
		{
			refused = true;
		}

		if (!refused)
		{
			throw new TestFixtureAssertionException(
				$"The superseded leader (token {superseded}) completed a message after the mark had advanced "
				+ $"to {fresher}. Both leaders acted on it.");
		}

		var staleClaim = await fenced.GetUnsentMessagesAsync(50, superseded, CancellationToken.None)
			.ConfigureAwait(false);
		if (staleClaim.Any())
		{
			throw new TestFixtureAssertionException(
				$"The superseded leader (token {superseded}) was handed work after the mark advanced to "
				+ $"{fresher}.");
		}

		var stillThere = await store.GetUnsentMessagesAsync(100, CancellationToken.None).ConfigureAwait(false);
		if (!stillThere.Any(m => string.Equals(m.Id, message.Id, StringComparison.Ordinal)))
		{
			throw new TestFixtureAssertionException(
				$"Message '{message.Id}' disappeared when the superseded leader's mutation was refused. "
				+ "Refusing a stale mutation must not delete the message it refused to complete.");
		}

		try
		{
			await fenced.MarkSentAsync(message.Id, fresher, CancellationToken.None).ConfigureAwait(false);
		}
		catch (StaleOutboxFencingTokenException ex)
		{
			throw new TestFixtureAssertionException(
				$"The current leader (token {fresher}) could not complete message '{message.Id}' after the "
				+ $"handover; it was refused against a high-water of "
				+ $"{ex.HighWaterToken?.ToString() ?? "<null>"}. The message is stranded rather than protected.");
		}
	}

	#endregion

	#region Failure-Anchored Re-claim Floor

	/// <summary>
	/// SAFETY: a message that has just failed is not immediately claimable again.
	/// </summary>
	/// <remarks>
	/// Without a floor a persistently failing destination is retried as fast as the poller can loop, which
	/// saturates the transport and the store both. The floor is anchored at the recorded failure.
	/// </remarks>
	public virtual async Task MarkFailed_WithinTheFloor_ShouldNotBeReclaimable_ReservedPath()
	{
		var store = await CreateStoreWithReclaimFloorAsync(60).ConfigureAwait(false);
		if (store is null)
		{
			SkipArm(nameof(MarkFailed_WithinTheFloor_ShouldNotBeReclaimable_ReservedPath), null, "This store does not implement a failure-anchored re-claim floor.");
			return;
		}

		RecordArmExecuted(nameof(MarkFailed_WithinTheFloor_ShouldNotBeReclaimable_ReservedPath));
		try
		{
			var message = CreateTestMessage();
			await store.StageMessageAsync(message, CancellationToken.None).ConfigureAwait(false);

			// Claiming first both establishes the reserved path and proves the message was claimable to
			// begin with, so the assertion below cannot pass because it was never claimable at all.
			var claimed = await store.GetUnsentMessagesAsync(10, CancellationToken.None).ConfigureAwait(false);
			if (!claimed.Any(m => string.Equals(m.Id, message.Id, StringComparison.Ordinal)))
			{
				throw new TestFixtureAssertionException(
					$"A freshly staged message ('{message.Id}') was not claimable, so the floor cannot be "
					+ "tested against it.");
			}

			await store.MarkFailedAsync(message.Id, "boom", 1, CancellationToken.None).ConfigureAwait(false);

			var reclaimed = await store.GetUnsentMessagesAsync(10, CancellationToken.None).ConfigureAwait(false);
			if (reclaimed.Any(m => string.Equals(m.Id, message.Id, StringComparison.Ordinal)))
			{
				throw new TestFixtureAssertionException(
					$"Message '{message.Id}' failed moments ago under a 60 second floor and was immediately "
					+ "claimable again. That is a zero-backoff retry loop against a failing destination.");
			}
		}
		finally
		{
			await DisposeStoreAsync(store).ConfigureAwait(false);
		}
	}

	/// <summary>
	/// SAFETY: the floor also applies to a message that failed without ever having been claimed.
	/// </summary>
	/// <remarks>
	/// This is the path a floor implemented as "wait for the claim lease to expire" misses entirely. A
	/// message that was never claimed has no lease, so such a floor yields nothing and the message is
	/// immediately retryable. The floor has to be anchored at the failure, not at the lease.
	/// </remarks>
	public virtual async Task MarkFailed_WithinTheFloor_ShouldNotBeReclaimable_UnclaimedPath()
	{
		var store = await CreateStoreWithReclaimFloorAsync(60).ConfigureAwait(false);
		if (store is null)
		{
			SkipArm(nameof(MarkFailed_WithinTheFloor_ShouldNotBeReclaimable_UnclaimedPath), null, "This store does not implement a failure-anchored re-claim floor.");
			return;
		}

		RecordArmExecuted(nameof(MarkFailed_WithinTheFloor_ShouldNotBeReclaimable_UnclaimedPath));
		try
		{
			var message = CreateTestMessage();
			await store.StageMessageAsync(message, CancellationToken.None).ConfigureAwait(false);

			// Deliberately no claim: the message is failed straight from the staged state.
			await store.MarkFailedAsync(message.Id, "boom", 1, CancellationToken.None).ConfigureAwait(false);

			var reclaimed = await store.GetUnsentMessagesAsync(10, CancellationToken.None).ConfigureAwait(false);
			if (reclaimed.Any(m => string.Equals(m.Id, message.Id, StringComparison.Ordinal)))
			{
				throw new TestFixtureAssertionException(
					$"Message '{message.Id}' failed without ever being claimed and was immediately claimable "
					+ "again under a 60 second floor. A floor derived from claim-lease expiry yields nothing "
					+ "here, because no lease was ever taken.");
			}
		}
		finally
		{
			await DisposeStoreAsync(store).ConfigureAwait(false);
		}
	}

	/// <summary>
	/// LIVENESS: once the floor elapses the failed message becomes claimable again.
	/// </summary>
	/// <remarks>
	/// The safety arms above are all satisfied by a store that drops a failed message permanently. This is
	/// the arm that fails against one. The floor is short and polled rather than slept through, so the
	/// timing is bounded rather than assumed.
	/// </remarks>
	public virtual async Task MarkFailed_AfterTheFloorElapses_ShouldBecomeReclaimable()
	{
		var store = await CreateStoreWithReclaimFloorAsync(1).ConfigureAwait(false);
		if (store is null)
		{
			SkipArm(nameof(MarkFailed_AfterTheFloorElapses_ShouldBecomeReclaimable), null, "This store does not implement a failure-anchored re-claim floor.");
			return;
		}

		RecordArmExecuted(nameof(MarkFailed_AfterTheFloorElapses_ShouldBecomeReclaimable));
		try
		{
			var message = CreateTestMessage();
			await store.StageMessageAsync(message, CancellationToken.None).ConfigureAwait(false);
			await store.MarkFailedAsync(message.Id, "boom", 1, CancellationToken.None).ConfigureAwait(false);

			if (!await PollForReclaimAsync(store, message.Id).ConfigureAwait(false))
			{
				throw new TestFixtureAssertionException(
					$"Message '{message.Id}' failed under a 1 second floor and was still not claimable after "
					+ "15 seconds. A failed message below the retry ceiling must become deliverable again; "
					+ "otherwise it is silently dropped.");
			}
		}
		finally
		{
			await DisposeStoreAsync(store).ConfigureAwait(false);
		}
	}

	/// <summary>
	/// LIVENESS: a failure reported by the claim's own owner is recorded and releases the claim.
	/// </summary>
	/// <remarks>
	/// <para>
	/// This is the schedule a silent no-op hides behind. A store whose failure path matches on ownership
	/// and then does nothing satisfies the reserved-path safety arm perfectly — the message is not
	/// claimable, because it is still held under its original claim, not because a floor was applied.
	/// </para>
	/// <para>
	/// Asserting both halves separates the two. The failure must be recorded, which a no-op does not do,
	/// and the message must return to the claimable set once the short floor elapses, which a message
	/// still held under its original long-lived claim does not do either.
	/// </para>
	/// </remarks>
	public virtual async Task MarkFailed_ByTheClaimOwner_ShouldRecordAndRelease()
	{
		var store = await CreateStoreWithReclaimFloorAsync(1).ConfigureAwait(false);
		if (store is null)
		{
			SkipArm(nameof(MarkFailed_ByTheClaimOwner_ShouldRecordAndRelease), null, "This store does not implement a failure-anchored re-claim floor.");
			return;
		}

		RecordArmExecuted(nameof(MarkFailed_ByTheClaimOwner_ShouldRecordAndRelease));
		try
		{
			var message = CreateTestMessage();
			await store.StageMessageAsync(message, CancellationToken.None).ConfigureAwait(false);

			var claimed = await store.GetUnsentMessagesAsync(10, CancellationToken.None).ConfigureAwait(false);
			if (!claimed.Any(m => string.Equals(m.Id, message.Id, StringComparison.Ordinal)))
			{
				throw new TestFixtureAssertionException(
					$"A freshly staged message ('{message.Id}') was not claimable, so the owned path cannot be "
					+ "set up.");
			}

			await store.MarkFailedAsync(message.Id, "owner-boom", 2, CancellationToken.None).ConfigureAwait(false);

			if (store.GetService(typeof(IOutboxStoreAdmin)) is IOutboxStoreAdmin admin)
			{
				var failed = await admin.GetAllTenantsFailedMessagesAsync(100, null, 100, CancellationToken.None)
					.ConfigureAwait(false);
				var recorded = failed.FirstOrDefault(m =>
					string.Equals(m.Id, message.Id, StringComparison.Ordinal));

				if (recorded is null)
				{
					throw new TestFixtureAssertionException(
						$"The claim owner reported message '{message.Id}' as failed and nothing was recorded. A "
						+ "failure path that matches on ownership and then does nothing looks identical to a "
						+ "floored message from the outside.");
				}

				if (recorded.RetryCount != 2)
				{
					throw new TestFixtureAssertionException(
						$"The recorded attempt count is {recorded.RetryCount} where 2 was reported.");
				}
			}

			if (!await PollForReclaimAsync(store, message.Id).ConfigureAwait(false))
			{
				throw new TestFixtureAssertionException(
					$"Message '{message.Id}' was failed by its claim owner under a 1 second floor and never "
					+ "became claimable again within 15 seconds. Recording the failure and releasing the claim "
					+ "have to be the same write; a store that records without releasing leaves the message "
					+ "held under its original claim until that expires.");
			}
		}
		finally
		{
			await DisposeStoreAsync(store).ConfigureAwait(false);
		}
	}

	/// <summary>
	/// SAFETY: a late failure report carrying a lower attempt count must not lower the recorded count.
	/// </summary>
	/// <remarks>
	/// The retry ceiling that eventually stops a message being redelivered is driven by the recorded
	/// count. A report that lowers it moves the ceiling further away every time it arrives, so a message
	/// that should have been given up on is retried indefinitely.
	/// </remarks>
	public virtual async Task MarkFailed_StaleLateReport_ShouldNotLowerTheAttemptCount()
	{
		var store = await CreateStoreWithReclaimFloorAsync(60).ConfigureAwait(false);
		if (store is null)
		{
			SkipArm(nameof(MarkFailed_StaleLateReport_ShouldNotLowerTheAttemptCount), null, "This store does not implement a failure-anchored re-claim floor.");
			return;
		}

		try
		{
			if (store.GetService(typeof(IOutboxStoreAdmin)) is not IOutboxStoreAdmin admin)
			{
				SkipArm(nameof(MarkFailed_StaleLateReport_ShouldNotLowerTheAttemptCount), typeof(IOutboxStoreAdmin), "Admin interface not supported");
				return;
			}

			RecordArmExecuted(nameof(MarkFailed_StaleLateReport_ShouldNotLowerTheAttemptCount));
			var message = CreateTestMessage();
			await store.StageMessageAsync(message, CancellationToken.None).ConfigureAwait(false);

			await store.MarkFailedAsync(message.Id, "attempt-3", 3, CancellationToken.None).ConfigureAwait(false);
			await store.MarkFailedAsync(message.Id, "stale-1", 1, CancellationToken.None).ConfigureAwait(false);

			var failed = await admin.GetAllTenantsFailedMessagesAsync(100, null, 100, CancellationToken.None)
				.ConfigureAwait(false);
			var reloaded = failed.FirstOrDefault(m => string.Equals(m.Id, message.Id, StringComparison.Ordinal));

			if (reloaded is null)
			{
				throw new TestFixtureAssertionException(
					$"Message '{message.Id}' was reported failed twice and is not retrievable as failed.");
			}

			if (reloaded.RetryCount != 3)
			{
				throw new TestFixtureAssertionException(
					$"A late report of attempt 1, arriving after attempt 3 was recorded, left the count at "
					+ $"{reloaded.RetryCount}. The recorded count must never decrease: the retry ceiling is "
					+ "driven by it, so lowering it postpones giving up indefinitely.");
			}
		}
		finally
		{
			await DisposeStoreAsync(store).ConfigureAwait(false);
		}
	}

	/// <summary>
	/// SAFETY: a dead-lettered message is terminal and is never handed back by either retrieval path.
	/// </summary>
	/// <remarks>
	/// Dead-lettering exists to stop a message that cannot succeed from being retried forever. A store
	/// that leaves it visible to the claim recreates the loop it was dead-lettered to end.
	/// </remarks>
	public virtual async Task DeadLettered_ShouldBeTerminalOnBothRetrievalPaths()
	{
		var store = await CreateStoreWithReclaimFloorAsync(60).ConfigureAwait(false);
		if (store is null)
		{
			SkipArm(nameof(DeadLettered_ShouldBeTerminalOnBothRetrievalPaths), null, "This store does not implement a failure-anchored re-claim floor.");
			return;
		}

		try
		{
			if (store.GetService(typeof(IDeadLetterableOutboxStore)) is not IDeadLetterableOutboxStore deadLetterable)
			{
				SkipArm(nameof(DeadLettered_ShouldBeTerminalOnBothRetrievalPaths), typeof(IDeadLetterableOutboxStore), "This store does not implement terminal dead-lettering.");
				return;
			}

			RecordArmExecuted(nameof(DeadLettered_ShouldBeTerminalOnBothRetrievalPaths));
			var message = CreateTestMessage();
			await store.StageMessageAsync(message, CancellationToken.None).ConfigureAwait(false);

			// The message is claimable before it is dead-lettered, so the assertions below cannot pass
			// because it was never visible in the first place.
			var claimable = await store.GetUnsentMessagesAsync(10, CancellationToken.None).ConfigureAwait(false);
			if (!claimable.Any(m => string.Equals(m.Id, message.Id, StringComparison.Ordinal)))
			{
				throw new TestFixtureAssertionException(
					$"A freshly staged message ('{message.Id}') was not claimable, so terminality cannot be "
					+ "distinguished from never having been visible.");
			}

			await deadLetterable.MarkDeadLetteredAsync(message.Id, "retries exhausted", CancellationToken.None)
				.ConfigureAwait(false);

			var afterClaim = await store.GetUnsentMessagesAsync(10, CancellationToken.None).ConfigureAwait(false);
			if (afterClaim.Any(m => string.Equals(m.Id, message.Id, StringComparison.Ordinal)))
			{
				throw new TestFixtureAssertionException(
					$"Dead-lettered message '{message.Id}' was handed back by the delivery claim. It will be "
					+ "delivered, fail, and be dead-lettered again without end.");
			}

			if (store.GetService(typeof(IOutboxStoreAdmin)) is IOutboxStoreAdmin admin)
			{
				var failed = await admin.GetAllTenantsFailedMessagesAsync(100, null, 100, CancellationToken.None)
					.ConfigureAwait(false);
				if (failed.Any(m => string.Equals(m.Id, message.Id, StringComparison.Ordinal)))
				{
					throw new TestFixtureAssertionException(
						$"Dead-lettered message '{message.Id}' is still returned as a retryable failure. It is "
						+ "terminal, not pending.");
				}
			}
		}
		finally
		{
			await DisposeStoreAsync(store).ConfigureAwait(false);
		}
	}

	/// <summary>
	/// SAFETY: a failure reported by a dispatcher that does not own the claim must not release it.
	/// </summary>
	/// <remarks>
	/// <para>
	/// If any dispatcher can release any claim, a second one can release the claim a first is still working
	/// under, and both then deliver the message. The liveness half — the owner's own report is still
	/// accepted — is what stops the guard being satisfied by refusing everybody, which would leave a store
	/// unable to record its own failures at all.
	/// </para>
	/// <para>
	/// The safety half is asserted by observing whether the foreign report was <em>recorded</em>, and only
	/// then by attempting a re-claim. Attempting a re-claim alone cannot see the violation on the canonical
	/// implementation shape: a store that releases the claim and stamps the failure-anchored floor in the
	/// same write leaves the message unclaimable for the floor's duration, so a released claim and a
	/// retained one look identical for as long as the arm can watch. That blind spot is not hypothetical —
	/// it was found by a mutant that deleted the ownership predicate and did not go red. Whether the report
	/// landed is independent of the floor: a store that refuses a non-owner writes nothing at all, so the
	/// message never reaches the failed listing, while a store missing the guard records the foreign
	/// dispatcher's failure against a message it does not own.
	/// </para>
	/// </remarks>
	public virtual async Task MarkFailed_ByANonOwner_ShouldNotReleaseTheClaim()
	{
		var store = await CreateStoreWithReclaimFloorAsync(120).ConfigureAwait(false);
		if (store is null)
		{
			SkipArm(nameof(MarkFailed_ByANonOwner_ShouldNotReleaseTheClaim), null, "This store does not implement a failure-anchored re-claim floor.");
			return;
		}

		try
		{
			var message = CreateTestMessage();
			await store.StageMessageAsync(message, CancellationToken.None).ConfigureAwait(false);

			var reservedByOther = await TryReserveMessageUnderForeignDispatcherAsync(store, message.Id)
				.ConfigureAwait(false);
			if (!reservedByOther)
			{
				SkipArm(nameof(MarkFailed_ByANonOwner_ShouldNotReleaseTheClaim), null, "This store exposes no way to give a message a foreign owner.");
				return;
			}

			RecordArmExecuted(nameof(MarkFailed_ByANonOwner_ShouldNotReleaseTheClaim));
			// With a 120 second floor the foreign claim cannot lapse during the arm, so the only way the
			// message can reappear below is if this store released a claim it does not own.
			var beforeFail = await store.GetUnsentMessagesAsync(10, CancellationToken.None).ConfigureAwait(false);
			if (beforeFail.Any(m => string.Equals(m.Id, message.Id, StringComparison.Ordinal)))
			{
				throw new TestFixtureAssertionException(
					$"Message '{message.Id}' was claimed by a foreign dispatcher and is still claimable by this "
					+ "one, so the setup for the ownership check did not hold.");
			}

			try
			{
				await store.MarkFailedAsync(message.Id, "not-my-claim", 1, CancellationToken.None)
					.ConfigureAwait(false);
			}
			catch (InvalidOperationException)
			{
				// Refusing a non-owner outright also satisfies the property; what matters is the outcome.
			}

			// PRIMARY DISCRIMINATOR: did the foreign report land at all? This does not depend on the
			// re-claim window, so it sees a store that releases the claim and stamps the floor in one write —
			// the shape the re-claim check below is blind to.
			if (store.GetService(typeof(IOutboxStoreAdmin)) is IOutboxStoreAdmin ownershipAdmin)
			{
				var recorded = await ownershipAdmin
					.GetAllTenantsFailedMessagesAsync(100, null, 100, CancellationToken.None)
					.ConfigureAwait(false);

				if (recorded.Any(m => string.Equals(m.Id, message.Id, StringComparison.Ordinal)))
				{
					throw new TestFixtureAssertionException(
						$"A dispatcher that does not own the claim on message '{message.Id}' reported it failed, "
						+ "and the store recorded that report. A store that guards on ownership matches no row "
						+ "for a non-owner and writes nothing, so this store applied a foreign dispatcher's "
						+ "failure to a message another dispatcher is still delivering — releasing the claim "
						+ "even where the failure floor makes the release unobservable by re-claiming.");
				}
			}

			var afterFail = await store.GetUnsentMessagesAsync(10, CancellationToken.None).ConfigureAwait(false);
			if (afterFail.Any(m => string.Equals(m.Id, message.Id, StringComparison.Ordinal)))
			{
				throw new TestFixtureAssertionException(
					$"A dispatcher that does not own the claim on message '{message.Id}' reported it failed, "
					+ "and the claim was released. A second dispatcher can now take a message the first is "
					+ "still delivering, and both will send it.");
			}

			// LIVENESS: the guard blocks non-owners only. The owner's own report still lands.
			if (store.GetService(typeof(IOutboxStoreAdmin)) is IOutboxStoreAdmin admin)
			{
				var owned = CreateTestMessage();
				await store.StageMessageAsync(owned, CancellationToken.None).ConfigureAwait(false);

				var claimed = await store.GetUnsentMessagesAsync(10, CancellationToken.None).ConfigureAwait(false);
				if (!claimed.Any(m => string.Equals(m.Id, owned.Id, StringComparison.Ordinal)))
				{
					throw new TestFixtureAssertionException(
						$"This store could not claim its own message '{owned.Id}'.");
				}

				await store.MarkFailedAsync(owned.Id, "owner-fail", 1, CancellationToken.None)
					.ConfigureAwait(false);

				var failed = await admin.GetAllTenantsFailedMessagesAsync(100, null, 100, CancellationToken.None)
					.ConfigureAwait(false);
				if (!failed.Any(m => string.Equals(m.Id, owned.Id, StringComparison.Ordinal)))
				{
					throw new TestFixtureAssertionException(
						$"The owner of the claim on message '{owned.Id}' reported it failed and nothing was "
						+ "recorded. The ownership guard is refusing everybody, so the store cannot record its "
						+ "own failures.");
				}
			}
		}
		finally
		{
			await DisposeStoreAsync(store).ConfigureAwait(false);
		}
	}

	/// <summary>
	/// LIVENESS: a suite that declares the re-claim floor must actually exercise it.
	/// </summary>
	/// <remarks>
	/// Every arm in this region returns without asserting when the seam yields no store, which is correct
	/// for a store that has no floor and useless as evidence for one that does. This arm distinguishes the
	/// two: a suite that has not overridden the seam at all has genuinely opted out, whereas one that
	/// overrides it and then yields nothing has declared the capability while exercising it zero times.
	/// That reads in the results as a region of passing arms and proves nothing whatsoever.
	/// </remarks>
	public virtual async Task ReclaimFloorSuite_ShouldExerciseThisStoreOrNotDeclareIt()
	{
		var store = await CreateStoreWithReclaimFloorAsync(60).ConfigureAwait(false);

		if (store is not null)
		{
			await DisposeStoreAsync(store).ConfigureAwait(false);
			return;
		}

		var seam = GetType().GetMethod(
			nameof(CreateStoreWithReclaimFloorAsync),
			System.Reflection.BindingFlags.Instance
				| System.Reflection.BindingFlags.NonPublic
				| System.Reflection.BindingFlags.Public);

		if (seam is not null && seam.DeclaringType != typeof(OutboxStoreConformanceTestKit))
		{
			throw new TestFixtureAssertionException(
				$"{GetType().Name} overrides {nameof(CreateStoreWithReclaimFloorAsync)} and returned null for a "
				+ "60 second floor, so every arm in the re-claim floor region returned without asserting. The "
				+ "region reads as passing while exercising this store zero times. Either return a "
				+ "floor-configured store, or remove the override so the opt-out is explicit.");
		}
	}

	/// <summary>
	/// LIVENESS: a suite whose store has a bespoke ownership guard must actually exercise it.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <see cref="MarkFailed_ByANonOwner_ShouldNotReleaseTheClaim" /> returns without asserting when
	/// <see cref="TryReserveMessageUnderForeignDispatcherAsync" /> yields <see langword="false" />, which is
	/// correct for a store that cannot express a foreign owner and useless as evidence for one that can.
	/// Without this arm the two are indistinguishable in the results: the region reads as passing either
	/// way.
	/// </para>
	/// <para>
	/// The distinction it draws is the same one the re-claim floor region draws. A suite that has not
	/// overridden the seam has genuinely opted out and is left alone. A suite that overrides it and then
	/// yields nothing has declared the capability while exercising it zero times, and that is the case
	/// worth failing on: a store whose ownership guard has a bespoke shape is exactly the store whose
	/// ownership arm most needs to run, and a guard of that kind has silently matched zero rows before.
	/// </para>
	/// </remarks>
	public virtual async Task OwnershipSuite_ShouldExerciseThisStoreOrNotDeclareIt()
	{
		var seam = GetType().GetMethod(
			nameof(TryReserveMessageUnderForeignDispatcherAsync),
			System.Reflection.BindingFlags.Instance
				| System.Reflection.BindingFlags.NonPublic
				| System.Reflection.BindingFlags.Public);

		if (seam is null || seam.DeclaringType == typeof(OutboxStoreConformanceTestKit))
		{
			// The suite never claimed to be able to stage a foreign owner, so the ownership arm's silence is
			// an explicit opt-out rather than an unexercised declaration.
			return;
		}

		var store = await CreateStoreWithReclaimFloorAsync(120).ConfigureAwait(false);
		if (store is null)
		{
			throw new TestFixtureAssertionException(
				$"{GetType().Name} overrides {nameof(TryReserveMessageUnderForeignDispatcherAsync)}, so it "
				+ "declares that this store can be given a foreign claim owner, but "
				+ $"{nameof(CreateStoreWithReclaimFloorAsync)} returned null and the ownership arm therefore "
				+ "cannot be staged at all. The ownership region reads as passing while exercising this store "
				+ "zero times.");
		}

		try
		{
			var message = CreateTestMessage();
			await store.StageMessageAsync(message, CancellationToken.None).ConfigureAwait(false);

			var reservedByOther = await TryReserveMessageUnderForeignDispatcherAsync(store, message.Id)
				.ConfigureAwait(false);

			if (!reservedByOther)
			{
				throw new TestFixtureAssertionException(
					$"{GetType().Name} overrides {nameof(TryReserveMessageUnderForeignDispatcherAsync)} and it "
					+ "returned false, so every arm in the ownership region returned without asserting. A "
					+ "suite that declares the seam must be able to stage a foreign owner; either make it do "
					+ "so, or remove the override so the opt-out is explicit and visible.");
			}
		}
		finally
		{
			await DisposeStoreAsync(store).ConfigureAwait(false);
		}
	}

	private static async Task<bool> PollForReclaimAsync(IOutboxStore store, string messageId)
	{
		// Polled rather than slept: the floor elapses on the wall clock, and the window is far wider than
		// the floor it waits on, so the arm is bounded without depending on a single timing guess.
		var deadline = DateTimeOffset.UtcNow.AddSeconds(15);
		while (DateTimeOffset.UtcNow < deadline)
		{
			var batch = await store.GetUnsentMessagesAsync(10, CancellationToken.None).ConfigureAwait(false);
			if (batch.Any(m => string.Equals(m.Id, messageId, StringComparison.Ordinal)))
			{
				return true;
			}

			await Task.Delay(100, CancellationToken.None).ConfigureAwait(false);
		}

		return false;
	}

	private static async Task DisposeStoreAsync(IOutboxStore store)
	{
		switch (store)
		{
			case IAsyncDisposable asyncDisposable:
				await asyncDisposable.DisposeAsync().ConfigureAwait(false);
				break;
			case IDisposable disposable:
				disposable.Dispose();
				break;
			default:
				break;
		}
	}

	#endregion

}
