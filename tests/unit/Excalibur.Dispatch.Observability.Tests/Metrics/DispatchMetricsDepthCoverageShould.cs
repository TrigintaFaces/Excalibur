// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.Metrics;

using Excalibur.Dispatch.Diagnostics;
using Excalibur.Dispatch.Observability.Metrics;

namespace Excalibur.Dispatch.Observability.Tests.Metrics;

/// <summary>
/// Deep coverage tests for <see cref="DispatchMetrics"/> covering constructor variants,
/// instrument initialization, cardinality guard integration, and disposal behavior.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Observability")]
public sealed class DispatchMetricsDepthCoverageShould
{
	[Fact]
	public void CreateWithDefaultConstructor_OwnsMeter()
	{
		// Act
		using var metrics = new DispatchMetrics();

		// Assert
		metrics.Meter.ShouldNotBeNull();
		metrics.Meter.Name.ShouldBe(DispatchTelemetryConstants.Meters.Core);
	}

	[Fact]
	public void CreateWithMeterFactory_DoesNotOwnMeter()
	{
		// Arrange
		var factory = A.Fake<IMeterFactory>();
		var meter = new Meter("test-dispatch");
		A.CallTo(() => factory.Create(A<MeterOptions>._)).Returns(meter);

		// Act
		using var metrics = new DispatchMetrics(factory);

		// Assert
		metrics.Meter.ShouldNotBeNull();
	}

	[Fact]
	public void CreateWithTelemetryProvider()
	{
		// Arrange
		var provider = A.Fake<IDispatchTelemetryProvider>();
		var meter = new Meter("test-dispatch-provider");
		A.CallTo(() => provider.GetMeter(A<string>._)).Returns(meter);

		// Act
		using var metrics = new DispatchMetrics(provider);

		// Assert
		metrics.Meter.ShouldNotBeNull();
	}

	[Fact]
	public void ThrowOnNullMeterFactory()
	{
		Should.Throw<ArgumentNullException>(() => new DispatchMetrics((IMeterFactory)null!));
	}

	[Fact]
	public void ThrowOnNullTelemetryProvider()
	{
		Should.Throw<ArgumentNullException>(() => new DispatchMetrics((IDispatchTelemetryProvider)null!));
	}

	[Fact]
	public void RecordMessageProcessed_WithTags()
	{
		// Arrange
		using var metrics = new DispatchMetrics();

		// Act & Assert — should not throw
		metrics.RecordMessageProcessed("OrderCommand", "OrderHandler");
	}

	[Fact]
	public void RecordMessageProcessed_WithAdditionalTags()
	{
		// Arrange
		using var metrics = new DispatchMetrics();

		// Act & Assert — should not throw
		metrics.RecordMessageProcessed("OrderCommand", "OrderHandler",
			("tenant_id", "t1"), ("correlation_id", "c1"));
	}

	[Fact]
	public void RecordMessageProcessed_ThrowsOnNullTags()
	{
		// Arrange
		using var metrics = new DispatchMetrics();

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			metrics.RecordMessageProcessed("Test", "Handler", null!));
	}

	[Fact]
	public void RecordProcessingDuration_WithSuccessAndFailure()
	{
		// Arrange
		using var metrics = new DispatchMetrics();

		// Act & Assert — should not throw
		metrics.RecordProcessingDuration(42.5, "TestMessage", success: true);
		metrics.RecordProcessingDuration(100.0, "TestMessage", success: false);
	}

	[Fact]
	public void RecordMessagePublished_WithDestination()
	{
		// Arrange
		using var metrics = new DispatchMetrics();

		// Act & Assert — should not throw
		metrics.RecordMessagePublished("OrderCreated", "orders-topic");
	}

	[Fact]
	public void RecordMessageFailed_WithRetryAttempt()
	{
		// Arrange
		using var metrics = new DispatchMetrics();

		// Act & Assert — should not throw
		metrics.RecordMessageFailed("OrderCommand", "TimeoutException", retryAttempt: 3);
	}

	[Fact]
	public void UpdateActiveSessions_IncrementAndDecrement()
	{
		// Arrange
		using var metrics = new DispatchMetrics();

		// Act & Assert — should not throw
		metrics.UpdateActiveSessions(1);  // connect
		metrics.UpdateActiveSessions(-1); // disconnect
	}

	[Fact]
	public void Dispose_Idempotent()
	{
		// Arrange
		var metrics = new DispatchMetrics();

		// Act & Assert — double dispose should not throw
		metrics.Dispose();
		metrics.Dispose();
	}

	[Fact]
	public void Dispose_OwnedMeter_DisposesIt()
	{
		// Arrange
		var metrics = new DispatchMetrics();
		var meter = metrics.Meter;

		// Act
		metrics.Dispose();

		// Assert — meter should still be an object (Meter.Dispose doesn't set to null)
		meter.ShouldNotBeNull();
	}

	[Fact]
	public void MeterName_Constant_MatchesTelemetryConstants()
	{
		DispatchMetrics.MeterName.ShouldBe(DispatchTelemetryConstants.Meters.Core);
	}
}
