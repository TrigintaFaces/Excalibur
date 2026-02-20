// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Abstractions.Tests.Outbox;

/// <summary>
/// Unit tests for <see cref="TransportDeliveryStatistics"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class TransportDeliveryStatisticsShould
{
	[Fact]
	public void DefaultValues_AreZero()
	{
		// Act
		var stats = new TransportDeliveryStatistics();

		// Assert
		stats.PendingCount.ShouldBe(0);
		stats.SendingCount.ShouldBe(0);
		stats.SentCount.ShouldBe(0);
		stats.FailedCount.ShouldBe(0);
		stats.SkippedCount.ShouldBe(0);
		stats.OldestPendingAge.ShouldBeNull();
		stats.TransportName.ShouldBeNull();
	}

	[Fact]
	public void AllProperties_CanBeSet()
	{
		// Act
		var stats = new TransportDeliveryStatistics
		{
			PendingCount = 5,
			SendingCount = 2,
			SentCount = 100,
			FailedCount = 3,
			SkippedCount = 1,
			OldestPendingAge = TimeSpan.FromMinutes(10),
			TransportName = "rabbitmq",
		};

		// Assert
		stats.PendingCount.ShouldBe(5);
		stats.SendingCount.ShouldBe(2);
		stats.SentCount.ShouldBe(100);
		stats.FailedCount.ShouldBe(3);
		stats.SkippedCount.ShouldBe(1);
		stats.OldestPendingAge.ShouldBe(TimeSpan.FromMinutes(10));
		stats.TransportName.ShouldBe("rabbitmq");
	}
}
