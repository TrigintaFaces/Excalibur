// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Observability.Context;

namespace Excalibur.Dispatch.Middleware.Tests.Observability.Context;

/// <summary>
/// Unit tests for <see cref="ContextLineage"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Observability")]
public sealed class ContextLineageShould : UnitTestBase
{
	[Fact]
	public void CreateWithRequiredProperties()
	{
		// Arrange & Act
		var lineage = new ContextLineage
		{
			CorrelationId = "corr-123",
			OriginMessageId = "msg-456",
			StartTime = DateTimeOffset.UtcNow,
			Snapshots = [],
			ServiceBoundaries = []
		};

		// Assert
		lineage.CorrelationId.ShouldBe("corr-123");
		lineage.OriginMessageId.ShouldBe("msg-456");
		lineage.Snapshots.ShouldNotBeNull();
		lineage.ServiceBoundaries.ShouldNotBeNull();
	}

	[Fact]
	public void AllowAddingSnapshots()
	{
		// Arrange
		var lineage = new ContextLineage
		{
			CorrelationId = "corr-123",
			OriginMessageId = "msg-456",
			StartTime = DateTimeOffset.UtcNow,
			Snapshots = [],
			ServiceBoundaries = []
		};

		var snapshot = new ContextSnapshot
		{
			MessageId = "msg-456",
			Stage = "Handler",
			Timestamp = DateTimeOffset.UtcNow,
			Fields = new Dictionary<string, object?>(StringComparer.Ordinal),
			FieldCount = 0,
			SizeBytes = 0,
			Metadata = new Dictionary<string, object>(StringComparer.Ordinal)
		};

		// Act
		lineage.Snapshots.Add(snapshot);

		// Assert
		lineage.Snapshots.Count.ShouldBe(1);
		lineage.Snapshots[0].ShouldBe(snapshot);
	}

	[Fact]
	public void AllowAddingServiceBoundaries()
	{
		// Arrange
		var lineage = new ContextLineage
		{
			CorrelationId = "corr-123",
			OriginMessageId = "msg-456",
			StartTime = DateTimeOffset.UtcNow,
			Snapshots = [],
			ServiceBoundaries = []
		};

		var boundary = new ServiceBoundaryTransition
		{
			ServiceName = "OrderService",
			Timestamp = DateTimeOffset.UtcNow,
			ContextPreserved = true
		};

		// Act
		lineage.ServiceBoundaries.Add(boundary);

		// Assert
		lineage.ServiceBoundaries.Count.ShouldBe(1);
		lineage.ServiceBoundaries[0].ServiceName.ShouldBe("OrderService");
	}

	[Fact]
	public void TrackMultipleBoundaryTransitions()
	{
		// Arrange
		var lineage = new ContextLineage
		{
			CorrelationId = "corr-123",
			OriginMessageId = "msg-456",
			StartTime = DateTimeOffset.UtcNow,
			Snapshots = [],
			ServiceBoundaries = []
		};

		// Act
		lineage.ServiceBoundaries.Add(new ServiceBoundaryTransition
		{
			ServiceName = "Gateway",
			Timestamp = DateTimeOffset.UtcNow,
			ContextPreserved = true
		});
		lineage.ServiceBoundaries.Add(new ServiceBoundaryTransition
		{
			ServiceName = "OrderService",
			Timestamp = DateTimeOffset.UtcNow.AddMilliseconds(100),
			ContextPreserved = true
		});
		lineage.ServiceBoundaries.Add(new ServiceBoundaryTransition
		{
			ServiceName = "PaymentService",
			Timestamp = DateTimeOffset.UtcNow.AddMilliseconds(200),
			ContextPreserved = true
		});

		// Assert
		lineage.ServiceBoundaries.Count.ShouldBe(3);
	}
}
