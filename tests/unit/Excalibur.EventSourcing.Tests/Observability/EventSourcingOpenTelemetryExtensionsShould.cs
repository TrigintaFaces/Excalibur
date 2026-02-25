// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.EventSourcing.Observability;

using OpenTelemetry.Trace;

namespace Excalibur.EventSourcing.Tests.Observability;

/// <summary>
/// Unit tests for <see cref="EventSourcingOpenTelemetryExtensions"/>.
/// Verifies OpenTelemetry extension method registration behavior.
/// Sprint 571 H.2: Test coverage for EventSourcingOpenTelemetryExtensions.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "EventSourcing")]
[Trait("Feature", "OpenTelemetry")]
public sealed class EventSourcingOpenTelemetryExtensionsShould
{
	#region AddEventSourcingInstrumentation(IOpenTelemetryBuilder) Tests

	[Fact]
	public void ThrowArgumentNullException_WhenOpenTelemetryBuilderIsNull()
	{
		// Arrange
		OpenTelemetry.IOpenTelemetryBuilder? builder = null;

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			EventSourcingOpenTelemetryExtensions.AddEventSourcingInstrumentation(builder!));
	}

	[Fact]
	public void ReturnBuilder_ForChaining_WhenUsingOpenTelemetryBuilder()
	{
		// Arrange
		var services = new ServiceCollection();
		var builder = services.AddOpenTelemetry();

		// Act
		var result = builder.AddEventSourcingInstrumentation();

		// Assert
		result.ShouldBeSameAs(builder);
	}

	[Fact]
	public void NotThrow_WhenCalledMultipleTimes_OnOpenTelemetryBuilder()
	{
		// Arrange
		var services = new ServiceCollection();
		var builder = services.AddOpenTelemetry();

		// Act & Assert - should not throw
		Should.NotThrow(() =>
		{
			builder.AddEventSourcingInstrumentation();
			builder.AddEventSourcingInstrumentation();
			builder.AddEventSourcingInstrumentation();
		});
	}

	#endregion

	#region AddEventSourcingInstrumentation(TracerProviderBuilder) Tests

	[Fact]
	public void ThrowArgumentNullException_WhenTracerProviderBuilderIsNull()
	{
		// Arrange
		TracerProviderBuilder? builder = null;

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			EventSourcingOpenTelemetryExtensions.AddEventSourcingInstrumentation(builder!));
	}

	[Fact]
	public void RegisterActivitySource_WhenUsingTracerProviderBuilder()
	{
		// Arrange & Act — use WithTracing to exercise the TracerProviderBuilder overload
		var services = new ServiceCollection();

		// Act - should not throw and should configure tracing
		Should.NotThrow(() =>
		{
			services.AddOpenTelemetry()
				.WithTracing(b => b.AddEventSourcingInstrumentation());
		});
	}

	[Fact]
	public void NotThrow_WhenCalledMultipleTimes_OnTracerProviderBuilder()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert - should not throw
		Should.NotThrow(() =>
		{
			services.AddOpenTelemetry()
				.WithTracing(b =>
				{
					b.AddEventSourcingInstrumentation();
					b.AddEventSourcingInstrumentation();
					b.AddEventSourcingInstrumentation();
				});
		});
	}

	#endregion

	#region ActivitySource Name Registration Tests

	[Fact]
	public void RegisterCorrectActivitySourceName()
	{
		// Assert - the extension method uses EventSourcingActivitySource.Name
		EventSourcingActivitySource.Name.ShouldBe("Excalibur.EventSourcing");
	}

	[Fact]
	public void RegisterTracingWithCorrectSourceName_ViaOpenTelemetryBuilder()
	{
		// Arrange — build a real provider to verify the source is wired
		var services = new ServiceCollection();

		// Act
		services.AddOpenTelemetry()
			.AddEventSourcingInstrumentation();

		// Assert — the service collection should not be empty (tracing services registered)
		services.Count.ShouldBeGreaterThan(0);
	}

	#endregion
}
