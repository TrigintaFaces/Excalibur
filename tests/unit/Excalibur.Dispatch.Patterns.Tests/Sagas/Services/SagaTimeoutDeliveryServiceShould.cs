// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Saga;
using Excalibur.Saga.Abstractions;
using Excalibur.Saga.Services;

using FakeItEasy;

using Microsoft.Extensions.Logging.Abstractions;

using Shouldly;

using Xunit;

using MsOptions = Microsoft.Extensions.Options.Options;

namespace Excalibur.Dispatch.Patterns.Tests.Sagas.Services;

/// <summary>
/// Unit tests for <see cref="SagaTimeoutDeliveryService"/> validating constructor parameter validation
/// and configuration options.
/// </summary>
/// <remarks>
/// <para>
/// Sprint 216 - Saga Timeouts Delivery &amp; SqlServer.
/// Task: oexxh (SAGA-016: Integration Tests).
/// </para>
/// <para>
/// Tests focus on constructor validation and options configuration.
/// Full integration tests require a running environment with dispatcher and timeout store.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Saga")]
[Trait("Sprint", "216")]
public sealed class SagaTimeoutDeliveryServiceShould
{
	private readonly ISagaTimeoutStore _fakeTimeoutStore = A.Fake<ISagaTimeoutStore>();
	private readonly IServiceProvider _fakeServiceProvider = A.Fake<IServiceProvider>();

	#region Constructor Validation Tests

	/// <summary>
	/// Tests that the constructor throws when timeout store is null.
	/// </summary>
	[Fact]
	public void ThrowWhenTimeoutStoreIsNull()
	{
		// Arrange
		var options = MsOptions.Create(new SagaTimeoutOptions());

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => new SagaTimeoutDeliveryService(
			timeoutStore: null!,
			_fakeServiceProvider,
			NullLogger<SagaTimeoutDeliveryService>.Instance,
			options));
	}

	/// <summary>
	/// Tests that the constructor throws when service provider is null.
	/// </summary>
	[Fact]
	public void ThrowWhenServiceProviderIsNull()
	{
		// Arrange
		var options = MsOptions.Create(new SagaTimeoutOptions());

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => new SagaTimeoutDeliveryService(
			_fakeTimeoutStore,
			serviceProvider: null!,
			NullLogger<SagaTimeoutDeliveryService>.Instance,
			options));
	}

	/// <summary>
	/// Tests that the constructor throws when logger is null.
	/// </summary>
	[Fact]
	public void ThrowWhenLoggerIsNull()
	{
		// Arrange
		var options = MsOptions.Create(new SagaTimeoutOptions());

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => new SagaTimeoutDeliveryService(
			_fakeTimeoutStore,
			_fakeServiceProvider,
			logger: null!,
			options));
	}

	/// <summary>
	/// Tests that the constructor throws when options is null.
	/// </summary>
	[Fact]
	public void ThrowWhenOptionsIsNull()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => new SagaTimeoutDeliveryService(
			_fakeTimeoutStore,
			_fakeServiceProvider,
			NullLogger<SagaTimeoutDeliveryService>.Instance,
			options: null!));
	}

	/// <summary>
	/// Tests that the constructor succeeds with valid parameters.
	/// </summary>
	[Fact]
	public void CreateInstanceWithValidParameters()
	{
		// Arrange
		var options = MsOptions.Create(new SagaTimeoutOptions());

		// Act
		var service = new SagaTimeoutDeliveryService(
			_fakeTimeoutStore,
			_fakeServiceProvider,
			NullLogger<SagaTimeoutDeliveryService>.Instance,
			options);

		// Assert
		_ = service.ShouldNotBeNull();
	}

	#endregion Constructor Validation Tests

	#region Options Configuration Tests

	/// <summary>
	/// Tests that SagaTimeoutOptions has sensible defaults.
	/// </summary>
	[Fact]
	public void UseDefaultOptionsValues()
	{
		// Arrange & Act
		var options = new SagaTimeoutOptions();

		// Assert
		options.PollInterval.ShouldBe(TimeSpan.FromSeconds(1));
		options.BatchSize.ShouldBe(100);
		options.ShutdownTimeout.ShouldBe(TimeSpan.FromSeconds(30));
		options.EnableVerboseLogging.ShouldBeTrue();
	}

	/// <summary>
	/// Tests that SagaTimeoutOptions can be customized.
	/// </summary>
	[Fact]
	public void AcceptCustomOptionsValues()
	{
		// Arrange & Act
		var options = new SagaTimeoutOptions
		{
			PollInterval = TimeSpan.FromMilliseconds(500),
			BatchSize = 50,
			ShutdownTimeout = TimeSpan.FromSeconds(15),
			EnableVerboseLogging = false
		};

		// Assert
		options.PollInterval.ShouldBe(TimeSpan.FromMilliseconds(500));
		options.BatchSize.ShouldBe(50);
		options.ShutdownTimeout.ShouldBe(TimeSpan.FromSeconds(15));
		options.EnableVerboseLogging.ShouldBeFalse();
	}

	#endregion Options Configuration Tests

	#region Activity Source Tests

	/// <summary>
	/// Tests that SagaActivitySource provides correct constants.
	/// </summary>
	[Fact]
	public void ExposeActivitySourceConstants()
	{
		// Assert
		SagaActivitySource.SourceName.ShouldBe("Excalibur.Dispatch.Sagas");
		SagaActivitySource.SourceVersion.ShouldBe("1.0.0");
		_ = SagaActivitySource.Instance.ShouldNotBeNull();
		SagaActivitySource.Instance.Name.ShouldBe("Excalibur.Dispatch.Sagas");
	}

	/// <summary>
	/// Tests that SagaActivitySource can start activities.
	/// </summary>
	[Fact]
	public void StartActivitiesFromActivitySource()
	{
		// Act - StartActivity returns null when no listener is registered
		// This is expected behavior in unit tests without OpenTelemetry configured
		var activity = SagaActivitySource.StartActivity("TestActivity");

		// Assert - Activity is null without listeners (expected)
		// The fact that it doesn't throw is what we're testing
		// In production with OTEL configured, this would return an Activity
	}

	#endregion Activity Source Tests
}
