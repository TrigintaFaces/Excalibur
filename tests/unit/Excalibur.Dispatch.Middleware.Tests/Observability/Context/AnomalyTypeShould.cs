// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Observability.Context;

namespace Excalibur.Dispatch.Middleware.Tests.Observability.Context;

/// <summary>
/// Unit tests for <see cref="AnomalyType"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Observability")]
public sealed class AnomalyTypeShould : UnitTestBase
{
	[Fact]
	public void HaveExpectedValues()
	{
		// Assert - Verify expected enum values exist
		AnomalyType.MissingCorrelation.ShouldBe(AnomalyType.MissingCorrelation);
		AnomalyType.InsufficientContext.ShouldBe(AnomalyType.InsufficientContext);
		AnomalyType.ExcessiveContext.ShouldBe(AnomalyType.ExcessiveContext);
		AnomalyType.CircularCausation.ShouldBe(AnomalyType.CircularCausation);
		AnomalyType.PotentialPII.ShouldBe(AnomalyType.PotentialPII);
		AnomalyType.OversizedItem.ShouldBe(AnomalyType.OversizedItem);
	}

	[Fact]
	public void HaveDistinctValues()
	{
		// Arrange
		var values = Enum.GetValues<AnomalyType>();

		// Assert
		values.Distinct().Count().ShouldBe(values.Length);
	}

	[Fact]
	public void HaveMissingCorrelationAsDefault()
	{
		// Arrange
		var defaultValue = default(AnomalyType);

		// Assert
		defaultValue.ShouldBe(AnomalyType.MissingCorrelation);
	}
}
