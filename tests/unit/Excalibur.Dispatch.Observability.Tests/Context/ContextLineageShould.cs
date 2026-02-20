// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Observability.Context;

namespace Excalibur.Dispatch.Observability.Tests.Context;

/// <summary>
/// Unit tests for <see cref="ContextLineage"/> class.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Observability")]
[Trait("Feature", "Context")]
public sealed class ContextLineageShould
{
	#region Required Property Tests

	[Fact]
	public void RequireCorrelationId()
	{
		// Arrange & Act
		var lineage = new ContextLineage
		{
			CorrelationId = "corr-123",
			Snapshots = new List<ContextSnapshot>(),
			ServiceBoundaries = new List<ServiceBoundaryTransition>(),
		};

		// Assert
		lineage.CorrelationId.ShouldBe("corr-123");
	}

	[Fact]
	public void RequireSnapshots()
	{
		// Arrange
		var snapshots = new List<ContextSnapshot>
		{
			new()
			{
				MessageId = "msg-1",
				Stage = "Pre",
				Fields = new Dictionary<string, object?>(),
				Metadata = new Dictionary<string, object>(),
			},
		};

		// Act
		var lineage = new ContextLineage
		{
			CorrelationId = "corr-456",
			Snapshots = snapshots,
			ServiceBoundaries = new List<ServiceBoundaryTransition>(),
		};

		// Assert
		lineage.Snapshots.ShouldBe(snapshots);
		lineage.Snapshots.Count.ShouldBe(1);
	}

	[Fact]
	public void RequireServiceBoundaries()
	{
		// Arrange
		var boundaries = new List<ServiceBoundaryTransition>
		{
			new()
			{
				ServiceName = "ServiceA",
			},
		};

		// Act
		var lineage = new ContextLineage
		{
			CorrelationId = "corr-789",
			Snapshots = new List<ContextSnapshot>(),
			ServiceBoundaries = boundaries,
		};

		// Assert
		lineage.ServiceBoundaries.ShouldBe(boundaries);
		lineage.ServiceBoundaries.Count.ShouldBe(1);
	}

	#endregion

	#region Optional Property Tests

	[Fact]
	public void HaveNullOriginMessageIdByDefault()
	{
		// Arrange & Act
		var lineage = new ContextLineage
		{
			CorrelationId = "corr-abc",
			Snapshots = new List<ContextSnapshot>(),
			ServiceBoundaries = new List<ServiceBoundaryTransition>(),
		};

		// Assert
		lineage.OriginMessageId.ShouldBeNull();
	}

	[Fact]
	public void AllowSettingOriginMessageId()
	{
		// Arrange & Act
		var lineage = new ContextLineage
		{
			CorrelationId = "corr-def",
			OriginMessageId = "origin-msg-123",
			Snapshots = new List<ContextSnapshot>(),
			ServiceBoundaries = new List<ServiceBoundaryTransition>(),
		};

		// Assert
		lineage.OriginMessageId.ShouldBe("origin-msg-123");
	}

	[Fact]
	public void HaveDefaultStartTime()
	{
		// Arrange & Act
		var lineage = new ContextLineage
		{
			CorrelationId = "corr-ghi",
			Snapshots = new List<ContextSnapshot>(),
			ServiceBoundaries = new List<ServiceBoundaryTransition>(),
		};

		// Assert
		lineage.StartTime.ShouldBe(default(DateTimeOffset));
	}

	[Fact]
	public void AllowSettingStartTime()
	{
		// Arrange
		var startTime = DateTimeOffset.UtcNow;

		// Act
		var lineage = new ContextLineage
		{
			CorrelationId = "corr-jkl",
			StartTime = startTime,
			Snapshots = new List<ContextSnapshot>(),
			ServiceBoundaries = new List<ServiceBoundaryTransition>(),
		};

		// Assert
		lineage.StartTime.ShouldBe(startTime);
	}

	#endregion

	#region Complete Object Tests

	[Fact]
	public void AllowSettingAllProperties()
	{
		// Arrange
		var startTime = DateTimeOffset.UtcNow;
		var snapshots = new List<ContextSnapshot>
		{
			new()
			{
				MessageId = "msg-a",
				Stage = "PreHandler",
				Fields = new Dictionary<string, object?> { ["Field1"] = "Value1" },
				Metadata = new Dictionary<string, object>(),
			},
			new()
			{
				MessageId = "msg-a",
				Stage = "PostHandler",
				Fields = new Dictionary<string, object?> { ["Field1"] = "Value1", ["Field2"] = "Value2" },
				Metadata = new Dictionary<string, object>(),
			},
		};
		var boundaries = new List<ServiceBoundaryTransition>
		{
			new() { ServiceName = "ServiceA", ContextPreserved = true },
			new() { ServiceName = "ServiceB", ContextPreserved = true },
		};

		// Act
		var lineage = new ContextLineage
		{
			CorrelationId = "corr-mno",
			OriginMessageId = "origin-mno",
			StartTime = startTime,
			Snapshots = snapshots,
			ServiceBoundaries = boundaries,
		};

		// Assert
		lineage.CorrelationId.ShouldBe("corr-mno");
		lineage.OriginMessageId.ShouldBe("origin-mno");
		lineage.StartTime.ShouldBe(startTime);
		lineage.Snapshots.Count.ShouldBe(2);
		lineage.ServiceBoundaries.Count.ShouldBe(2);
	}

	[Fact]
	public void SupportEmptyCollections()
	{
		// Arrange & Act
		var lineage = new ContextLineage
		{
			CorrelationId = "corr-empty",
			Snapshots = new List<ContextSnapshot>(),
			ServiceBoundaries = new List<ServiceBoundaryTransition>(),
		};

		// Assert
		lineage.Snapshots.ShouldBeEmpty();
		lineage.ServiceBoundaries.ShouldBeEmpty();
	}

	[Fact]
	public void SupportAddingSnapshots()
	{
		// Arrange
		var lineage = new ContextLineage
		{
			CorrelationId = "corr-add",
			Snapshots = new List<ContextSnapshot>(),
			ServiceBoundaries = new List<ServiceBoundaryTransition>(),
		};

		// Act
		lineage.Snapshots.Add(new ContextSnapshot
		{
			MessageId = "msg-new",
			Stage = "New",
			Fields = new Dictionary<string, object?>(),
			Metadata = new Dictionary<string, object>(),
		});

		// Assert
		lineage.Snapshots.Count.ShouldBe(1);
	}

	[Fact]
	public void SupportAddingServiceBoundaries()
	{
		// Arrange
		var lineage = new ContextLineage
		{
			CorrelationId = "corr-boundary",
			Snapshots = new List<ContextSnapshot>(),
			ServiceBoundaries = new List<ServiceBoundaryTransition>(),
		};

		// Act
		lineage.ServiceBoundaries.Add(new ServiceBoundaryTransition
		{
			ServiceName = "NewService",
		});

		// Assert
		lineage.ServiceBoundaries.Count.ShouldBe(1);
	}

	#endregion
}
