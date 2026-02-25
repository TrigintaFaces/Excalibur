// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Aws;

namespace Excalibur.Dispatch.Transport.Tests.AwsSqs.DeadLetterQueue;

[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
public sealed class DlqStatisticsShould
{
	[Fact]
	public void HaveCorrectDefaults()
	{
		// Arrange & Act
		var stats = new DlqStatistics();

		// Assert
		stats.TotalMessages.ShouldBe(0);
		stats.MessagesByAge.ShouldNotBeNull();
		stats.MessagesByAge.ShouldBeEmpty();
		stats.MessagesByErrorType.ShouldNotBeNull();
		stats.MessagesByErrorType.ShouldBeEmpty();
		stats.OldestMessageTimestamp.ShouldBeNull();
		stats.NewestMessageTimestamp.ShouldBeNull();
		stats.AverageRetryCount.ShouldBe(0.0);
		stats.RedrivenToday.ShouldBe(0);
		stats.ArchivedToday.ShouldBe(0);
		stats.MessagesProcessed.ShouldBe(0);
		stats.MessagesRequeued.ShouldBe(0);
		stats.MessagesDiscarded.ShouldBe(0);
		stats.GeneratedAt.ShouldBe(default);
	}

	[Fact]
	public void AllowSettingAllProperties()
	{
		// Arrange
		var now = DateTime.UtcNow;
		var generatedAt = DateTimeOffset.UtcNow;

		// Act
		var stats = new DlqStatistics
		{
			TotalMessages = 1500,
			OldestMessageTimestamp = now.AddDays(-7),
			NewestMessageTimestamp = now.AddMinutes(-5),
			AverageRetryCount = 3.5,
			RedrivenToday = 42,
			ArchivedToday = 8,
			MessagesProcessed = 1200,
			MessagesRequeued = 200,
			MessagesDiscarded = 100,
			GeneratedAt = generatedAt,
		};
		stats.MessagesByAge["<1h"] = 50;
		stats.MessagesByAge["1h-24h"] = 300;
		stats.MessagesByAge[">24h"] = 1150;
		stats.MessagesByErrorType["TimeoutException"] = 800;
		stats.MessagesByErrorType["DeserializationError"] = 700;

		// Assert
		stats.TotalMessages.ShouldBe(1500);
		stats.OldestMessageTimestamp.ShouldBe(now.AddDays(-7));
		stats.NewestMessageTimestamp.ShouldBe(now.AddMinutes(-5));
		stats.AverageRetryCount.ShouldBe(3.5);
		stats.RedrivenToday.ShouldBe(42);
		stats.ArchivedToday.ShouldBe(8);
		stats.MessagesProcessed.ShouldBe(1200);
		stats.MessagesRequeued.ShouldBe(200);
		stats.MessagesDiscarded.ShouldBe(100);
		stats.GeneratedAt.ShouldBe(generatedAt);
		stats.MessagesByAge.Count.ShouldBe(3);
		stats.MessagesByErrorType.Count.ShouldBe(2);
	}
}
