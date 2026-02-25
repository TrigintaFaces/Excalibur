// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Observability.Context;

namespace Excalibur.Dispatch.Middleware.Tests.Observability.Context;

/// <summary>
/// Unit tests for <see cref="AnomalySeverity"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Observability")]
public sealed class AnomalySeverityShould : UnitTestBase
{
	[Fact]
	public void HaveExpectedValues()
	{
		// Assert - Verify expected enum values exist
		AnomalySeverity.Low.ShouldBe(AnomalySeverity.Low);
		AnomalySeverity.Medium.ShouldBe(AnomalySeverity.Medium);
		AnomalySeverity.High.ShouldBe(AnomalySeverity.High);
	}

	[Fact]
	public void HaveDistinctValues()
	{
		// Arrange
		var values = Enum.GetValues<AnomalySeverity>();

		// Assert
		values.Distinct().Count().ShouldBe(values.Length);
	}

	[Fact]
	public void BeComparable()
	{
		// Assert - Severity ordering
		((int)AnomalySeverity.Low).ShouldBeLessThan((int)AnomalySeverity.Medium);
		((int)AnomalySeverity.Medium).ShouldBeLessThan((int)AnomalySeverity.High);
	}

	[Fact]
	public void HaveLowAsDefault()
	{
		// Arrange
		var defaultValue = default(AnomalySeverity);

		// Assert
		defaultValue.ShouldBe(AnomalySeverity.Low);
	}
}
