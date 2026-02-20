// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Reflection;
using System.Text.RegularExpressions;

using Excalibur.Data.Abstractions.Persistence;
using Excalibur.Data.ElasticSearch.Diagnostics;
using Excalibur.Data.SqlServer.Diagnostics;
using Excalibur.Dispatch.Compliance.Diagnostics;
using Excalibur.Dispatch.Diagnostics;
using Excalibur.Dispatch.Observability.Diagnostics;
using Excalibur.Dispatch.Transport.Diagnostics;
using Excalibur.EventSourcing.Observability;
using Excalibur.LeaderElection.Diagnostics;

namespace Excalibur.Dispatch.Observability.Tests.Diagnostics;

/// <summary>
/// Boundary tests verifying that all TelemetryConstants classes across the codebase follow
/// consistent naming conventions and have no duplicates or empty values.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Observability")]
[Trait("Feature", "Telemetry")]
public sealed class TelemetryConstantsNamingShould
{
	/// <summary>
	/// Regex matching the <c>Excalibur.*</c> prefix required for all meter and activity source names.
	/// </summary>
	private static readonly Regex ExcaliburPrefixPattern = new(
		@"^Excalibur\.",
		RegexOptions.Compiled);

	/// <summary>
	/// Regex matching lower.dot.separated metric names (e.g., <c>excalibur.cdc.events.processed</c>
	/// or <c>dispatch.transport.messages.sent</c>). Requires at least two dot-separated segments.
	/// </summary>
	private static readonly Regex LowerDotSeparatedPattern = new(
		@"^[a-z][a-z0-9]*(\.[a-z][a-z0-9_]*)+$",
		RegexOptions.Compiled);

	/// <summary>
	/// Regex matching lowercase tag/attribute names following OpenTelemetry conventions.
	/// Requires at least two dot-separated segments (e.g., <c>message.type</c>, <c>message.is_duplicate</c>,
	/// <c>dispatch.transport.name</c>). Single-word names like <c>version</c> are not allowed;
	/// they must have a namespace prefix (e.g., <c>event.version</c>).
	/// </summary>
	private static readonly Regex LowerCaseTagNamePattern = new(
		@"^[a-z][a-z0-9_]*(\.[a-z][a-z0-9_]*)+$",
		RegexOptions.Compiled);

	/// <summary>
	/// Force-load all assemblies containing TelemetryConstants so reflection can discover them.
	/// </summary>
	static TelemetryConstantsNamingShould()
	{
		// Force-load assemblies via concrete type references
		_ = typeof(DispatchTelemetryConstants);
		_ = typeof(StreamingHandlerTelemetryConstants);
		_ = typeof(ContextObservabilityTelemetryConstants);
		_ = typeof(ErasureTelemetryConstants);
		_ = typeof(TransportTelemetryConstants);
		_ = typeof(CdcTelemetryConstants);
		_ = typeof(AuditTelemetryConstants);
		_ = typeof(EventSourcingActivitySources);
		_ = typeof(EventSourcingMeters);
		_ = typeof(EventSourcingMetricNames);
		_ = typeof(LeaderElectionTelemetryConstants);
		_ = typeof(PersistenceTelemetryConstants);
		_ = typeof(SqlServerPersistenceTelemetryConstants);
	}

	#region Meter Name Convention Tests

	[Fact]
	public void HaveAllMeterNamesFollowExcaliburPrefix()
	{
		// Arrange
		var meterNames = GetAllMeterNames();

		// Act & Assert
		meterNames.ShouldNotBeEmpty("No meter names were discovered — assembly loading may have failed");

		foreach (var (typeName, fieldName, value) in meterNames)
		{
			ExcaliburPrefixPattern.IsMatch(value).ShouldBeTrue(
				$"Meter name '{value}' in {typeName}.{fieldName} does not follow 'Excalibur.*' convention");
		}
	}

	[Fact]
	public void HaveAllMeterNamesNonNullAndNonEmpty()
	{
		// Arrange
		var meterNames = GetAllMeterNames();

		// Act & Assert
		meterNames.ShouldNotBeEmpty("No meter names were discovered");

		foreach (var (typeName, fieldName, value) in meterNames)
		{
			value.ShouldNotBeNullOrWhiteSpace(
				$"Meter name in {typeName}.{fieldName} is null or empty");
		}
	}

	[Fact]
	public void HaveNoDuplicateMeterNames()
	{
		// Arrange
		var meterNames = GetAllMeterNames();

		// Act
		var duplicates = meterNames
			.GroupBy(x => x.Value)
			.Where(g => g.Count() > 1)
			.Select(g => new
			{
				Name = g.Key,
				Sources = g.Select(x => $"{x.TypeName}.{x.FieldName}").ToList(),
			})
			.ToList();

		// Assert
		duplicates.ShouldBeEmpty(
			$"Duplicate meter names found: {string.Join("; ", duplicates.Select(d => $"'{d.Name}' in [{string.Join(", ", d.Sources)}]"))}");
	}

	#endregion

	#region Activity Source Name Convention Tests

	[Fact]
	public void HaveAllActivitySourceNamesFollowExcaliburPrefix()
	{
		// Arrange
		var activitySourceNames = GetAllActivitySourceNames();

		// Act & Assert
		activitySourceNames.ShouldNotBeEmpty("No activity source names were discovered — assembly loading may have failed");

		foreach (var (typeName, fieldName, value) in activitySourceNames)
		{
			ExcaliburPrefixPattern.IsMatch(value).ShouldBeTrue(
				$"Activity source name '{value}' in {typeName}.{fieldName} does not follow 'Excalibur.*' convention");
		}
	}

	[Fact]
	public void HaveAllActivitySourceNamesNonNullAndNonEmpty()
	{
		// Arrange
		var activitySourceNames = GetAllActivitySourceNames();

		// Act & Assert
		activitySourceNames.ShouldNotBeEmpty("No activity source names were discovered");

		foreach (var (typeName, fieldName, value) in activitySourceNames)
		{
			value.ShouldNotBeNullOrWhiteSpace(
				$"Activity source name in {typeName}.{fieldName} is null or empty");
		}
	}

	[Fact]
	public void HaveNoDuplicateActivitySourceNames()
	{
		// Arrange
		var activitySourceNames = GetAllActivitySourceNames();

		// Act
		var duplicates = activitySourceNames
			.GroupBy(x => x.Value)
			.Where(g => g.Count() > 1)
			.Select(g => new
			{
				Name = g.Key,
				Sources = g.Select(x => $"{x.TypeName}.{x.FieldName}").ToList(),
			})
			.ToList();

		// Assert
		duplicates.ShouldBeEmpty(
			$"Duplicate activity source names found: {string.Join("; ", duplicates.Select(d => $"'{d.Name}' in [{string.Join(", ", d.Sources)}]"))}");
	}

	#endregion

	#region Metric Name Convention Tests

	[Fact]
	public void HaveAllMetricNamesFollowLowerDotSeparatedConvention()
	{
		// Arrange
		var metricNames = GetAllMetricNames();

		// Act & Assert
		metricNames.ShouldNotBeEmpty("No metric names were discovered — assembly loading may have failed");

		foreach (var (typeName, fieldName, value) in metricNames)
		{
			LowerDotSeparatedPattern.IsMatch(value).ShouldBeTrue(
				$"Metric name '{value}' in {typeName}.{fieldName} does not follow 'lower.dot.separated' convention");
		}
	}

	[Fact]
	public void HaveAllMetricNamesNonNullAndNonEmpty()
	{
		// Arrange
		var metricNames = GetAllMetricNames();

		// Act & Assert
		metricNames.ShouldNotBeEmpty("No metric names were discovered");

		foreach (var (typeName, fieldName, value) in metricNames)
		{
			value.ShouldNotBeNullOrWhiteSpace(
				$"Metric name in {typeName}.{fieldName} is null or empty");
		}
	}

	[Fact]
	public void HaveNoDuplicateMetricNames()
	{
		// Arrange
		var metricNames = GetAllMetricNames();

		// Act
		var duplicates = metricNames
			.GroupBy(x => x.Value)
			.Where(g => g.Count() > 1)
			.Select(g => new
			{
				Name = g.Key,
				Sources = g.Select(x => $"{x.TypeName}.{x.FieldName}").ToList(),
			})
			.ToList();

		// Assert
		duplicates.ShouldBeEmpty(
			$"Duplicate metric names found: {string.Join("; ", duplicates.Select(d => $"'{d.Name}' in [{string.Join(", ", d.Sources)}]"))}");
	}

	#endregion

	#region Known TelemetryConstants Completeness Tests

	[Theory]
	[InlineData(typeof(DispatchTelemetryConstants), "BaseNamespace")]
	[InlineData(typeof(CdcTelemetryConstants), nameof(CdcTelemetryConstants.MeterName))]
	[InlineData(typeof(CdcTelemetryConstants), nameof(CdcTelemetryConstants.ActivitySourceName))]
	[InlineData(typeof(AuditTelemetryConstants), nameof(AuditTelemetryConstants.MeterName))]
	[InlineData(typeof(AuditTelemetryConstants), nameof(AuditTelemetryConstants.ActivitySourceName))]
	[InlineData(typeof(ErasureTelemetryConstants), nameof(ErasureTelemetryConstants.MeterName))]
	[InlineData(typeof(ErasureTelemetryConstants), nameof(ErasureTelemetryConstants.ActivitySourceName))]
	[InlineData(typeof(StreamingHandlerTelemetryConstants), nameof(StreamingHandlerTelemetryConstants.MeterName))]
	[InlineData(typeof(StreamingHandlerTelemetryConstants), nameof(StreamingHandlerTelemetryConstants.ActivitySourceName))]
	[InlineData(typeof(LeaderElectionTelemetryConstants), nameof(LeaderElectionTelemetryConstants.MeterName))]
	[InlineData(typeof(LeaderElectionTelemetryConstants), nameof(LeaderElectionTelemetryConstants.ActivitySourceName))]
	[InlineData(typeof(ContextObservabilityTelemetryConstants), nameof(ContextObservabilityTelemetryConstants.MeterName))]
	[InlineData(typeof(ContextObservabilityTelemetryConstants), nameof(ContextObservabilityTelemetryConstants.ActivitySourceName))]
	[InlineData(typeof(PersistenceTelemetryConstants), nameof(PersistenceTelemetryConstants.SourceName))]
	[InlineData(typeof(SqlServerPersistenceTelemetryConstants), nameof(SqlServerPersistenceTelemetryConstants.MeterName))]
	public void HaveKnownConstantsStartingWithExcalibur(Type constantsType, string fieldName)
	{
		// Arrange
		var field = constantsType.GetField(fieldName, BindingFlags.Public | BindingFlags.Static);

		// Act
		field.ShouldNotBeNull($"Field {fieldName} not found on {constantsType.Name}");
		var value = field.GetValue(null) as string;

		// Assert
		value.ShouldNotBeNullOrWhiteSpace($"{constantsType.Name}.{fieldName} is null or empty");
		value.StartsWith("Excalibur.", StringComparison.Ordinal).ShouldBeTrue(
			$"{constantsType.Name}.{fieldName} = '{value}' does not start with 'Excalibur.'");
	}

	[Theory]
	[InlineData(typeof(ContextObservabilityTelemetryConstants), "MiddlewareActivitySourceName")]
	public void HaveAdditionalActivitySourceNamesStartingWithExcalibur(Type constantsType, string fieldName)
	{
		// Arrange
		var field = constantsType.GetField(fieldName, BindingFlags.Public | BindingFlags.Static);

		// Act
		field.ShouldNotBeNull($"Field {fieldName} not found on {constantsType.Name}");
		var value = field.GetValue(null) as string;

		// Assert
		value.ShouldNotBeNullOrWhiteSpace($"{constantsType.Name}.{fieldName} is null or empty");
		value.StartsWith("Excalibur.", StringComparison.Ordinal).ShouldBeTrue(
			$"{constantsType.Name}.{fieldName} = '{value}' does not start with 'Excalibur.'");
	}

	#endregion

	#region EventSourcing Separate Classes Tests

	[Fact]
	public void HaveEventSourcingActivitySourceNamesFollowExcaliburPrefix()
	{
		// Arrange — EventSourcing uses separate classes instead of nested types
		var fields = typeof(EventSourcingActivitySources)
			.GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
			.Where(f => f.IsLiteral && !f.IsInitOnly && f.FieldType == typeof(string))
			.ToList();

		// Act & Assert
		fields.ShouldNotBeEmpty("EventSourcingActivitySources has no public const string fields");

		foreach (var field in fields)
		{
			var value = (string)field.GetRawConstantValue()!;
			value.ShouldNotBeNullOrWhiteSpace($"EventSourcingActivitySources.{field.Name} is null or empty");
			ExcaliburPrefixPattern.IsMatch(value).ShouldBeTrue(
				$"EventSourcingActivitySources.{field.Name} = '{value}' does not follow 'Excalibur.*' convention");
		}
	}

	[Fact]
	public void HaveEventSourcingMeterNamesFollowExcaliburPrefix()
	{
		// Arrange
		var fields = typeof(EventSourcingMeters)
			.GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
			.Where(f => f.IsLiteral && !f.IsInitOnly && f.FieldType == typeof(string))
			.ToList();

		// Act & Assert
		fields.ShouldNotBeEmpty("EventSourcingMeters has no public const string fields");

		foreach (var field in fields)
		{
			var value = (string)field.GetRawConstantValue()!;
			value.ShouldNotBeNullOrWhiteSpace($"EventSourcingMeters.{field.Name} is null or empty");
			ExcaliburPrefixPattern.IsMatch(value).ShouldBeTrue(
				$"EventSourcingMeters.{field.Name} = '{value}' does not follow 'Excalibur.*' convention");
		}
	}

	[Fact]
	public void HaveEventSourcingMetricNamesFollowLowerDotSeparatedConvention()
	{
		// Arrange
		var fields = typeof(EventSourcingMetricNames)
			.GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
			.Where(f => f.IsLiteral && !f.IsInitOnly && f.FieldType == typeof(string))
			.ToList();

		// Act & Assert
		fields.ShouldNotBeEmpty("EventSourcingMetricNames has no public const string fields");

		foreach (var field in fields)
		{
			var value = (string)field.GetRawConstantValue()!;
			value.ShouldNotBeNullOrWhiteSpace($"EventSourcingMetricNames.{field.Name} is null or empty");
			LowerDotSeparatedPattern.IsMatch(value).ShouldBeTrue(
				$"EventSourcingMetricNames.{field.Name} = '{value}' does not follow 'lower.dot.separated' convention");
		}
	}

	#endregion

	#region DispatchTelemetryConstants Nested Classes Tests

	[Fact]
	public void HaveDispatchActivitySourcesFollowExcaliburPrefix()
	{
		// Arrange
		var fields = typeof(DispatchTelemetryConstants.ActivitySources)
			.GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
			.Where(f => f.IsLiteral && !f.IsInitOnly && f.FieldType == typeof(string))
			.ToList();

		// Act & Assert
		fields.ShouldNotBeEmpty("DispatchTelemetryConstants.ActivitySources has no public const string fields");

		foreach (var field in fields)
		{
			var value = (string)field.GetRawConstantValue()!;
			value.ShouldNotBeNullOrWhiteSpace(
				$"DispatchTelemetryConstants.ActivitySources.{field.Name} is null or empty");
			ExcaliburPrefixPattern.IsMatch(value).ShouldBeTrue(
				$"DispatchTelemetryConstants.ActivitySources.{field.Name} = '{value}' does not follow 'Excalibur.*' convention");
		}
	}

	[Fact]
	public void HaveDispatchMetersFollowExcaliburPrefix()
	{
		// Arrange
		var fields = typeof(DispatchTelemetryConstants.Meters)
			.GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
			.Where(f => f.IsLiteral && !f.IsInitOnly && f.FieldType == typeof(string))
			.ToList();

		// Act & Assert
		fields.ShouldNotBeEmpty("DispatchTelemetryConstants.Meters has no public const string fields");

		foreach (var field in fields)
		{
			var value = (string)field.GetRawConstantValue()!;
			value.ShouldNotBeNullOrWhiteSpace(
				$"DispatchTelemetryConstants.Meters.{field.Name} is null or empty");
			ExcaliburPrefixPattern.IsMatch(value).ShouldBeTrue(
				$"DispatchTelemetryConstants.Meters.{field.Name} = '{value}' does not follow 'Excalibur.*' convention");
		}
	}

	#endregion

	#region Tag Name Convention Tests

	[Fact]
	public void HaveAllTagNamesFollowLowerCaseConvention()
	{
		// Arrange — Tag names follow OpenTelemetry attribute naming conventions:
		// lowercase with dots and/or underscores, at least two segments (e.g., "message.type", "message.is_duplicate", "event.version")
		var tagNames = GetAllTagNames();

		// Act & Assert
		tagNames.ShouldNotBeEmpty("No tag names were discovered — assembly loading may have failed");

		foreach (var (typeName, fieldName, value) in tagNames)
		{
			LowerCaseTagNamePattern.IsMatch(value).ShouldBeTrue(
				$"Tag name '{value}' in {typeName}.{fieldName} does not follow lowercase OTel attribute naming convention");
		}
	}

	[Fact]
	public void HaveAllTagNamesNonNullAndNonEmpty()
	{
		// Arrange
		var tagNames = GetAllTagNames();

		// Act & Assert
		tagNames.ShouldNotBeEmpty("No tag names were discovered");

		foreach (var (typeName, fieldName, value) in tagNames)
		{
			value.ShouldNotBeNullOrWhiteSpace(
				$"Tag name in {typeName}.{fieldName} is null or empty");
		}
	}

	#endregion

	#region Discovery Minimum Count Tests

	[Fact]
	public void DiscoverAtLeastSevenMeterNames()
	{
		// Arrange & Act — verifies assembly loading worked and sufficient constants are found
		var meterNames = GetAllMeterNames();

		// Assert — we know there are at least 7 distinct meter name constants across the codebase
		meterNames.Count.ShouldBeGreaterThanOrEqualTo(7,
			$"Expected at least 7 meter name constants but found {meterNames.Count}. Assembly loading may have failed.");
	}

	[Fact]
	public void DiscoverAtLeastSevenActivitySourceNames()
	{
		// Arrange & Act
		var activitySourceNames = GetAllActivitySourceNames();

		// Assert — we know there are at least 7 distinct activity source name constants
		activitySourceNames.Count.ShouldBeGreaterThanOrEqualTo(7,
			$"Expected at least 7 activity source name constants but found {activitySourceNames.Count}. Assembly loading may have failed.");
	}

	[Fact]
	public void DiscoverAtLeastTwentyMetricNames()
	{
		// Arrange & Act
		var metricNames = GetAllMetricNames();

		// Assert — across all constants classes there are 20+ metric name constants
		metricNames.Count.ShouldBeGreaterThanOrEqualTo(20,
			$"Expected at least 20 metric name constants but found {metricNames.Count}. Assembly loading may have failed.");
	}

	#endregion

	#region Helpers

	/// <summary>
	/// All TelemetryConstants types that use nested classes (Meters/MetricNames) or top-level MeterName/ActivitySourceName.
	/// </summary>
	private static readonly Type[] KnownTelemetryConstantsTypes =
	[
		typeof(DispatchTelemetryConstants),
		typeof(StreamingHandlerTelemetryConstants),
		typeof(ContextObservabilityTelemetryConstants),
		typeof(ErasureTelemetryConstants),
		typeof(TransportTelemetryConstants),
		typeof(CdcTelemetryConstants),
		typeof(AuditTelemetryConstants),
		typeof(LeaderElectionTelemetryConstants),
		typeof(PersistenceTelemetryConstants),
		typeof(SqlServerPersistenceTelemetryConstants),
	];

	/// <summary>
	/// EventSourcing uses separate top-level classes for its telemetry constants.
	/// </summary>
	private static readonly Type[] EventSourcingTelemetryTypes =
	[
		typeof(EventSourcingActivitySources),
		typeof(EventSourcingMeters),
		typeof(EventSourcingMetricNames),
	];

	/// <summary>
	/// Collects all meter name constants across all TelemetryConstants types.
	/// Searches for: top-level <c>MeterName</c> fields, nested <c>Meters</c> class fields.
	/// </summary>
	private static List<(string TypeName, string FieldName, string Value)> GetAllMeterNames()
	{
		var results = new List<(string TypeName, string FieldName, string Value)>();

		foreach (var type in KnownTelemetryConstantsTypes)
		{
			// Check top-level MeterName field
			var meterNameField = type.GetField("MeterName", BindingFlags.Public | BindingFlags.Static);
			if (meterNameField is { IsLiteral: true, FieldType.Name: "String" })
			{
				var value = (string)meterNameField.GetRawConstantValue()!;
				results.Add((type.Name, "MeterName", value));
			}

			// Check nested Meters class
			var metersType = type.GetNestedType("Meters", BindingFlags.Public);
			if (metersType != null)
			{
				foreach (var field in GetConstStringFields(metersType))
				{
					results.Add(($"{type.Name}.Meters", field.Name, (string)field.GetRawConstantValue()!));
				}
			}
		}

		// EventSourcing separate classes
		foreach (var field in GetConstStringFields(typeof(EventSourcingMeters)))
		{
			results.Add(("EventSourcingMeters", field.Name, (string)field.GetRawConstantValue()!));
		}

		return results;
	}

	/// <summary>
	/// Collects all activity source name constants across all TelemetryConstants types.
	/// Searches for: top-level <c>ActivitySourceName</c> fields, nested <c>ActivitySources</c> class fields,
	/// and additional fields matching <c>*ActivitySourceName</c>.
	/// </summary>
	private static List<(string TypeName, string FieldName, string Value)> GetAllActivitySourceNames()
	{
		var results = new List<(string TypeName, string FieldName, string Value)>();

		foreach (var type in KnownTelemetryConstantsTypes)
		{
			// Check all fields ending with "ActivitySourceName"
			var activitySourceFields = type
				.GetFields(BindingFlags.Public | BindingFlags.Static)
				.Where(f => f.IsLiteral && !f.IsInitOnly && f.FieldType == typeof(string)
					&& f.Name.EndsWith("ActivitySourceName", StringComparison.Ordinal));

			foreach (var field in activitySourceFields)
			{
				var value = (string)field.GetRawConstantValue()!;
				results.Add((type.Name, field.Name, value));
			}

			// Check nested ActivitySources class
			var activitySourcesType = type.GetNestedType("ActivitySources", BindingFlags.Public);
			if (activitySourcesType != null)
			{
				foreach (var field in GetConstStringFields(activitySourcesType))
				{
					results.Add(($"{type.Name}.ActivitySources", field.Name, (string)field.GetRawConstantValue()!));
				}
			}
		}

		// EventSourcing separate classes
		foreach (var field in GetConstStringFields(typeof(EventSourcingActivitySources)))
		{
			results.Add(("EventSourcingActivitySources", field.Name, (string)field.GetRawConstantValue()!));
		}

		return results;
	}

	/// <summary>
	/// Collects all metric name constants across all TelemetryConstants types.
	/// Searches for nested <c>MetricNames</c> classes.
	/// </summary>
	private static List<(string TypeName, string FieldName, string Value)> GetAllMetricNames()
	{
		var results = new List<(string TypeName, string FieldName, string Value)>();

		foreach (var type in KnownTelemetryConstantsTypes)
		{
			var metricNamesType = type.GetNestedType("MetricNames", BindingFlags.Public);
			if (metricNamesType != null)
			{
				foreach (var field in GetConstStringFields(metricNamesType))
				{
					results.Add(($"{type.Name}.MetricNames", field.Name, (string)field.GetRawConstantValue()!));
				}
			}
		}

		// EventSourcing separate class
		foreach (var field in GetConstStringFields(typeof(EventSourcingMetricNames)))
		{
			results.Add(("EventSourcingMetricNames", field.Name, (string)field.GetRawConstantValue()!));
		}

		return results;
	}

	/// <summary>
	/// Collects all tag name constants across all TelemetryConstants types.
	/// Searches for nested <c>Tags</c> classes and the separate <c>EventSourcingTags</c> class.
	/// </summary>
	private static List<(string TypeName, string FieldName, string Value)> GetAllTagNames()
	{
		var results = new List<(string TypeName, string FieldName, string Value)>();

		foreach (var type in KnownTelemetryConstantsTypes)
		{
			var tagsType = type.GetNestedType("Tags", BindingFlags.Public);
			if (tagsType != null)
			{
				foreach (var field in GetConstStringFields(tagsType))
				{
					results.Add(($"{type.Name}.Tags", field.Name, (string)field.GetRawConstantValue()!));
				}
			}
		}

		// EventSourcing separate class
		foreach (var field in GetConstStringFields(typeof(EventSourcingTags)))
		{
			results.Add(("EventSourcingTags", field.Name, (string)field.GetRawConstantValue()!));
		}

		return results;
	}

	/// <summary>
	/// Returns all public static const string fields from the given type.
	/// </summary>
	private static IEnumerable<FieldInfo> GetConstStringFields(Type type) =>
		type.GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
			.Where(f => f.IsLiteral && !f.IsInitOnly && f.FieldType == typeof(string));

	#endregion
}
