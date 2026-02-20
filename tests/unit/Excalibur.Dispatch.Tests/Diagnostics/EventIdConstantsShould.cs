// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Reflection;

using Excalibur.Dispatch.Diagnostics;

namespace Excalibur.Dispatch.Tests.Diagnostics;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class EventIdConstantsShould
{
	private static IReadOnlyList<(string Name, int Value)> GetConstantFields(Type type) =>
		type.GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
			.Where(static f => f is { IsLiteral: true, FieldType.Name: "Int32" })
			.Select(static f => (f.Name, (int)f.GetRawConstantValue()!))
			.ToList();

	// --- CoreEventId ---

	[Fact]
	public void CoreEventId_HasUniqueValues()
	{
		// Arrange
		var fields = GetConstantFields(typeof(CoreEventId));

		// Act
		var duplicates = fields
			.GroupBy(static f => f.Value)
			.Where(static g => g.Count() > 1)
			.ToList();

		// Assert
		duplicates.ShouldBeEmpty(
			$"CoreEventId has duplicate values: {string.Join(", ", duplicates.Select(g => $"{g.Key} -> [{string.Join(", ", g.Select(f => f.Name))}]"))}");
	}

	[Fact]
	public void CoreEventId_ValuesAreInExpectedRange()
	{
		// Arrange
		var fields = GetConstantFields(typeof(CoreEventId));

		// Assert
		fields.ShouldNotBeEmpty();
		foreach (var (name, value) in fields)
		{
			value.ShouldBeGreaterThanOrEqualTo(10000, $"CoreEventId.{name} = {value} is below 10000");
			value.ShouldBeLessThan(11000, $"CoreEventId.{name} = {value} is above 10999");
		}
	}

	[Fact]
	public void CoreEventId_ContainsExpectedSubcategories()
	{
		// Assert - verify key constants exist in each subcategory
		CoreEventId.DispatcherStarting.ShouldBeInRange(10000, 10099);
		CoreEventId.MessageBusConnected.ShouldBeInRange(10100, 10199);
		CoreEventId.RouteResolved.ShouldBeInRange(10200, 10299);
		CoreEventId.DispatchingMessage.ShouldBeInRange(10300, 10399);
		CoreEventId.ChannelCreated.ShouldBeInRange(10400, 10499);
		CoreEventId.CircuitBreakerCreated.ShouldBeInRange(10500, 10599);
		CoreEventId.CloudEventReceived.ShouldBeInRange(10600, 10699);
		CoreEventId.MicroBatchStarted.ShouldBeInRange(10700, 10799);
		CoreEventId.PoolCreated.ShouldBeInRange(10800, 10899);
		CoreEventId.BackgroundTaskStarted.ShouldBeInRange(10900, 10999);
	}

	// --- MiddlewareEventId ---

	[Fact]
	public void MiddlewareEventId_HasUniqueValues()
	{
		// Arrange
		var fields = GetConstantFields(typeof(MiddlewareEventId));

		// Act
		var duplicates = fields
			.GroupBy(static f => f.Value)
			.Where(static g => g.Count() > 1)
			.ToList();

		// Assert
		duplicates.ShouldBeEmpty(
			$"MiddlewareEventId has duplicate values: {string.Join(", ", duplicates.Select(g => $"{g.Key} -> [{string.Join(", ", g.Select(f => f.Name))}]"))}");
	}

	// --- DeliveryEventId ---

	[Fact]
	public void DeliveryEventId_HasUniqueValues()
	{
		// Arrange
		var fields = GetConstantFields(typeof(DeliveryEventId));

		// Act
		var duplicates = fields
			.GroupBy(static f => f.Value)
			.Where(static g => g.Count() > 1)
			.ToList();

		// Assert
		duplicates.ShouldBeEmpty(
			$"DeliveryEventId has duplicate values: {string.Join(", ", duplicates.Select(g => $"{g.Key} -> [{string.Join(", ", g.Select(f => f.Name))}]"))}");
	}

	// --- PerformanceEventId ---

	[Fact]
	public void PerformanceEventId_HasUniqueValues()
	{
		// Arrange
		var fields = GetConstantFields(typeof(PerformanceEventId));

		// Act
		var duplicates = fields
			.GroupBy(static f => f.Value)
			.Where(static g => g.Count() > 1)
			.ToList();

		// Assert
		duplicates.ShouldBeEmpty(
			$"PerformanceEventId has duplicate values: {string.Join(", ", duplicates.Select(g => $"{g.Key} -> [{string.Join(", ", g.Select(f => f.Name))}]"))}");
	}

	// --- StreamingHandlerEventId ---

	[Fact]
	public void StreamingHandlerEventId_HasUniqueValues()
	{
		// Arrange
		var fields = GetConstantFields(typeof(StreamingHandlerEventId));

		// Act
		var duplicates = fields
			.GroupBy(static f => f.Value)
			.Where(static g => g.Count() > 1)
			.ToList();

		// Assert
		duplicates.ShouldBeEmpty(
			$"StreamingHandlerEventId has duplicate values: {string.Join(", ", duplicates.Select(g => $"{g.Key} -> [{string.Join(", ", g.Select(f => f.Name))}]"))}");
	}

	// --- Cross-class uniqueness ---

	[Fact]
	public void AllEventIdClasses_HaveNoCrossClassDuplicates()
	{
		// Arrange
		var allFields = new List<(string ClassName, string Name, int Value)>();
		foreach (var type in new[]
		         {
			         typeof(CoreEventId),
			         typeof(MiddlewareEventId),
			         typeof(DeliveryEventId),
			         typeof(PerformanceEventId),
			         typeof(StreamingHandlerEventId),
		         })
		{
			var fields = GetConstantFields(type);
			foreach (var (name, value) in fields)
			{
				allFields.Add((type.Name, name, value));
			}
		}

		// Act
		var crossDuplicates = allFields
			.GroupBy(static f => f.Value)
			.Where(static g => g.Select(f => f.ClassName).Distinct(StringComparer.Ordinal).Count() > 1)
			.ToList();

		// Assert
		crossDuplicates.ShouldBeEmpty(
			$"Cross-class duplicate Event IDs: {string.Join("; ", crossDuplicates.Select(g => $"{g.Key} -> [{string.Join(", ", g.Select(f => $"{f.ClassName}.{f.Name}"))}]"))}");
	}

	// --- StreamingHandlerTelemetryConstants ---

	[Fact]
	public void StreamingHandlerTelemetryConstants_MeterName_IsCorrect()
	{
		StreamingHandlerTelemetryConstants.MeterName.ShouldBe("Excalibur.Dispatch.Streaming");
	}

	[Fact]
	public void StreamingHandlerTelemetryConstants_ActivitySourceName_IsCorrect()
	{
		StreamingHandlerTelemetryConstants.ActivitySourceName.ShouldBe("Excalibur.Dispatch.Streaming");
	}

	[Fact]
	public void StreamingHandlerTelemetryConstants_ActivitySource_IsNotNull()
	{
		StreamingHandlerTelemetryConstants.ActivitySource.ShouldNotBeNull();
		StreamingHandlerTelemetryConstants.ActivitySource.Name.ShouldBe("Excalibur.Dispatch.Streaming");
	}

	[Fact]
	public void StreamingHandlerTelemetryConstants_Meter_IsNotNull()
	{
		StreamingHandlerTelemetryConstants.Meter.ShouldNotBeNull();
		StreamingHandlerTelemetryConstants.Meter.Name.ShouldBe("Excalibur.Dispatch.Streaming");
	}

	[Fact]
	public void StreamingHandlerTelemetryConstants_MetricNames_FollowOTelConventions()
	{
		StreamingHandlerTelemetryConstants.MetricNames.DocumentsProcessed
			.ShouldStartWith("dispatch.streaming.");
		StreamingHandlerTelemetryConstants.MetricNames.DocumentsFailed
			.ShouldStartWith("dispatch.streaming.");
		StreamingHandlerTelemetryConstants.MetricNames.ProcessingDuration
			.ShouldStartWith("dispatch.streaming.");
		StreamingHandlerTelemetryConstants.MetricNames.ChunksProduced
			.ShouldStartWith("dispatch.streaming.");
	}

	[Fact]
	public void StreamingHandlerTelemetryConstants_Tags_AreNotEmpty()
	{
		StreamingHandlerTelemetryConstants.Tags.HandlerType.ShouldNotBeNullOrEmpty();
		StreamingHandlerTelemetryConstants.Tags.DocumentType.ShouldNotBeNullOrEmpty();
		StreamingHandlerTelemetryConstants.Tags.ErrorType.ShouldNotBeNullOrEmpty();
	}
}
