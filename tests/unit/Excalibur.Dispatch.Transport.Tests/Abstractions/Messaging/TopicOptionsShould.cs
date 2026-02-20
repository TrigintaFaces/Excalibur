// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport;
using Shouldly;

namespace Excalibur.Dispatch.Transport.Tests.Abstractions.Messaging;

[Trait("Category", "Unit")]
[Trait("Component", "Transport.Abstractions")]
public class TopicOptionsShould
{
    [Fact]
    public void HaveCorrectDefaultValues()
    {
        var options = new TopicOptions();

        options.MaxSizeInMB.ShouldBeNull();
        options.DefaultMessageTimeToLive.ShouldBeNull();
        options.EnableDeduplication.ShouldBeNull();
        options.DuplicateDetectionWindow.ShouldBeNull();
        options.RequiresDuplicateDetection.ShouldBeNull();
        options.SupportOrdering.ShouldBeNull();
        options.EnablePartitioning.ShouldBeNull();
        options.EnableBatchedOperations.ShouldBeNull();
        options.Status.ShouldBeNull();
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
        var options = new TopicOptions { MaxSizeInMB = maxSize };

        options.MaxSizeInMB.ShouldBe(maxSize);
    }

    [Fact]
    public void AllowSettingDefaultMessageTimeToLive()
    {
        var ttl = TimeSpan.FromDays(7);
        var options = new TopicOptions { DefaultMessageTimeToLive = ttl };

        options.DefaultMessageTimeToLive.ShouldBe(ttl);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    [InlineData(null)]
    public void AllowSettingEnableDeduplication(bool? enable)
    {
        var options = new TopicOptions { EnableDeduplication = enable };

        options.EnableDeduplication.ShouldBe(enable);
    }

    [Fact]
    public void AllowSettingDuplicateDetectionWindow()
    {
        var window = TimeSpan.FromMinutes(10);
        var options = new TopicOptions { DuplicateDetectionWindow = window };

        options.DuplicateDetectionWindow.ShouldBe(window);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    [InlineData(null)]
    public void AllowSettingRequiresDuplicateDetection(bool? requires)
    {
        var options = new TopicOptions { RequiresDuplicateDetection = requires };

        options.RequiresDuplicateDetection.ShouldBe(requires);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    [InlineData(null)]
    public void AllowSettingSupportOrdering(bool? support)
    {
        var options = new TopicOptions { SupportOrdering = support };

        options.SupportOrdering.ShouldBe(support);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    [InlineData(null)]
    public void AllowSettingEnablePartitioning(bool? enable)
    {
        var options = new TopicOptions { EnablePartitioning = enable };

        options.EnablePartitioning.ShouldBe(enable);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    [InlineData(null)]
    public void AllowSettingEnableBatchedOperations(bool? enable)
    {
        var options = new TopicOptions { EnableBatchedOperations = enable };

        options.EnableBatchedOperations.ShouldBe(enable);
    }

    [Theory]
    [InlineData(EntityStatus.Active)]
    [InlineData(EntityStatus.Disabled)]
    [InlineData(EntityStatus.ReceiveDisabled)]
    [InlineData(EntityStatus.SendDisabled)]
    [InlineData(null)]
    public void AllowSettingStatus(EntityStatus? status)
    {
        var options = new TopicOptions { Status = status };

        options.Status.ShouldBe(status);
    }

    [Fact]
    public void AllowSettingProperties()
    {
        var options = new TopicOptions();
        options.Properties["retentionMs"] = 604800000L; // 7 days
        options.Properties["partitions"] = 12;

        options.Properties.Count.ShouldBe(2);
        options.Properties["retentionMs"].ShouldBe(604800000L);
        options.Properties["partitions"].ShouldBe(12);
    }

    [Fact]
    public void AllowAzureServiceBusStyleConfiguration()
    {
        var options = new TopicOptions
        {
            MaxSizeInMB = 5120, // Premium tier max
            DefaultMessageTimeToLive = TimeSpan.FromDays(14),
            EnableDeduplication = true,
            DuplicateDetectionWindow = TimeSpan.FromMinutes(10),
            RequiresDuplicateDetection = true,
            SupportOrdering = true,
            EnablePartitioning = true,
            EnableBatchedOperations = true,
            Status = EntityStatus.Active
        };

        options.MaxSizeInMB.ShouldBe(5120);
        options.DefaultMessageTimeToLive.ShouldBe(TimeSpan.FromDays(14));
        options.EnableDeduplication.ShouldBe(true);
        options.DuplicateDetectionWindow.ShouldBe(TimeSpan.FromMinutes(10));
        options.RequiresDuplicateDetection.ShouldBe(true);
        options.SupportOrdering.ShouldBe(true);
        options.EnablePartitioning.ShouldBe(true);
        options.EnableBatchedOperations.ShouldBe(true);
        options.Status.ShouldBe(EntityStatus.Active);
    }

    [Fact]
    public void AllowKafkaStyleConfiguration()
    {
        var options = new TopicOptions
        {
            SupportOrdering = true, // key-based ordering
            EnablePartitioning = true
        };
        options.Properties["partitions"] = 12;
        options.Properties["replicationFactor"] = 3;
        options.Properties["retention.ms"] = 604800000L; // 7 days
        options.Properties["cleanup.policy"] = "delete";
        options.Properties["compression.type"] = "lz4";

        options.SupportOrdering.ShouldBe(true);
        options.EnablePartitioning.ShouldBe(true);
        options.Properties["partitions"].ShouldBe(12);
        options.Properties["replicationFactor"].ShouldBe(3);
        options.Properties["retention.ms"].ShouldBe(604800000L);
        options.Properties["cleanup.policy"].ShouldBe("delete");
    }

    [Fact]
    public void AllowAwsSnsStyleConfiguration()
    {
        var options = new TopicOptions
        {
            EnableDeduplication = true // FIFO topic
        };
        options.Properties["FifoTopic"] = true;
        options.Properties["ContentBasedDeduplication"] = true;
        options.Properties["DisplayName"] = "Order Events";
        options.Properties["KmsMasterKeyId"] = "alias/my-key";

        options.EnableDeduplication.ShouldBe(true);
        options.Properties["FifoTopic"].ShouldBe(true);
        options.Properties["ContentBasedDeduplication"].ShouldBe(true);
        options.Properties["DisplayName"].ShouldBe("Order Events");
    }

    [Fact]
    public void AllowGooglePubSubStyleConfiguration()
    {
        var options = new TopicOptions
        {
            DefaultMessageTimeToLive = TimeSpan.FromDays(7) // message retention
        };
        options.Properties["schemaSettings"] = new Dictionary<string, object>
        {
            ["schema"] = "projects/my-project/schemas/my-schema",
            ["encoding"] = "JSON"
        };
        options.Properties["messageStoragePolicy"] = "us-central1";

        options.DefaultMessageTimeToLive.ShouldBe(TimeSpan.FromDays(7));
        options.Properties.ContainsKey("schemaSettings").ShouldBeTrue();
    }

    [Fact]
    public void AllowDisabledTopicConfiguration()
    {
        var options = new TopicOptions
        {
            Status = EntityStatus.Disabled
        };

        options.Status.ShouldBe(EntityStatus.Disabled);
    }

    [Fact]
    public void AllowSendDisabledConfiguration()
    {
        var options = new TopicOptions
        {
            Status = EntityStatus.SendDisabled
        };

        options.Status.ShouldBe(EntityStatus.SendDisabled);
    }

    [Fact]
    public void AllowReceiveDisabledConfiguration()
    {
        var options = new TopicOptions
        {
            Status = EntityStatus.ReceiveDisabled
        };

        options.Status.ShouldBe(EntityStatus.ReceiveDisabled);
    }
}
