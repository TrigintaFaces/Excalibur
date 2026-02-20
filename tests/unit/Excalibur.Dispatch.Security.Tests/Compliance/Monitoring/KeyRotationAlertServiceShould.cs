// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Dispatch.Security.Tests.Compliance.Monitoring;

[Trait("Category", TestCategories.Unit)]
public sealed class KeyRotationAlertServiceShould
{
	private readonly IKeyRotationAlertHandler _handler;
	private readonly IComplianceMetrics _metrics;
	private readonly ILogger<KeyRotationAlertService> _logger;
	private readonly KeyRotationAlertService _sut;

	public KeyRotationAlertServiceShould()
	{
		_handler = A.Fake<IKeyRotationAlertHandler>();
		_metrics = A.Fake<IComplianceMetrics>();
		_logger = NullLogger<KeyRotationAlertService>.Instance;
		_sut = new KeyRotationAlertService(
			[_handler],
			_metrics,
			_logger,
			new KeyRotationAlertOptions
			{
				AlertAfterFailures = 1,
				NotifyOnSuccess = true,
				ExpirationWarningDays = 14
			});
	}

	[Fact]
	public void ThrowWhenHandlersIsNull()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			new KeyRotationAlertService(null!, _metrics, _logger));
	}

	[Fact]
	public void ThrowWhenLoggerIsNull()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			new KeyRotationAlertService([_handler], _metrics, null!));
	}

	[Fact]
	public async Task ReportRotationFailureAsync_TriggerAlert_OnFirstFailure()
	{
		// Act
		await _sut.ReportRotationFailureAsync("key-1", "InMemory", "Connection timeout", CancellationToken.None);

		// Assert
		_ = A.CallTo(() => _handler.HandleRotationFailureAsync(
				A<KeyRotationFailureAlert>.That.Matches(a =>
					a.KeyId == "key-1" &&
					a.Provider == "InMemory" &&
					a.ErrorMessage == "Connection timeout" &&
					a.ConsecutiveFailures == 1),
				A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();

		_ = A.CallTo(() => _metrics.RecordKeyRotationFailure("key-1", "InMemory", "Timeout"))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task ReportRotationFailureAsync_TrackConsecutiveFailures()
	{
		// Act
		await _sut.ReportRotationFailureAsync("key-1", "InMemory", "Error 1", CancellationToken.None);
		await _sut.ReportRotationFailureAsync("key-1", "InMemory", "Error 2", CancellationToken.None);
		await _sut.ReportRotationFailureAsync("key-1", "InMemory", "Error 3", CancellationToken.None);

		// Assert
		_ = A.CallTo(() => _handler.HandleRotationFailureAsync(
				A<KeyRotationFailureAlert>.That.Matches(a => a.ConsecutiveFailures == 3),
				A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task ReportRotationFailureAsync_ThrowOnNullKeyId()
	{
		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentException>(() =>
			_sut.ReportRotationFailureAsync(null!, "provider", "error", CancellationToken.None));
	}

	[Fact]
	public async Task ReportRotationFailureAsync_ThrowOnEmptyProvider()
	{
		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentException>(() =>
			_sut.ReportRotationFailureAsync("key-1", "", "error", CancellationToken.None));
	}

	[Fact]
	public async Task ReportRotationFailureAsync_ExtractErrorType_Timeout()
	{
		// Act
		await _sut.ReportRotationFailureAsync("key-1", "InMemory", "Operation timeout exceeded", CancellationToken.None);

		// Assert
		_ = A.CallTo(() => _metrics.RecordKeyRotationFailure("key-1", "InMemory", "Timeout"))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task ReportRotationFailureAsync_ExtractErrorType_Unauthorized()
	{
		// Act
		await _sut.ReportRotationFailureAsync("key-1", "AzureKeyVault", "Unauthorized access denied", CancellationToken.None);

		// Assert
		_ = A.CallTo(() => _metrics.RecordKeyRotationFailure("key-1", "AzureKeyVault", "AuthorizationError"))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task ReportRotationFailureAsync_ExtractErrorType_NotFound()
	{
		// Act
		await _sut.ReportRotationFailureAsync("key-1", "AwsKms", "Key not found in vault", CancellationToken.None);

		// Assert
		_ = A.CallTo(() => _metrics.RecordKeyRotationFailure("key-1", "AwsKms", "NotFound"))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task ReportRotationFailureAsync_ExtractErrorType_NetworkError()
	{
		// Act
		await _sut.ReportRotationFailureAsync("key-1", "Vault", "Network connection failed", CancellationToken.None);

		// Assert
		_ = A.CallTo(() => _metrics.RecordKeyRotationFailure("key-1", "Vault", "NetworkError"))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task ReportRotationFailureAsync_ExtractErrorType_General()
	{
		// Act
		await _sut.ReportRotationFailureAsync("key-1", "InMemory", "Something unexpected happened", CancellationToken.None);

		// Assert
		_ = A.CallTo(() => _metrics.RecordKeyRotationFailure("key-1", "InMemory", "GeneralError"))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task ReportRotationSuccessAsync_ResetFailureCount()
	{
		// Arrange - simulate some failures first
		await _sut.ReportRotationFailureAsync("key-1", "InMemory", "Error", CancellationToken.None);
		await _sut.ReportRotationFailureAsync("key-1", "InMemory", "Error", CancellationToken.None);

		// Act
		await _sut.ReportRotationSuccessAsync("key-1", "InMemory", "v1", "v2", CancellationToken.None);

		// Assert
		_sut.GetFailureCount("key-1", "InMemory").ShouldBe(0);

		_ = A.CallTo(() => _metrics.RecordKeyRotation("key-1", "InMemory"))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task ReportRotationSuccessAsync_NotifyHandler_WhenEnabled()
	{
		// Act
		await _sut.ReportRotationSuccessAsync("key-1", "InMemory", "v1", "v2", CancellationToken.None);

		// Assert
		_ = A.CallTo(() => _handler.HandleRotationSuccessAsync(
				A<KeyRotationSuccessNotification>.That.Matches(n =>
					n.KeyId == "key-1" &&
					n.Provider == "InMemory" &&
					n.OldKeyVersion == "v1" &&
					n.NewKeyVersion == "v2"),
				A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task ReportRotationSuccessAsync_SkipNotification_WhenDisabled()
	{
		// Arrange
		var sut = new KeyRotationAlertService(
			[_handler],
			_metrics,
			_logger,
			new KeyRotationAlertOptions { NotifyOnSuccess = false });

		// Act
		await sut.ReportRotationSuccessAsync("key-1", "InMemory", "v1", "v2", CancellationToken.None);

		// Assert
		A.CallTo(() => _handler.HandleRotationSuccessAsync(
				A<KeyRotationSuccessNotification>._,
				A<CancellationToken>._))
			.MustNotHaveHappened();
	}

	[Fact]
	public async Task ReportExpirationWarningAsync_TriggerAlert_WhenWithinWarningPeriod()
	{
		// Arrange
		var expiresAt = DateTimeOffset.UtcNow.AddDays(7); // 7 days from now

		// Act
		await _sut.ReportExpirationWarningAsync("key-1", "InMemory", expiresAt, CancellationToken.None);

		// Assert
		_ = A.CallTo(() => _handler.HandleExpirationWarningAsync(
				A<KeyExpirationAlert>.That.Matches(a =>
					a.KeyId == "key-1" &&
					a.Provider == "InMemory" &&
					a.DaysUntilExpiration <= 14),
				A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task ReportExpirationWarningAsync_SkipAlert_WhenOutsideWarningPeriod()
	{
		// Arrange
		var expiresAt = DateTimeOffset.UtcNow.AddDays(30); // 30 days from now

		// Act
		await _sut.ReportExpirationWarningAsync("key-1", "InMemory", expiresAt, CancellationToken.None);

		// Assert
		A.CallTo(() => _handler.HandleExpirationWarningAsync(
				A<KeyExpirationAlert>._,
				A<CancellationToken>._))
			.MustNotHaveHappened();
	}

	[Fact]
	public void ResetFailureCount_ClearConsecutiveFailures()
	{
		// Arrange
		for (var i = 0; i < 5; i++)
		{
			_ = _sut.ReportRotationFailureAsync("key-1", "InMemory", "Error", CancellationToken.None);
		}

		// Act
		_sut.ResetFailureCount("key-1", "InMemory");

		// Assert
		_sut.GetFailureCount("key-1", "InMemory").ShouldBe(0);
	}

	[Fact]
	public void GetFailureCount_ReturnZero_WhenNoFailures()
	{
		// Act
		var count = _sut.GetFailureCount("non-existent", "provider");

		// Assert
		count.ShouldBe(0);
	}

	[Fact]
	public async Task NotifyHandlers_ContinueOnHandlerException()
	{
		// Arrange
		var failingHandler = A.Fake<IKeyRotationAlertHandler>();
		var successHandler = A.Fake<IKeyRotationAlertHandler>();

		_ = A.CallTo(() => failingHandler.HandleRotationFailureAsync(
				A<KeyRotationFailureAlert>._,
				A<CancellationToken>._))
			.Throws(new InvalidOperationException("Handler failed"));

		var sut = new KeyRotationAlertService(
			[failingHandler, successHandler],
			_metrics,
			_logger,
			new KeyRotationAlertOptions { AlertAfterFailures = 1 });

		// Act
		await sut.ReportRotationFailureAsync("key-1", "InMemory", "Error", CancellationToken.None);

		// Assert - second handler should still be called
		_ = A.CallTo(() => successHandler.HandleRotationFailureAsync(
				A<KeyRotationFailureAlert>._,
				A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task ReportRotationFailureAsync_DelayAlert_UntilThresholdReached()
	{
		// Arrange
		var sut = new KeyRotationAlertService(
			[_handler],
			_metrics,
			_logger,
			new KeyRotationAlertOptions { AlertAfterFailures = 3 });

		// Act - first two failures
		await sut.ReportRotationFailureAsync("key-1", "InMemory", "Error 1", CancellationToken.None);
		await sut.ReportRotationFailureAsync("key-1", "InMemory", "Error 2", CancellationToken.None);

		// Assert - no alert yet
		A.CallTo(() => _handler.HandleRotationFailureAsync(
				A<KeyRotationFailureAlert>._,
				A<CancellationToken>._))
			.MustNotHaveHappened();

		// Act - third failure
		await sut.ReportRotationFailureAsync("key-1", "InMemory", "Error 3", CancellationToken.None);

		// Assert - alert triggered
		_ = A.CallTo(() => _handler.HandleRotationFailureAsync(
				A<KeyRotationFailureAlert>.That.Matches(a => a.ConsecutiveFailures == 3),
				A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}
}
