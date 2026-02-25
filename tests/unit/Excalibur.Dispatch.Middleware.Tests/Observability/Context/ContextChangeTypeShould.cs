// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Observability.Context;

namespace Excalibur.Dispatch.Middleware.Tests.Observability.Context;

/// <summary>
/// Unit tests for <see cref="ContextChangeType"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Observability")]
public sealed class ContextChangeTypeShould : UnitTestBase
{
	[Fact]
	public void HaveExpectedValues()
	{
		// Assert
		ContextChangeType.Added.ShouldBe(ContextChangeType.Added);
		ContextChangeType.Modified.ShouldBe(ContextChangeType.Modified);
		ContextChangeType.Removed.ShouldBe(ContextChangeType.Removed);
	}

	[Fact]
	public void HaveDistinctValues()
	{
		// Arrange
		var values = Enum.GetValues<ContextChangeType>();

		// Assert
		values.Distinct().Count().ShouldBe(values.Length);
	}

	[Fact]
	public void HaveAtLeastThreeValues()
	{
		// Arrange
		var values = Enum.GetValues<ContextChangeType>();

		// Assert
		values.Length.ShouldBeGreaterThanOrEqualTo(3);
	}
}
