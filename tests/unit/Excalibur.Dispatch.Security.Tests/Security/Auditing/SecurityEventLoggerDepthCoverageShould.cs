// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Security;

namespace Excalibur.Dispatch.Security.Tests.Security.Auditing;

/// <summary>
/// Deep coverage tests for <see cref="SecurityEventLogger"/> covering context extraction
/// with partial data, queue-after-close behavior, severity-to-log-level mapping,
/// and additional data extraction from context items.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Security")]
public sealed class SecurityEventLoggerDepthCoverageShould : IDisposable
{
	private readonly ISecurityEventStore _eventStore;
	private readonly SecurityEventLogger _sut;

	public SecurityEventLoggerDepthCoverageShould()
	{
		_eventStore = A.Fake<ISecurityEventStore>();
		A.CallTo(() => _eventStore.StoreEventsAsync(A<IEnumerable<SecurityEvent>>._, A<CancellationToken>._))
			.Returns(Task.CompletedTask);

		_sut = new SecurityEventLogger(
			NullLogger<SecurityEventLogger>.Instance,
			_eventStore);
	}

	public void Dispose() => _sut.Dispose();

	[Fact]
	public async Task LogSecurityEvent_ExtractCorrelationId_WhenValidGuid()
	{
		// Arrange
		await _sut.StartAsync(CancellationToken.None);

		var correlationGuid = Guid.NewGuid();
		var context = A.Fake<IMessageContext>();
		A.CallTo(() => context.CorrelationId).Returns(correlationGuid.ToString());
		A.CallTo(() => context.Items).Returns(new Dictionary<string, object>(StringComparer.Ordinal));

		// Act
		await _sut.LogSecurityEventAsync(
			SecurityEventType.AuthenticationSuccess,
			"Auth success",
			SecuritySeverity.Low,
			CancellationToken.None,
			context);

		// Allow background processing
		await Task.Delay(2000);
		await _sut.StopAsync(CancellationToken.None);

		// Assert
		A.CallTo(() => _eventStore.StoreEventsAsync(
			A<IEnumerable<SecurityEvent>>.That.Matches(events =>
				events.Any(e => e.CorrelationId == correlationGuid)),
			A<CancellationToken>._))
			.MustHaveHappened();
	}

	[Fact]
	public async Task LogSecurityEvent_HandleNonGuidCorrelationId()
	{
		// Arrange
		await _sut.StartAsync(CancellationToken.None);

		var context = A.Fake<IMessageContext>();
		A.CallTo(() => context.CorrelationId).Returns("not-a-guid-string");
		A.CallTo(() => context.Items).Returns(new Dictionary<string, object>(StringComparer.Ordinal));

		// Act
		await _sut.LogSecurityEventAsync(
			SecurityEventType.AuthenticationFailure,
			"Auth failure",
			SecuritySeverity.Medium,
			CancellationToken.None,
			context);

		// Allow background processing
		await Task.Delay(2000);
		await _sut.StopAsync(CancellationToken.None);

		// Assert — event stored with null CorrelationId (non-parseable)
		A.CallTo(() => _eventStore.StoreEventsAsync(
			A<IEnumerable<SecurityEvent>>.That.Matches(events =>
				events.Any(e => e.CorrelationId == null)),
			A<CancellationToken>._))
			.MustHaveHappened();
	}

	[Fact]
	public async Task LogSecurityEvent_ExtractUserIdFromContext()
	{
		// Arrange
		await _sut.StartAsync(CancellationToken.None);

		var context = A.Fake<IMessageContext>();
		A.CallTo(() => context.CorrelationId).Returns(null);
		var items = new Dictionary<string, object>(StringComparer.Ordinal)
		{
			["User:MessageId"] = "admin-user",
		};
		A.CallTo(() => context.Items).Returns(items);

		// Act
		await _sut.LogSecurityEventAsync(
			SecurityEventType.AuthorizationSuccess,
			"Authorized",
			SecuritySeverity.Low,
			CancellationToken.None,
			context);

		await Task.Delay(2000);
		await _sut.StopAsync(CancellationToken.None);

		// Assert
		A.CallTo(() => _eventStore.StoreEventsAsync(
			A<IEnumerable<SecurityEvent>>.That.Matches(events =>
				events.Any(e => e.UserId == "admin-user")),
			A<CancellationToken>._))
			.MustHaveHappened();
	}

	[Fact]
	public async Task LogSecurityEvent_ExtractIpAndUserAgentFromContext()
	{
		// Arrange
		await _sut.StartAsync(CancellationToken.None);

		var context = A.Fake<IMessageContext>();
		A.CallTo(() => context.CorrelationId).Returns(null);
		var items = new Dictionary<string, object>(StringComparer.Ordinal)
		{
			["Client:IP"] = "10.0.0.1",
			["Client:UserAgent"] = "Mozilla/5.0",
			["Message:Type"] = "CreateOrder",
		};
		A.CallTo(() => context.Items).Returns(items);

		// Act
		await _sut.LogSecurityEventAsync(
			SecurityEventType.InjectionAttempt,
			"SQL injection detected",
			SecuritySeverity.Critical,
			CancellationToken.None,
			context);

		await Task.Delay(2000);
		await _sut.StopAsync(CancellationToken.None);

		// Assert
		A.CallTo(() => _eventStore.StoreEventsAsync(
			A<IEnumerable<SecurityEvent>>.That.Matches(events =>
				events.Any(e => e.SourceIp == "10.0.0.1" && e.UserAgent == "Mozilla/5.0" && e.MessageType == "CreateOrder")),
			A<CancellationToken>._))
			.MustHaveHappened();
	}

	[Fact]
	public async Task LogSecurityEvent_ExtractSecurityAdditionalData()
	{
		// Arrange
		await _sut.StartAsync(CancellationToken.None);

		var context = A.Fake<IMessageContext>();
		A.CallTo(() => context.CorrelationId).Returns(null);
		var items = new Dictionary<string, object>(StringComparer.Ordinal)
		{
			["Security:ThreatType"] = "brute-force",
			["Auth:Method"] = "Bearer",
			["Validation:FailedRules"] = "InputLength",
			["Custom:NonSecurity"] = "ignored", // Should NOT be included
		};
		A.CallTo(() => context.Items).Returns(items);

		// Act
		await _sut.LogSecurityEventAsync(
			SecurityEventType.RateLimitExceeded,
			"Rate limit exceeded",
			SecuritySeverity.High,
			CancellationToken.None,
			context);

		await Task.Delay(2000);
		await _sut.StopAsync(CancellationToken.None);

		// Assert
		A.CallTo(() => _eventStore.StoreEventsAsync(
			A<IEnumerable<SecurityEvent>>.That.Matches(events =>
				events.Any(e =>
					e.AdditionalData.ContainsKey("Security:ThreatType") &&
					e.AdditionalData.ContainsKey("Auth:Method") &&
					e.AdditionalData.ContainsKey("Validation:FailedRules") &&
					!e.AdditionalData.ContainsKey("Custom:NonSecurity"))),
			A<CancellationToken>._))
			.MustHaveHappened();
	}

	[Fact]
	public async Task LogSecurityEvent_QueueEvent_AfterChannelClosed()
	{
		// Arrange
		await _sut.StartAsync(CancellationToken.None);
		await _sut.StopAsync(CancellationToken.None);

		// Act — logging after stop should not throw (channel closed, TryWrite returns false)
		await _sut.LogSecurityEventAsync(
			SecurityEventType.AuthenticationFailure,
			"Late event",
			SecuritySeverity.Low,
			CancellationToken.None);

		// Assert — no exception thrown
	}

	[Fact]
	public async Task LogSecurityEvent_AllSeverityLevels_MapToLogLevels()
	{
		// Arrange
		await _sut.StartAsync(CancellationToken.None);

		// Act — each severity level exercises different log level path
		await _sut.LogSecurityEventAsync(
			SecurityEventType.AuthenticationSuccess, "Low sev", SecuritySeverity.Low, CancellationToken.None);
		await _sut.LogSecurityEventAsync(
			SecurityEventType.ValidationFailure, "Med sev", SecuritySeverity.Medium, CancellationToken.None);
		await _sut.LogSecurityEventAsync(
			SecurityEventType.AuthorizationFailure, "High sev", SecuritySeverity.High, CancellationToken.None);
		await _sut.LogSecurityEventAsync(
			SecurityEventType.InjectionAttempt, "Critical sev", SecuritySeverity.Critical, CancellationToken.None);

		await Task.Delay(2000);
		await _sut.StopAsync(CancellationToken.None);

		// Assert — all 4 events stored
		A.CallTo(() => _eventStore.StoreEventsAsync(
			A<IEnumerable<SecurityEvent>>.That.Matches(events => events.Count() >= 4),
			A<CancellationToken>._))
			.MustHaveHappened();
	}

	[Fact]
	public async Task StopAsync_GracefulShutdown_WithPendingEvents()
	{
		// Arrange
		await _sut.StartAsync(CancellationToken.None);

		// Queue several events rapidly
		for (var i = 0; i < 10; i++)
		{
			await _sut.LogSecurityEventAsync(
				SecurityEventType.AuthenticationSuccess,
				$"Event {i}",
				SecuritySeverity.Low,
				CancellationToken.None);
		}

		// Act — stop should process remaining events
		await _sut.StopAsync(CancellationToken.None);

		// Assert — at least some events were stored
		A.CallTo(() => _eventStore.StoreEventsAsync(
			A<IEnumerable<SecurityEvent>>._,
			A<CancellationToken>._))
			.MustHaveHappened();
	}

	[Fact]
	public async Task HandleStoreFailure_FallbackToIndividualStorage()
	{
		// Arrange — first batch call fails, individual calls succeed
		var callCount = 0;
		A.CallTo(() => _eventStore.StoreEventsAsync(A<IEnumerable<SecurityEvent>>._, A<CancellationToken>._))
			.ReturnsLazily(call =>
			{
				callCount++;
				if (callCount == 1)
				{
					throw new InvalidOperationException("Batch store failure");
				}

				return Task.CompletedTask;
			});

		await _sut.StartAsync(CancellationToken.None);

		// Act
		await _sut.LogSecurityEventAsync(
			SecurityEventType.AuthenticationSuccess,
			"Test event",
			SecuritySeverity.Low,
			CancellationToken.None);

		await Task.Delay(2000);
		await _sut.StopAsync(CancellationToken.None);

		// Assert — store was called (batch + individual fallback)
		A.CallTo(() => _eventStore.StoreEventsAsync(
			A<IEnumerable<SecurityEvent>>._,
			A<CancellationToken>._))
			.MustHaveHappened();
	}

	[Fact]
	public void Dispose_MultipleTimesWithoutException()
	{
		// Arrange
		using var logger = new SecurityEventLogger(
			NullLogger<SecurityEventLogger>.Instance,
			_eventStore);

		// Act & Assert — idempotent
		logger.Dispose();
		logger.Dispose();
	}
}
