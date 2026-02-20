// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Reflection;

using Excalibur.Dispatch.Diagnostics;

namespace Excalibur.Dispatch.Tests.Diagnostics;

/// <summary>
/// Verifies that <see cref="DispatchTelemetryConstants"/> nested class constants all follow the
/// <c>Excalibur.Dispatch.*</c> naming convention, have no duplicates, and have no empty values.
/// Sprint 562 S562.59: Telemetry constants conformance tests for Dispatch core.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Diagnostics")]
public sealed class DispatchTelemetryConstantsUsageShould
{
	[Fact]
	public void HaveAllActivitySourcesStartingWithExcaliburDispatch()
	{
		// Arrange
		var fields = GetConstStringFields(typeof(DispatchTelemetryConstants.ActivitySources));

		// Assert
		fields.ShouldNotBeEmpty("ActivitySources nested class has no public const string fields");

		foreach (var field in fields)
		{
			var value = (string)field.GetRawConstantValue()!;
			value.ShouldStartWith("Excalibur.Dispatch.");
		}
	}

	[Fact]
	public void HaveAllMetersStartingWithExcaliburDispatch()
	{
		// Arrange
		var fields = GetConstStringFields(typeof(DispatchTelemetryConstants.Meters));

		// Assert
		fields.ShouldNotBeEmpty("Meters nested class has no public const string fields");

		foreach (var field in fields)
		{
			var value = (string)field.GetRawConstantValue()!;
			value.ShouldStartWith("Excalibur.Dispatch.");
		}
	}

	[Fact]
	public void HaveNoDuplicateActivitySourceNames()
	{
		// Arrange
		var fields = GetConstStringFields(typeof(DispatchTelemetryConstants.ActivitySources));
		var values = fields.Select(f => (string)f.GetRawConstantValue()!).ToList();

		// Assert
		var duplicates = values.GroupBy(v => v).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
		duplicates.ShouldBeEmpty($"Duplicate ActivitySource names: {string.Join(", ", duplicates)}");
	}

	[Fact]
	public void HaveNoDuplicateMeterNames()
	{
		// Arrange
		var fields = GetConstStringFields(typeof(DispatchTelemetryConstants.Meters));
		var values = fields.Select(f => (string)f.GetRawConstantValue()!).ToList();

		// Assert
		var duplicates = values.GroupBy(v => v).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
		duplicates.ShouldBeEmpty($"Duplicate Meter names: {string.Join(", ", duplicates)}");
	}

	[Fact]
	public void HaveAtLeastSixActivitySources()
	{
		// Arrange - we added Core, Pipeline, TimePolicy, BatchProcessor, PoisonMessage,
		// PoisonMessageMiddleware, PoisonMessageCleanup, AuditLoggingMiddleware,
		// CircuitBreakerMiddleware, RetryMiddleware, UnifiedBatchingMiddleware,
		// ChannelTransport, OutboxBackgroundService
		var fields = GetConstStringFields(typeof(DispatchTelemetryConstants.ActivitySources));

		// Assert
		fields.Count.ShouldBeGreaterThanOrEqualTo(6,
			$"Expected at least 6 ActivitySource constants but found {fields.Count}");
	}

	[Fact]
	public void HaveAtLeastFourMeters()
	{
		// Arrange - Core, Pipeline, TimePolicy, BatchProcessor, Messaging, ChannelTransport
		var fields = GetConstStringFields(typeof(DispatchTelemetryConstants.Meters));

		// Assert
		fields.Count.ShouldBeGreaterThanOrEqualTo(4,
			$"Expected at least 4 Meter constants but found {fields.Count}");
	}

	[Fact]
	public void HaveAllTagsFollowLowerDotNotation()
	{
		// Arrange
		var fields = GetConstStringFields(typeof(DispatchTelemetryConstants.Tags));

		// Assert
		fields.ShouldNotBeEmpty("Tags nested class has no public const string fields");

		foreach (var field in fields)
		{
			var value = (string)field.GetRawConstantValue()!;
			value.ShouldNotBeNullOrWhiteSpace($"Tags.{field.Name} is null or empty");

			// Tags should be lowercase and dot-separated (OTel convention)
			value.ShouldMatch(@"^[a-z][a-z0-9_]*(\.[a-z][a-z0-9_]*)+$",
				$"Tags.{field.Name} = '{value}' doesn't follow 'lower.dot.separated' OTel convention");
		}
	}

	[Fact]
	public void HaveAllTagValuesNonEmpty()
	{
		// Arrange
		var fields = GetConstStringFields(typeof(DispatchTelemetryConstants.TagValues));

		// Assert
		fields.ShouldNotBeEmpty("TagValues nested class has no public const string fields");

		foreach (var field in fields)
		{
			var value = (string)field.GetRawConstantValue()!;
			value.ShouldNotBeNullOrWhiteSpace($"TagValues.{field.Name} is null or empty");
		}
	}

	[Fact]
	public void HaveAllTagValuesLowercase()
	{
		// Arrange
		var fields = GetConstStringFields(typeof(DispatchTelemetryConstants.TagValues));

		// Assert
		foreach (var field in fields)
		{
			var value = (string)field.GetRawConstantValue()!;
			// Verify no uppercase letters exist
			var hasUppercase = value.Any(char.IsUpper);
			hasUppercase.ShouldBeFalse(
				$"TagValues.{field.Name} = '{value}' contains uppercase characters");
		}
	}

	[Fact]
	public void HaveAllActivitiesNonEmpty()
	{
		// Arrange
		var fields = GetConstStringFields(typeof(DispatchTelemetryConstants.Activities));

		// Assert
		fields.ShouldNotBeEmpty("Activities nested class has no public const string fields");

		foreach (var field in fields)
		{
			var value = (string)field.GetRawConstantValue()!;
			value.ShouldNotBeNullOrWhiteSpace($"Activities.{field.Name} is null or empty");
		}
	}

	[Theory]
	[InlineData(nameof(DispatchTelemetryConstants.ActivitySources.Core), "Excalibur.Dispatch.Core")]
	[InlineData(nameof(DispatchTelemetryConstants.ActivitySources.Pipeline), "Excalibur.Dispatch.Pipeline")]
	[InlineData(nameof(DispatchTelemetryConstants.ActivitySources.OutboxBackgroundService), "Excalibur.Dispatch.Outbox.Publisher")]
	[InlineData(nameof(DispatchTelemetryConstants.ActivitySources.ChannelTransport), "Excalibur.Dispatch.Transport.Common")]
	public void HaveExpectedActivitySourceValues(string fieldName, string expectedValue)
	{
		// Arrange
		var field = typeof(DispatchTelemetryConstants.ActivitySources)
			.GetField(fieldName, BindingFlags.Public | BindingFlags.Static);

		// Assert
		field.ShouldNotBeNull($"Field {fieldName} not found");
		var value = (string)field.GetRawConstantValue()!;
		value.ShouldBe(expectedValue);
	}

	[Theory]
	[InlineData(nameof(DispatchTelemetryConstants.Meters.Core), "Excalibur.Dispatch.Core")]
	[InlineData(nameof(DispatchTelemetryConstants.Meters.Pipeline), "Excalibur.Dispatch.Pipeline")]
	[InlineData(nameof(DispatchTelemetryConstants.Meters.Messaging), "Excalibur.Dispatch.Messaging")]
	[InlineData(nameof(DispatchTelemetryConstants.Meters.ChannelTransport), "Excalibur.Dispatch.Transport.Common")]
	public void HaveExpectedMeterValues(string fieldName, string expectedValue)
	{
		// Arrange
		var field = typeof(DispatchTelemetryConstants.Meters)
			.GetField(fieldName, BindingFlags.Public | BindingFlags.Static);

		// Assert
		field.ShouldNotBeNull($"Field {fieldName} not found");
		var value = (string)field.GetRawConstantValue()!;
		value.ShouldBe(expectedValue);
	}

	private static List<FieldInfo> GetConstStringFields(Type type) =>
		type.GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
			.Where(f => f.IsLiteral && !f.IsInitOnly && f.FieldType == typeof(string))
			.ToList();
}
