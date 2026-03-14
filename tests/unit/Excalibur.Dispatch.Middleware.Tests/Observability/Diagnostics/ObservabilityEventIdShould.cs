// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Reflection;

using Excalibur.Dispatch.Observability.Diagnostics;

namespace Excalibur.Dispatch.Middleware.Tests.Observability.Diagnostics;

[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
public sealed class ObservabilityEventIdShould
{
	[Fact]
	public void HaveUniqueEventIds()
	{
		// Arrange - get all public const int fields
		var fields = typeof(ObservabilityEventId)
			.GetFields(BindingFlags.Public | BindingFlags.Static)
			.Where(f => f.IsLiteral && f.FieldType == typeof(int))
			.ToList();

		// Act
		var values = fields.Select(f => (int)f.GetValue(null)!).ToList();

		// Assert
		fields.Count.ShouldBeGreaterThan(0);
		values.Distinct().Count().ShouldBe(values.Count,
			"All ObservabilityEventId constants should have unique values");
	}

	[Fact]
	public void HaveEventIdsInObservabilityRange()
	{
		// Observability package range: 80000-80999
		var fields = typeof(ObservabilityEventId)
			.GetFields(BindingFlags.Public | BindingFlags.Static)
			.Where(f => f.IsLiteral && f.FieldType == typeof(int))
			.ToList();

		foreach (var field in fields)
		{
			var value = (int)field.GetValue(null)!;
			value.ShouldBeGreaterThanOrEqualTo(80000,
				$"{field.Name} should be >= 80000 (Observability range)");
			value.ShouldBeLessThan(81000,
				$"{field.Name} should be < 81000 (Observability range)");
		}
	}
}
