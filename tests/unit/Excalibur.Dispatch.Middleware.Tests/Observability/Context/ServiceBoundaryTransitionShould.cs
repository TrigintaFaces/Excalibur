// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Observability.Context;

namespace Excalibur.Dispatch.Middleware.Tests.Observability.Context;

/// <summary>
/// Unit tests for <see cref="ServiceBoundaryTransition"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Observability")]
public sealed class ServiceBoundaryTransitionShould : UnitTestBase
{
	[Fact]
	public void CreateWithRequiredProperties()
	{
		// Arrange & Act
		var transition = new ServiceBoundaryTransition
		{
			ServiceName = "OrderService",
			Timestamp = DateTimeOffset.UtcNow,
			ContextPreserved = true
		};

		// Assert
		transition.ServiceName.ShouldBe("OrderService");
		transition.ContextPreserved.ShouldBeTrue();
	}

	[Fact]
	public void TrackWhenContextIsNotPreserved()
	{
		// Arrange & Act
		var transition = new ServiceBoundaryTransition
		{
			ServiceName = "LegacyService",
			Timestamp = DateTimeOffset.UtcNow,
			ContextPreserved = false
		};

		// Assert
		transition.ContextPreserved.ShouldBeFalse();
	}

	[Fact]
	public void RecordAccurateTimestamp()
	{
		// Arrange
		var before = DateTimeOffset.UtcNow;

		// Act
		var transition = new ServiceBoundaryTransition
		{
			ServiceName = "TestService",
			Timestamp = DateTimeOffset.UtcNow,
			ContextPreserved = true
		};

		var after = DateTimeOffset.UtcNow;

		// Assert
		transition.Timestamp.ShouldBeGreaterThanOrEqualTo(before);
		transition.Timestamp.ShouldBeLessThanOrEqualTo(after);
	}
}
