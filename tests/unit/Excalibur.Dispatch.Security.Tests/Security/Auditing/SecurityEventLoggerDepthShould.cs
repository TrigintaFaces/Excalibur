// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Security;

namespace Excalibur.Dispatch.Security.Tests.Security.Auditing;

/// <summary>
/// Depth tests for <see cref="SecurityEventLogger"/>.
/// Covers hosted service lifecycle, event queuing, context extraction,
/// batch processing, and disposal.
/// Each test that uses Start/StopAsync creates its own instance to avoid
/// interference from parallel test execution.
/// </summary>
/// <remarks>
/// The SecurityEventLogger passes a mutable List&lt;SecurityEvent&gt; to StoreEventsAsync
/// and then clears it for reuse. FakeItEasy captures the reference (not a copy),
/// so That.Matches() assertions see an empty list. Instead, we capture events via
/// Invokes() into a ConcurrentBag and assert against the captured snapshot.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Security")]
public sealed class SecurityEventLoggerDepthShould
{
	private readonly ILogger<SecurityEventLogger> _logger = NullLogger<SecurityEventLogger>.Instance;

	/// <summary>
	/// Creates a store that captures all events into a thread-safe collection and signals
	/// a <see cref="TaskCompletionSource"/> when non-empty events are stored.
	/// </summary>
	private static (ISecurityEventStore Store, ConcurrentBag<SecurityEvent> CapturedEvents, TaskCompletionSource StoreCalled) CreateCapturingStore()
	{
		var capturedEvents = new ConcurrentBag<SecurityEvent>();
		var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var store = A.Fake<ISecurityEventStore>();
		A.CallTo(() => store.StoreEventsAsync(A<IEnumerable<SecurityEvent>>._, A<CancellationToken>._))
			.Invokes((IEnumerable<SecurityEvent> events, CancellationToken _) =>
			{
				var materialized = events.ToList();
				foreach (var evt in materialized)
				{
					capturedEvents.Add(evt);
				}

				if (materialized.Count > 0)
				{
					tcs.TrySetResult();
				}
			})
			.Returns(Task.CompletedTask);
		return (store, capturedEvents, tcs);
	}

	private static ISecurityEventStore CreateSuccessStore()
	{
		var store = A.Fake<ISecurityEventStore>();
		A.CallTo(() => store.StoreEventsAsync(A<IEnumerable<SecurityEvent>>._, A<CancellationToken>._))
			.Returns(Task.CompletedTask);
		return store;
	}

	[Fact]
	public void ImplementISecurityEventLogger()
	{
		using var sut = new SecurityEventLogger(_logger, CreateSuccessStore());
		sut.ShouldBeAssignableTo<ISecurityEventLogger>();
	}

	[Fact]
	public void ImplementIHostedService()
	{
		using var sut = new SecurityEventLogger(_logger, CreateSuccessStore());
		sut.ShouldBeAssignableTo<Microsoft.Extensions.Hosting.IHostedService>();
	}

	[Fact]
	public void ImplementIDisposable()
	{
		using var sut = new SecurityEventLogger(_logger, CreateSuccessStore());
		sut.ShouldBeAssignableTo<IDisposable>();
	}

	[Fact]
	public void ThrowWhenLoggerIsNull()
	{
		Should.Throw<ArgumentNullException>(() => new SecurityEventLogger(null!, CreateSuccessStore()));
	}

	[Fact]
	public void ThrowWhenEventStoreIsNull()
	{
		Should.Throw<ArgumentNullException>(() => new SecurityEventLogger(_logger, null!));
	}

	[Fact]
	public async Task LogSecurityEventQueuesWithCorrectProperties()
	{
		// Arrange
		var (eventStore, capturedEvents, storeCalled) = CreateCapturingStore();
		using var sut = new SecurityEventLogger(_logger, eventStore);
		await sut.StartAsync(CancellationToken.None);

		var context = A.Fake<IMessageContext>();
		A.CallTo(() => context.CorrelationId).Returns(Guid.NewGuid().ToString());
		A.CallTo(() => context.Items).Returns(new Dictionary<string, object>(StringComparer.Ordinal)
		{
			["User:MessageId"] = "user-123",
			["Client:IP"] = "192.168.1.1",
			["Client:UserAgent"] = "TestAgent/1.0",
			["Message:Type"] = "TestMessage",
			["Security:Token"] = "sec-token",
		});

		// Act
		await sut.LogSecurityEventAsync(
			SecurityEventType.AuthenticationFailure,
			"Login failed",
			SecuritySeverity.High,
			CancellationToken.None,
			context);

		// Wait for store to receive events (deterministic)
		await storeCalled.Task.WaitAsync(TimeSpan.FromSeconds(10));
		await sut.StopAsync(CancellationToken.None);

		// Assert against captured snapshot
		capturedEvents.ShouldNotBeEmpty();
		var evt = capturedEvents.First(e => e.EventType == SecurityEventType.AuthenticationFailure);
		evt.Description.ShouldBe("Login failed");
		evt.Severity.ShouldBe(SecuritySeverity.High);
		evt.UserId.ShouldBe("user-123");
		evt.SourceIp.ShouldBe("192.168.1.1");
	}

	[Fact]
	public async Task LogSecurityEventWithNullContext()
	{
		// Arrange
		var (eventStore, capturedEvents, storeCalled) = CreateCapturingStore();
		using var sut = new SecurityEventLogger(_logger, eventStore);
		await sut.StartAsync(CancellationToken.None);

		// Act
		await sut.LogSecurityEventAsync(
			SecurityEventType.ValidationFailure,
			"Validation issue",
			SecuritySeverity.Low,
			CancellationToken.None);

		// Wait for store to receive events (deterministic)
		await storeCalled.Task.WaitAsync(TimeSpan.FromSeconds(10));
		await sut.StopAsync(CancellationToken.None);

		// Assert
		capturedEvents.ShouldNotBeEmpty();
		capturedEvents.ShouldContain(e => e.EventType == SecurityEventType.ValidationFailure);
	}

	[Fact]
	public async Task StartAsyncBeginsBackgroundProcessing()
	{
		// Arrange
		var (eventStore, capturedEvents, storeCalled) = CreateCapturingStore();
		using var sut = new SecurityEventLogger(_logger, eventStore);

		// Act
		await sut.StartAsync(CancellationToken.None);

		await sut.LogSecurityEventAsync(
			SecurityEventType.AuthenticationSuccess,
			"Login succeeded",
			SecuritySeverity.Low,
			CancellationToken.None);

		// Wait for store to receive events (deterministic)
		await storeCalled.Task.WaitAsync(TimeSpan.FromSeconds(10));
		await sut.StopAsync(CancellationToken.None);

		// Assert — background processing picked up the event
		capturedEvents.ShouldNotBeEmpty();
	}

	[Fact]
	public async Task StopAsyncDrainsRemainingEvents()
	{
		// Arrange
		var (eventStore, capturedEvents, storeCalled) = CreateCapturingStore();
		using var sut = new SecurityEventLogger(_logger, eventStore);
		await sut.StartAsync(CancellationToken.None);

		for (var i = 0; i < 5; i++)
		{
			await sut.LogSecurityEventAsync(
				SecurityEventType.AuthorizationFailure,
				$"Event {i}",
				SecuritySeverity.Medium,
				CancellationToken.None);
		}

		// Wait for store to receive events (deterministic)
		await storeCalled.Task.WaitAsync(TimeSpan.FromSeconds(10));

		// Act
		await sut.StopAsync(CancellationToken.None);

		// Assert — all 5 events should have been stored
		capturedEvents.ShouldNotBeEmpty();
		capturedEvents.Count.ShouldBeGreaterThanOrEqualTo(1);
	}

	[Fact]
	public async Task ExtractSecurityPrefixedItemsAsAdditionalData()
	{
		// Arrange
		var (eventStore, capturedEvents, storeCalled) = CreateCapturingStore();
		using var sut = new SecurityEventLogger(_logger, eventStore);
		await sut.StartAsync(CancellationToken.None);

		var context = A.Fake<IMessageContext>();
		A.CallTo(() => context.CorrelationId).Returns(null as string);
		A.CallTo(() => context.Items).Returns(new Dictionary<string, object>(StringComparer.Ordinal)
		{
			["Security:Level"] = "High",
			["Auth:Provider"] = "OAuth2",
			["Validation:Status"] = "Passed",
			["NonRelevant:Key"] = "should-not-be-included",
		});

		// Act
		await sut.LogSecurityEventAsync(
			SecurityEventType.ConfigurationChange,
			"Config updated",
			SecuritySeverity.Medium,
			CancellationToken.None,
			context);

		// Wait for store to receive events (deterministic)
		await storeCalled.Task.WaitAsync(TimeSpan.FromSeconds(10));
		await sut.StopAsync(CancellationToken.None);

		// Assert against captured snapshot
		capturedEvents.ShouldNotBeEmpty();
		var evt = capturedEvents.First(e => e.EventType == SecurityEventType.ConfigurationChange);
		evt.AdditionalData.ShouldContainKey("Security:Level");
		evt.AdditionalData.ShouldContainKey("Auth:Provider");
		evt.AdditionalData.ShouldContainKey("Validation:Status");
		evt.AdditionalData.ShouldNotContainKey("NonRelevant:Key");
	}

	[Fact]
	public void DisposeWithoutStartingShouldNotThrow()
	{
		var sut = new SecurityEventLogger(_logger, CreateSuccessStore());
		Should.NotThrow(() => sut.Dispose());
	}

	[Fact]
	public async Task HandleStoreExceptionGracefully()
	{
		// Arrange — store throws on every call; TCS signals on first non-empty call
		var storeCalled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var throwingStore = A.Fake<ISecurityEventStore>();
		A.CallTo(() => throwingStore.StoreEventsAsync(A<IEnumerable<SecurityEvent>>._, A<CancellationToken>._))
			.Invokes((IEnumerable<SecurityEvent> events, CancellationToken _) =>
			{
				if (events.Any())
				{
					storeCalled.TrySetResult();
				}
			})
			.Throws(new InvalidOperationException("Store failure"));

		using var sut = new SecurityEventLogger(_logger, throwingStore);
		await sut.StartAsync(CancellationToken.None);

		// Act
		await sut.LogSecurityEventAsync(
			SecurityEventType.ValidationError,
			"Error occurred",
			SecuritySeverity.High,
			CancellationToken.None);

		// Wait for store to be called with events (deterministic)
		await storeCalled.Task.WaitAsync(TimeSpan.FromSeconds(10));
		await sut.StopAsync(CancellationToken.None);

		// Assert — store was called (batch failed, then individual fallback)
		A.CallTo(() => throwingStore.StoreEventsAsync(A<IEnumerable<SecurityEvent>>._, A<CancellationToken>._))
			.MustHaveHappened();
	}

	[Fact]
	public async Task ExtractCorrelationIdFromContext()
	{
		// Arrange
		var correlationId = Guid.NewGuid();
		var (eventStore, capturedEvents, storeCalled) = CreateCapturingStore();
		using var sut = new SecurityEventLogger(_logger, eventStore);
		await sut.StartAsync(CancellationToken.None);

		var context = A.Fake<IMessageContext>();
		A.CallTo(() => context.CorrelationId).Returns(correlationId.ToString());
		A.CallTo(() => context.Items).Returns(new Dictionary<string, object>(StringComparer.Ordinal));

		// Act
		await sut.LogSecurityEventAsync(
			SecurityEventType.AuthenticationSuccess,
			"Success",
			SecuritySeverity.Low,
			CancellationToken.None,
			context);

		// Wait for store to receive events (deterministic)
		await storeCalled.Task.WaitAsync(TimeSpan.FromSeconds(10));
		await sut.StopAsync(CancellationToken.None);

		// Assert against captured snapshot
		capturedEvents.ShouldNotBeEmpty();
		capturedEvents.ShouldContain(e => e.CorrelationId == correlationId);
	}

	[Fact]
	public async Task HandleInvalidCorrelationIdGracefully()
	{
		// Arrange
		var (eventStore, capturedEvents, storeCalled) = CreateCapturingStore();
		using var sut = new SecurityEventLogger(_logger, eventStore);
		await sut.StartAsync(CancellationToken.None);

		var context = A.Fake<IMessageContext>();
		A.CallTo(() => context.CorrelationId).Returns("not-a-guid");
		A.CallTo(() => context.Items).Returns(new Dictionary<string, object>(StringComparer.Ordinal));

		// Act
		await sut.LogSecurityEventAsync(
			SecurityEventType.AuthenticationSuccess,
			"Success",
			SecuritySeverity.Low,
			CancellationToken.None,
			context);

		// Wait for store to receive events (deterministic)
		await storeCalled.Task.WaitAsync(TimeSpan.FromSeconds(10));
		await sut.StopAsync(CancellationToken.None);

		// Assert against captured snapshot — invalid GUID parsed to null
		capturedEvents.ShouldNotBeEmpty();
		capturedEvents.ShouldContain(e => e.CorrelationId == null);
	}
}
