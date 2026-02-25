// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport;
using Shouldly;

namespace Excalibur.Dispatch.Transport.Tests.Abstractions.DeadLetterQueue;

[Trait("Category", "Unit")]
[Trait("Component", "Transport.Abstractions")]
public class DeadLetterStatisticsShould
{
    [Fact]
    public void HaveCorrectDefaultValues()
    {
        var stats = new DeadLetterStatistics();

        stats.MessageCount.ShouldBe(0);
        stats.AverageDeliveryAttempts.ShouldBe(0);
        stats.OldestMessageAge.ShouldBe(TimeSpan.Zero);
        stats.NewestMessageAge.ShouldBe(TimeSpan.Zero);
        stats.ReasonBreakdown.ShouldNotBeNull();
        stats.ReasonBreakdown.ShouldBeEmpty();
        stats.SourceBreakdown.ShouldNotBeNull();
        stats.SourceBreakdown.ShouldBeEmpty();
        stats.MessageTypeBreakdown.ShouldNotBeNull();
        stats.MessageTypeBreakdown.ShouldBeEmpty();
        stats.SizeInBytes.ShouldBe(0L);
        stats.GeneratedAt.ShouldNotBe(default);
    }

    [Fact]
    public void SetGeneratedAtToUtcNowByDefault()
    {
        var before = DateTimeOffset.UtcNow;
        var stats = new DeadLetterStatistics();
        var after = DateTimeOffset.UtcNow;

        stats.GeneratedAt.ShouldBeGreaterThanOrEqualTo(before);
        stats.GeneratedAt.ShouldBeLessThanOrEqualTo(after);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(10)]
    [InlineData(1000)]
    public void AllowSettingMessageCount(int count)
    {
        var stats = new DeadLetterStatistics { MessageCount = count };

        stats.MessageCount.ShouldBe(count);
    }

    [Theory]
    [InlineData(1.5)]
    [InlineData(3.2)]
    [InlineData(5.0)]
    public void AllowSettingAverageDeliveryAttempts(double average)
    {
        var stats = new DeadLetterStatistics { AverageDeliveryAttempts = average };

        stats.AverageDeliveryAttempts.ShouldBe(average);
    }

    [Fact]
    public void AllowSettingOldestMessageAge()
    {
        var age = TimeSpan.FromDays(7);
        var stats = new DeadLetterStatistics { OldestMessageAge = age };

        stats.OldestMessageAge.ShouldBe(age);
    }

    [Fact]
    public void AllowSettingNewestMessageAge()
    {
        var age = TimeSpan.FromMinutes(30);
        var stats = new DeadLetterStatistics { NewestMessageAge = age };

        stats.NewestMessageAge.ShouldBe(age);
    }

    [Fact]
    public void AllowAddingToReasonBreakdown()
    {
        var stats = new DeadLetterStatistics();

        stats.ReasonBreakdown["Timeout"] = 50;
        stats.ReasonBreakdown["Validation Failed"] = 30;
        stats.ReasonBreakdown["Authorization Failed"] = 20;

        stats.ReasonBreakdown.Count.ShouldBe(3);
        stats.ReasonBreakdown["Timeout"].ShouldBe(50);
        stats.ReasonBreakdown["Validation Failed"].ShouldBe(30);
        stats.ReasonBreakdown["Authorization Failed"].ShouldBe(20);
    }

    [Fact]
    public void AllowAddingToSourceBreakdown()
    {
        var stats = new DeadLetterStatistics();

        stats.SourceBreakdown["orders-queue"] = 75;
        stats.SourceBreakdown["payments-queue"] = 25;

        stats.SourceBreakdown.Count.ShouldBe(2);
        stats.SourceBreakdown["orders-queue"].ShouldBe(75);
        stats.SourceBreakdown["payments-queue"].ShouldBe(25);
    }

    [Fact]
    public void AllowAddingToMessageTypeBreakdown()
    {
        var stats = new DeadLetterStatistics();

        stats.MessageTypeBreakdown["OrderCreated"] = 40;
        stats.MessageTypeBreakdown["PaymentProcessed"] = 35;
        stats.MessageTypeBreakdown["InventoryUpdated"] = 25;

        stats.MessageTypeBreakdown.Count.ShouldBe(3);
        stats.MessageTypeBreakdown["OrderCreated"].ShouldBe(40);
        stats.MessageTypeBreakdown["PaymentProcessed"].ShouldBe(35);
        stats.MessageTypeBreakdown["InventoryUpdated"].ShouldBe(25);
    }

    [Theory]
    [InlineData(0L)]
    [InlineData(1_048_576L)]
    [InlineData(1_073_741_824L)]
    public void AllowSettingSizeInBytes(long size)
    {
        var stats = new DeadLetterStatistics { SizeInBytes = size };

        stats.SizeInBytes.ShouldBe(size);
    }

    [Fact]
    public void AllowSettingGeneratedAt()
    {
        var timestamp = DateTimeOffset.UtcNow.AddMinutes(-5);
        var stats = new DeadLetterStatistics { GeneratedAt = timestamp };

        stats.GeneratedAt.ShouldBe(timestamp);
    }

    [Fact]
    public void AllowComprehensiveStatistics()
    {
        var generatedAt = DateTimeOffset.UtcNow;
        var stats = new DeadLetterStatistics
        {
            MessageCount = 150,
            AverageDeliveryAttempts = 4.2,
            OldestMessageAge = TimeSpan.FromDays(14),
            NewestMessageAge = TimeSpan.FromMinutes(5),
            SizeInBytes = 52_428_800L,
            GeneratedAt = generatedAt
        };

        stats.ReasonBreakdown["Database Error"] = 60;
        stats.ReasonBreakdown["Network Timeout"] = 50;
        stats.ReasonBreakdown["Validation Error"] = 40;

        stats.SourceBreakdown["orders-queue"] = 80;
        stats.SourceBreakdown["inventory-queue"] = 70;

        stats.MessageTypeBreakdown["OrderCreated"] = 100;
        stats.MessageTypeBreakdown["InventoryReserved"] = 50;

        stats.MessageCount.ShouldBe(150);
        stats.AverageDeliveryAttempts.ShouldBe(4.2);
        stats.OldestMessageAge.ShouldBe(TimeSpan.FromDays(14));
        stats.NewestMessageAge.ShouldBe(TimeSpan.FromMinutes(5));
        stats.SizeInBytes.ShouldBe(52_428_800L);
        stats.GeneratedAt.ShouldBe(generatedAt);
        stats.ReasonBreakdown.Count.ShouldBe(3);
        stats.SourceBreakdown.Count.ShouldBe(2);
        stats.MessageTypeBreakdown.Count.ShouldBe(2);
    }
}
