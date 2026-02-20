// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Security.Tests.Compliance.Monitoring;

/// <summary>
/// Extended unit tests for <see cref="KeyRotationAlertService"/>.
/// Tests error type extraction, threshold behavior, and edge cases.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "Compliance")]
public sealed class KeyRotationAlertServiceExtendedShould
{
	private readonly IKeyRotationAlertHandler _handler;
	private readonly IComplianceMetrics _metrics;

	public KeyRotationAlertServiceExtendedShould()
	{
		_handler = A.Fake<IKeyRotationAlertHandler>();
		_metrics = A.Fake<IComplianceMetrics>();
	}

	#region Constructor Tests

	[Fact]
	public void Constructor_ThrowsArgumentNullException_WhenHandlersIsNull()
	{
		_ = Should.Throw<ArgumentNullException>(() => new KeyRotationAlertService(
			null!,
			_metrics,
			NullLogger<KeyRotationAlertService>.Instance));
	}

	[Fact]
	public void Constructor_ThrowsArgumentNullException_WhenLoggerIsNull()
	{
		_ = Should.Throw<ArgumentNullException>(() => new KeyRotationAlertService(
			new[] { _handler },
			_metrics,
			null!));
	}

	[Fact]
	public void Constructor_AcceptsNullMetrics()
	{
		// Should not throw
		var sut = new KeyRotationAlertService(
			new[] { _handler },
			null,
			NullLogger<KeyRotationAlertService>.Instance);
		sut.ShouldNotBeNull();
	}

	#endregion

	#region ReportRotationFailureAsync Tests

	[Fact]
	public async Task ReportRotationFailureAsync_ThrowsArgumentException_WhenKeyIdIsEmpty()
	{
		var sut = CreateSut();
		await Should.ThrowAsync<ArgumentException>(
			() => sut.ReportRotationFailureAsync("", "provider", "error", CancellationToken.None)).ConfigureAwait(false);
	}

	[Fact]
	public async Task ReportRotationFailureAsync_ThrowsArgumentException_WhenProviderIsEmpty()
	{
		var sut = CreateSut();
		await Should.ThrowAsync<ArgumentException>(
			() => sut.ReportRotationFailureAsync("key-1", "", "error", CancellationToken.None)).ConfigureAwait(false);
	}

	[Fact]
	public async Task ReportRotationFailureAsync_RecordsMetric()
	{
		// Arrange
		var sut = CreateSut();

		// Act
		await sut.ReportRotationFailureAsync("key-1", "provider-1", "timeout error", CancellationToken.None).ConfigureAwait(false);

		// Assert
		A.CallTo(() => _metrics.RecordKeyRotationFailure("key-1", "provider-1", "Timeout"))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task ReportRotationFailureAsync_TriggersAlert_OnFirstFailure_WithDefaultOptions()
	{
		// Arrange - default AlertAfterFailures is 1
		var sut = CreateSut();

		// Act
		await sut.ReportRotationFailureAsync("key-1", "provider-1", "some error", CancellationToken.None).ConfigureAwait(false);

		// Assert
		A.CallTo(() => _handler.HandleRotationFailureAsync(A<KeyRotationFailureAlert>._, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task ReportRotationFailureAsync_DoesNotTriggerAlert_WhenBelowThreshold()
	{
		// Arrange - set threshold to 3
		var options = new KeyRotationAlertOptions { AlertAfterFailures = 3 };
		var sut = CreateSut(options);

		// Act - only 2 failures
		await sut.ReportRotationFailureAsync("key-1", "provider-1", "error1", CancellationToken.None).ConfigureAwait(false);
		await sut.ReportRotationFailureAsync("key-1", "provider-1", "error2", CancellationToken.None).ConfigureAwait(false);

		// Assert
		A.CallTo(() => _handler.HandleRotationFailureAsync(A<KeyRotationFailureAlert>._, A<CancellationToken>._))
			.MustNotHaveHappened();
	}

	[Fact]
	public async Task ReportRotationFailureAsync_TriggersAlert_WhenThresholdReached()
	{
		// Arrange
		var options = new KeyRotationAlertOptions { AlertAfterFailures = 3 };
		var sut = CreateSut(options);

		// Act - exactly 3 failures
		await sut.ReportRotationFailureAsync("key-1", "provider-1", "error1", CancellationToken.None).ConfigureAwait(false);
		await sut.ReportRotationFailureAsync("key-1", "provider-1", "error2", CancellationToken.None).ConfigureAwait(false);
		await sut.ReportRotationFailureAsync("key-1", "provider-1", "error3", CancellationToken.None).ConfigureAwait(false);

		// Assert
		A.CallTo(() => _handler.HandleRotationFailureAsync(A<KeyRotationFailureAlert>._, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task ReportRotationFailureAsync_ContinuesOnHandlerException()
	{
		// Arrange
		var handler1 = A.Fake<IKeyRotationAlertHandler>();
		var handler2 = A.Fake<IKeyRotationAlertHandler>();
		A.CallTo(() => handler1.HandleRotationFailureAsync(A<KeyRotationFailureAlert>._, A<CancellationToken>._))
			.Throws(new InvalidOperationException("handler error"));

		var sut = new KeyRotationAlertService(
			new[] { handler1, handler2 },
			_metrics,
			NullLogger<KeyRotationAlertService>.Instance);

		// Act
		await sut.ReportRotationFailureAsync("key-1", "provider-1", "error", CancellationToken.None).ConfigureAwait(false);

		// Assert - second handler still called despite first throwing
		A.CallTo(() => handler2.HandleRotationFailureAsync(A<KeyRotationFailureAlert>._, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task ReportRotationFailureAsync_ExtractsTimeoutErrorType()
	{
		var sut = CreateSut();
		await sut.ReportRotationFailureAsync("key-1", "prov", "Connection timeout occurred", CancellationToken.None).ConfigureAwait(false);
		A.CallTo(() => _metrics.RecordKeyRotationFailure("key-1", "prov", "Timeout")).MustHaveHappened();
	}

	[Fact]
	public async Task ReportRotationFailureAsync_ExtractsAuthorizationErrorType()
	{
		var sut = CreateSut();
		await sut.ReportRotationFailureAsync("key-1", "prov", "unauthorized access", CancellationToken.None).ConfigureAwait(false);
		A.CallTo(() => _metrics.RecordKeyRotationFailure("key-1", "prov", "AuthorizationError")).MustHaveHappened();
	}

	[Fact]
	public async Task ReportRotationFailureAsync_ExtractsForbiddenErrorType()
	{
		var sut = CreateSut();
		await sut.ReportRotationFailureAsync("key-1", "prov", "access forbidden", CancellationToken.None).ConfigureAwait(false);
		A.CallTo(() => _metrics.RecordKeyRotationFailure("key-1", "prov", "AuthorizationError")).MustHaveHappened();
	}

	[Fact]
	public async Task ReportRotationFailureAsync_ExtractsNotFoundErrorType()
	{
		var sut = CreateSut();
		await sut.ReportRotationFailureAsync("key-1", "prov", "key not found in vault", CancellationToken.None).ConfigureAwait(false);
		A.CallTo(() => _metrics.RecordKeyRotationFailure("key-1", "prov", "NotFound")).MustHaveHappened();
	}

	[Fact]
	public async Task ReportRotationFailureAsync_ExtractsNetworkErrorType()
	{
		var sut = CreateSut();
		await sut.ReportRotationFailureAsync("key-1", "prov", "network error occurred", CancellationToken.None).ConfigureAwait(false);
		A.CallTo(() => _metrics.RecordKeyRotationFailure("key-1", "prov", "NetworkError")).MustHaveHappened();
	}

	[Fact]
	public async Task ReportRotationFailureAsync_ExtractsConnectionErrorType()
	{
		var sut = CreateSut();
		await sut.ReportRotationFailureAsync("key-1", "prov", "connection refused", CancellationToken.None).ConfigureAwait(false);
		A.CallTo(() => _metrics.RecordKeyRotationFailure("key-1", "prov", "NetworkError")).MustHaveHappened();
	}

	[Fact]
	public async Task ReportRotationFailureAsync_ExtractsGeneralErrorType()
	{
		var sut = CreateSut();
		await sut.ReportRotationFailureAsync("key-1", "prov", "something bad happened", CancellationToken.None).ConfigureAwait(false);
		A.CallTo(() => _metrics.RecordKeyRotationFailure("key-1", "prov", "GeneralError")).MustHaveHappened();
	}

	[Fact]
	public async Task ReportRotationFailureAsync_ExtractsUnknownErrorType_WhenEmpty()
	{
		var sut = CreateSut();
		await sut.ReportRotationFailureAsync("key-1", "prov", "", CancellationToken.None).ConfigureAwait(false);
		A.CallTo(() => _metrics.RecordKeyRotationFailure("key-1", "prov", "Unknown")).MustHaveHappened();
	}

	#endregion

	#region ReportRotationSuccessAsync Tests

	[Fact]
	public async Task ReportRotationSuccessAsync_ThrowsArgumentException_WhenKeyIdIsEmpty()
	{
		var sut = CreateSut();
		await Should.ThrowAsync<ArgumentException>(
			() => sut.ReportRotationSuccessAsync("", "provider", "v1", "v2", CancellationToken.None)).ConfigureAwait(false);
	}

	[Fact]
	public async Task ReportRotationSuccessAsync_ThrowsArgumentException_WhenProviderIsEmpty()
	{
		var sut = CreateSut();
		await Should.ThrowAsync<ArgumentException>(
			() => sut.ReportRotationSuccessAsync("key-1", "", "v1", "v2", CancellationToken.None)).ConfigureAwait(false);
	}

	[Fact]
	public async Task ReportRotationSuccessAsync_ResetsFailureCount()
	{
		// Arrange
		var options = new KeyRotationAlertOptions { AlertAfterFailures = 5 };
		var sut = CreateSut(options);

		// Report some failures
		await sut.ReportRotationFailureAsync("key-1", "prov", "err", CancellationToken.None).ConfigureAwait(false);
		await sut.ReportRotationFailureAsync("key-1", "prov", "err", CancellationToken.None).ConfigureAwait(false);
		sut.GetFailureCount("key-1", "prov").ShouldBe(2);

		// Act
		await sut.ReportRotationSuccessAsync("key-1", "prov", "v1", "v2", CancellationToken.None).ConfigureAwait(false);

		// Assert
		sut.GetFailureCount("key-1", "prov").ShouldBe(0);
	}

	[Fact]
	public async Task ReportRotationSuccessAsync_DoesNotNotify_WhenNotifyOnSuccessIsFalse()
	{
		// Arrange
		var sut = CreateSut(new KeyRotationAlertOptions { NotifyOnSuccess = false });

		// Act
		await sut.ReportRotationSuccessAsync("key-1", "prov", "v1", "v2", CancellationToken.None).ConfigureAwait(false);

		// Assert
		A.CallTo(() => _handler.HandleRotationSuccessAsync(A<KeyRotationSuccessNotification>._, A<CancellationToken>._))
			.MustNotHaveHappened();
	}

	[Fact]
	public async Task ReportRotationSuccessAsync_Notifies_WhenNotifyOnSuccessIsTrue()
	{
		// Arrange
		var sut = CreateSut(new KeyRotationAlertOptions { NotifyOnSuccess = true });

		// Act
		await sut.ReportRotationSuccessAsync("key-1", "prov", "v1", "v2", CancellationToken.None).ConfigureAwait(false);

		// Assert
		A.CallTo(() => _handler.HandleRotationSuccessAsync(A<KeyRotationSuccessNotification>._, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task ReportRotationSuccessAsync_RecordsMetric()
	{
		var sut = CreateSut();
		await sut.ReportRotationSuccessAsync("key-1", "prov", "v1", "v2", CancellationToken.None).ConfigureAwait(false);
		A.CallTo(() => _metrics.RecordKeyRotation("key-1", "prov")).MustHaveHappenedOnceExactly();
	}

	#endregion

	#region ReportExpirationWarningAsync Tests

	[Fact]
	public async Task ReportExpirationWarningAsync_ThrowsArgumentException_WhenKeyIdIsEmpty()
	{
		var sut = CreateSut();
		await Should.ThrowAsync<ArgumentException>(
			() => sut.ReportExpirationWarningAsync("", "prov", DateTimeOffset.UtcNow.AddDays(7), CancellationToken.None)).ConfigureAwait(false);
	}

	[Fact]
	public async Task ReportExpirationWarningAsync_ThrowsArgumentException_WhenProviderIsEmpty()
	{
		var sut = CreateSut();
		await Should.ThrowAsync<ArgumentException>(
			() => sut.ReportExpirationWarningAsync("key-1", "", DateTimeOffset.UtcNow.AddDays(7), CancellationToken.None)).ConfigureAwait(false);
	}

	[Fact]
	public async Task ReportExpirationWarningAsync_SendsAlert_WhenWithinWarningPeriod()
	{
		// Arrange - default warning period is 14 days
		var sut = CreateSut();
		var expiresAt = DateTimeOffset.UtcNow.AddDays(7);

		// Act
		await sut.ReportExpirationWarningAsync("key-1", "prov", expiresAt, CancellationToken.None).ConfigureAwait(false);

		// Assert
		A.CallTo(() => _handler.HandleExpirationWarningAsync(A<KeyExpirationAlert>._, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task ReportExpirationWarningAsync_DoesNotSendAlert_WhenOutsideWarningPeriod()
	{
		// Arrange
		var sut = CreateSut();
		var expiresAt = DateTimeOffset.UtcNow.AddDays(30); // Beyond 14-day default

		// Act
		await sut.ReportExpirationWarningAsync("key-1", "prov", expiresAt, CancellationToken.None).ConfigureAwait(false);

		// Assert
		A.CallTo(() => _handler.HandleExpirationWarningAsync(A<KeyExpirationAlert>._, A<CancellationToken>._))
			.MustNotHaveHappened();
	}

	[Fact]
	public async Task ReportExpirationWarningAsync_UsesCustomWarningDays()
	{
		// Arrange - set warning to 30 days
		var options = new KeyRotationAlertOptions { ExpirationWarningDays = 30 };
		var sut = CreateSut(options);
		var expiresAt = DateTimeOffset.UtcNow.AddDays(25);

		// Act
		await sut.ReportExpirationWarningAsync("key-1", "prov", expiresAt, CancellationToken.None).ConfigureAwait(false);

		// Assert
		A.CallTo(() => _handler.HandleExpirationWarningAsync(A<KeyExpirationAlert>._, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	#endregion

	#region ResetFailureCount / GetFailureCount Tests

	[Fact]
	public async Task ResetFailureCount_RemovesCounter()
	{
		// Arrange
		var sut = CreateSut(new KeyRotationAlertOptions { AlertAfterFailures = 99 });
		await sut.ReportRotationFailureAsync("key-1", "prov", "err", CancellationToken.None).ConfigureAwait(false);
		sut.GetFailureCount("key-1", "prov").ShouldBe(1);

		// Act
		sut.ResetFailureCount("key-1", "prov");

		// Assert
		sut.GetFailureCount("key-1", "prov").ShouldBe(0);
	}

	[Fact]
	public void GetFailureCount_ReturnsZero_WhenNoFailures()
	{
		var sut = CreateSut();
		sut.GetFailureCount("nonexistent", "provider").ShouldBe(0);
	}

	#endregion

	#region Helpers

	private KeyRotationAlertService CreateSut(KeyRotationAlertOptions? options = null)
	{
		return new KeyRotationAlertService(
			new[] { _handler },
			_metrics,
			NullLogger<KeyRotationAlertService>.Instance,
			options);
	}

	#endregion
}
