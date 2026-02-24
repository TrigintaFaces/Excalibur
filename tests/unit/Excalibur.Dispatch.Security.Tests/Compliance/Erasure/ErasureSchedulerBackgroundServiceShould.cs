// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using MsOptions = Microsoft.Extensions.Options.Options;

namespace Excalibur.Dispatch.Security.Tests.Compliance.Erasure;

/// <summary>
/// Unit tests for <see cref="ErasureSchedulerBackgroundService"/>.
/// Tests constructor validation and options configuration per ADR-054.
/// </summary>
/// <remarks>
/// Note: Full integration tests of the background service execution loop
/// require mocking IServiceScopeFactory.CreateAsyncScope(), which returns
/// AsyncServiceScope (a struct). These scenarios are better tested via
/// integration tests with real DI container setup.
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "Compliance")]
public sealed class ErasureSchedulerBackgroundServiceShould
{
	private readonly IServiceScopeFactory _scopeFactory;
	private readonly IOptions<ErasureSchedulerOptions> _options;

	public ErasureSchedulerBackgroundServiceShould()
	{
		_scopeFactory = A.Fake<IServiceScopeFactory>();
		_options = MsOptions.Create(new ErasureSchedulerOptions());
	}

	#region Constructor Tests

	[Fact]
	public void ThrowArgumentNullException_WhenScopeFactoryIsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() => new ErasureSchedulerBackgroundService(
			null!,
			_options,
			NullLogger<ErasureSchedulerBackgroundService>.Instance))
			.ParamName.ShouldBe("scopeFactory");
	}

	[Fact]
	public void ThrowArgumentNullException_WhenOptionsIsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() => new ErasureSchedulerBackgroundService(
			_scopeFactory,
			null!,
			NullLogger<ErasureSchedulerBackgroundService>.Instance))
			.ParamName.ShouldBe("options");
	}

	[Fact]
	public void ThrowArgumentNullException_WhenLoggerIsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() => new ErasureSchedulerBackgroundService(
			_scopeFactory,
			_options,
			null!))
			.ParamName.ShouldBe("logger");
	}

	[Fact]
	public void ConstructSuccessfully_WithValidParameters()
	{
		// Act
		var sut = new ErasureSchedulerBackgroundService(
			_scopeFactory,
			_options,
			NullLogger<ErasureSchedulerBackgroundService>.Instance);

		// Assert
		sut.ShouldNotBeNull();
	}

	#endregion

	#region ErasureSchedulerOptions Tests

	[Fact]
	public void ErasureSchedulerOptions_HasCorrectDefaults()
	{
		// Arrange
		var options = new ErasureSchedulerOptions();

		// Assert
		options.PollingInterval.ShouldBe(TimeSpan.FromMinutes(5));
		options.BatchSize.ShouldBe(10);
		options.Enabled.ShouldBeTrue();
		options.MaxRetryAttempts.ShouldBe(3);
		options.RetryDelayBase.ShouldBe(TimeSpan.FromSeconds(30));
		options.UseExponentialBackoff.ShouldBeTrue();
		options.CertificateCleanupInterval.ShouldBe(TimeSpan.FromHours(24));
	}

	[Fact]
	public void ErasureSchedulerOptions_AllowsCustomization()
	{
		// Arrange & Act
		var options = new ErasureSchedulerOptions
		{
			PollingInterval = TimeSpan.FromMinutes(10),
			BatchSize = 20,
			Enabled = false,
			MaxRetryAttempts = 5,
			RetryDelayBase = TimeSpan.FromSeconds(60),
			UseExponentialBackoff = false,
			CertificateCleanupInterval = TimeSpan.FromHours(48)
		};

		// Assert
		options.PollingInterval.ShouldBe(TimeSpan.FromMinutes(10));
		options.BatchSize.ShouldBe(20);
		options.Enabled.ShouldBeFalse();
		options.MaxRetryAttempts.ShouldBe(5);
		options.RetryDelayBase.ShouldBe(TimeSpan.FromSeconds(60));
		options.UseExponentialBackoff.ShouldBeFalse();
		options.CertificateCleanupInterval.ShouldBe(TimeSpan.FromHours(48));
	}

	[Theory]
	[InlineData(1)]
	[InlineData(5)]
	[InlineData(100)]
	public void ErasureSchedulerOptions_AcceptsBatchSizeValues(int batchSize)
	{
		// Arrange & Act
		var options = new ErasureSchedulerOptions { BatchSize = batchSize };

		// Assert
		options.BatchSize.ShouldBe(batchSize);
	}

	[Theory]
	[InlineData(1)]
	[InlineData(10)]
	[InlineData(60)]
	public void ErasureSchedulerOptions_AcceptsPollingIntervalMinutes(int minutes)
	{
		// Arrange & Act
		var options = new ErasureSchedulerOptions
		{
			PollingInterval = TimeSpan.FromMinutes(minutes)
		};

		// Assert
		options.PollingInterval.ShouldBe(TimeSpan.FromMinutes(minutes));
	}

	[Theory]
	[InlineData(0)]
	[InlineData(1)]
	[InlineData(5)]
	[InlineData(10)]
	public void ErasureSchedulerOptions_AcceptsMaxRetryAttemptValues(int maxRetries)
	{
		// Arrange & Act
		var options = new ErasureSchedulerOptions { MaxRetryAttempts = maxRetries };

		// Assert
		options.MaxRetryAttempts.ShouldBe(maxRetries);
	}

	[Fact]
	public void ErasureSchedulerOptions_AcceptsZeroRetryDelay()
	{
		// Arrange & Act
		var options = new ErasureSchedulerOptions
		{
			RetryDelayBase = TimeSpan.Zero
		};

		// Assert
		options.RetryDelayBase.ShouldBe(TimeSpan.Zero);
	}

	[Fact]
	public void ErasureSchedulerOptions_AcceptsLongCertificateCleanupInterval()
	{
		// Arrange & Act
		var options = new ErasureSchedulerOptions
		{
			CertificateCleanupInterval = TimeSpan.FromDays(7)
		};

		// Assert
		options.CertificateCleanupInterval.ShouldBe(TimeSpan.FromDays(7));
	}

	#endregion

	#region ExecuteAsync Tests - Disabled Scheduler

	[Fact]
	public async Task ExecuteAsync_ExitsImmediately_WhenDisabled()
	{
		// Arrange
		var options = MsOptions.Create(new ErasureSchedulerOptions { Enabled = false });
		var sut = new ErasureSchedulerBackgroundService(
			_scopeFactory,
			options,
			NullLogger<ErasureSchedulerBackgroundService>.Instance);

		using var cts = new CancellationTokenSource();

		// Act
		await sut.StartAsync(cts.Token).ConfigureAwait(false);
		await global::Tests.Shared.Infrastructure.TestTiming.DelayAsync(2000).ConfigureAwait(false); // Give time for ExecuteAsync to run (generous for full-suite load)
		await sut.StopAsync(cts.Token).ConfigureAwait(false);

		// Assert - should not have created any scope when disabled
		A.CallTo(() => _scopeFactory.CreateScope()).MustNotHaveHappened();
	}

	[Fact]
	public async Task ExecuteAsync_DoesNotFail_WhenDisabledAndImmediatelyCancelled()
	{
		// Arrange
		var options = MsOptions.Create(new ErasureSchedulerOptions { Enabled = false });
		var sut = new ErasureSchedulerBackgroundService(
			_scopeFactory,
			options,
			NullLogger<ErasureSchedulerBackgroundService>.Instance);

		using var cts = new CancellationTokenSource();
		cts.Cancel(); // Cancel immediately

		// Act & Assert - should not throw
		await Should.NotThrowAsync(async () =>
		{
			await sut.StartAsync(cts.Token).ConfigureAwait(false);
			await sut.StopAsync(CancellationToken.None).ConfigureAwait(false);
		}).ConfigureAwait(false);
	}

	#endregion

	#region ErasureExecutionResult Tests

	[Fact]
	public void ErasureExecutionResult_Succeeded_CreatesSuccessResult()
	{
		// Act
		var result = ErasureExecutionResult.Succeeded(keysDeleted: 5, recordsAffected: 10);

		// Assert
		result.Success.ShouldBeTrue();
		result.KeysDeleted.ShouldBe(5);
		result.RecordsAffected.ShouldBe(10);
		result.ErrorMessage.ShouldBeNull();
	}

	[Fact]
	public void ErasureExecutionResult_Failed_CreatesFailureResult()
	{
		// Act
		var result = ErasureExecutionResult.Failed("Test error message");

		// Assert
		result.Success.ShouldBeFalse();
		result.ErrorMessage.ShouldBe("Test error message");
		result.KeysDeleted.ShouldBe(0);
		result.RecordsAffected.ShouldBe(0);
	}

	[Fact]
	public void ErasureExecutionResult_Succeeded_WithZeroCounts()
	{
		// Act
		var result = ErasureExecutionResult.Succeeded(keysDeleted: 0, recordsAffected: 0);

		// Assert
		result.Success.ShouldBeTrue();
		result.KeysDeleted.ShouldBe(0);
		result.RecordsAffected.ShouldBe(0);
	}

	[Fact]
	public void ErasureExecutionResult_Succeeded_WithLargeCounts()
	{
		// Act
		var result = ErasureExecutionResult.Succeeded(keysDeleted: 1000000, recordsAffected: 5000000);

		// Assert
		result.Success.ShouldBeTrue();
		result.KeysDeleted.ShouldBe(1000000);
		result.RecordsAffected.ShouldBe(5000000);
	}

	#endregion
}
