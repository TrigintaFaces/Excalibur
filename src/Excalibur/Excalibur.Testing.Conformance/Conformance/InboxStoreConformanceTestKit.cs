// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

#pragma warning disable IDE0270 // Null check can be simplified

using System.Text;

using Excalibur.Dispatch;

namespace Excalibur.Testing.Conformance;

/// <summary>
/// Abstract base class for IInboxStore conformance testing.
/// </summary>
/// <remarks>
/// <para>
/// Inherit from this class and implement <see cref="CreateStore"/> to verify that
/// your inbox store implementation conforms to the IInboxStore contract.
/// </para>
/// <para>
/// The test kit verifies core inbox operations including create, process, fail,
/// query, and cleanup behavior.
/// </para>
/// <para>
/// <b>This kit is trim-excluded, not trim-safe, and that is a statement about the inbox-store contract
/// rather than about the kit.</b> The arms round-trip entries through the store, and a conformant store rehydrates the entry's object-valued metadata into the consumer's own types. No annotation on this kit can reach
/// those types, so a deriving suite must itself carry
/// <see cref="System.Diagnostics.CodeAnalysis.RequiresUnreferencedCodeAttribute"/> — or suppress the
/// warning deliberately — when it is compiled with the trim analyzer enabled. Overriding an arm
/// rather than wrapping it requires the same annotation on the override. A trimmed test host is not
/// a supported configuration for this kit.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // The kit resolves the store from a container built by the store's own registration
/// // extension, so every arm runs against the object a consumer actually gets -- including
/// // the ambient ITenantContext the extension registers. Constructing the store by hand
/// // certifies an instance you assembled rather than the one your registration produces.
/// public class SqlServerInboxStoreConformanceTests : InboxStoreConformanceTestKit
/// {
///     private readonly ServiceProvider _provider;
///
///     public SqlServerInboxStoreConformanceTests(SqlServerFixture fixture) =>
///         _provider = new ServiceCollection()
///             .AddLogging()
///             .AddSqlServerInboxStore(options => options.ConnectionString = fixture.ConnectionString)
///             .BuildServiceProvider();
///
///     protected override IInboxStore CreateStore() =>
///         _provider.GetRequiredService&lt;IInboxStore&gt;();
/// }
/// </code>
/// </example>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1707:Identifiers should not contain underscores",
	Justification = "Test method naming convention")]
[System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode(
	"Inbox conformance arms round-trip entries through the store, which rehydrates the entry's object-valued metadata reflectively. A trimmed test host is not a supported configuration for this kit.")]
public abstract class InboxStoreConformanceTestKit : ConformanceTestKit
{
	/// <summary>
	/// Creates a fresh inbox store instance for testing.
	/// </summary>
	/// <returns>An IInboxStore implementation to test.</returns>
	protected abstract IInboxStore CreateStore();

	/// <summary>
	/// Creates a fresh inbox store instance for testing, allowing asynchronous construction.
	/// </summary>
	/// <returns>An <see cref="IInboxStore"/> implementation to test.</returns>
	/// <remarks>
	/// <para>
	/// Override this INSTEAD of <see cref="CreateStore"/> when building the store requires awaiting —
	/// starting a container, provisioning a schema, opening a connection. The default forwards to
	/// <see cref="CreateStore"/>, so a suite with a synchronous factory needs no change.
	/// </para>
	/// <para>
	/// This exists because the synchronous seam alone was not implementable by the providers that matter.
	/// A store backed by a real database cannot be constructed without first awaiting the container and its
	/// schema, and blocking on that inside a synchronous factory deadlocks or flakes. Suites in that
	/// position could not derive this kit at all, so they derived a private base instead and the contract
	/// this kit imposes on a consumer went unverified for every one of them.
	/// </para>
	/// <para>
	/// A deriver that overrides this and leaves <see cref="CreateStore"/> unimplemented must still satisfy
	/// the abstract member; throw from it with a message naming this override, so the failure names its own
	/// cause rather than surfacing as a NotImplementedException from an unrelated arm.
	/// </para>
	/// </remarks>
	protected virtual Task<IInboxStore> CreateStoreAsync() => Task.FromResult(CreateStore());

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
	protected async Task<IInboxStore> CreateStoreForArmAsync()
	{
		var store = await CreateStoreAsync().ConfigureAwait(false);
		AssertCapabilityFlagsMatchTheDeclaredSurface(store);
		await ResetDataAsync().ConfigureAwait(false);
		return store;
	}

	/// <summary>
	/// Fails a store whose capability panel advertises a protocol its declared surface cannot execute.
	/// </summary>
	/// <param name="store">The store under test.</param>
	/// <remarks>
	/// <para>
	/// <see cref="IInboxStoreCapabilities"/> is the panel a HOST reads: the startup guards admit or refuse a
	/// configuration on it, and the dispatch path selects its processing protocol from it. A flag reporting
	/// <see langword="true"/> over a surface that is not there is therefore worse than the missing
	/// capability, because the host is told it has a guarantee and routes real work through it. The
	/// resulting failure surfaces as a cast or a <see cref="NotSupportedException"/> at first use, in the
	/// dispatch path, in production -- not at startup, where the panel was read.
	/// </para>
	/// <para>
	/// This is checked on every arm rather than in one arm of its own, deliberately. A dedicated arm has to
	/// be adopted by each deriving suite before it runs, and a provider added later that forgets the
	/// override is exactly the provider whose panel nobody checked. Routing it through the seam every arm
	/// already calls makes the check unforgettable: a store cannot run a single arm of this kit without
	/// answering for its panel.
	/// </para>
	/// <para>
	/// A store implementing no capability panel makes no claim and passes: silence is not an advertisement.
	/// The transactional flag spans BOTH seams by contract -- the relational
	/// <see cref="ITransactionalInboxStore"/> and the document-store
	/// <see cref="IScopedTransactionalInboxStore"/> -- so it is satisfied by either, while
	/// <see cref="IInboxStoreCapabilities.SupportsScopedTransactional"/> names one seam and is satisfied by
	/// that seam alone.
	/// </para>
	/// </remarks>
	private static void AssertCapabilityFlagsMatchTheDeclaredSurface(IInboxStore store)
	{
		if (store is not IInboxStoreCapabilities capabilities)
		{
			return;
		}

		AssertFlag(store, capabilities.SupportsClaim, store is IClaimableInboxStore,
			nameof(IInboxStoreCapabilities.SupportsClaim), nameof(IClaimableInboxStore));

		AssertFlag(store, capabilities.SupportsLeasedClaim, store is ILeasedInboxStore,
			nameof(IInboxStoreCapabilities.SupportsLeasedClaim), nameof(ILeasedInboxStore));

		AssertFlag(store, capabilities.SupportsProcessingTracking, store is IProcessingTrackingInboxStore,
			nameof(IInboxStoreCapabilities.SupportsProcessingTracking), nameof(IProcessingTrackingInboxStore));

		AssertFlag(store, capabilities.SupportsBackoffScheduling, store is IBackoffSchedulableInboxStore,
			nameof(IInboxStoreCapabilities.SupportsBackoffScheduling), nameof(IBackoffSchedulableInboxStore));

		AssertFlag(store, capabilities.SupportsScopedTransactional, store is IScopedTransactionalInboxStore,
			nameof(IInboxStoreCapabilities.SupportsScopedTransactional), nameof(IScopedTransactionalInboxStore));

		AssertFlag(
			store,
			capabilities.SupportsTransactional,
			store is ITransactionalInboxStore or IScopedTransactionalInboxStore,
			nameof(IInboxStoreCapabilities.SupportsTransactional),
			$"{nameof(ITransactionalInboxStore)} or {nameof(IScopedTransactionalInboxStore)}");
	}

	/// <summary>
	/// Fails when a capability flag is set and the surface backing it is absent.
	/// </summary>
	/// <param name="store">The store under test, named in the failure.</param>
	/// <param name="advertised">What the store's capability panel reports.</param>
	/// <param name="declared">Whether the surface that flag stands for is present.</param>
	/// <param name="flag">The capability flag's name.</param>
	/// <param name="surface">The surface the flag stands for, as the failure should name it.</param>
	/// <remarks>
	/// One direction only. A store may implement a surface and report <see langword="false"/> for it -- a
	/// document store that declares the scoped seam but is configured without the shared partition key its
	/// transactional batch requires reports exactly that, and the report is the truth while the interface is
	/// the over-statement. Failing that direction would force a store to advertise a guarantee its
	/// configuration has disclaimed, which is the defect this method exists to prevent, inverted.
	/// </remarks>
	private static void AssertFlag(IInboxStore store, bool advertised, bool declared, string flag, string surface)
	{
		if (!advertised || declared)
		{
			return;
		}

		throw new TestFixtureAssertionException(
			$"{store.GetType().Name} reports {flag} = true and implements no {surface}, so it advertises a "
			+ "protocol it cannot execute. That flag is the one a host reads: a startup guard admits the "
			+ "configuration on it and the dispatch path selects its protocol from it, so the contradiction "
			+ "is not discovered until first use, in production, as a failed cast or a NotSupportedException "
			+ $"-- long after the panel said the guarantee was there. Either implement {surface} or report "
			+ $"{flag} = false: a caller can act on only one of the two answers, so they must not disagree.");
	}

	/// <summary>
	/// Creates the admin interface from the store. Requires the store to implement <see cref="IInboxStoreAdmin"/>.
	/// </summary>
	/// <param name="store">The inbox store instance.</param>
	/// <returns>The admin interface.</returns>
	/// <exception cref="InvalidOperationException">If the store does not implement <see cref="IInboxStoreAdmin"/>.</exception>
	protected static IInboxStoreAdmin CreateAdminStore(IInboxStore store) =>
		store as IInboxStoreAdmin
		?? throw new InvalidOperationException(
			$"Conformance test requires {store.GetType().Name} to implement IInboxStoreAdmin.");

	/// <summary>
	/// Generates a unique message ID for test isolation.
	/// </summary>
	/// <returns>A unique message identifier.</returns>
	protected virtual string GenerateMessageId() => Guid.NewGuid().ToString();

	/// <summary>
	/// Generates a unique handler type name for test isolation.
	/// </summary>
	/// <returns>A unique handler type name.</returns>
	protected virtual string GenerateHandlerType() => $"TestHandler_{Guid.NewGuid():N}";

	/// <summary>
	/// Creates a payload from the given content string.
	/// </summary>
	/// <param name="content">The content to encode.</param>
	/// <returns>The encoded payload bytes.</returns>
	protected virtual byte[] CreatePayload(string content) =>
		Encoding.UTF8.GetBytes(content);

	/// <summary>
	/// Creates default metadata for testing.
	/// </summary>
	/// <returns>A dictionary with default test metadata.</returns>
	protected virtual IDictionary<string, object> CreateDefaultMetadata() =>
		new Dictionary<string, object>(StringComparer.Ordinal)
		{
			["TestKey"] = "TestValue",
			["Timestamp"] = DateTimeOffset.UtcNow.ToString("O")
		};

	#region Create Tests

	/// <summary>
	/// Verifies that creating a new inbox entry succeeds.
	/// </summary>
	public virtual async Task CreateEntryAsync_NewEntry_ShouldSucceed()
	{
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);
		var messageId = GenerateMessageId();
		var handlerType = GenerateHandlerType();
		var messageType = "TestMessageType";
		var payload = CreatePayload("Test payload content");
		var metadata = CreateDefaultMetadata();

		var entry = await store.CreateEntryAsync(
			messageId,
			handlerType,
			messageType,
			payload,
			metadata,
			CancellationToken.None).ConfigureAwait(false);

		if (entry is null)
		{
			throw new TestFixtureAssertionException("Expected inbox entry but got null");
		}

		if (entry.MessageId != messageId)
		{
			throw new TestFixtureAssertionException(
				$"MessageId mismatch: expected '{messageId}', got '{entry.MessageId}'");
		}

		if (entry.HandlerType != handlerType)
		{
			throw new TestFixtureAssertionException(
				$"HandlerType mismatch: expected '{handlerType}', got '{entry.HandlerType}'");
		}

		if (entry.MessageType != messageType)
		{
			throw new TestFixtureAssertionException(
				$"MessageType mismatch: expected '{messageType}', got '{entry.MessageType}'");
		}

		if (entry.Status != InboxStatus.Received)
		{
			throw new TestFixtureAssertionException(
				$"Expected status Received but got {entry.Status}");
		}
	}

	/// <summary>
	/// Verifies that creating a duplicate entry throws.
	/// </summary>
	public virtual async Task CreateEntryAsync_DuplicateEntry_ShouldThrow()
	{
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);
		var messageId = GenerateMessageId();
		var handlerType = GenerateHandlerType();
		var messageType = "TestMessageType";
		var payload = CreatePayload("Test payload");
		var metadata = CreateDefaultMetadata();

		_ = await store.CreateEntryAsync(
			messageId,
			handlerType,
			messageType,
			payload,
			metadata,
			CancellationToken.None).ConfigureAwait(false);

		var exceptionThrown = false;
		try
		{
			_ = await store.CreateEntryAsync(
				messageId,
				handlerType,
				messageType,
				payload,
				metadata,
				CancellationToken.None).ConfigureAwait(false);
		}
		catch (InvalidOperationException)
		{
			exceptionThrown = true;
		}

		if (!exceptionThrown)
		{
			throw new TestFixtureAssertionException(
				"Expected InvalidOperationException for duplicate entry but no exception was thrown");
		}
	}

	/// <summary>
	/// Verifies that creating an entry preserves all metadata.
	/// </summary>
	public virtual async Task CreateEntryAsync_WithAllMetadata_ShouldPreserve()
	{
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);
		var messageId = GenerateMessageId();
		var handlerType = GenerateHandlerType();
		var messageType = "TestMessageType";
		var payloadContent = "Full metadata test payload";
		var payload = CreatePayload(payloadContent);
		var metadata = new Dictionary<string, object>(StringComparer.Ordinal)
		{
			["StringKey"] = "StringValue",
			["IntKey"] = 42,
			["BoolKey"] = true
		};

		var entry = await store.CreateEntryAsync(
			messageId,
			handlerType,
			messageType,
			payload,
			metadata,
			CancellationToken.None).ConfigureAwait(false);

		if (entry is null)
		{
			throw new TestFixtureAssertionException("Expected inbox entry but got null");
		}

		// Verify payload is preserved
		var decodedPayload = Encoding.UTF8.GetString(entry.Payload);
		if (decodedPayload != payloadContent)
		{
			throw new TestFixtureAssertionException(
				$"Payload mismatch: expected '{payloadContent}', got '{decodedPayload}'");
		}

		// Verify metadata is preserved
		if (entry.Metadata is null || entry.Metadata.Count < 3)
		{
			throw new TestFixtureAssertionException(
				$"Expected at least 3 metadata entries but got {entry.Metadata?.Count ?? 0}");
		}

		if (!entry.Metadata.TryGetValue("StringKey", out var stringValue) ||
			stringValue?.ToString() != "StringValue")
		{
			throw new TestFixtureAssertionException("Expected Metadata['StringKey'] = 'StringValue'");
		}
	}

	#endregion

	#region Process Tests

	/// <summary>
	/// Verifies that marking an existing entry as processed succeeds.
	/// </summary>
	public virtual async Task MarkProcessedAsync_ExistingEntry_ShouldSucceed()
	{
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);
		var messageId = GenerateMessageId();
		var handlerType = GenerateHandlerType();
		var messageType = "TestMessageType";
		var payload = CreatePayload("Test payload");
		var metadata = CreateDefaultMetadata();

		_ = await store.CreateEntryAsync(
			messageId,
			handlerType,
			messageType,
			payload,
			metadata,
			CancellationToken.None).ConfigureAwait(false);

		await store.MarkProcessedAsync(messageId, handlerType, CancellationToken.None)
			.ConfigureAwait(false);

		var entry = await store.GetEntryAsync(messageId, handlerType, CancellationToken.None)
			.ConfigureAwait(false);

		if (entry is null)
		{
			throw new TestFixtureAssertionException("Expected inbox entry but got null");
		}

		if (entry.Status != InboxStatus.Processed)
		{
			throw new TestFixtureAssertionException(
				$"Expected status Processed but got {entry.Status}");
		}

		if (entry.ProcessedAt is null)
		{
			throw new TestFixtureAssertionException("Expected ProcessedAt to be set");
		}
	}

	/// <summary>
	/// Verifies that TryMarkAsProcessedAsync returns true for new messages.
	/// </summary>
	public virtual async Task TryMarkAsProcessedAsync_FirstTime_ShouldReturnTrue()
	{
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);
		var messageId = GenerateMessageId();
		var handlerType = GenerateHandlerType();

		var result = await store.TryMarkAsProcessedAsync(
			messageId,
			handlerType,
			CancellationToken.None).ConfigureAwait(false);

		if (!result)
		{
			throw new TestFixtureAssertionException(
				"Expected TryMarkAsProcessedAsync to return true for first call");
		}
	}

	/// <summary>
	/// Verifies that TryMarkAsProcessedAsync returns false for already processed messages.
	/// </summary>
	public virtual async Task TryMarkAsProcessedAsync_AlreadyProcessed_ShouldReturnFalse()
	{
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);
		var messageId = GenerateMessageId();
		var handlerType = GenerateHandlerType();

		// First call - should return true
		_ = await store.TryMarkAsProcessedAsync(
			messageId,
			handlerType,
			CancellationToken.None).ConfigureAwait(false);

		// Second call - should return false (duplicate)
		var result = await store.TryMarkAsProcessedAsync(
			messageId,
			handlerType,
			CancellationToken.None).ConfigureAwait(false);

		if (result)
		{
			throw new TestFixtureAssertionException(
				"Expected TryMarkAsProcessedAsync to return false for duplicate call");
		}
	}

	/// <summary>
	/// Verifies that IsProcessedAsync returns true for processed messages.
	/// </summary>
	public virtual async Task IsProcessedAsync_ProcessedMessage_ShouldReturnTrue()
	{
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);
		var messageId = GenerateMessageId();
		var handlerType = GenerateHandlerType();

		_ = await store.TryMarkAsProcessedAsync(
			messageId,
			handlerType,
			CancellationToken.None).ConfigureAwait(false);

		var isProcessed = await store.IsProcessedAsync(
			messageId,
			handlerType,
			CancellationToken.None).ConfigureAwait(false);

		if (!isProcessed)
		{
			throw new TestFixtureAssertionException(
				"Expected IsProcessedAsync to return true for processed message");
		}
	}

	/// <summary>
	/// Verifies that IsProcessedAsync returns false for unprocessed messages.
	/// </summary>
	public virtual async Task IsProcessedAsync_UnprocessedMessage_ShouldReturnFalse()
	{
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);
		var messageId = GenerateMessageId();
		var handlerType = GenerateHandlerType();

		var isProcessed = await store.IsProcessedAsync(
			messageId,
			handlerType,
			CancellationToken.None).ConfigureAwait(false);

		if (isProcessed)
		{
			throw new TestFixtureAssertionException(
				"Expected IsProcessedAsync to return false for unprocessed message");
		}
	}

	#endregion

	#region Fail Tests

	/// <summary>
	/// Verifies that marking an entry as failed sets the status and error.
	/// </summary>
	public virtual async Task MarkFailedAsync_ExistingEntry_ShouldSetStatusAndError()
	{
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);
		var messageId = GenerateMessageId();
		var handlerType = GenerateHandlerType();
		var messageType = "TestMessageType";
		var payload = CreatePayload("Test payload");
		var metadata = CreateDefaultMetadata();
		var errorMessage = "Test error message";

		_ = await store.CreateEntryAsync(
			messageId,
			handlerType,
			messageType,
			payload,
			metadata,
			CancellationToken.None).ConfigureAwait(false);

		await store.MarkFailedAsync(
			messageId,
			handlerType,
			errorMessage,
			CancellationToken.None).ConfigureAwait(false);

		var entry = await store.GetEntryAsync(messageId, handlerType, CancellationToken.None)
			.ConfigureAwait(false);

		if (entry is null)
		{
			throw new TestFixtureAssertionException("Expected inbox entry but got null");
		}

		if (entry.Status != InboxStatus.Failed)
		{
			throw new TestFixtureAssertionException(
				$"Expected status Failed but got {entry.Status}");
		}

		if (entry.LastError != errorMessage)
		{
			throw new TestFixtureAssertionException(
				$"Expected LastError '{errorMessage}' but got '{entry.LastError}'");
		}
	}

	/// <summary>
	/// Verifies that marking an entry as failed increments retry count.
	/// </summary>
	public virtual async Task MarkFailedAsync_ShouldIncrementRetryCount()
	{
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);
		var messageId = GenerateMessageId();
		var handlerType = GenerateHandlerType();
		var messageType = "TestMessageType";
		var payload = CreatePayload("Test payload");
		var metadata = CreateDefaultMetadata();

		_ = await store.CreateEntryAsync(
			messageId,
			handlerType,
			messageType,
			payload,
			metadata,
			CancellationToken.None).ConfigureAwait(false);

		// First failure
		await store.MarkFailedAsync(messageId, handlerType, "Error 1", CancellationToken.None)
			.ConfigureAwait(false);

		var entry1 = await store.GetEntryAsync(messageId, handlerType, CancellationToken.None)
			.ConfigureAwait(false);

		if (entry1 is null || entry1.RetryCount != 1)
		{
			throw new TestFixtureAssertionException(
				$"Expected RetryCount 1 after first failure but got {entry1?.RetryCount ?? -1}");
		}

		// Second failure
		await store.MarkFailedAsync(messageId, handlerType, "Error 2", CancellationToken.None)
			.ConfigureAwait(false);

		var entry2 = await store.GetEntryAsync(messageId, handlerType, CancellationToken.None)
			.ConfigureAwait(false);

		if (entry2 is null || entry2.RetryCount != 2)
		{
			throw new TestFixtureAssertionException(
				$"Expected RetryCount 2 after second failure but got {entry2?.RetryCount ?? -1}");
		}
	}

	/// <summary>
	/// Verifies that GetAllTenantsFailedEntriesAsync respects maxRetries filter.
	/// </summary>
	public virtual async Task GetAllTenantsFailedEntriesAsync_ShouldRespectMaxRetries()
	{
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);
		var handlerType = GenerateHandlerType();
		var messageType = "TestMessageType";
		var payload = CreatePayload("Test payload");
		var metadata = CreateDefaultMetadata();

		// Create entry 1 with 1 retry
		var messageId1 = GenerateMessageId();
		_ = await store.CreateEntryAsync(messageId1, handlerType, messageType, payload, metadata, CancellationToken.None)
			.ConfigureAwait(false);
		await store.MarkFailedAsync(messageId1, handlerType, "Error", CancellationToken.None)
			.ConfigureAwait(false);

		// Create entry 2 with 3 retries
		var messageId2 = GenerateMessageId();
		_ = await store.CreateEntryAsync(messageId2, handlerType, messageType, payload, metadata, CancellationToken.None)
			.ConfigureAwait(false);
		await store.MarkFailedAsync(messageId2, handlerType, "Error 1", CancellationToken.None).ConfigureAwait(false);
		await store.MarkFailedAsync(messageId2, handlerType, "Error 2", CancellationToken.None).ConfigureAwait(false);
		await store.MarkFailedAsync(messageId2, handlerType, "Error 3", CancellationToken.None).ConfigureAwait(false);

		// Query with maxRetries=2 - should only return entry1 (1 retry <= 2)
		var failedEntries = await CreateAdminStore(store).GetAllTenantsFailedEntriesAsync(
			maxRetries: 2,
			olderThan: null,
			batchSize: 100,
			CancellationToken.None).ConfigureAwait(false);

		var entriesList = failedEntries.ToList();
		var hasEntryWithExcessiveRetries = entriesList.Any(e => e.RetryCount > 2);

		if (hasEntryWithExcessiveRetries)
		{
			throw new TestFixtureAssertionException(
				"GetAllTenantsFailedEntriesAsync returned entries exceeding maxRetries");
		}
	}

	/// <summary>
	/// LIVENESS: the failed-entry retry read is estate-wide and must return every tenant's failed entries,
	/// including from a call site that has a different tenant ambient.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The retry sweeper runs on a background loop with no tenant of its own. If a store scopes this read to
	/// whatever tenant happens to be ambient, every tenant except that one accumulates failed entries that
	/// are never retried — a silent, per-tenant delivery stall that no other arm on this kit detects, because
	/// each tenant's own reads keep working.
	/// </para>
	/// <para>
	/// The name of the operation is the contract: <c>AllTenants</c>. This arm is what a tenant-scoped
	/// implementation fails.
	/// </para>
	/// </remarks>
	public virtual async Task GetAllTenantsFailedEntriesAsync_MustReturnEveryTenantsFailedEntries()
	{
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);
		var handlerType = GenerateHandlerType();
		var messageType = "TestMessageType";
		var payload = CreatePayload("Test payload");
		var metadata = CreateDefaultMetadata();

		var messageIdA = GenerateMessageId();
		using (TenantContextHolder.BeginScope(IsolationTenantA))
		{
			_ = await store.CreateEntryAsync(messageIdA, handlerType, messageType, payload, metadata, CancellationToken.None)
				.ConfigureAwait(false);
			await store.MarkFailedAsync(messageIdA, handlerType, "Error A", CancellationToken.None).ConfigureAwait(false);
		}

		var messageIdB = GenerateMessageId();
		using (TenantContextHolder.BeginScope(IsolationTenantB))
		{
			_ = await store.CreateEntryAsync(messageIdB, handlerType, messageType, payload, metadata, CancellationToken.None)
				.ConfigureAwait(false);
			await store.MarkFailedAsync(messageIdB, handlerType, "Error B", CancellationToken.None).ConfigureAwait(false);
		}

		// Read from INSIDE tenant A's scope. A store that scopes this read by ambient tenant returns only A,
		// which is exactly the stall this arm exists to catch.
		List<InboxEntry> entries;
		using (TenantContextHolder.BeginScope(IsolationTenantA))
		{
			entries = (await CreateAdminStore(store).GetAllTenantsFailedEntriesAsync(
				maxRetries: 10,
				olderThan: null,
				batchSize: 100,
				CancellationToken.None).ConfigureAwait(false)).ToList();
		}

		var sawA = entries.Exists(e => string.Equals(e.MessageId, messageIdA, StringComparison.Ordinal));
		var sawB = entries.Exists(e => string.Equals(e.MessageId, messageIdB, StringComparison.Ordinal));

		if (!sawA)
		{
			throw new TestFixtureAssertionException(
				$"GetAllTenantsFailedEntriesAsync did not return failed entry '{messageIdA}' staged by tenant "
				+ $"'{IsolationTenantA}', which is also the ambient tenant of the read. This is a "
				+ "failed-entry retrieval failure rather than a tenancy failure — reported separately so the "
				+ "two causes are not confused.");
		}

		if (!sawB)
		{
			throw new TestFixtureAssertionException(
				$"GetAllTenantsFailedEntriesAsync is tenant-scoped: called with '{IsolationTenantA}' ambient it "
				+ $"returned that tenant's failed entry but not '{messageIdB}', staged by "
				+ $"'{IsolationTenantB}'. This read is the retry sweeper's, and the sweeper has no tenant of "
				+ "its own — every tenant other than the one that happens to be ambient would accumulate "
				+ "failed entries that are never retried. Remove the tenant term from this read.");
		}
	}

	#endregion

	#region Query Tests

	/// <summary>
	/// Verifies that GetEntryAsync returns an existing entry.
	/// </summary>
	public virtual async Task GetEntryAsync_Existing_ShouldReturnEntry()
	{
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);
		var messageId = GenerateMessageId();
		var handlerType = GenerateHandlerType();
		var messageType = "TestMessageType";
		var payload = CreatePayload("Test payload");
		var metadata = CreateDefaultMetadata();

		_ = await store.CreateEntryAsync(
			messageId,
			handlerType,
			messageType,
			payload,
			metadata,
			CancellationToken.None).ConfigureAwait(false);

		var entry = await store.GetEntryAsync(messageId, handlerType, CancellationToken.None)
			.ConfigureAwait(false);

		if (entry is null)
		{
			throw new TestFixtureAssertionException("Expected inbox entry but got null");
		}

		if (entry.MessageId != messageId)
		{
			throw new TestFixtureAssertionException(
				$"MessageId mismatch: expected '{messageId}', got '{entry.MessageId}'");
		}
	}

	/// <summary>
	/// Verifies that GetEntryAsync returns null for non-existent entry.
	/// </summary>
	public virtual async Task GetEntryAsync_NonExistent_ShouldReturnNull()
	{
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);
		var messageId = GenerateMessageId();
		var handlerType = GenerateHandlerType();

		var entry = await store.GetEntryAsync(messageId, handlerType, CancellationToken.None)
			.ConfigureAwait(false);

		if (entry is not null)
		{
			throw new TestFixtureAssertionException(
				$"Expected null for non-existent entry but got entry with status {entry.Status}");
		}
	}

	/// <summary>
	/// Verifies that GetAllTenantsStatisticsAsync returns correct counts.
	/// </summary>
	public virtual async Task GetAllTenantsStatisticsAsync_ShouldReturnCorrectCounts()
	{
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);
		var handlerType = GenerateHandlerType();
		var messageType = "TestMessageType";
		var payload = CreatePayload("Test payload");
		var metadata = CreateDefaultMetadata();

		// Create received entry
		var receivedMsgId = GenerateMessageId();
		_ = await store.CreateEntryAsync(receivedMsgId, handlerType, messageType, payload, metadata, CancellationToken.None)
			.ConfigureAwait(false);

		// Create and process an entry
		var processedMsgId = GenerateMessageId();
		_ = await store.CreateEntryAsync(processedMsgId, handlerType, messageType, payload, metadata, CancellationToken.None)
			.ConfigureAwait(false);
		await store.MarkProcessedAsync(processedMsgId, handlerType, CancellationToken.None)
			.ConfigureAwait(false);

		// Create and fail an entry
		var failedMsgId = GenerateMessageId();
		_ = await store.CreateEntryAsync(failedMsgId, handlerType, messageType, payload, metadata, CancellationToken.None)
			.ConfigureAwait(false);
		await store.MarkFailedAsync(failedMsgId, handlerType, "Test error", CancellationToken.None)
			.ConfigureAwait(false);

		var stats = await CreateAdminStore(store).GetAllTenantsStatisticsAsync(CancellationToken.None).ConfigureAwait(false);

		if (stats is null)
		{
			throw new TestFixtureAssertionException("Expected statistics but got null");
		}

		// We created 3 entries total in this test
		if (stats.TotalEntries < 3)
		{
			throw new TestFixtureAssertionException(
				$"Expected at least 3 total entries but got {stats.TotalEntries}");
		}

		if (stats.ProcessedEntries < 1)
		{
			throw new TestFixtureAssertionException(
				$"Expected at least 1 processed entry but got {stats.ProcessedEntries}");
		}

		if (stats.FailedEntries < 1)
		{
			throw new TestFixtureAssertionException(
				$"Expected at least 1 failed entry but got {stats.FailedEntries}");
		}
	}

	#endregion

	#region Cleanup Tests

	/// <summary>
	/// Verifies that CleanupAllTenantsProcessedEntriesAsync removes old processed entries.
	/// </summary>
	public virtual async Task CleanupAllTenantsProcessedEntriesAsync_OldProcessed_ShouldRemove()
	{
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);
		var messageId = GenerateMessageId();
		var handlerType = GenerateHandlerType();
		var messageType = "TestMessageType";
		var payload = CreatePayload("Test payload");
		var metadata = CreateDefaultMetadata();

		// Create and process an entry
		_ = await store.CreateEntryAsync(messageId, handlerType, messageType, payload, metadata, CancellationToken.None)
			.ConfigureAwait(false);
		await store.MarkProcessedAsync(messageId, handlerType, CancellationToken.None)
			.ConfigureAwait(false);

		// Cleanup with very short retention (0 seconds) - should remove processed entries.
		// Retry briefly to avoid timestamp boundary races when ProcessedAt ~= cutoff.
		var removedCount = 0;
		var deadline = DateTimeOffset.UtcNow.AddSeconds(2);
		var removedOrMissing = false;
		do
		{
			removedCount += await CreateAdminStore(store).CleanupAllTenantsProcessedEntriesAsync(DateTimeOffset.UtcNow, CancellationToken.None).ConfigureAwait(false);
			var currentEntry = await store.GetEntryAsync(messageId, handlerType, CancellationToken.None).ConfigureAwait(false);
			if (currentEntry is null || removedCount > 0)
			{
				removedOrMissing = true;
				break;
			}

			await Task.Yield();
		}
		while (DateTimeOffset.UtcNow < deadline);

		// Either the entry should be removed, or removedCount should be >= 1
		if (!removedOrMissing && removedCount == 0)
		{
			throw new TestFixtureAssertionException(
				"Expected CleanupAllTenantsProcessedEntriesAsync to remove processed entries with zero retention");
		}
	}

	/// <summary>
	/// Verifies that CleanupAllTenantsProcessedEntriesAsync preserves recent entries.
	/// </summary>
	public virtual async Task CleanupAllTenantsProcessedEntriesAsync_ShouldPreserveRecent()
	{
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);
		var messageId = GenerateMessageId();
		var handlerType = GenerateHandlerType();
		var messageType = "TestMessageType";
		var payload = CreatePayload("Test payload");
		var metadata = CreateDefaultMetadata();

		// Create and process an entry
		_ = await store.CreateEntryAsync(messageId, handlerType, messageType, payload, metadata, CancellationToken.None)
			.ConfigureAwait(false);
		await store.MarkProcessedAsync(messageId, handlerType, CancellationToken.None)
			.ConfigureAwait(false);

		// Cleanup with long retention (1 hour) - should preserve recent entries
		_ = await CreateAdminStore(store).CleanupAllTenantsProcessedEntriesAsync(DateTimeOffset.UtcNow.AddHours(-1), CancellationToken.None)
			.ConfigureAwait(false);

		// Entry should still exist (was just created)
		var entry = await store.GetEntryAsync(messageId, handlerType, CancellationToken.None)
			.ConfigureAwait(false);

		if (entry is null)
		{
			throw new TestFixtureAssertionException(
				"Expected recent entry to be preserved but it was removed");
		}
	}

	#endregion

	#region Isolation Tests

	/// <summary>
	/// Verifies that entries are isolated by (messageId, handlerType) composite key.
	/// </summary>
	public virtual async Task Entries_ShouldIsolateByMessageIdAndHandlerType()
	{
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);
		var messageId1 = GenerateMessageId();
		var messageId2 = GenerateMessageId();
		var handlerType = GenerateHandlerType();
		var messageType = "TestMessageType";
		var payload = CreatePayload("Test payload");
		var metadata = CreateDefaultMetadata();

		_ = await store.CreateEntryAsync(messageId1, handlerType, messageType, payload, metadata, CancellationToken.None)
			.ConfigureAwait(false);
		_ = await store.CreateEntryAsync(messageId2, handlerType, messageType, payload, metadata, CancellationToken.None)
			.ConfigureAwait(false);

		// Process only messageId1
		await store.MarkProcessedAsync(messageId1, handlerType, CancellationToken.None)
			.ConfigureAwait(false);

		// messageId2 should still be in Received status
		var entry2 = await store.GetEntryAsync(messageId2, handlerType, CancellationToken.None)
			.ConfigureAwait(false);

		if (entry2 is null)
		{
			throw new TestFixtureAssertionException("Expected entry2 but got null");
		}

		if (entry2.Status != InboxStatus.Received)
		{
			throw new TestFixtureAssertionException(
				$"Expected entry2 status Received but got {entry2.Status}");
		}
	}

	/// <summary>
	/// Verifies that the same messageId can be processed by different handlers independently.
	/// </summary>
	public virtual async Task SameMessageId_DifferentHandlers_ShouldBeIndependent()
	{
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);
		var messageId = GenerateMessageId();
		var handlerType1 = GenerateHandlerType();
		var handlerType2 = GenerateHandlerType();
		var messageType = "TestMessageType";
		var payload = CreatePayload("Test payload");
		var metadata = CreateDefaultMetadata();

		// Create entries for same message but different handlers
		_ = await store.CreateEntryAsync(messageId, handlerType1, messageType, payload, metadata, CancellationToken.None)
			.ConfigureAwait(false);
		_ = await store.CreateEntryAsync(messageId, handlerType2, messageType, payload, metadata, CancellationToken.None)
			.ConfigureAwait(false);

		// Process only handler1
		await store.MarkProcessedAsync(messageId, handlerType1, CancellationToken.None)
			.ConfigureAwait(false);

		// handler2 should still be in Received status
		var entry1 = await store.GetEntryAsync(messageId, handlerType1, CancellationToken.None)
			.ConfigureAwait(false);
		var entry2 = await store.GetEntryAsync(messageId, handlerType2, CancellationToken.None)
			.ConfigureAwait(false);

		if (entry1 is null || entry1.Status != InboxStatus.Processed)
		{
			throw new TestFixtureAssertionException(
				$"Expected handler1 status Processed but got {entry1?.Status}");
		}

		if (entry2 is null || entry2.Status != InboxStatus.Received)
		{
			throw new TestFixtureAssertionException(
				$"Expected handler2 status Received but got {entry2?.Status}");
		}
	}

	#endregion

	#region Edge Cases

	/// <summary>
	/// Verifies that GetAllTenantsEntriesAsync returns all entries.
	/// </summary>
	public virtual async Task GetAllTenantsEntriesAsync_ShouldReturnAllEntries()
	{
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);
		var handlerType = GenerateHandlerType();
		var messageType = "TestMessageType";
		var payload = CreatePayload("Test payload");
		var metadata = CreateDefaultMetadata();

		// Create multiple entries
		var messageIds = new List<string>();
		for (var i = 0; i < 3; i++)
		{
			var msgId = GenerateMessageId();
			messageIds.Add(msgId);
			_ = await store.CreateEntryAsync(msgId, handlerType, messageType, payload, metadata, CancellationToken.None)
				.ConfigureAwait(false);
		}

		var allEntries = await CreateAdminStore(store).GetAllTenantsEntriesAsync(CancellationToken.None).ConfigureAwait(false);
		var entriesList = allEntries.ToList();

		// Should contain at least our 3 entries
		var foundCount = messageIds.Count(msgId =>
			entriesList.Any(e => e.MessageId == msgId && e.HandlerType == handlerType));

		if (foundCount != 3)
		{
			throw new TestFixtureAssertionException(
				$"Expected to find all 3 created entries but found {foundCount}");
		}
	}

	#endregion

	#region Tenant Isolation Tests

	/// <summary>The tenant term the isolation arms write under first.</summary>
	private const string IsolationTenantA = "conformance-tenant-a";

	/// <summary>The tenant term the isolation arms contrast against <see cref="IsolationTenantA"/>.</summary>
	private const string IsolationTenantB = "conformance-tenant-b";

	/// <summary>
	/// LIVENESS: a tenant's message must not be swallowed as a duplicate because another tenant already
	/// processed a message carrying the same id.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The inbox is the one store where the tenant is part of a message's IDENTITY rather than merely
	/// carried alongside it. Message ids are chosen by producers and are only unique within the system that
	/// issued them, so two tenants routinely present distinct messages under the same id. A store that keys
	/// deduplication on (messageId, handlerType) alone resolves both to one entry, and the second tenant's
	/// claim is refused as a duplicate.
	/// </para>
	/// <para>
	/// The consequence is a message that is never processed and never retried, because the store reports it
	/// as already handled. That is silent message loss on the SUCCESS path — the failure produces no error
	/// for an operator to see — and it is simultaneously a cross-tenant isolation breach, since one tenant's
	/// traffic determines whether another's is delivered.
	/// </para>
	/// <para>
	/// This arm uses ONE store and switches the ambient tenant around it, which is the topology a host runs:
	/// the store is registered once and resolves the tenant per call. Obtaining a separate store per tenant
	/// would let an implementation satisfy the arm by instance separation, with no tenant term in the key at
	/// all. It therefore requires the store under test to resolve its tenant from the ambient scope — inject
	/// <see cref="ConformanceAmbientTenantContext"/> into the store handed to this kit. A store bound to a
	/// FIXED tenant context fails here rather than passing silently, because both scopes then address one
	/// partition and the second claim is refused.
	/// </para>
	/// </remarks>
	public virtual async Task TryMarkAsProcessed_SameMessageIdInAnotherTenant_MustNotBeSwallowedAsADuplicate()
	{
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);
		var messageId = GenerateMessageId();
		var handlerType = GenerateHandlerType();

		bool firstTenantClaimed;
		using (TenantContextHolder.BeginScope(IsolationTenantA))
		{
			firstTenantClaimed = await store
				.TryMarkAsProcessedAsync(messageId, handlerType, CancellationToken.None)
				.ConfigureAwait(false);
		}

		// Reported separately from the isolation failure below: a store that refuses every claim would fail
		// the next check too, and the two causes call for entirely different fixes.
		if (!firstTenantClaimed)
		{
			throw new TestFixtureAssertionException(
				$"Tenant '{IsolationTenantA}' could not claim message '{messageId}' for handler "
				+ $"'{handlerType}' on a store that had never seen it. This is a first-claim failure rather "
				+ "than a tenancy failure — TryMarkAsProcessedAsync must return true the first time.");
		}

		bool secondTenantClaimed;
		using (TenantContextHolder.BeginScope(IsolationTenantB))
		{
			secondTenantClaimed = await store
				.TryMarkAsProcessedAsync(messageId, handlerType, CancellationToken.None)
				.ConfigureAwait(false);
		}

		if (!secondTenantClaimed)
		{
			throw new TestFixtureAssertionException(
				$"Cross-tenant deduplication collision: tenant '{IsolationTenantB}' was refused its claim on "
				+ $"message '{messageId}' for handler '{handlerType}' because tenant '{IsolationTenantA}' had "
				+ "already processed a DIFFERENT message carrying that id. The store is keying deduplication "
				+ "on (messageId, handlerType) without a tenant term, so one tenant's traffic silently "
				+ "suppresses another's. Compose the resolved tenant into the deduplication key.");
		}
	}

	/// <summary>
	/// SAFETY and LIVENESS: a processed-check must answer for the asking tenant only.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The safety half is that one tenant's completed work must not mark another tenant's message as
	/// already handled, which would cause the second tenant's message to be skipped without ever running.
	/// The liveness half is that a tenant must still see its OWN completed work.
	/// </para>
	/// <para>
	/// Both halves are asserted here deliberately. A store that answers <see langword="false"/> to every
	/// caller satisfies the safety half perfectly — isolation is trivially total when nothing is ever
	/// reported processed — and such a store would re-run every message forever. Without the liveness half
	/// this arm would certify it.
	/// </para>
	/// </remarks>
	public virtual async Task IsProcessed_MustNotReportAnotherTenantsMessageAsProcessed()
	{
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);
		var messageId = GenerateMessageId();
		var handlerType = GenerateHandlerType();

		using (TenantContextHolder.BeginScope(IsolationTenantA))
		{
			_ = await store.TryMarkAsProcessedAsync(messageId, handlerType, CancellationToken.None)
				.ConfigureAwait(false);
		}

		bool otherTenantSeesIt;
		using (TenantContextHolder.BeginScope(IsolationTenantB))
		{
			otherTenantSeesIt = await store.IsProcessedAsync(messageId, handlerType, CancellationToken.None)
				.ConfigureAwait(false);
		}

		if (otherTenantSeesIt)
		{
			throw new TestFixtureAssertionException(
				$"Tenant isolation violated: tenant '{IsolationTenantB}' was told message '{messageId}' for "
				+ $"handler '{handlerType}' was already processed, when it was tenant '{IsolationTenantA}' "
				+ "that processed it. That tenant's own message would now be skipped without ever running. "
				+ "Scope the processed-check by the resolved tenant.");
		}

		bool owningTenantSeesIt;
		using (TenantContextHolder.BeginScope(IsolationTenantA))
		{
			owningTenantSeesIt = await store.IsProcessedAsync(messageId, handlerType, CancellationToken.None)
				.ConfigureAwait(false);
		}

		if (!owningTenantSeesIt)
		{
			throw new TestFixtureAssertionException(
				$"Tenant '{IsolationTenantA}' processed message '{messageId}' for handler '{handlerType}' and "
				+ "is not told so on a subsequent check. A store that reports nothing as processed passes the "
				+ "isolation half of this arm while re-running every message indefinitely, which is why the "
				+ "owning tenant's own view is asserted here.");
		}
	}

	/// <summary>
	/// SAFETY and LIVENESS: an entry must be addressable per tenant, so two tenants may hold entries under
	/// one message id and each reads back its own.
	/// </summary>
	/// <remarks>
	/// <para>
	/// This is a distinct code path from the deduplication claim: implementations that compose the tenant
	/// into a document id, a sort key, or a key prefix do so where the entry is WRITTEN, and a store may
	/// scope one path and not the other. A kit exercising only the claim path cannot tell the difference.
	/// </para>
	/// <para>
	/// The payload is asserted rather than merely the entry's presence. A store that returns the other
	/// tenant's entry has both disclosed that tenant's message body and given this one the wrong data to
	/// act on, and a non-null check cannot see either.
	/// </para>
	/// </remarks>
	public virtual async Task CreateEntry_SameMessageIdInAnotherTenant_MustNotCollide()
	{
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);
		var messageId = GenerateMessageId();
		var handlerType = GenerateHandlerType();
		const string MessageType = "TestMessageType";
		const string TenantAPayload = "payload written by tenant a";
		const string TenantBPayload = "payload written by tenant b";

		using (TenantContextHolder.BeginScope(IsolationTenantA))
		{
			_ = await store.CreateEntryAsync(
				messageId,
				handlerType,
				MessageType,
				CreatePayload(TenantAPayload),
				CreateDefaultMetadata(),
				CancellationToken.None).ConfigureAwait(false);
		}

		using (TenantContextHolder.BeginScope(IsolationTenantB))
		{
			try
			{
				_ = await store.CreateEntryAsync(
					messageId,
					handlerType,
					MessageType,
					CreatePayload(TenantBPayload),
					CreateDefaultMetadata(),
					CancellationToken.None).ConfigureAwait(false);
			}
			catch (InvalidOperationException ex)
			{
				throw new TestFixtureAssertionException(
					$"Cross-tenant entry collision: tenant '{IsolationTenantB}' could not record its own "
					+ $"message '{messageId}' for handler '{handlerType}' because tenant "
					+ $"'{IsolationTenantA}' holds an entry under that id. The entry key carries no tenant "
					+ "term, so the two tenants' messages are one row. Compose the resolved tenant into the "
					+ "entry's identity.",
					ex);
			}
		}

		await AssertEntryPayloadAsync(store, IsolationTenantA, messageId, handlerType, TenantAPayload)
			.ConfigureAwait(false);
		await AssertEntryPayloadAsync(store, IsolationTenantB, messageId, handlerType, TenantBPayload)
			.ConfigureAwait(false);
	}

	/// <summary>
	/// Reads an entry in the given tenant's scope and asserts it is that tenant's own, by payload.
	/// </summary>
	/// <param name="store">The store under test.</param>
	/// <param name="tenantId">The tenant whose scope the read is performed in.</param>
	/// <param name="messageId">The message id shared by both tenants' entries.</param>
	/// <param name="handlerType">The handler type shared by both tenants' entries.</param>
	/// <param name="expectedPayload">The payload this tenant wrote.</param>
	/// <returns>A task that completes when the entry has been verified.</returns>
	private static async Task AssertEntryPayloadAsync(
		IInboxStore store,
		string tenantId,
		string messageId,
		string handlerType,
		string expectedPayload)
	{
		InboxEntry? entry;
		using (TenantContextHolder.BeginScope(tenantId))
		{
			entry = await store.GetEntryAsync(messageId, handlerType, CancellationToken.None)
				.ConfigureAwait(false);
		}

		if (entry is null)
		{
			throw new TestFixtureAssertionException(
				$"Tenant '{tenantId}' created an entry for message '{messageId}' and handler '{handlerType}' "
				+ "and cannot read it back. A store that returns nothing to anybody satisfies the isolation "
				+ "half of this arm, which is why each tenant's own entry is asserted.");
		}

		var actualPayload = Encoding.UTF8.GetString(entry.Payload);
		if (!string.Equals(actualPayload, expectedPayload, StringComparison.Ordinal))
		{
			throw new TestFixtureAssertionException(
				$"Tenant isolation violated: tenant '{tenantId}' read back the payload '{actualPayload}' for "
				+ $"message '{messageId}', but wrote '{expectedPayload}'. The entry is keyed without a tenant "
				+ "term, so one tenant's message body is disclosed to the other and each acts on the wrong "
				+ "data.");
		}
	}

	#endregion

	#region Concurrency Tests

	/// <summary>Callers released into one race, together.</summary>
	private const int RaceParticipants = 8;

	/// <summary>Independent races the arm runs, each on a key of its own.</summary>
	private const int RaceIterations = 48;

	/// <summary>
	/// Long enough that no lease taken during a race can expire before that race is over.
	/// </summary>
	/// <remarks>
	/// A lease that expired mid-race would make a second claim legitimate, and the arm would then be
	/// asserting against a moving target: it could not tell a store that lost a claim from one whose lease
	/// simply ran out. Pinning the lease well past the race removes that reading entirely.
	/// </remarks>
	private static readonly TimeSpan RaceLeaseDuration = TimeSpan.FromMinutes(5);

	/// <summary>The acquisition each participant in a race attempts.</summary>
	private enum RaceOperation
	{
		/// <summary>Claim by marking processed outright, the idempotent-consumer short path.</summary>
		MarkProcessed,

		/// <summary>Claim for processing, to be finalised by the holder.</summary>
		Claim,

		/// <summary>Claim for processing under a lease, so a dead holder's claim can be reclaimed.</summary>
		LeaseClaim,
	}

	/// <summary>What one participant in a race was asked to do, and what happened to it.</summary>
	private readonly record struct RaceOutcome(RaceOperation Operation, bool Won, Exception? Failure);

	/// <summary>
	/// SAFETY and LIVENESS: concurrent callers racing for one message must elect exactly one winner, and a
	/// processed marker, once set, must survive the race that set it.
	/// </summary>
	/// <remarks>
	/// <para>
	/// This is the property an inbox exists to provide, and it is the only one that cannot be observed one
	/// caller at a time. Every sequential arm in this kit calls a method, waits for it, then calls the next
	/// — the single condition under which no store can fail, because there is never a second caller to lose
	/// to. A duplicate is produced by two callers being told, at the same instant, that each of them is the
	/// one that may proceed.
	/// </para>
	/// <para>
	/// The race deliberately MIXES the acquisition paths rather than repeating one call. A store commonly
	/// makes each operation atomic against copies of itself and leaves the paths unguarded against each
	/// other: one method's read-decide-write straddles another's write, and the second write lands inside
	/// the first method's window. Both callers are then told they hold the message exclusively, and the
	/// marker written by one of them is overwritten by the other, so a later redelivery finds no record and
	/// runs the handler a second time. N identical calls cannot reach that fault — the destruction needs two
	/// DIFFERENT operations interleaving — which is why the participants here are drawn from all of the
	/// acquisition paths the store offers.
	/// </para>
	/// <para>
	/// <b>Exactly one winner</b> is asserted rather than at most one. At-most-one is satisfied perfectly by a
	/// store that refuses every caller, which would deadlock a host on the first message and pass this arm
	/// while doing so. The two lone acquisitions taken before the race, on keys nobody is competing for,
	/// fail such a store deterministically and by a message that names its own cause, so a genuinely refusing
	/// store is never reported as a concurrency failure. After the race the winner's message must also be
	/// processed — the claim holder finalises exactly as a host does when its handler returns — which is what
	/// makes the destroyed-marker case visible as an absence of the very record the winner was promised.
	/// </para>
	/// <para>
	/// <b>What this arm cannot prove.</b> It is sound but not complete. A correct store passes it under every
	/// interleaving, so a failure here is always a real defect and never a scheduling artefact. The converse
	/// does not hold: the arm cannot force an interleaving from outside the store, so a store carrying this
	/// fault passes on any run where the operations happen not to overlap inside the offending window. A
	/// barrier releasing every participant at one instant, and many independent races, make that outcome
	/// progressively less likely — they do not make it impossible. Read a pass here as evidence, not proof;
	/// read a failure as conclusive.
	/// </para>
	/// </remarks>
	public virtual async Task ConcurrentClaimAndMark_MustElectExactlyOneWinner_AndKeepTheProcessedMarker()
	{
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);

		var claimable = store as IClaimableInboxStore
			?? throw new InvalidOperationException(
				$"Conformance test requires {store.GetType().Name} to implement IClaimableInboxStore. "
				+ "Concurrent claiming is the defining property of an inbox: with no claim path there is "
				+ "nothing for a competing processor to lose a race to.");

		await AssertAcquisitionPathsAreLiveAsync(store, claimable).ConfigureAwait(false);

		var leased = AsLeasedStore(store);

		for (var iteration = 1; iteration <= RaceIterations; iteration++)
		{
			var messageId = GenerateMessageId();
			var handlerType = GenerateHandlerType();

			var outcomes = await RunOneRaceAsync(store, claimable, leased, messageId, handlerType)
				.ConfigureAwait(false);

			AssertExactlyOneWinner(outcomes, iteration, messageId, handlerType);

			// The claim paths hand the message to their winner mid-flight; a host finalises it when the
			// handler returns. Doing the same here is what turns "someone won" into "the message is
			// genuinely processable afterwards", and it is the step a destroyed marker cannot survive.
			var winner = Array.Find(outcomes, static o => o.Won);
			if (winner.Operation != RaceOperation.MarkProcessed)
			{
				await store.MarkProcessedAsync(messageId, handlerType, CancellationToken.None)
					.ConfigureAwait(false);
			}

			var processed = await store.IsProcessedAsync(messageId, handlerType, CancellationToken.None)
				.ConfigureAwait(false);

			if (!processed)
			{
				throw new TestFixtureAssertionException(
					$"Race {iteration} of {RaceIterations} on message '{messageId}' for handler "
					+ $"'{handlerType}' elected one winner by {winner.Operation}, and the store then reports "
					+ "the message as NOT processed. The record the winner was promised is gone, so the next "
					+ "redelivery of this message finds nothing and runs the handler again — a duplicate side "
					+ "effect, which is the single outcome an inbox exists to prevent. A losing caller's write "
					+ "landed on top of the winner's: the acquisition paths are each atomic against copies of "
					+ "themselves but not against one another. " + DescribeRace(outcomes));
			}
		}
	}

	/// <summary>
	/// Verifies both acquisition paths grant an uncontested acquisition, before any race is run.
	/// </summary>
	/// <param name="store">The store under test.</param>
	/// <param name="claimable">The same store, through its claim surface.</param>
	/// <returns>A task that completes when both paths have been shown live.</returns>
	/// <remarks>
	/// A store that answers <see langword="false"/> to everyone satisfies every safety assertion a race can
	/// make, because a caller that never wins can never win twice. These two acquisitions are uncontested
	/// and deterministic — no schedule can make them flap — so such a store fails here, with a message that
	/// names refusal rather than leaving it to be misread as a concurrency fault.
	/// </remarks>
	private async Task AssertAcquisitionPathsAreLiveAsync(IInboxStore store, IClaimableInboxStore claimable)
	{
		var claimId = GenerateMessageId();
		var claimHandler = GenerateHandlerType();

		var claimed = await claimable.TryClaimAsync(claimId, claimHandler, CancellationToken.None)
			.ConfigureAwait(false);

		if (!claimed)
		{
			throw new TestFixtureAssertionException(
				$"TryClaimAsync refused message '{claimId}' for handler '{claimHandler}' on a key the store "
				+ "had never seen, with no competing caller. Every message would stall on its first delivery. "
				+ "This is reported before the concurrency assertions because a store that grants no claims "
				+ "cannot lose a race, and would otherwise satisfy them without exercising anything.");
		}

		var markId = GenerateMessageId();
		var markHandler = GenerateHandlerType();

		var marked = await store.TryMarkAsProcessedAsync(markId, markHandler, CancellationToken.None)
			.ConfigureAwait(false);

		if (!marked)
		{
			throw new TestFixtureAssertionException(
				$"TryMarkAsProcessedAsync refused message '{markId}' for handler '{markHandler}' on a key the "
				+ "store had never seen, with no competing caller. Reported separately from the concurrency "
				+ "assertions for the same reason as the claim above: a store that admits no first writer "
				+ "passes a race by never electing anyone.");
		}
	}

	/// <summary>
	/// Determines whether the store implements the optional lease protocol.
	/// </summary>
	/// <param name="store">The store under test.</param>
	/// <returns>The store's lease surface, or <see langword="null"/> when it offers no lease protocol.</returns>
	/// <remarks>
	/// The lease protocol is a separate interface, so support is a static fact about the type rather than
	/// something discovered by calling and seeing what happens. This matters: the previous form probed by
	/// invoking the operation and catching the exception a non-supporting store threw, which certified a
	/// store against a crash and asked the same question — in the same way — that the dispatch path was
	/// asking when it selected a protocol the store did not implement.
	/// <para>
	/// A store that declares the interface is held to every lease arm, and failing one is a failure, not a
	/// decline. A store that does not declare it is raced on the claim path it does offer, and does not
	/// certify as leased: every store is exercised on every acquisition path it actually has, and none is
	/// skipped.
	/// </para>
	/// </remarks>
	private static ILeasedInboxStore? AsLeasedStore(IInboxStore store) => store as ILeasedInboxStore;

	/// <summary>
	/// Fails a store that reports a lease capability it has not declared.
	/// </summary>
	/// <param name="store">The store under test.</param>
	/// <remarks>
	/// <para>
	/// The three lease arms return without asserting anything when the store declares no lease surface,
	/// which is right — the protocol is optional and there is no lease behaviour to hold it to. It is also,
	/// on its own, indistinguishable in the results from three arms that passed. "The arms did not run"
	/// and "the store is not certified as leased" are two properties; returning delivers only the first.
	/// </para>
	/// <para>
	/// The second is delivered here. A store advertises the lease protocol through
	/// <see cref="IInboxStoreCapabilities.SupportsLeasedClaim"/> — the EFFECTIVE capability a host reads,
	/// deliberately preferred over the declared interface so a decorator answers for the store it actually
	/// wraps. A store answering <see langword="true"/> there while implementing no lease surface claims a
	/// protocol nothing in it can execute, and the only artifact that would have contradicted it is this
	/// kit, quietly returning three times.
	/// </para>
	/// <para>
	/// A store that implements no capability surface makes no such claim, so there is nothing to
	/// contradict and this passes: silence is not an advertisement.
	/// </para>
	/// </remarks>
	private static void AssertLeaseProtocolIsNotAdvertised(IInboxStore store)
	{
		if (store is not IInboxStoreCapabilities { SupportsLeasedClaim: true })
		{
			return;
		}

		throw new TestFixtureAssertionException(
			$"{store.GetType().Name} reports SupportsLeasedClaim = true and implements no ILeasedInboxStore, "
			+ "so it advertises a protocol it cannot execute. That capability is the one a host reads: a "
			+ "startup guard requiring the lease protocol admits this store, and the dispatch path then "
			+ "finds no lease surface to call and takes the claim that never expires instead. A processor "
			+ "that dies holding one of those claims strands its message permanently — the single outcome "
			+ "the lease protocol exists to prevent, and the host was told it had that protection. Either "
			+ "implement ILeasedInboxStore or report SupportsLeasedClaim = false: a caller can act on only "
			+ "one of the two answers, so they must not disagree.");
	}

	/// <summary>
	/// Fails a store that advertises durable processing tracking while implementing none.
	/// </summary>
	/// <param name="store">The store under test.</param>
	/// <remarks>
	/// The sibling of <see cref="AssertLeaseProtocolIsNotAdvertised"/>, for the capability the
	/// demotion arm gates on. A store answering <see langword="true"/> to
	/// <see cref="IInboxStoreCapabilities.SupportsProcessingTracking"/> without implementing
	/// <see cref="IProcessingTrackingInboxStore"/> tells a host the in-flight status is durable when
	/// nothing writes it, which turns the at-most-once concurrency guard and the stuck-processing timeout
	/// into dead code that reads a status no second consumer can ever observe. Silence is not an
	/// advertisement: a store implementing no capability surface passes.
	/// </remarks>
	private static void AssertProcessingTrackingIsNotAdvertised(IInboxStore store)
	{
		if (store is not IInboxStoreCapabilities { SupportsProcessingTracking: true })
		{
			return;
		}

		throw new TestFixtureAssertionException(
			$"{store.GetType().Name} reports SupportsProcessingTracking = true and implements no "
			+ "IProcessingTrackingInboxStore, so it advertises a capability it cannot execute. That "
			+ "capability is the one a host reads: it is admitted by a startup guard requiring durable "
			+ "processing tracking, and the dispatch path then finds no surface to call, leaving the "
			+ "Processing status in memory where no competing consumer can see it. Either implement "
			+ "IProcessingTrackingInboxStore or report SupportsProcessingTracking = false: a caller can "
			+ "act on only one of the two answers, so they must not disagree.");
	}

	/// <summary>
	/// Runs one race: every participant issued against a single key, released together.
	/// </summary>
	/// <param name="store">The store under test.</param>
	/// <param name="claimable">The same store, through its claim surface.</param>
	/// <param name="messageId">The contested message identifier.</param>
	/// <param name="handlerType">The contested handler type.</param>
	/// <param name="leased">The store's lease surface, or <see langword="null"/> when it offers none.</param>
	/// <returns>What each participant was asked to do and what happened to it, in slot order.</returns>
	private static async Task<RaceOutcome[]> RunOneRaceAsync(
		IInboxStore store,
		IClaimableInboxStore claimable,
		ILeasedInboxStore? leased,
		string messageId,
		string handlerType)
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
			var operation = SelectRaceOperation(index, leased is not null);

			racers[index] = Task.Factory.StartNew(
				async () =>
				{
					// Parked until the last participant arrives, so the calls are issued at one instant
					// instead of in whatever order the threads happened to start.
					gate.SignalAndWait();

					try
					{
						var won = operation switch
						{
							RaceOperation.MarkProcessed => await store
								.TryMarkAsProcessedAsync(messageId, handlerType, CancellationToken.None)
								.ConfigureAwait(false),
							RaceOperation.LeaseClaim => await leased!
								.TryAcquireLeaseAsync(messageId, handlerType, RaceLeaseDuration, CancellationToken.None)
								.ConfigureAwait(false) is not null,
							_ => await claimable
								.TryClaimAsync(messageId, handlerType, CancellationToken.None)
								.ConfigureAwait(false),
						};

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
	/// Spreads the participants across every acquisition path the store offers.
	/// </summary>
	/// <param name="slot">The participant's position in the race.</param>
	/// <param name="leaseSupported">Whether the lease claim is implemented by the store.</param>
	/// <returns>The operation this participant attempts.</returns>
	private static RaceOperation SelectRaceOperation(int slot, bool leaseSupported) => (slot % 3) switch
	{
		0 => RaceOperation.MarkProcessed,
		1 => leaseSupported ? RaceOperation.LeaseClaim : RaceOperation.Claim,
		_ => RaceOperation.Claim,
	};

	/// <summary>
	/// Fails unless exactly one participant was told it holds the message.
	/// </summary>
	/// <param name="outcomes">Every participant's result, in slot order.</param>
	/// <param name="iteration">Which race this was.</param>
	/// <param name="messageId">The contested message identifier.</param>
	/// <param name="handlerType">The contested handler type.</param>
	private static void AssertExactlyOneWinner(
		RaceOutcome[] outcomes,
		int iteration,
		string messageId,
		string handlerType)
	{
		var thrown = Array.FindAll(outcomes, static o => o.Failure is not null);

		if (thrown.Length > 0)
		{
			throw new TestFixtureAssertionException(
				$"Race {iteration} of {RaceIterations} on message '{messageId}' for handler '{handlerType}': "
				+ $"{thrown.Length} of {RaceParticipants} concurrent callers threw. Losing a race is a return "
				+ "value, not an exception: a caller that loses must be told so, because a throwing loser is "
				+ "indistinguishable to the host from a store outage and is retried as one. "
				+ DescribeRace(outcomes));
		}

		var winners = Array.FindAll(outcomes, static o => o.Won).Length;

		if (winners > 1)
		{
			throw new TestFixtureAssertionException(
				$"Race {iteration} of {RaceIterations} on message '{messageId}' for handler '{handlerType}' "
				+ $"told {winners} of {RaceParticipants} concurrent callers that each of them holds the "
				+ "message. Every one of them proceeds to run the handler, so this message produces "
				+ $"{winners} sets of side effects instead of one — the duplicate execution an inbox exists "
				+ "to prevent. The acquisition paths are atomic against copies of themselves but not against "
				+ "one another: one path's read-decide-write straddles another path's write. Make the "
				+ "decision and the write a single atomic step against EVERY path that mutates the record, "
				+ "not just against the same method running twice. " + DescribeRace(outcomes));
		}

		if (winners == 0)
		{
			throw new TestFixtureAssertionException(
				$"Race {iteration} of {RaceIterations} on message '{messageId}' for handler '{handlerType}' "
				+ $"refused all {RaceParticipants} concurrent callers, so no processor holds it and none will "
				+ "retry: the message stalls forever. Contention must elect a winner, not annul the round. "
				+ "Note the uncontested acquisitions taken before this race both succeeded, so the store does "
				+ "grant acquisitions — it loses the winner specifically under contention. "
				+ DescribeRace(outcomes));
		}
	}

	/// <summary>
	/// Renders what every participant was asked to do and what it was told.
	/// </summary>
	/// <param name="outcomes">Every participant's result, in slot order.</param>
	/// <returns>A one-line account of the race.</returns>
	/// <remarks>
	/// A count alone names the symptom and discards the evidence. Which operations were in flight together,
	/// and which of them were told they had won, is the whole of what distinguishes one unguarded pair of
	/// paths from another, and it cannot be recovered by re-running: the next run interleaves differently.
	/// </remarks>
	private static string DescribeRace(RaceOutcome[] outcomes) =>
		"Every caller in this race, in slot order: " + string.Join("; ", outcomes.Select(DescribeOutcome)) + ".";

	/// <summary>
	/// Renders one participant's operation and result.
	/// </summary>
	/// <param name="outcome">The participant's result.</param>
	/// <param name="slot">The participant's position in the race.</param>
	/// <returns>A short account of that participant.</returns>
	private static string DescribeOutcome(RaceOutcome outcome, int slot)
	{
		var result = outcome.Failure is not null
			? $"THREW {outcome.Failure.GetType().Name}: {outcome.Failure.Message}"
			: outcome.Won ? "WON" : "lost";

		return FormattableString.Invariant($"[{slot}] {outcome.Operation} -> {result}");
	}

	#endregion

	#region Claim Release Tests

	/// <summary>
	/// LIVENESS: releasing a claim must re-admit the message, so a redelivery can be processed.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Release is the failure half of the claim protocol. The handler runs after the claim is taken, and
	/// when it throws the claim has to go back so the transport's redelivery is admitted rather than
	/// swallowed as a duplicate. A release that does not actually remove the claim turns every handler
	/// failure into a silent message loss: the entry stays non-terminal forever, every redelivery is
	/// refused as already-claimed, and nothing anywhere reports an error.
	/// </para>
	/// <para>
	/// The claim is proved held before it is released - a second claim on the same key must be refused -
	/// so the assertion after the release cannot pass on a store whose claim never took hold in the first
	/// place.
	/// </para>
	/// </remarks>
	public virtual async Task ReleasedClaim_MustBeReadmittedForRedelivery()
	{
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);
		var claimable = RequireClaimableStore(store);

		var messageId = GenerateMessageId();
		var handlerType = GenerateHandlerType();

		var held = await claimable
			.TryClaimAsync(messageId, handlerType, CancellationToken.None)
			.ConfigureAwait(false);

		if (!held)
		{
			throw new TestFixtureAssertionException(
				$"The claim path refused message '{messageId}' for handler '{handlerType}' on a key the store "
				+ "had never seen, with no competing caller, so this arm never established the claim it exists "
				+ "to release.");
		}

		var duplicate = await claimable
			.TryClaimAsync(messageId, handlerType, CancellationToken.None)
			.ConfigureAwait(false);

		if (duplicate)
		{
			throw new TestFixtureAssertionException(
				$"Message '{messageId}' for handler '{handlerType}' was granted to a second caller while the "
				+ "first claim was still held, so the claim never took hold and the release assertion below "
				+ "would pass over a store that admits everything.");
		}

		await claimable.ReleaseAsync(messageId, handlerType, CancellationToken.None).ConfigureAwait(false);

		var readmitted = await claimable
			.TryClaimAsync(messageId, handlerType, CancellationToken.None)
			.ConfigureAwait(false);

		if (!readmitted)
		{
			throw new TestFixtureAssertionException(
				$"The claim on message '{messageId}' for handler '{handlerType}' was released and the store "
				+ "still refuses to re-admit it. That is the handler-failure path: the claim was taken, the "
				+ "handler threw, the claim was released so a redelivery could be processed - and the "
				+ "redelivery is now rejected as a duplicate of work that never happened. The message is lost "
				+ "with no error raised anywhere. Release must remove the non-terminal entry, not merely mark "
				+ "it.");
		}
	}

	/// <summary>
	/// SAFETY: releasing what this caller does not hold must change nothing, and must never erase a
	/// finalized record.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Release carries no owner token - the claim's identity is the <c>(messageId, handlerType)</c> key
	/// itself - so the store cannot tell a releasing owner from any other caller presenting the same key.
	/// What separates them is the state of the entry, and there are two states in which a release must do
	/// nothing at all.
	/// </para>
	/// <para>
	/// The first is benign: an entry that was never claimed, or was already released, has nothing to
	/// remove. Release is specified as a no-op there, so a store that throws instead turns an ordinary
	/// double-release on a retry path into a failure of its own making.
	/// </para>
	/// <para>
	/// The second is not benign, and it is the reason this arm exists. A caller whose own claim lapsed can
	/// arrive here after a second processor has taken the message over and finalized it. Deleting the
	/// entry then erases the record that the message really was processed, and the next delivery is
	/// admitted and handled a second time - every side effect repeated, with no duplicate visible to
	/// anyone. A late release must leave a terminal entry exactly where it is.
	/// </para>
	/// </remarks>
	public virtual async Task Release_MustNoOpOnAnUnheldClaim_AndMustNotEraseAFinalizedRecord()
	{
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);
		var claimable = RequireClaimableStore(store);

		// 1. A key the store has never seen. Releasing it is specified as a no-op: it must not throw, and
		// it must leave the key claimable.
		var unheldId = GenerateMessageId();
		var unheldHandler = GenerateHandlerType();

		try
		{
			await claimable
				.ReleaseAsync(unheldId, unheldHandler, CancellationToken.None)
				.ConfigureAwait(false);
		}
		catch (Exception ex) when (ex is not TestFixtureAssertionException)
		{
			throw new TestFixtureAssertionException(
				$"Releasing message '{unheldId}' for handler '{unheldHandler}', which was never claimed, threw "
				+ $"{ex.GetType().Name}. Release of an entry that is not held is a no-op by contract: a "
				+ "handler's failure path calls it without knowing whether its claim still exists, so throwing "
				+ "here converts a recovered failure into a second one.",
				ex);
		}

		var stillClaimable = await claimable
			.TryClaimAsync(unheldId, unheldHandler, CancellationToken.None)
			.ConfigureAwait(false);

		if (!stillClaimable)
		{
			throw new TestFixtureAssertionException(
				$"Releasing the never-claimed key '{unheldId}' / '{unheldHandler}' left it unclaimable, so the "
				+ "release wrote state rather than doing nothing. A release that creates a record blocks the "
				+ "very message it was meant to re-admit.");
		}

		// 2. The claim taken above, released twice. The second call has nothing left to remove and must
		// behave exactly like the first - a failure path that is itself retried calls it more than once.
		await claimable.ReleaseAsync(unheldId, unheldHandler, CancellationToken.None).ConfigureAwait(false);
		await claimable.ReleaseAsync(unheldId, unheldHandler, CancellationToken.None).ConfigureAwait(false);

		var reclaimableAfterDoubleRelease = await claimable
			.TryClaimAsync(unheldId, unheldHandler, CancellationToken.None)
			.ConfigureAwait(false);

		if (!reclaimableAfterDoubleRelease)
		{
			throw new TestFixtureAssertionException(
				$"Message '{unheldId}' for handler '{unheldHandler}' was claimed and then released twice, and "
				+ "the store now refuses to re-admit it. Release is idempotent by contract; a second call that "
				+ "leaves the key blocked loses the message on any path that retries the release.");
		}

		// 3. The finalized record. This is the release that must do nothing.
		var processedId = GenerateMessageId();
		var processedHandler = GenerateHandlerType();

		var claimedForProcessing = await claimable
			.TryClaimAsync(processedId, processedHandler, CancellationToken.None)
			.ConfigureAwait(false);

		if (!claimedForProcessing)
		{
			throw new TestFixtureAssertionException(
				$"The claim path refused message '{processedId}' for handler '{processedHandler}' on a key the "
				+ "store had never seen, so the finalized-record assertion below was never set up.");
		}

		await store.MarkProcessedAsync(processedId, processedHandler, CancellationToken.None)
			.ConfigureAwait(false);

		// The lapsed claimant's late release, arriving after a replacement processor finalized the entry.
		await claimable.ReleaseAsync(processedId, processedHandler, CancellationToken.None)
			.ConfigureAwait(false);

		var stillProcessed = await store
			.IsProcessedAsync(processedId, processedHandler, CancellationToken.None)
			.ConfigureAwait(false);

		if (!stillProcessed)
		{
			throw new TestFixtureAssertionException(
				$"Message '{processedId}' for handler '{processedHandler}' was finalized as processed and a "
				+ "later release erased the record. The caller whose claim lapsed has just deleted the proof "
				+ "that a replacement processor handled the message; the next delivery is admitted and the "
				+ "handler runs a second time, repeating every side effect it has. Restrict the removal to "
				+ "non-terminal entries, so a release can never reach a finalized one.");
		}

		var readmittedAfterProcessing = await claimable
			.TryClaimAsync(processedId, processedHandler, CancellationToken.None)
			.ConfigureAwait(false);

		if (readmittedAfterProcessing)
		{
			throw new TestFixtureAssertionException(
				$"Message '{processedId}' for handler '{processedHandler}' was finalized as processed, then "
				+ "released, and the claim path admitted it again. Whatever the processed marker still reports, "
				+ "the entry a caller actually races for is gone, so a redelivery is handled a second time.");
		}
	}

	#endregion

	#region Lease Reclaim Tests

	/// <summary>
	/// A lease short enough that an arm can outlast it, taken when the arm intends it to expire.
	/// </summary>
	private static readonly TimeSpan ReclaimableLease = TimeSpan.FromMilliseconds(250);

	/// <summary>
	/// A lease far longer than any arm's runtime, taken when the arm intends it to still be held at the end.
	/// </summary>
	private static readonly TimeSpan LiveLease = TimeSpan.FromMinutes(5);

	/// <summary>
	/// How long the liveness arm keeps offering an expired lease to a reclaimer before reporting it stuck.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Generous on purpose. The expiry is decided by the store's own clock, which for every remote provider
	/// is a different clock from the one this process waits on, so the exact instant a lease becomes
	/// reclaimable is not knowable from here. A deadline three orders of magnitude above the lease turns
	/// that unknown into a wait rather than into a flake, and still fails a store that never reclaims at all.
	/// </para>
	/// <para>
	/// Overridable so a store whose clock advances coarsely can raise it. Lower it only in a harness that
	/// drives the clock: shortening it against a real provider trades a false failure for the wait it was
	/// sized to absorb.
	/// </para>
	/// </remarks>
	protected virtual TimeSpan LeaseReclaimDeadline => TimeSpan.FromSeconds(30);

	/// <summary>Gap between reclaim attempts while the store's clock passes the expiry.</summary>
	private static readonly TimeSpan ReclaimPollInterval = TimeSpan.FromMilliseconds(25);

	/// <summary>
	/// Advances past the expiry of a lease of <paramref name="leaseDuration"/>, from the point of view of
	/// whichever clock the store under test evaluates its expiry against.
	/// </summary>
	/// <param name="leaseDuration">The lease whose expiry must be passed.</param>
	/// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
	/// <returns>A task that completes once the lease can be considered expired.</returns>
	/// <remarks>
	/// <para>
	/// Override this when the store's clock can be driven. A store constructed with a controllable
	/// <see cref="TimeProvider"/> should advance it here and return immediately: the reclaim then happens at
	/// a decided instant instead of a waited one, which is both faster and not a function of machine load.
	/// </para>
	/// <para>
	/// The default waits, because for most providers there is nothing to drive. A lease expiry is required
	/// to be evaluated against the store's own server clock — the only clock that is single across the
	/// processors competing for a message — so a store backed by a database, cache or document service has
	/// no app-side clock this kit could advance. Waiting out a deliberately short lease is the only
	/// mechanism available to those providers, and the arm below pairs it with a bounded retry rather than
	/// a single timed assertion, so the wait supplies the minimum and the retry absorbs the skew.
	/// </para>
	/// </remarks>
	protected virtual Task ExpireLeaseAsync(TimeSpan leaseDuration, CancellationToken cancellationToken) =>
		Task.Delay(leaseDuration, cancellationToken);

	/// <summary>
	/// LIVENESS: a lease that has genuinely expired must be reclaimable by another processor.
	/// </summary>
	/// <remarks>
	/// <para>
	/// This is the half of the lease contract that no safety assertion can reach. Every other claim
	/// assertion in this kit is satisfied by a store that refuses, because a caller that is never granted a
	/// message can never be granted it twice. Tighten a reclaim predicate until it always answers no and
	/// the concurrency arm stays green while the store quietly stops handing out expired leases: the
	/// processor that died holding a claim is never replaced, its message is never retried, and the
	/// backlog behind it grows without a single failing assertion anywhere.
	/// </para>
	/// <para>
	/// The claim is taken for a short lease and reclaimed after it expires. What is asserted is the
	/// property — that an expired lease becomes reclaimable — never the instant at which it does. The
	/// reclaim is retried until a deadline far above the lease, so a slow machine, a loaded container or a
	/// server clock a little behind this process changes only how many attempts are made, not the verdict.
	/// A store that has genuinely stopped reclaiming fails here no matter how long it is given.
	/// </para>
	/// </remarks>
	public virtual async Task ExpiredLease_MustBeReclaimableByAnotherProcessor()
	{
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);
		var claimable = RequireClaimableStore(store);

		if (AsLeasedStore(store) is not { } leased)
		{
			// NOT an empty pass, though it is the shape of one. Reaching here means the store does not
			// declare the lease interface at all — a static fact about the type, not an outcome observed
			// by calling something and seeing it fail. A store that DOES declare it cannot arrive here, so
			// it is held to every assertion below. A store with no lease path has no lease behaviour to
			// assert, and asserting one anyway would pin a requirement the contract makes optional.
			//
			// Not running the arms and not certifying as leased are two separate properties, and only the
			// first of them is delivered by returning. The next line is the second one; without it this
			// branch asserts nothing and the paragraph above is a comment claiming to be a guarantee.
			SkipArm(
				nameof(ExpiredLease_MustBeReclaimableByAnotherProcessor),
				typeof(ILeasedInboxStore),
				"the store does not implement the optional lease protocol, so there is no lease "
				+ "behaviour to hold it to. Nothing here was verified about lease reclaim.");
			AssertLeaseProtocolIsNotAdvertised(store);
			return;
		}

		var messageId = GenerateMessageId();
		var handlerType = GenerateHandlerType();

		var held = await leased
			.TryAcquireLeaseAsync(messageId, handlerType, ReclaimableLease, CancellationToken.None)
			.ConfigureAwait(false);

		if (held is null)
		{
			throw new TestFixtureAssertionException(
				$"The lease claim refused message '{messageId}' for handler '{handlerType}' on a key the "
				+ "store had never seen, with no competing caller, so there is no lease for this arm to "
				+ "expire. Reported separately from the reclaim assertion below because a store that grants "
				+ "no lease at all fails for a different reason, and in a different place, than one that "
				+ "grants leases but will not reclaim them.");
		}

		await ExpireLeaseAsync(ReclaimableLease, CancellationToken.None).ConfigureAwait(false);

		var elapsed = Excalibur.Dispatch.Diagnostics.ValueStopwatch.StartNew();
		var attempts = 0;

		while (true)
		{
			attempts++;

			var reclaimed = await leased
				.TryAcquireLeaseAsync(messageId, handlerType, LiveLease, CancellationToken.None)
				.ConfigureAwait(false);

			if (reclaimed is not null)
			{
				return;
			}

			if (elapsed.Elapsed >= LeaseReclaimDeadline)
			{
				throw new TestFixtureAssertionException(
					$"The lease on message '{messageId}' for handler '{handlerType}' was taken for "
					+ $"{ReclaimableLease.TotalMilliseconds}ms and then refused to {attempts} reclaim "
					+ $"attempts over {elapsed.Elapsed.TotalSeconds:F1}s — more than a hundred times the "
					+ "lease. The entry is left holding a claim that can never be taken from it, which is "
					+ "what a processor that died mid-handler leaves behind. That message is now stuck "
					+ "forever: no processor holds it, none can take it, and nothing retries it. Every "
					+ "safety assertion in this kit still passes in this state, because a store that grants "
					+ "nothing can never grant anything twice — so this is the only arm that fails. The "
					+ "reclaim predicate admits no expired lease: check that it compares the stored expiry "
					+ "against the store's own clock, and that the comparison can actually be satisfied.");
			}

			await Task.Delay(ReclaimPollInterval, CancellationToken.None).ConfigureAwait(false);
		}
	}

	/// <summary>
	/// SAFETY: a lease still inside its window must NOT be reclaimable by another processor.
	/// </summary>
	/// <remarks>
	/// The bound on the liveness arm above. Reclaiming is a store's answer to a processor that died holding
	/// a claim, and a store that cannot tell that case from a processor that is merely still working hands
	/// the message to a second processor while the first one's handler is running. Both then produce the
	/// message's side effects. This arm and the one above fail under opposite mutations of the same
	/// predicate, which is why neither is sufficient alone: loosen it and this fails, tighten it and the
	/// other does.
	/// </remarks>
	public virtual async Task LiveLease_MustNotBeReclaimableByAnotherProcessor()
	{
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);
		var claimable = RequireClaimableStore(store);

		if (AsLeasedStore(store) is not { } leased)
		{
			// The store does not declare the optional lease interface; see the note on the liveness arm
			// above. The arms do not run, and the store does not certify as leased either.
			SkipArm(
				nameof(LiveLease_MustNotBeReclaimableByAnotherProcessor),
				typeof(ILeasedInboxStore),
				"the store does not implement the optional lease protocol, so there is no lease "
				+ "behaviour to hold it to. Nothing here was verified about lease reclaim.");
			AssertLeaseProtocolIsNotAdvertised(store);
			return;
		}

		var messageId = GenerateMessageId();
		var handlerType = GenerateHandlerType();

		var held = await leased
			.TryAcquireLeaseAsync(messageId, handlerType, LiveLease, CancellationToken.None)
			.ConfigureAwait(false);

		if (held is null)
		{
			throw new TestFixtureAssertionException(
				$"The lease claim refused message '{messageId}' for handler '{handlerType}' on a key the "
				+ "store had never seen, with no competing caller, so this arm never established the live "
				+ "lease it exists to defend. Without that, a store which grants nothing would pass this "
				+ "arm by having no lease to steal.");
		}

		var stolen = await leased
			.TryAcquireLeaseAsync(messageId, handlerType, LiveLease, CancellationToken.None)
			.ConfigureAwait(false);

		if (stolen is not null)
		{
			throw new TestFixtureAssertionException(
				$"Message '{messageId}' for handler '{handlerType}' was claimed under a "
				+ $"{LiveLease.TotalMinutes}-minute lease and then granted again, immediately, to a second "
				+ "caller. The first holder's handler is still running: both processors now run it, and the "
				+ "message produces two sets of side effects — the duplicate execution an inbox exists to "
				+ "prevent. A lease is reclaimable only once it has EXPIRED, and this one had barely "
				+ "started. Check that the reclaim predicate compares the stored expiry against the store's "
				+ "own clock rather than treating any Processing entry as reclaimable.");
		}
	}

	/// <summary>
	/// LIVENESS: the retry drain's read MUST return a Processing entry whose lease has expired.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The reclaim arms above prove the store will GRANT a lease over a dead processor's entry. They say
	/// nothing about whether the retry drain ever ASKS, and it only asks about entries its read returned.
	/// A store that reclaims correctly but reads only <see cref="InboxStatus.Failed"/> therefore strands
	/// exactly the entry reclaim exists to rescue: the drain leases an entry, moves it to
	/// <see cref="InboxStatus.Processing"/>, dies mid-handler, and no later pass ever selects it again.
	/// The message is reachable only by a redelivery, and a message already consumed off the transport
	/// never gets one.
	/// </para>
	/// <para>
	/// This is the arm that makes the drain's cross-instance fence safe to switch on at all. Without it,
	/// leasing the drain trades a bounded duplicate dispatch for permanent silent loss — the worse of the
	/// two failures. It is paired with the safety arm below, and neither is sufficient alone: a store that
	/// returns every Processing entry passes this one and fails that one.
	/// </para>
	/// </remarks>
	public virtual async Task ExpiredLease_MustBeReadmittedByTheRetryDrainRead()
	{
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);

		if (AsLeasedStore(store) is not { } leased)
		{
			SkipArm(
				nameof(ExpiredLease_MustBeReadmittedByTheRetryDrainRead),
				typeof(ILeasedInboxStore),
				"the store does not implement the optional lease protocol, so its drain read is required to "
				+ "return Failed entries only and there is no expired-lease admission to hold it to. "
				+ "Nothing here was verified about the drain read.");
			AssertLeaseProtocolIsNotAdvertised(store);
			return;
		}

		var messageId = GenerateMessageId();
		var handlerType = GenerateHandlerType();

		var held = await leased
			.TryAcquireLeaseAsync(messageId, handlerType, ReclaimableLease, CancellationToken.None)
			.ConfigureAwait(false);

		if (held is null)
		{
			throw new TestFixtureAssertionException(
				$"The lease claim refused message '{messageId}' for handler '{handlerType}' on a key the "
				+ "store had never seen, with no competing caller, so this arm never created the abandoned "
				+ "Processing entry it exists to rescue. Reported separately from the read assertion below "
				+ "because a store that grants no lease fails for a different reason than one that grants "
				+ "leases and then cannot see the expired ones.");
		}

		await ExpireLeaseAsync(ReclaimableLease, CancellationToken.None).ConfigureAwait(false);

		var elapsed = Excalibur.Dispatch.Diagnostics.ValueStopwatch.StartNew();
		var attempts = 0;

		while (true)
		{
			attempts++;

			var entries = (await CreateAdminStore(store).GetAllTenantsFailedEntriesAsync(
				maxRetries: 10,
				olderThan: null,
				batchSize: 100,
				CancellationToken.None).ConfigureAwait(false)).ToList();

			if (entries.Exists(e => string.Equals(e.MessageId, messageId, StringComparison.Ordinal)))
			{
				return;
			}

			if (elapsed.Elapsed >= LeaseReclaimDeadline)
			{
				throw new TestFixtureAssertionException(
					$"Message '{messageId}' for handler '{handlerType}' was left Processing under a "
					+ $"{ReclaimableLease.TotalMilliseconds}ms lease that has long since expired, and the "
					+ $"retry drain's read did not return it in {attempts} attempts over "
					+ $"{elapsed.Elapsed.TotalSeconds:F1}s. This is the abandoned entry a dead processor "
					+ "leaves behind, and this read is the only thing that ever offers it to a retry. Every "
					+ "other arm in this kit still passes in this state — reclaim works, the fence works — "
					+ "because they all test whether a lease CAN be taken, never whether the drain is told "
					+ "the entry exists. The message is now permanently lost: no processor holds it, and "
					+ "nothing will select it again. The read must admit Processing entries whose recorded "
					+ "expiry is earlier than the store's own clock, alongside Failed ones.");
			}

			await Task.Delay(ReclaimPollInterval, CancellationToken.None).ConfigureAwait(false);
		}
	}

	/// <summary>
	/// SAFETY: the retry drain's read must NOT return a Processing entry whose lease is still live.
	/// </summary>
	/// <remarks>
	/// The bound on the liveness arm above, and the reason the pair is required. Widening the drain's read
	/// to "any Processing entry" satisfies the liveness arm completely while handing the drain every entry
	/// a healthy processor is at that moment working on. The drain's own lease acquisition would refuse
	/// most of those, so the damage is not certain — but the read would be offering live work as though it
	/// were abandoned, and the fence is the last line rather than the only one. The two arms fail under
	/// opposite mutations of one predicate: loosen it and this fails, tighten it and the other does.
	/// </remarks>
	public virtual async Task LiveLease_MustNotBeReadmittedByTheRetryDrainRead()
	{
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);

		if (AsLeasedStore(store) is not { } leased)
		{
			SkipArm(
				nameof(LiveLease_MustNotBeReadmittedByTheRetryDrainRead),
				typeof(ILeasedInboxStore),
				"the store does not implement the optional lease protocol, so there is no live lease for "
				+ "its drain read to exclude. Nothing here was verified about the drain read.");
			AssertLeaseProtocolIsNotAdvertised(store);
			return;
		}

		var messageId = GenerateMessageId();
		var handlerType = GenerateHandlerType();

		var held = await leased
			.TryAcquireLeaseAsync(messageId, handlerType, LiveLease, CancellationToken.None)
			.ConfigureAwait(false);

		if (held is null)
		{
			throw new TestFixtureAssertionException(
				$"The lease claim refused message '{messageId}' for handler '{handlerType}' on a key the "
				+ "store had never seen, with no competing caller, so this arm never established the live "
				+ "lease it exists to defend. Without it, a store whose drain read returns nothing at all "
				+ "would pass by having no entry to wrongly return.");
		}

		var entries = (await CreateAdminStore(store).GetAllTenantsFailedEntriesAsync(
			maxRetries: 10,
			olderThan: null,
			batchSize: 100,
			CancellationToken.None).ConfigureAwait(false)).ToList();

		if (entries.Exists(e => string.Equals(e.MessageId, messageId, StringComparison.Ordinal)))
		{
			throw new TestFixtureAssertionException(
				$"Message '{messageId}' for handler '{handlerType}' is held under a "
				+ $"{LiveLease.TotalMinutes}-minute lease that has barely started, and the retry drain's "
				+ "read offered it for retry anyway. The holder's handler is still running. The read is "
				+ "treating any Processing entry as abandoned rather than comparing the recorded expiry "
				+ "against the store's own clock, which turns every in-flight message in the estate into a "
				+ "retry candidate on every pass.");
		}
	}

	/// <summary>
	/// SAFETY: a claim taken through the lease-less overload must NEVER be reclaimable by the lease path.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The two-argument claim records no expiry, because it has none: the contract states it never
	/// auto-expires and that the caller governs its lifetime instead. So a store holding such a claim has
	/// no expiry to compare, and the lease path's reclaim predicate must therefore refuse it outright and
	/// permanently — not compare the absent value against the clock and act on whatever that yields.
	/// </para>
	/// <para>
	/// This is the cell that fails silently, and it is worth being precise about why: the two paths are
	/// each correct in isolation. The lease path reclaims expired leases, which is right; the lease-less
	/// path takes a claim with no expiry, which is also right. The defect only exists where they meet, so
	/// no arm that exercises one path alone can see it — and an absent value is not a value a test
	/// naturally supplies, which is how it survives a suite that otherwise covers both. What it produces
	/// is not a race: it is a wrong answer, reached the same way every time, that no amount of atomicity
	/// prevents. A caller holding a perfectly valid claim has its message handed to someone else while its
	/// handler runs.
	/// </para>
	/// </remarks>
	public virtual async Task LeaselessClaim_MustNotBeReclaimableByTheLeasePath()
	{
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);
		var claimable = RequireClaimableStore(store);

		if (AsLeasedStore(store) is not { } leased)
		{
			// The store does not declare the optional lease interface; see the note on the liveness arm
			// above. The arms do not run, and the store does not certify as leased either.
			SkipArm(
				nameof(LeaselessClaim_MustNotBeReclaimableByTheLeasePath),
				typeof(ILeasedInboxStore),
				"the store does not implement the optional lease protocol, so there is no lease "
				+ "behaviour to hold it to. Nothing here was verified about lease reclaim.");
			AssertLeaseProtocolIsNotAdvertised(store);
			return;
		}

		var messageId = GenerateMessageId();
		var handlerType = GenerateHandlerType();

		// The LEASE-LESS overload: this records no expiry anywhere, which is the whole point of the arm.
		// It is IClaimableInboxStore.TryClaimAsync, which still returns bool -- the lease protocol's term
		// does not apply here, and this claim never auto-expires, so there is no lapsed caller to fence.
		var held = await claimable
			.TryClaimAsync(messageId, handlerType, CancellationToken.None)
			.ConfigureAwait(false);

		if (!held)
		{
			throw new TestFixtureAssertionException(
				$"The lease-less claim refused message '{messageId}' for handler '{handlerType}' on a key "
				+ "the store had never seen, with no competing caller, so this arm never established the "
				+ "claim it exists to defend.");
		}

		var stolen = await leased
			.TryAcquireLeaseAsync(messageId, handlerType, LiveLease, CancellationToken.None)
			.ConfigureAwait(false);

		if (stolen is not null)
		{
			throw new TestFixtureAssertionException(
				$"Message '{messageId}' for handler '{handlerType}' was claimed through the two-argument "
				+ "overload — which records NO lease expiry, by contract — and the lease path then granted "
				+ "the same message to a second caller anyway. The first holder's handler is still running, "
				+ "so the message now produces two sets of side effects. The reclaim predicate read the "
				+ "ABSENCE of an expiry as an expiry in the past. Absent is not expired: it means this "
				+ "claim never expires and the caller governs its lifetime, so the lease path must refuse "
				+ "it permanently. Require a real expiry value to be present BEFORE comparing it against "
				+ "the clock — a comparison that orders a missing value below every real one will place it "
				+ "infinitely far in the past and reclaim a claim that is perfectly live.");
		}
	}

	/// <summary>
	/// Verifies that a terminal Processed entry is not re-admitted by the claim path.
	/// </summary>
	/// <returns>A task representing the arm.</returns>
	/// <remarks>
	/// <para>
	/// Terminal-ness was proven only through the mark path -- a second
	/// <see cref="IInboxStore.TryMarkAsProcessedAsync"/> returning <see langword="false"/>. That is a
	/// different surface from the one a duplicate delivery actually races for. A caller handling a
	/// redelivery calls <see cref="IClaimableInboxStore.TryClaimAsync(string, string, CancellationToken)"/>
	/// FIRST and skips the message when it is refused; it never reaches the mark path at all. So a store
	/// whose mark path is terminal and whose claim path is not would pass every existing arm and still run
	/// the handler a second time on every redelivery, repeating each side effect with no duplicate visible
	/// to anyone.
	/// </para>
	/// <para>
	/// The contract states this directly: the claim returns <see langword="false"/> when the message is
	/// already claimed <em>or processed</em>. The nearest existing arm reaches the same state only after an
	/// intervening release, so it cannot distinguish a store that refuses a processed entry from one that
	/// merely refuses an entry the release declined to remove.
	/// </para>
	/// <para>
	/// The lease half is asserted only where the store declares the lease protocol, and is reported as a
	/// skip otherwise rather than passing silently. The claim half above is not optional and runs either
	/// way.
	/// </para>
	/// </remarks>
	public virtual async Task ProcessedEntry_MustNotBeReadmittedByTheClaimPath()
	{
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);
		var claimable = RequireClaimableStore(store);

		var messageId = GenerateMessageId();
		var handlerType = GenerateHandlerType();

		var claimed = await claimable
			.TryClaimAsync(messageId, handlerType, CancellationToken.None)
			.ConfigureAwait(false);

		if (!claimed)
		{
			throw new TestFixtureAssertionException(
				$"The claim path refused message '{messageId}' for handler '{handlerType}' on a key the store "
				+ "had never seen, with no competing caller, so this arm never established the claim it goes on "
				+ "to finalize. Reported separately from the assertion below because a store that grants no "
				+ "claim at all fails for a different reason than one that grants it and then re-admits a "
				+ "finalized entry.");
		}

		await store.MarkProcessedAsync(messageId, handlerType, CancellationToken.None).ConfigureAwait(false);

		var readmitted = await claimable
			.TryClaimAsync(messageId, handlerType, CancellationToken.None)
			.ConfigureAwait(false);

		if (readmitted)
		{
			throw new TestFixtureAssertionException(
				$"Message '{messageId}' for handler '{handlerType}' was finalized as processed, and the claim "
				+ "path admitted it again. A duplicate delivery races the claim, not the mark, so this store "
				+ "runs the handler a second time on every redelivery of an already-processed message -- every "
				+ "side effect repeated, and no duplicate visible to any caller. The claim must report false "
				+ "for an entry that is already claimed or processed.");
		}

		if (AsLeasedStore(store) is not { } leased)
		{
			SkipArm(
				nameof(ProcessedEntry_MustNotBeReadmittedByTheClaimPath),
				typeof(ILeasedInboxStore),
				"the store does not implement the optional lease protocol, so the lease half of this arm had "
				+ "nothing to hold it to. The claim half above DID run and did assert terminal-ness; only "
				+ "lease re-admission of a processed entry is unverified.");
			AssertLeaseProtocolIsNotAdvertised(store);
			return;
		}

		var leaseOnTerminal = await leased
			.TryAcquireLeaseAsync(messageId, handlerType, LiveLease, CancellationToken.None)
			.ConfigureAwait(false);

		if (leaseOnTerminal is not null)
		{
			throw new TestFixtureAssertionException(
				$"Message '{messageId}' for handler '{handlerType}' is terminal Processed, and the lease path "
				+ "granted a term over it. A terminal processed entry is never reclaimed: granting a lease "
				+ "here hands a second processor what looks like sole ownership of a message that has already "
				+ "been handled.");
		}
	}

	/// <summary>
	/// Verifies that the durable Processing mark cannot pull an entry back out of terminal Processed.
	/// </summary>
	/// <returns>A task representing the arm.</returns>
	/// <remarks>
	/// <para>
	/// Processed is an absorbing state: the dedup guarantee rests on at most one transaction ever
	/// committing that transition, and every read that decides whether to run a handler asks for it. A
	/// store whose Processing mark writes over Processed puts a finalized entry back into the state the
	/// stuck-processing timeout treats as an abandoned attempt, so the entry is re-admitted and the
	/// handler runs on a message whose side effects are already committed.
	/// </para>
	/// <para>
	/// The neighbouring arm asserts the claim path refuses a processed entry, which is the surface a
	/// redelivery races. This one is the surface a caller reaches AFTER admission, and no arm reached it:
	/// a store can refuse the claim correctly and still demote the row the moment anything marks
	/// processing, which puts the entry back within reach of the next claim rather than running the
	/// handler immediately. The two fail under different mutations, so neither substitutes for the other.
	/// </para>
	/// <para>
	/// A refusal is silent, not an error. The mark is issued speculatively by a caller that read a
	/// non-terminal status a moment earlier, so losing that race is ordinary and the store absorbs it —
	/// the assertion is on the state that survives, never on how the refusal was reported.
	/// </para>
	/// </remarks>
	public virtual async Task ProcessedEntry_MustNotBeDemotedByTheProcessingMark()
	{
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);

		if (store is not IProcessingTrackingInboxStore tracker)
		{
			// The store persists no Processing status at all, so there is no transition here to refuse.
			// Reported rather than returned for the reason the lease arms give: an arm that did not run
			// and an arm that passed are the same line in a result.
			SkipArm(
				nameof(ProcessedEntry_MustNotBeDemotedByTheProcessingMark),
				typeof(IProcessingTrackingInboxStore),
				"the store does not implement the optional durable processing-tracking capability, so it "
				+ "has no Processing mark that could demote a finalized entry. Nothing here was verified "
				+ "about terminal-ness of the processed state under that mark.");
			AssertProcessingTrackingIsNotAdvertised(store);
			return;
		}

		var messageId = GenerateMessageId();
		var handlerType = GenerateHandlerType();

		_ = await store.CreateEntryAsync(
			messageId,
			handlerType,
			"TestMessageType",
			CreatePayload("Test payload"),
			CreateDefaultMetadata(),
			CancellationToken.None).ConfigureAwait(false);

		await store.MarkProcessedAsync(messageId, handlerType, CancellationToken.None).ConfigureAwait(false);

		await tracker.MarkProcessingAsync(messageId, handlerType, CancellationToken.None).ConfigureAwait(false);

		var entry = await store.GetEntryAsync(messageId, handlerType, CancellationToken.None)
			.ConfigureAwait(false);

		if (entry is null)
		{
			throw new TestFixtureAssertionException(
				$"Message '{messageId}' for handler '{handlerType}' was finalized as processed and then read "
				+ "back as absent. The processing mark removed a terminal entry rather than refusing to "
				+ "change it, which loses the dedup record entirely: the next redelivery finds nothing and "
				+ "runs the handler again.");
		}

		if (entry.Status != InboxStatus.Processed)
		{
			throw new TestFixtureAssertionException(
				$"Message '{messageId}' for handler '{handlerType}' was terminal Processed, and the "
				+ $"processing mark moved it to {entry.Status}. Processed is absorbing — it is the state "
				+ "every duplicate check reads — so an entry demoted out of it is re-admittable: the "
				+ "stuck-processing timeout sees an abandoned attempt, the claim path sees a message nobody "
				+ "has handled, and the handler runs a second time on side effects already committed. The "
				+ "mark must refuse the transition and leave the entry as it found it.");
		}
	}

	/// <summary>
	/// Verifies that a Failed entry is re-admitted for retry by the lease path.
	/// </summary>
	/// <returns>A task representing the arm.</returns>
	/// <remarks>
	/// <para>
	/// The lease contract names this case explicitly: acquisition succeeds when the entry is
	/// <see cref="InboxStatus.Failed"/>, re-admitted for retry. Nothing asserted it. The existing failure
	/// arms read the entry back through <see cref="IInboxStore.GetEntryAsync"/> and
	/// <c>GetFailedEntriesAsync</c>, which prove the status was recorded and prove nothing about whether a
	/// retrying processor can take the message again -- and a store that records Failed but will not
	/// re-admit it has not deferred the message, it has dropped it. The reclaim arms next door cover the
	/// expired-lease case, which is a different state reached a different way.
	/// </para>
	/// <para>
	/// This is asserted on the LEASE path only, deliberately. The claim protocol reaches retry by
	/// releasing the entry -- <c>ReleaseAsync</c> removes it so a redelivery is re-admitted -- and its
	/// published contract refuses a claim whenever an entry exists for the key, a Failed entry included.
	/// Asserting re-admission there would assert the opposite of that contract; the claim path's own
	/// answer is asserted by <see cref="FailedEntry_MustNotBeReadmittedByTheClaimPath"/>.
	/// </para>
	/// </remarks>
	public virtual async Task FailedEntry_MustBeReAdmittedByTheLeasePath()
	{
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);

		var messageId = GenerateMessageId();
		var handlerType = GenerateHandlerType();

		if (AsLeasedStore(store) is not { } leased)
		{
			SkipArm(
				nameof(FailedEntry_MustBeReAdmittedByTheLeasePath),
				typeof(ILeasedInboxStore),
				"the store does not implement the optional lease protocol, so there is no lease path to "
				+ "re-admit a failed entry. Nothing here was verified about retry after failure.");
			AssertLeaseProtocolIsNotAdvertised(store);
			return;
		}

		var firstTerm = await leased
			.TryAcquireLeaseAsync(messageId, handlerType, LiveLease, CancellationToken.None)
			.ConfigureAwait(false);

		if (firstTerm is null)
		{
			throw new TestFixtureAssertionException(
				$"The lease path refused message '{messageId}' for handler '{handlerType}' on a key the store "
				+ "had never seen, with no competing caller, so this arm never established the term it goes on "
				+ "to fail.");
		}

		var recorded = await leased
			.FailAsync(messageId, handlerType, firstTerm.Value, "conformance: handler failed", CancellationToken.None)
			.ConfigureAwait(false);

		if (!recorded)
		{
			throw new TestFixtureAssertionException(
				$"The lease path reported that the failure of message '{messageId}' for handler "
				+ $"'{handlerType}' was not recorded, although the term had just been granted and nothing "
				+ "could have reclaimed it. The entry is not in the Failed state this arm exists to re-admit.");
		}

		var retryTerm = await leased
			.TryAcquireLeaseAsync(messageId, handlerType, LiveLease, CancellationToken.None)
			.ConfigureAwait(false);

		if (retryTerm is null)
		{
			throw new TestFixtureAssertionException(
				$"Message '{messageId}' for handler '{handlerType}' is in the Failed state and the lease path "
				+ "refused to re-admit it. Acquisition is specified to succeed on a Failed entry so a "
				+ "redelivery retries it; a store that refuses has not deferred the message, it has dropped "
				+ "it -- the handler failed once and nothing will ever run it again.");
		}
	}

	/// <summary>
	/// Verifies that a Failed entry is not re-admitted by the claim path.
	/// </summary>
	/// <returns>A task representing the arm.</returns>
	/// <remarks>
	/// <para>
	/// The claim protocol admits on the ABSENCE of an entry, not on its status: a claim is refused for a
	/// Processing, a terminal Processed, and a Failed entry alike. Retry here is reached by
	/// <see cref="IClaimableInboxStore.ReleaseAsync"/> removing the row, which the neighbouring liveness arm
	/// asserts; a Failed entry is one the caller handed to the estate-wide retry drain instead, and the
	/// drain dispatches it itself.
	/// </para>
	/// <para>
	/// Re-admitting it here would not be a harmless extra retry. This protocol carries no term — the claim
	/// never expires and nothing identifies its holder — so a redelivery admitted alongside the drain cannot
	/// be fenced against it, and the handler runs twice concurrently on one message. That is the invariant
	/// the inbox exists to hold, and no existing arm reached this state: the processed arm next door
	/// finalizes the entry, and the release arm removes it, so both leave the store in a state a Failed
	/// entry never passes through.
	/// </para>
	/// <para>
	/// This is the deliberate divergence from <see cref="ILeasedInboxStore"/>, whose acquisition DOES
	/// re-admit a Failed entry because recording the failure clears the lease term and every later write is
	/// fenced by the term acquisition returned. A store may declare both protocols; the two answers are both
	/// correct and are asserted separately.
	/// </para>
	/// <para>
	/// The staged status is read back before the assertion. A store whose failure mark silently did nothing
	/// leaves the entry Processing, which the claim path also refuses — so without the read-back this arm
	/// would pass over a store that never reached the state it exists to test.
	/// </para>
	/// </remarks>
	public virtual async Task FailedEntry_MustNotBeReadmittedByTheClaimPath()
	{
		var store = await CreateStoreForArmAsync().ConfigureAwait(false);
		var claimable = RequireClaimableStore(store);

		var messageId = GenerateMessageId();
		var handlerType = GenerateHandlerType();

		var claimed = await claimable
			.TryClaimAsync(messageId, handlerType, CancellationToken.None)
			.ConfigureAwait(false);

		if (!claimed)
		{
			throw new TestFixtureAssertionException(
				$"The claim path refused message '{messageId}' for handler '{handlerType}' on a key the store "
				+ "had never seen, with no competing caller, so this arm never established the claim it goes on "
				+ "to fail. Reported separately from the assertion below because a store that grants no claim "
				+ "at all fails for a different reason than one that grants it and then re-admits a failed "
				+ "entry.");
		}

		await store.MarkFailedAsync(messageId, handlerType, "conformance: handler failed", CancellationToken.None)
			.ConfigureAwait(false);

		var staged = await store.GetEntryAsync(messageId, handlerType, CancellationToken.None)
			.ConfigureAwait(false);

		if (staged is null || staged.Status != InboxStatus.Failed)
		{
			throw new TestFixtureAssertionException(
				$"Message '{messageId}' for handler '{handlerType}' was claimed and then marked failed, and "
				+ $"read back as {(staged is null ? "absent" : staged.Status.ToString())} rather than Failed. "
				+ "The state this arm exists to test was never reached, so the assertion below would have "
				+ "passed without testing anything: the claim path refuses a Processing entry too, and it "
				+ "refuses a key with no entry for a different reason again. Reported as a staging failure "
				+ "rather than a re-admission failure, which is a different defect.");
		}

		var readmitted = await claimable
			.TryClaimAsync(messageId, handlerType, CancellationToken.None)
			.ConfigureAwait(false);

		if (readmitted)
		{
			throw new TestFixtureAssertionException(
				$"Message '{messageId}' for handler '{handlerType}' is in the Failed state, and the claim path "
				+ "admitted it. That entry belongs to the estate-wide retry drain, which dispatches it on its "
				+ "own schedule, and this protocol has no term with which to fence the two apart -- so a "
				+ "redelivery can now enter the handler while the drain is dispatching the same entry, running "
				+ "it twice with every side effect repeated. The claim must be refused whenever an entry "
				+ "exists for the key, whatever its status; retry on this protocol is reached by releasing "
				+ "the entry, which removes the row. The lease path deliberately answers differently: it "
				+ "re-admits a failed entry because the failure clears the term, and its writes are fenced.");
		}
	}

	/// <summary>
	/// Returns the store's claim surface, or fails naming what is missing.
	/// </summary>
	/// <param name="store">The store under test.</param>
	/// <returns>The store, through <see cref="IClaimableInboxStore"/>.</returns>
	private static IClaimableInboxStore RequireClaimableStore(IInboxStore store) =>
		store as IClaimableInboxStore
		?? throw new InvalidOperationException(
			$"Conformance test requires {store.GetType().Name} to implement IClaimableInboxStore. "
			+ "Lease reclaim is a property of the claim protocol: with no claim path there is no lease to "
			+ "expire and nothing for a replacement processor to take over.");

	#endregion

}
