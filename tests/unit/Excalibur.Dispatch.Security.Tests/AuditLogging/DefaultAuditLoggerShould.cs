// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.AuditLogging;

namespace Excalibur.Dispatch.Security.Tests.AuditLogging;

[Trait("Category", "Unit")]
[UnitTest]
public sealed class DefaultAuditLoggerShould
{
	private readonly IAuditStore _mockStore;
	private readonly ILogger<DefaultAuditLogger> _logger;
	private readonly DefaultAuditLogger _auditLogger;

	public DefaultAuditLoggerShould()
	{
		_mockStore = A.Fake<IAuditStore>();
		_logger = NullLogger<DefaultAuditLogger>.Instance;
		_auditLogger = new DefaultAuditLogger(_mockStore, _logger);
	}

	#region Constructor Tests

	[Fact]
	public void Constructor_ThrowsOnNullStore()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			new DefaultAuditLogger(null!, _logger));
	}

	[Fact]
	public void Constructor_ThrowsOnNullLogger()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			new DefaultAuditLogger(_mockStore, null!));
	}

	#endregion Constructor Tests

	#region LogAsync Tests

	[Fact]
	public async Task LogAsync_DelegatestoStore()
	{
		// Arrange
		var auditEvent = CreateTestAuditEvent();
		var expectedResult = new AuditEventId
		{
			EventId = auditEvent.EventId,
			EventHash = "test-hash",
			SequenceNumber = 1,
			RecordedAt = DateTimeOffset.UtcNow
		};

		_ = A.CallTo(() => _mockStore.StoreAsync(auditEvent, A<CancellationToken>._))
			.Returns(Task.FromResult(expectedResult));

		// Act
		var result = await _auditLogger.LogAsync(auditEvent, CancellationToken.None);

		// Assert
		result.EventId.ShouldBe(expectedResult.EventId);
		result.EventHash.ShouldBe(expectedResult.EventHash);
		_ = A.CallTo(() => _mockStore.StoreAsync(auditEvent, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task LogAsync_ReturnsFailureIndicatorOnException()
	{
		// Arrange
		var auditEvent = CreateTestAuditEvent();

		_ = A.CallTo(() => _mockStore.StoreAsync(auditEvent, A<CancellationToken>._))
			.ThrowsAsync(new InvalidOperationException("Store error"));

		// Act - should not throw
		var result = await _auditLogger.LogAsync(auditEvent, CancellationToken.None);

		// Assert
		result.EventId.ShouldBe(auditEvent.EventId);
		result.EventHash.ShouldBeEmpty();
		result.SequenceNumber.ShouldBe(-1);
	}

	[Fact]
	public async Task LogAsync_FailureIndicatorHasRecordedAtTimestamp()
	{
		// Arrange
		var auditEvent = CreateTestAuditEvent();
		var beforeCall = DateTimeOffset.UtcNow;

		_ = A.CallTo(() => _mockStore.StoreAsync(auditEvent, A<CancellationToken>._))
			.ThrowsAsync(new InvalidOperationException("Store error"));

		// Act
		var result = await _auditLogger.LogAsync(auditEvent, CancellationToken.None);

		// Assert
		result.RecordedAt.ShouldBeGreaterThanOrEqualTo(beforeCall);
	}

	[Fact]
	public async Task LogAsync_ThrowsOnCancellation()
	{
		// Arrange
		var auditEvent = CreateTestAuditEvent();
		using var cts = new CancellationTokenSource();
		await cts.CancelAsync();

		_ = A.CallTo(() => _mockStore.StoreAsync(auditEvent, A<CancellationToken>._))
			.ThrowsAsync(new OperationCanceledException());

		// Act & Assert
		_ = await Should.ThrowAsync<OperationCanceledException>(() =>
			_auditLogger.LogAsync(auditEvent, cts.Token));
	}

	[Fact]
	public async Task LogAsync_ThrowsOnNullEvent()
	{
		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentNullException>(() =>
			_auditLogger.LogAsync(null!, CancellationToken.None));
	}

	[Fact]
	public async Task LogAsync_ThrowsOnEmptyEventId()
	{
		// Arrange
		var auditEvent = CreateTestAuditEvent() with { EventId = "" };

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentException>(() =>
			_auditLogger.LogAsync(auditEvent, CancellationToken.None));
	}

	[Fact]
	public async Task LogAsync_ThrowsOnWhitespaceEventId()
	{
		// Arrange
		var auditEvent = CreateTestAuditEvent() with { EventId = "   " };

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentException>(() =>
			_auditLogger.LogAsync(auditEvent, CancellationToken.None));
	}

	[Fact]
	public async Task LogAsync_ThrowsOnEmptyAction()
	{
		// Arrange
		var auditEvent = CreateTestAuditEvent() with { Action = "" };

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentException>(() =>
			_auditLogger.LogAsync(auditEvent, CancellationToken.None));
	}

	[Fact]
	public async Task LogAsync_ThrowsOnWhitespaceAction()
	{
		// Arrange
		var auditEvent = CreateTestAuditEvent() with { Action = "   " };

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentException>(() =>
			_auditLogger.LogAsync(auditEvent, CancellationToken.None));
	}

	[Fact]
	public async Task LogAsync_ThrowsOnEmptyActorId()
	{
		// Arrange
		var auditEvent = CreateTestAuditEvent() with { ActorId = "" };

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentException>(() =>
			_auditLogger.LogAsync(auditEvent, CancellationToken.None));
	}

	[Fact]
	public async Task LogAsync_ThrowsOnWhitespaceActorId()
	{
		// Arrange
		var auditEvent = CreateTestAuditEvent() with { ActorId = "   " };

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentException>(() =>
			_auditLogger.LogAsync(auditEvent, CancellationToken.None));
	}

	[Fact]
	public async Task LogAsync_ThrowsOnDefaultTimestamp()
	{
		// Arrange
		var auditEvent = CreateTestAuditEvent() with { Timestamp = default };

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentException>(() =>
			_auditLogger.LogAsync(auditEvent, CancellationToken.None));
	}

	[Fact]
	public async Task LogAsync_PassesCancellationTokenToStore()
	{
		// Arrange
		var auditEvent = CreateTestAuditEvent();
		using var cts = new CancellationTokenSource();
		var token = cts.Token;
		var expectedResult = new AuditEventId
		{
			EventId = auditEvent.EventId,
			EventHash = "hash",
			SequenceNumber = 1,
			RecordedAt = DateTimeOffset.UtcNow
		};

		_ = A.CallTo(() => _mockStore.StoreAsync(auditEvent, token))
			.Returns(Task.FromResult(expectedResult));

		// Act
		_ = await _auditLogger.LogAsync(auditEvent, token);

		// Assert
		_ = A.CallTo(() => _mockStore.StoreAsync(auditEvent, token))
			.MustHaveHappenedOnceExactly();
	}

	#endregion LogAsync Tests

	#region VerifyIntegrityAsync Tests

	[Fact]
	public async Task VerifyIntegrityAsync_DelegatestoStore()
	{
		// Arrange
		var startDate = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);
		var endDate = new DateTimeOffset(2025, 1, 31, 0, 0, 0, TimeSpan.Zero);
		var expectedResult = AuditIntegrityResult.Valid(100, startDate, endDate);

		_ = A.CallTo(() => _mockStore.VerifyChainIntegrityAsync(startDate, endDate, A<CancellationToken>._))
			.Returns(Task.FromResult(expectedResult));

		// Act
		var result = await _auditLogger.VerifyIntegrityAsync(startDate, endDate, CancellationToken.None);

		// Assert
		result.IsValid.ShouldBeTrue();
		result.EventsVerified.ShouldBe(100);
		_ = A.CallTo(() => _mockStore.VerifyChainIntegrityAsync(startDate, endDate, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task VerifyIntegrityAsync_ReturnsFailedResult()
	{
		// Arrange
		var startDate = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);
		var endDate = new DateTimeOffset(2025, 1, 31, 0, 0, 0, TimeSpan.Zero);
		var expectedResult = AuditIntegrityResult.Invalid(
			50, startDate, endDate, "evt-25", "Hash mismatch", 3);

		_ = A.CallTo(() => _mockStore.VerifyChainIntegrityAsync(startDate, endDate, A<CancellationToken>._))
			.Returns(Task.FromResult(expectedResult));

		// Act
		var result = await _auditLogger.VerifyIntegrityAsync(startDate, endDate, CancellationToken.None);

		// Assert
		result.IsValid.ShouldBeFalse();
		result.EventsVerified.ShouldBe(50);
		result.FirstViolationEventId.ShouldBe("evt-25");
		result.ViolationDescription.ShouldBe("Hash mismatch");
	}

	[Fact]
	public async Task VerifyIntegrityAsync_ThrowsWhenStartAfterEnd()
	{
		// Arrange
		var startDate = new DateTimeOffset(2025, 2, 1, 0, 0, 0, TimeSpan.Zero);
		var endDate = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentException>(() =>
			_auditLogger.VerifyIntegrityAsync(startDate, endDate, CancellationToken.None));
	}

	[Fact]
	public async Task VerifyIntegrityAsync_AllowsEqualStartAndEnd()
	{
		// Arrange
		var date = new DateTimeOffset(2025, 1, 15, 0, 0, 0, TimeSpan.Zero);
		var expectedResult = AuditIntegrityResult.Valid(0, date, date);

		_ = A.CallTo(() => _mockStore.VerifyChainIntegrityAsync(date, date, A<CancellationToken>._))
			.Returns(Task.FromResult(expectedResult));

		// Act
		var result = await _auditLogger.VerifyIntegrityAsync(date, date, CancellationToken.None);

		// Assert
		result.IsValid.ShouldBeTrue();
	}

	[Fact]
	public async Task VerifyIntegrityAsync_PropagatesExceptions()
	{
		// Arrange
		var startDate = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);
		var endDate = new DateTimeOffset(2025, 1, 31, 0, 0, 0, TimeSpan.Zero);

		_ = A.CallTo(() => _mockStore.VerifyChainIntegrityAsync(startDate, endDate, A<CancellationToken>._))
			.ThrowsAsync(new InvalidOperationException("Verification error"));

		// Act & Assert
		_ = await Should.ThrowAsync<InvalidOperationException>(() =>
			_auditLogger.VerifyIntegrityAsync(startDate, endDate, CancellationToken.None));
	}

	[Fact]
	public async Task VerifyIntegrityAsync_PassesCancellationToken()
	{
		// Arrange
		var startDate = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);
		var endDate = new DateTimeOffset(2025, 1, 31, 0, 0, 0, TimeSpan.Zero);
		using var cts = new CancellationTokenSource();
		var token = cts.Token;
		var expectedResult = AuditIntegrityResult.Valid(0, startDate, endDate);

		_ = A.CallTo(() => _mockStore.VerifyChainIntegrityAsync(startDate, endDate, token))
			.Returns(Task.FromResult(expectedResult));

		// Act
		_ = await _auditLogger.VerifyIntegrityAsync(startDate, endDate, token);

		// Assert
		_ = A.CallTo(() => _mockStore.VerifyChainIntegrityAsync(startDate, endDate, token))
			.MustHaveHappenedOnceExactly();
	}

	#endregion VerifyIntegrityAsync Tests

	private static AuditEvent CreateTestAuditEvent() =>
		new()
		{
			EventId = "test-event-id",
			EventType = AuditEventType.DataAccess,
			Action = "Read",
			Outcome = AuditOutcome.Success,
			Timestamp = DateTimeOffset.UtcNow,
			ActorId = "user-123"
		};
}
