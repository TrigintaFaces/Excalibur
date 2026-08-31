// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Runtime.CompilerServices;

using Shouldly;

using Xunit;

namespace Excalibur.Dispatch.Tests.Conformance.Transport;

/// <summary>
/// Base class for transport conformance tests.
/// All transport implementations MUST pass this test suite to ensure consistent behavior.
/// Validates requirements: R2.1-R2.8, R2.15-R2.18, R4.5, R15.2, R15.7, T10.27, T10.34.
/// </summary>
/// <typeparam name="TSender">The transport sender type.</typeparam>
/// <typeparam name="TReceiver">The transport receiver type.</typeparam>
public abstract class TransportConformanceTestBase<TSender, TReceiver> : IAsyncLifetime
	where TSender : IChannelSender
	where TReceiver : IChannelReceiver
{
	/// <summary>
	/// Default timeout for receive operations to prevent tests from hanging indefinitely.
	/// </summary>
	// Generous, CI-scaled window so transport round-trips complete deterministically under heavy
	// TestContainers load (notably Kafka consumer-group rebalance / partition assignment, which can take
	// well over 30s when many containers contend on a CI runner). Receivers block until the message
	// arrives, so the happy path returns immediately; only a genuine delivery failure waits the full window.
	private static readonly TimeSpan ReceiveTimeout = global::Tests.Shared.Infrastructure.TestTimeouts.Scale(TimeSpan.FromSeconds(60));

	/// <summary>
	/// Wall-clock budget for <see cref="InitializeTransportAsync" />, which for a container-backed
	/// transport covers the docker image PULL as well as the container start and broker readiness.
	/// </summary>
	/// <remarks>
	/// Was a hard-coded 30 seconds, which a cold runner cannot meet: the Pub/Sub emulator and RabbitMQ
	/// images are pulled on first use, and the whole GooglePubSub and RabbitMQ suites failed on the bound
	/// rather than on the transport. Two suites had already worked around it by moving container start
	/// into an <c>IClassFixture</c>, which has no such cap -- evidence the bound, not the transport, was
	/// wrong. <see cref="Tests.Shared.Infrastructure.TestTimeouts.ContainerInitBudget" /> is the constant
	/// that already encodes this: 240s, deliberately unscaled, and held below the SHORTEST
	/// <c>--blame-hang-timeout</c> in use (5m) so a container failure still surfaces as a diagnosable
	/// error instead of a killed host that reports Failed: 0.
	/// </remarks>
	private static readonly TimeSpan InitializationBudget =
		global::Tests.Shared.Infrastructure.TestTimeouts.ContainerInitBudget;

	/// <summary>
	/// Caches the transport-initialization outcome per closed generic type (e.g., Kafka, RabbitMQ).
	/// Once init fails for a transport, all remaining tests in that class skip immediately
	/// instead of each waiting out the full initialization budget. null = not yet checked.
	/// </summary>
	private static bool? s_transportInitialized;

	/// <summary>
	/// Caches WHY initialization failed, so the fast-path skips report the real cause rather than a
	/// fabricated one. See <see cref="IsTransportAvailable" /> for what this signal can and cannot say.
	/// </summary>
	private static string? s_initializationFailure;

	private bool _transportAvailable;

	private string? _initializationFailure;

	protected TSender? Sender { get; private set; }
	protected TReceiver? Receiver { get; private set; }
	protected IDeadLetterQueueManager? DlqManager { get; private set; }

	/// <summary>
	/// Optional advanced conformance capabilities (header-surfacing, CloudEvents binding, ack/nack
	/// redelivery, filtering) the transport supports beyond the body-only send/receive surface. A deriver
	/// returns a provider to opt in; the capability-gated facts then make real, RED-able assertions against
	/// it (bd-urttf7). Returns null when the transport supports only body-only send/receive — capability-gated
	/// facts then no-op for that transport rather than asserting falsely.
	/// </summary>
	protected virtual ITransportConformanceCapabilities? AdvancedCapabilities => null;

	/// <summary>
	/// Gets a value indicating whether this transport may report its facts as SKIPPED when the transport
	/// cannot be initialized, instead of failing the run.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Default is <see langword="false" />: a transport that cannot initialize FAILS. This mirrors
	/// <c>ContainerFixtureBase.AllowGracefulDegradation</c>, which defaults false so required
	/// infrastructure fails loudly, and is opted into only by the cloud-emulator fixtures (AWS SQS, Azure
	/// Service Bus) whose emulators are genuinely absent in some environments.
	/// </para>
	/// <para>
	/// The default matters because the alternative is indistinguishable from success. When every gated fact
	/// skips, the assembly reports no failures, and "this transport was never verified" reads exactly like
	/// "this transport conformed". Failing closed makes the difference visible.
	/// </para>
	/// </remarks>
	protected virtual bool AllowUnavailableTransport => false;

	/// <summary>
	/// Gets a value indicating whether this transport talks to real external infrastructure (a broker in a
	/// container) rather than running in-process.
	/// </summary>
	/// <remarks>
	/// Only external-broker suites count toward the run's liveness. An in-process transport needs no
	/// infrastructure, so its arms execute whether or not the container runtime is up; counting them would
	/// make <see cref="ConformanceLivenessGate" /> green in exactly the situation it exists to catch.
	/// </remarks>
	protected virtual bool UsesExternalBroker => true;

	/// <summary>
	/// Gates a conformance arm on transport availability and records that the arm is executing.
	/// </summary>
	/// <remarks>
	/// The recording is what lets the run be asked whether anything was verified. It sits AFTER the skip so
	/// it means "this arm ran its body", not "this arm was declared".
	/// </remarks>
	/// <param name="arm">The calling fact's name; supplied by the compiler.</param>
	protected void RequireTransport([CallerMemberName] string arm = "")
	{
		Assert.SkipUnless(IsTransportAvailable(), TransportUnavailableReason());
		ConformanceExecutionLedger.RecordArmExecuted(GetType().Name, arm, UsesExternalBroker);
	}

	public async ValueTask InitializeAsync()
	{
		// Recorded first, before any early return or throw: a transport that fails to initialize must still
		// count as SELECTED, or the liveness gate cannot tell "no broker was asked for" from "every broker
		// was asked for and none answered".
		if (UsesExternalBroker)
		{
			ConformanceExecutionLedger.RecordBrokerSuiteAttempted(GetType().Name);
		}

		// Fast-path: if a previous test already determined initialization fails, skip immediately,
		// carrying forward the recorded cause so the skip still names it.
		if (s_transportInitialized == false)
		{
			_transportAvailable = false;
			_initializationFailure = s_initializationFailure;

			// The cache exists so the remaining facts do not each wait out the 30s budget. It must not also
			// convert a hard failure into silence: when this transport is required, every fact after the
			// first failure fails too, naming the original cause.
			ThrowIfTransportRequired(null);
			return;
		}

		// Captured rather than rethrown in place: the throw belongs AFTER the catch, or the catch below
		// swallows it and re-reports a required-transport failure as "initialization threw
		// InvalidOperationException", burying the real cause one level down.
		Exception? initializationException = null;

		try
		{
			// Timeout initialization to prevent indefinite hangs when Docker is unavailable
			var initTask = InitializeTransportAsync();
			var completedTask = await Task.WhenAny(initTask, Task.Delay(InitializationBudget)).ConfigureAwait(false);

			if (completedTask != initTask)
			{
				RecordInitializationFailure(
					FormattableString.Invariant($"transport initialization did not complete within {InitializationBudget.TotalSeconds:0} seconds"));
			}
			else
			{
				// Propagate any exception from the init task
				await initTask.ConfigureAwait(false);
				s_transportInitialized = true;
				s_initializationFailure = null;
				_transportAvailable = true;
			}
		}
		catch (Exception ex) when (ex is not OutOfMemoryException)
		{
			RecordInitializationFailure($"transport initialization threw {ex.GetType().Name}: {ex.Message}");
			initializationException = ex;
		}

		if (!_transportAvailable)
		{
			ThrowIfTransportRequired(initializationException);
		}
	}

	/// <summary>
	/// Fails the run when this transport is REQUIRED and could not be initialized. No-ops for a transport
	/// that has opted into <see cref="AllowUnavailableTransport" />, which skips instead.
	/// </summary>
	/// <param name="cause">The originating exception, preserved as the inner exception when there is one.</param>
	private void ThrowIfTransportRequired(Exception? cause)
	{
		if (!AllowUnavailableTransport)
		{
			throw new InvalidOperationException(BuildUnavailableFailureMessage(), cause);
		}
	}

	/// <summary>
	/// Builds the failure message used when a REQUIRED transport cannot be initialized.
	/// </summary>
	private string BuildUnavailableFailureMessage() =>
		$"Transport conformance initialization failed and this transport is REQUIRED: "
		+ $"{_initializationFailure ?? "transport initialization did not run"}. "
		+ "This is thrown deliberately rather than skipped: a skipped conformance suite reports no failures, "
		+ "which is indistinguishable from a suite that verified the transport. Override "
		+ $"{nameof(AllowUnavailableTransport)} to true ONLY for genuinely optional infrastructure "
		+ "(a cloud emulator), matching the fixture that already declares it optional.";

	/// <summary>
	/// Records the verbatim cause of an initialization failure, both on this instance and in the
	/// per-closed-generic cache, so a skip (or a required-transport failure) can name it.
	/// </summary>
	private void RecordInitializationFailure(string reason)
	{
		Console.WriteLine($"Transport conformance initialization failed: {reason}");
		s_transportInitialized = false;
		s_initializationFailure = reason;
		_transportAvailable = false;
		_initializationFailure = reason;
		ConformanceExecutionLedger.RecordTransportUnavailable(GetType().Name, reason);
	}

	private async Task InitializeTransportAsync()
	{
		Sender = await CreateSenderAsync().ConfigureAwait(false);
		Receiver = await CreateReceiverAsync().ConfigureAwait(false);
		DlqManager = await CreateDlqManagerAsync().ConfigureAwait(false);
	}

	/// <summary>
	/// Returns true when the transport initialized successfully and the conformance facts can execute.
	/// Every infrastructure-gated fact reaches this through <see cref="RequireTransport" />.
	/// </summary>
	/// <remarks>
	/// <para>
	/// This is a single bool and it CONFLATES two different facts: "the infrastructure (Docker, or the
	/// broker behind it) was unreachable" and "the transport's own initialization threw". Both land in
	/// the same catch in <see cref="InitializeAsync" />, so a genuinely BROKEN transport is reported
	/// unavailable exactly as an absent Docker daemon is.
	/// </para>
	/// <para>
	/// That conflation no longer decides the outcome. <see cref="AllowUnavailableTransport" /> supplies the
	/// per-transport required-vs-optional policy this class previously lacked, defaulting to REQUIRED, so an
	/// unavailable transport fails rather than skipping. This method is now reached only by suites that have
	/// explicitly opted out, and <see cref="TransportUnavailableReason" /> still carries the captured cause
	/// into their skip lines.
	/// </para>
	/// </remarks>
	protected bool IsTransportAvailable() => _transportAvailable;

	/// <summary>
	/// The reason the transport is unavailable, reported verbatim in every skip so the run output can
	/// tell an unreachable daemon apart from a transport that failed to initialize.
	/// </summary>
	protected string TransportUnavailableReason() =>
		$"[transport-unavailable] {_initializationFailure ?? "transport initialization did not run"}. "
		+ "This fact did NOT execute; it is reported skipped, never passed. This reason does not by itself "
		+ "classify unreachable infrastructure versus a broken transport — read the captured cause above.";

	public async ValueTask DisposeAsync()
	{
		// Gating disposal on _transportAvailable leaked every partially-initialized transport: when
		// CreateReceiverAsync throws, the sender (and its container) is already built, but the flag is
		// false, so nothing was ever disposed. Dispose whenever initialization produced anything.
		if (!_transportAvailable && Sender is null && Receiver is null && DlqManager is null)
		{
			return;
		}

		try
		{
			await DisposeTransportAsync().ConfigureAwait(false);
		}
		catch (Exception ex) when (ex is not OutOfMemoryException)
		{
			// Best-effort cleanup, matching ContainerFixtureBase.DisposeAsync: a teardown fault must not
			// overwrite the verdict of the test that just ran. Reported so it is not invisible.
			Console.WriteLine($"Transport conformance disposal failed (ignored): {ex.GetType().Name}: {ex.Message}");
		}
	}

	/// <summary>
	/// Creates and initializes the transport sender.
	/// </summary>
	protected abstract Task<TSender> CreateSenderAsync();

	/// <summary>
	/// Creates and initializes the transport receiver.
	/// </summary>
	protected abstract Task<TReceiver> CreateReceiverAsync();

	/// <summary>
	/// Creates and initializes the dead-letter queue manager.
	/// Optional - return null if DLQ is not supported by this transport.
	/// </summary>
	protected abstract Task<IDeadLetterQueueManager?> CreateDlqManagerAsync();

	/// <summary>
	/// Disposes transport resources.
	/// </summary>
	protected abstract Task DisposeTransportAsync();

	#region Core Conformance Tests

	/// <summary>
	/// R2.1: Transport MUST support basic send and receive round-trip.
	/// </summary>
	[Fact]
	public virtual async Task Should_Send_And_Receive_Message_RoundTrip()
	{
		RequireTransport();

		// Arrange
		var testMessage = new TestMessage
		{
			Id = Guid.NewGuid().ToString(),
			Content = "Test message content",
			Timestamp = DateTimeOffset.UtcNow
		};

		// Act
		using var cts = new CancellationTokenSource(ReceiveTimeout);
		await Sender.SendAsync(testMessage, cts.Token).ConfigureAwait(false);
		var received = await Receiver.ReceiveAsync<TestMessage>(cts.Token).ConfigureAwait(false);

		// Assert
		_ = received.ShouldNotBeNull();
		received.Id.ShouldBe(testMessage.Id);
		received.Content.ShouldBe(testMessage.Content);
		received.Timestamp.ShouldBe(testMessage.Timestamp, TimeSpan.FromMilliseconds(100));
	}

	/// <summary>
	/// R2.2: Transport MUST preserve message metadata (correlation IDs, custom headers).
	/// </summary>
	/// <remarks>
	/// This asserts metadata survives at the <b>body</b> level (the fields round-trip through serialization),
	/// which the harness can express. Asserting metadata on the <b>transport carrier</b> (Kafka
	/// <c>Headers</c> / RabbitMQ <c>BasicProperties</c>) requires a header-surfacing receive context the
	/// IChannelReceiver harness does not expose — tracked bd-liyait (umbrella Excalibur.Dispatch-urttf7).
	/// </remarks>
	[Fact]
	public virtual async Task Should_Preserve_Message_Metadata()
	{
		RequireTransport();

		// Arrange
		var testMessage = new TestMessageWithMetadata
		{
			Id = Guid.NewGuid().ToString(),
			MessageId = Guid.NewGuid().ToString(),
			CorrelationId = Guid.NewGuid().ToString(),
			UserId = "test-user-123",
			TenantId = "tenant-456",
			Content = "Test content"
		};

		// Act
		using var cts = new CancellationTokenSource(ReceiveTimeout);
		await Sender.SendAsync(testMessage, cts.Token).ConfigureAwait(false);
		var received = await Receiver.ReceiveAsync<TestMessageWithMetadata>(cts.Token).ConfigureAwait(false);

		// Assert
		_ = received.ShouldNotBeNull();
		received.MessageId.ShouldBe(testMessage.MessageId);
		received.CorrelationId.ShouldBe(testMessage.CorrelationId);
		received.UserId.ShouldBe(testMessage.UserId);
		received.TenantId.ShouldBe(testMessage.TenantId);
	}

	/// <summary>
	/// R2.3, R9.1: Transport MUST handle concurrent messages without loss.
	/// </summary>
	[Fact]
	public virtual async Task Should_Handle_Concurrent_Messages()
	{
		RequireTransport();

		// Arrange
		const int messageCount = 100;
		var sentMessages = new List<TestMessage>();
		var receivedMessages = new List<TestMessage>();

		for (int i = 0; i < messageCount; i++)
		{
			sentMessages.Add(new TestMessage
			{
				Id = Guid.NewGuid().ToString(),
				Content = $"Message {i}",
				Timestamp = DateTimeOffset.UtcNow
			});
		}

		// Act - Send concurrently
		var sendTasks = sentMessages.Select(msg =>
			Sender.SendAsync(msg, CancellationToken.None)).ToList();
		await Task.WhenAll(sendTasks).ConfigureAwait(false);

		// Act - Receive all messages (use the shared, CI-scaled receive window so a slow rebalance under
		// heavy TestContainers load does not truncate the loop -> deterministic).
		using var cts = new CancellationTokenSource(ReceiveTimeout);
		for (int i = 0; i < messageCount; i++)
		{
			var received = await Receiver.ReceiveAsync<TestMessage>(cts.Token).ConfigureAwait(false);
			if (received != null)
			{
				receivedMessages.Add(received);
			}
		}

		// Assert
		receivedMessages.Count.ShouldBe(messageCount);
		var receivedIds = receivedMessages.Select(m => m.Id).ToHashSet();
		var sentIds = sentMessages.Select(m => m.Id).ToHashSet();
		receivedIds.SetEquals(sentIds).ShouldBeTrue("All messages should be received with no duplicates");
	}

	/// <summary>
	/// R2.15: Transport MUST support message filtering capabilities.
	/// </summary>
	/// <remarks>
	/// Capability-gated on <see cref="TransportCapability.Filtering" />. A transport that advertises server-side
	/// filtering via <see cref="AdvancedCapabilities" /> is asserted to deliver ONLY the matching message; a
	/// transport that does not advertise it no-ops (the seam design — no false conformance). The assertion is
	/// proven RED-able against a non-filtering double in <c>HarnessCapabilityNonVacuityShould</c>.
	/// </remarks>
	[Fact]
	public virtual async Task Should_Support_Message_Filtering()
	{
		RequireTransport();

		var capabilities = AdvancedCapabilities;
		if (capabilities is null || !capabilities.Capabilities.HasFlag(TransportCapability.Filtering))
		{
			Assert.Skip("[capability-not-applicable] This transport does not advertise server-side filtering, so the filtering fact does NOT apply to it. Reported skipped rather than passed: a transport that cannot filter must not appear to have conformed.");
		}

		// Arrange: a message to drop and a message to keep, tagged with distinct filter attributes. The
		// non-matching ("drop") message is sent FIRST so a transport that ignores the filter returns it (RED).
		var keep = new TestMessage
		{
			Id = Guid.NewGuid().ToString(),
			Content = "keep",
			Timestamp = DateTimeOffset.UtcNow
		};
		var drop = new TestMessage
		{
			Id = Guid.NewGuid().ToString(),
			Content = "drop",
			Timestamp = DateTimeOffset.UtcNow
		};

		using var cts = new CancellationTokenSource(ReceiveTimeout);
		await capabilities.SendFilterableAsync(
			drop,
			new Dictionary<string, string>(StringComparer.Ordinal) { ["label"] = "drop" },
			cts.Token).ConfigureAwait(false);
		await capabilities.SendFilterableAsync(
			keep,
			new Dictionary<string, string>(StringComparer.Ordinal) { ["label"] = "keep" },
			cts.Token).ConfigureAwait(false);

		// Act: receive only messages matching the "keep" filter.
		var received = await capabilities.ReceiveMatchingAsync<TestMessage>(
			new Dictionary<string, string>(StringComparer.Ordinal) { ["label"] = "keep" },
			cts.Token).ConfigureAwait(false);

		// Assert: the matching message is delivered; the non-matching one is filtered out.
		_ = received.ShouldNotBeNull();
		_ = received.Body.ShouldNotBeNull();
		received.Body.Content.ShouldBe("keep");
	}

	/// <summary>
	/// R4.5: Transport MUST route poison messages to DLQ after retry exhaustion.
	/// </summary>
	[Fact]
	public virtual async Task Should_Handle_Poison_Messages()
	{
		RequireTransport();

		if (DlqManager == null)
		{
			Assert.Skip("[capability-not-applicable] This transport exposes no dead-letter queue manager, so the poison-message fact does NOT apply to it. Reported skipped rather than passed.");
		}

		// Arrange
		var poisonMessage = new TestMessage
		{
			Id = Guid.NewGuid().ToString(),
			Content = "Poison message",
			Timestamp = DateTimeOffset.UtcNow
		};

		// Act
		using var cts = new CancellationTokenSource(ReceiveTimeout);
		await Sender.SendAsync(poisonMessage, cts.Token).ConfigureAwait(false);
		var received = await Receiver.ReceiveAsync<TestMessage>(cts.Token).ConfigureAwait(false);

		// Simulate failure and DLQ routing
		_ = received.ShouldNotBeNull();
		var dlqId = await DlqManager.MoveToDeadLetterAsync(
			new TransportMessage { Id = received.Id },
			"MaxRetries",
			new InvalidOperationException("Simulated processing failure"),
			cts.Token).ConfigureAwait(false);

		// Assert
		dlqId.ShouldNotBeNullOrEmpty();

		var dlqMessages = await DlqManager.GetDeadLetterMessagesAsync(10, cts.Token).ConfigureAwait(false);
		dlqMessages.ShouldContain(m => m.OriginalMessage.Id == received.Id || m.OriginalMessage.Id == dlqId);
	}

	/// <summary>
	/// R15.2: Transport MUST support graceful shutdown and restart without errors.
	/// Verifies that the transport can cleanly shut down and resume operations.
	/// </summary>
	[Fact]
	public virtual async Task Should_Support_Graceful_Shutdown()
	{
		RequireTransport();

		// Act - Trigger graceful shutdown
		await DisposeTransportAsync().ConfigureAwait(false);

		// Re-initialize by calling the transport factory DIRECTLY, not the IAsyncLifetime entry point.
		//
		// InitializeAsync writes the per-closed-generic STATIC failure cache. Routing a restart through it
		// meant a single flaky container restart inside THIS fact latched s_transportInitialized = false for
		// the whole transport, and nothing ever reset it -- so every fact that happened to run afterwards
		// skipped. xUnit does not guarantee intra-class ordering, so WHICH facts were disabled varied run to
		// run, and the suite got quieter under load: exactly when it should be loudest.
		//
		// A restart that fails is this fact's own failure -- R15.2 is the guarantee under test -- so it
		// propagates here instead of being recorded as an availability problem.
		await InitializeTransportAsync().ConfigureAwait(false);

		// Verify transport is functional after restart by sending and receiving a new message
		var testMessage = new TestMessage
		{
			Id = Guid.NewGuid().ToString(),
			Content = "Post-restart verification",
			Timestamp = DateTimeOffset.UtcNow
		};

		using var cts = new CancellationTokenSource(ReceiveTimeout);
		await Sender.SendAsync(testMessage, cts.Token).ConfigureAwait(false);
		var received = await Receiver.ReceiveAsync<TestMessage>(cts.Token).ConfigureAwait(false);

		// Assert
		_ = received.ShouldNotBeNull();
		received.Id.ShouldBe(testMessage.Id);
	}

	#endregion Core Conformance Tests
}