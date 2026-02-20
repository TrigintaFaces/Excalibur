// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Reflection;

using Excalibur.Dispatch.Observability.Metrics;

namespace Excalibur.Dispatch.Observability.Tests.Metrics;

/// <summary>
/// Boundary tests for <see cref="OpenTelemetryExtensions"/>.
/// Verifies that AddAllDispatchMetrics and AddAllDispatchTracing register all expected names.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Observability")]
[Trait("Feature", "OpenTelemetry")]
public sealed class OpenTelemetryExtensionsShould
{
	/// <summary>
	/// Expected meter names that must be present in <c>AllMeterNames</c>.
	/// This list represents the minimum set of meters the framework provides.
	/// </summary>
	private static readonly string[] ExpectedMeterNames =
	[
		"Excalibur.Dispatch.Core",
		"Excalibur.Dispatch.Pipeline",
		"Excalibur.Dispatch.TimePolicy",
		"Excalibur.Dispatch.Transport",
		"Excalibur.Dispatch.DeadLetterQueue",
		"Excalibur.Dispatch.CircuitBreaker",
		"Excalibur.Dispatch.Streaming",
		"Excalibur.Dispatch.Compliance",
		"Excalibur.Dispatch.Compliance.Erasure",
		"Excalibur.Dispatch.Encryption",
		"Excalibur.EventSourcing.MaterializedViews",
		"Excalibur.Dispatch.WriteStores",
		"Excalibur.Data.Cdc",
		"Excalibur.Data.Audit",
		"Excalibur.LeaderElection",
		"Excalibur.Dispatch.Sagas",
		"Excalibur.Dispatch.BackgroundServices",
		"Excalibur.Dispatch.BatchProcessor",
		"Excalibur.Dispatch.Observability.Context",
	];

	/// <summary>
	/// Expected ActivitySource names that must be present in <c>AllActivitySourceNames</c>.
	/// </summary>
	private static readonly string[] ExpectedActivitySourceNames =
	[
		"Excalibur.Dispatch.Core",
		"Excalibur.Dispatch.Pipeline",
		"Excalibur.Dispatch.TimePolicy",
		"Excalibur.Dispatch.Streaming",
		"Excalibur.Dispatch.Compliance.Erasure",
		"Excalibur.Data.Cdc",
		"Excalibur.Data.Audit",
		"Excalibur.LeaderElection",
	];

	#region AllMeterNames boundary tests

	[Fact]
	public void RegisterAllExpectedMeterNames()
	{
		// Arrange — read the private static array via reflection
		var allMeterNames = GetPrivateStaticField<string[]>("AllMeterNames");

		// Assert — every expected name must be present
		foreach (var expected in ExpectedMeterNames)
		{
			allMeterNames.ShouldContain(expected,
				$"AllMeterNames is missing meter '{expected}'. " +
				"This means AddAllDispatchMetrics() will silently drop this meter.");
		}
	}

	[Fact]
	public void HaveAtLeast19MeterNames()
	{
		// Arrange
		var allMeterNames = GetPrivateStaticField<string[]>("AllMeterNames");

		// Assert — at least the 19 known meters
		allMeterNames.Length.ShouldBeGreaterThanOrEqualTo(19,
			"AllMeterNames has fewer meters than expected. " +
			"Did a meter registration get accidentally removed?");
	}

	[Fact]
	public void HaveNoDuplicateMeterNames()
	{
		// Arrange
		var allMeterNames = GetPrivateStaticField<string[]>("AllMeterNames");

		// Assert — no duplicates
		var distinct = allMeterNames.Distinct(StringComparer.Ordinal).ToArray();
		distinct.Length.ShouldBe(allMeterNames.Length,
			$"AllMeterNames contains duplicates: " +
			$"{string.Join(", ", allMeterNames.GroupBy(n => n).Where(g => g.Count() > 1).Select(g => g.Key))}");
	}

	[Fact]
	public void HaveNoEmptyOrNullMeterNames()
	{
		// Arrange
		var allMeterNames = GetPrivateStaticField<string[]>("AllMeterNames");

		// Assert
		foreach (var name in allMeterNames)
		{
			name.ShouldNotBeNullOrWhiteSpace("AllMeterNames contains a null or empty meter name");
		}
	}

	#endregion

	#region AllActivitySourceNames boundary tests

	[Fact]
	public void RegisterAllExpectedActivitySourceNames()
	{
		// Arrange
		var allSourceNames = GetPrivateStaticField<string[]>("AllActivitySourceNames");

		// Assert
		foreach (var expected in ExpectedActivitySourceNames)
		{
			allSourceNames.ShouldContain(expected,
				$"AllActivitySourceNames is missing source '{expected}'. " +
				"This means AddAllDispatchTracing() will silently drop this activity source.");
		}
	}

	[Fact]
	public void HaveAtLeast8ActivitySourceNames()
	{
		// Arrange
		var allSourceNames = GetPrivateStaticField<string[]>("AllActivitySourceNames");

		// Assert
		allSourceNames.Length.ShouldBeGreaterThanOrEqualTo(8,
			"AllActivitySourceNames has fewer sources than expected.");
	}

	[Fact]
	public void HaveNoDuplicateActivitySourceNames()
	{
		// Arrange
		var allSourceNames = GetPrivateStaticField<string[]>("AllActivitySourceNames");

		// Assert
		var distinct = allSourceNames.Distinct(StringComparer.Ordinal).ToArray();
		distinct.Length.ShouldBe(allSourceNames.Length,
			$"AllActivitySourceNames contains duplicates: " +
			$"{string.Join(", ", allSourceNames.GroupBy(n => n).Where(g => g.Count() > 1).Select(g => g.Key))}");
	}

	[Fact]
	public void HaveNoEmptyOrNullActivitySourceNames()
	{
		// Arrange
		var allSourceNames = GetPrivateStaticField<string[]>("AllActivitySourceNames");

		// Assert
		foreach (var name in allSourceNames)
		{
			name.ShouldNotBeNullOrWhiteSpace("AllActivitySourceNames contains a null or empty source name");
		}
	}

	#endregion

	#region Helpers

	private static T GetPrivateStaticField<T>(string fieldName)
	{
		var field = typeof(OpenTelemetryExtensions)
			.GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Static);

		field.ShouldNotBeNull($"Could not find private static field '{fieldName}' on OpenTelemetryExtensions");

		var value = field.GetValue(null);
		value.ShouldNotBeNull($"Field '{fieldName}' returned null");

		return (T)value;
	}

	#endregion
}
