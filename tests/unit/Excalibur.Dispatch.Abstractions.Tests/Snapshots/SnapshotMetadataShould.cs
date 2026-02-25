// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// Licensed under the Excalibur License 1.0 - see LICENSE files for details.

using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Dispatch.Abstractions.Tests.Snapshots;

/// <summary>
/// Unit tests for the <see cref="SnapshotMetadata"/> class.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Abstractions")]
public sealed class SnapshotMetadataShould
{
	[Fact]
	public void Should_SetAllRequiredProperties()
	{
		// Act
		var metadata = new SnapshotMetadata
		{
			LastAppliedEventTimestamp = new DateTimeOffset(2026, 1, 15, 10, 30, 0, TimeSpan.Zero),
			LastAppliedEventId = "event-123",
			SnapshotVersion = "1.0",
			SerializerVersion = "2.0",
		};

		// Assert
		metadata.LastAppliedEventTimestamp.ShouldBe(new DateTimeOffset(2026, 1, 15, 10, 30, 0, TimeSpan.Zero));
		metadata.LastAppliedEventId.ShouldBe("event-123");
		metadata.SnapshotVersion.ShouldBe("1.0");
		metadata.SerializerVersion.ShouldBe("2.0");
	}

	[Fact]
	public void Should_BeSealed()
	{
		// Assert
		typeof(SnapshotMetadata).IsSealed.ShouldBeTrue();
	}
}
