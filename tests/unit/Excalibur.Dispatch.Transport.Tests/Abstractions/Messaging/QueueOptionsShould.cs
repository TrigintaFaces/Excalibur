// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport;
using Shouldly;

namespace Excalibur.Dispatch.Transport.Tests.Abstractions.Messaging;

[Trait("Category", "Unit")]
[Trait("Component", "Transport.Abstractions")]
public class QueueOptionsShould
{
    [Fact]
    public void HaveCorrectDefaultValues()
    {
        var options = new QueueOptions();

        options.MaxSizeInMB.ShouldBeNull();
        options.DefaultMessageTimeToLive.ShouldBeNull();
        options.LockDuration.ShouldBeNull();
        options.EnableDeduplication.ShouldBeNull();
        options.DuplicateDetectionWindow.ShouldBeNull();
        options.RequiresSession.ShouldBeNull();
        options.DeadLetteringOnMessageExpiration.ShouldBeNull();
        options.MaxDeliveryCount.ShouldBeNull();
        options.EnablePartitioning.ShouldBeNull();
        options.Properties.ShouldNotBeNull();
        options.Properties.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(1L)]
    [InlineData(1024L)]
    [InlineData(5120L)]
    [InlineData(null)]
    public void AllowSettingMaxSizeInMB(long? maxSize)
    {
        var options = new QueueOptions { MaxSizeInMB = maxSize };

        options.MaxSizeInMB.ShouldBe(maxSize);
    }

    [Fact]
    public void AllowSettingDefaultMessageTimeToLive()
    {
        var ttl = TimeSpan.FromDays(7);
        var options = new QueueOptions { DefaultMessageTimeToLive = ttl };

        options.DefaultMessageTimeToLive.ShouldBe(ttl);
    }

    [Fact]
    public void AllowSettingLockDuration()
    {
        var lockDuration = TimeSpan.FromMinutes(5);
        var options = new QueueOptions { LockDuration = lockDuration };

        options.LockDuration.ShouldBe(lockDuration);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    [InlineData(null)]
    public void AllowSettingEnableDeduplication(bool? enable)
    {
        var options = new QueueOptions { EnableDeduplication = enable };

        options.EnableDeduplication.ShouldBe(enable);
    }

    [Fact]
    public void AllowSettingDuplicateDetectionWindow()
    {
        var window = TimeSpan.FromMinutes(10);
        var options = new QueueOptions { DuplicateDetectionWindow = window };

        options.DuplicateDetectionWindow.ShouldBe(window);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    [InlineData(null)]
    public void AllowSettingRequiresSession(bool? requires)
    {
        var options = new QueueOptions { RequiresSession = requires };

        options.RequiresSession.ShouldBe(requires);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    [InlineData(null)]
    public void AllowSettingDeadLetteringOnMessageExpiration(bool? enable)
    {
        var options = new QueueOptions { DeadLetteringOnMessageExpiration = enable };

        options.DeadLetteringOnMessageExpiration.ShouldBe(enable);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(10)]
    [InlineData(100)]
    [InlineData(null)]
    public void AllowSettingMaxDeliveryCount(int? maxCount)
    {
        var options = new QueueOptions { MaxDeliveryCount = maxCount };

        options.MaxDeliveryCount.ShouldBe(maxCount);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    [InlineData(null)]
    public void AllowSettingEnablePartitioning(bool? enable)
    {
        var options = new QueueOptions { EnablePartitioning = enable };

        options.EnablePartitioning.ShouldBe(enable);
    }

    [Fact]
    public void AllowSettingProperties()
    {
        var options = new QueueOptions();
        options.Properties["customKey"] = "customValue";
        options.Properties["numericKey"] = 42;

        options.Properties.Count.ShouldBe(2);
        options.Properties["customKey"].ShouldBe("customValue");
        options.Properties["numericKey"].ShouldBe(42);
    }

    [Fact]
    public void AllowAzureServiceBusStyleConfiguration()
    {
        var options = new QueueOptions
        {
            MaxSizeInMB = 5120, // Premium tier max
            DefaultMessageTimeToLive = TimeSpan.FromDays(14),
            LockDuration = TimeSpan.FromMinutes(5),
            EnableDeduplication = true,
            DuplicateDetectionWindow = TimeSpan.FromMinutes(10),
            RequiresSession = true,
            DeadLetteringOnMessageExpiration = true,
            MaxDeliveryCount = 10,
            EnablePartitioning = true
        };

        options.MaxSizeInMB.ShouldBe(5120);
        options.DefaultMessageTimeToLive.ShouldBe(TimeSpan.FromDays(14));
        options.LockDuration.ShouldBe(TimeSpan.FromMinutes(5));
        options.EnableDeduplication.ShouldBe(true);
        options.DuplicateDetectionWindow.ShouldBe(TimeSpan.FromMinutes(10));
        options.RequiresSession.ShouldBe(true);
        options.DeadLetteringOnMessageExpiration.ShouldBe(true);
        options.MaxDeliveryCount.ShouldBe(10);
        options.EnablePartitioning.ShouldBe(true);
    }

    [Fact]
    public void AllowAwsSqsStyleConfiguration()
    {
        var options = new QueueOptions
        {
            DefaultMessageTimeToLive = TimeSpan.FromDays(14), // retention period
            EnableDeduplication = true, // FIFO content-based deduplication
            DuplicateDetectionWindow = TimeSpan.FromMinutes(5), // FIFO deduplication scope
            MaxDeliveryCount = 3 // redrive policy
        };
        options.Properties["FifoQueue"] = true;
        options.Properties["ContentBasedDeduplication"] = true;

        options.DefaultMessageTimeToLive.ShouldBe(TimeSpan.FromDays(14));
        options.EnableDeduplication.ShouldBe(true);
        options.DuplicateDetectionWindow.ShouldBe(TimeSpan.FromMinutes(5));
        options.MaxDeliveryCount.ShouldBe(3);
        options.Properties["FifoQueue"].ShouldBe(true);
    }

    [Fact]
    public void AllowRabbitMqStyleConfiguration()
    {
        var options = new QueueOptions
        {
            DefaultMessageTimeToLive = TimeSpan.FromHours(24), // x-message-ttl
            MaxDeliveryCount = 5, // conceptual retry limit
            DeadLetteringOnMessageExpiration = true
        };
        options.Properties["x-queue-type"] = "quorum";
        options.Properties["x-max-length"] = 10000;
        options.Properties["x-overflow"] = "reject-publish";

        options.DefaultMessageTimeToLive.ShouldBe(TimeSpan.FromHours(24));
        options.MaxDeliveryCount.ShouldBe(5);
        options.DeadLetteringOnMessageExpiration.ShouldBe(true);
        options.Properties["x-queue-type"].ShouldBe("quorum");
        options.Properties["x-max-length"].ShouldBe(10000);
    }

    [Fact]
    public void AllowMinimalConfiguration()
    {
        var options = new QueueOptions
        {
            MaxDeliveryCount = 5,
            DeadLetteringOnMessageExpiration = true
        };

        options.MaxSizeInMB.ShouldBeNull();
        options.DefaultMessageTimeToLive.ShouldBeNull();
        options.LockDuration.ShouldBeNull();
        options.EnableDeduplication.ShouldBeNull();
        options.DuplicateDetectionWindow.ShouldBeNull();
        options.RequiresSession.ShouldBeNull();
        options.EnablePartitioning.ShouldBeNull();
        options.MaxDeliveryCount.ShouldBe(5);
        options.DeadLetteringOnMessageExpiration.ShouldBe(true);
    }
}
