// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Reflection;

using Excalibur.Dispatch.Observability.Diagnostics;

namespace Excalibur.Dispatch.Observability.Tests.Diagnostics;

/// <summary>
/// Unit tests for <see cref="ObservabilityEventId"/> constants.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Observability")]
[Trait("Feature", "Diagnostics")]
public sealed class ObservabilityEventIdShould
{
	[Fact]
	public void HaveAllEventIdsInExpectedRange()
	{
		// Arrange
		var fields = typeof(ObservabilityEventId)
			.GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
			.Where(f => f.IsLiteral && f.FieldType == typeof(int))
			.ToList();

		// Act & Assert — all event IDs should be in range 80000-80999
		foreach (var field in fields)
		{
			var value = (int)field.GetValue(null)!;
			value.ShouldBeGreaterThanOrEqualTo(80000, $"Event ID {field.Name} = {value} is below 80000");
			value.ShouldBeLessThanOrEqualTo(80999, $"Event ID {field.Name} = {value} is above 80999");
		}
	}

	[Fact]
	public void HaveUniqueEventIds()
	{
		// Arrange
		var fields = typeof(ObservabilityEventId)
			.GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
			.Where(f => f.IsLiteral && f.FieldType == typeof(int))
			.ToList();

		// Act
		var ids = fields.Select(f => (int)f.GetValue(null)!).ToList();
		var duplicates = ids.GroupBy(id => id).Where(g => g.Count() > 1).ToList();

		// Assert
		duplicates.ShouldBeEmpty($"Found duplicate event IDs: {string.Join(", ", duplicates.Select(g => g.Key))}");
	}

	[Fact]
	public void HaveContextFlowIdsInCorrectRange()
	{
		// Assert — Context Flow range: 80000-80099
		ObservabilityEventId.ContextFlowTrackerCreated.ShouldBeInRange(80000, 80099);
		ObservabilityEventId.NullContextAttempted.ShouldBeInRange(80000, 80099);
		ObservabilityEventId.ContextStateRecorded.ShouldBeInRange(80000, 80099);
		ObservabilityEventId.ContextChangesDetected.ShouldBeInRange(80000, 80099);
		ObservabilityEventId.ContextCorrelated.ShouldBeInRange(80000, 80099);
	}

	[Fact]
	public void HaveContextObservabilityIdsInCorrectRange()
	{
		// Assert — Context Observability range: 80100-80199
		ObservabilityEventId.ContextObservabilityExecuting.ShouldBeInRange(80100, 80199);
		ObservabilityEventId.ContextIntegrityValidationFailed.ShouldBeInRange(80100, 80199);
		ObservabilityEventId.PipelineStageException.ShouldBeInRange(80100, 80199);
	}

	[Fact]
	public void HaveContextEnrichmentIdsInCorrectRange()
	{
		// Assert — Context Enrichment range: 80200-80299
		ObservabilityEventId.ActivityEnriched.ShouldBeInRange(80200, 80299);
		ObservabilityEventId.TraceLinkCreated.ShouldBeInRange(80200, 80299);
		ObservabilityEventId.BaggagePropagated.ShouldBeInRange(80200, 80299);
	}

	[Fact]
	public void HaveSanitizationIdsInCorrectRange()
	{
		// Assert — Sanitization range: 80600-80699
		ObservabilityEventId.PiiSanitizationBypassed.ShouldBeInRange(80600, 80699);
		ObservabilityEventId.ComplianceSanitizerRegistered.ShouldBeInRange(80600, 80699);
		ObservabilityEventId.CompliancePiiPatternDetected.ShouldBeInRange(80600, 80699);
	}
}
