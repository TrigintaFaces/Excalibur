// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport;
using Shouldly;

namespace Excalibur.Dispatch.Transport.Tests.Abstractions.DeadLetterQueue;

[Trait("Category", "Unit")]
[Trait("Component", "Transport.Abstractions")]
public class DeadLetterOptionsShould
{
    [Fact]
    public void HaveCorrectDefaultValues()
    {
        var options = new DeadLetterOptions();

        options.DeadLetterQueueName.ShouldBeNull();
        options.MaxDeliveryAttempts.ShouldBe(5);
        options.MessageRetentionPeriod.ShouldBe(TimeSpan.FromDays(14));
        options.EnableAutomaticDeadLettering.ShouldBeTrue();
        options.IncludeStackTrace.ShouldBeTrue();
        options.MaxQueueSizeInBytes.ShouldBe(1_073_741_824L);
        options.EnableMonitoring.ShouldBeTrue();
        options.MonitoringInterval.ShouldBe(TimeSpan.FromMinutes(5));
        options.AlertThresholds.ShouldNotBeNull();
    }

    [Theory]
    [InlineData("orders-dlq")]
    [InlineData("events-dead-letter")]
    [InlineData(null)]
    public void AllowSettingDeadLetterQueueName(string? queueName)
    {
        var options = new DeadLetterOptions { DeadLetterQueueName = queueName };

        options.DeadLetterQueueName.ShouldBe(queueName);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(5)]
    [InlineData(10)]
    public void AllowSettingMaxDeliveryAttempts(int attempts)
    {
        var options = new DeadLetterOptions { MaxDeliveryAttempts = attempts };

        options.MaxDeliveryAttempts.ShouldBe(attempts);
    }

    [Fact]
    public void AllowSettingMessageRetentionPeriod()
    {
        var retention = TimeSpan.FromDays(30);
        var options = new DeadLetterOptions { MessageRetentionPeriod = retention };

        options.MessageRetentionPeriod.ShouldBe(retention);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void AllowSettingEnableAutomaticDeadLettering(bool enable)
    {
        var options = new DeadLetterOptions { EnableAutomaticDeadLettering = enable };

        options.EnableAutomaticDeadLettering.ShouldBe(enable);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void AllowSettingIncludeStackTrace(bool include)
    {
        var options = new DeadLetterOptions { IncludeStackTrace = include };

        options.IncludeStackTrace.ShouldBe(include);
    }

    [Theory]
    [InlineData(1_073_741_824L)]
    [InlineData(5_368_709_120L)]
    [InlineData(10_737_418_240L)]
    public void AllowSettingMaxQueueSizeInBytes(long size)
    {
        var options = new DeadLetterOptions { MaxQueueSizeInBytes = size };

        options.MaxQueueSizeInBytes.ShouldBe(size);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void AllowSettingEnableMonitoring(bool enable)
    {
        var options = new DeadLetterOptions { EnableMonitoring = enable };

        options.EnableMonitoring.ShouldBe(enable);
    }

    [Fact]
    public void AllowSettingMonitoringInterval()
    {
        var interval = TimeSpan.FromMinutes(10);
        var options = new DeadLetterOptions { MonitoringInterval = interval };

        options.MonitoringInterval.ShouldBe(interval);
    }

    [Fact]
    public void AllowSettingAlertThresholds()
    {
        var thresholds = new DeadLetterAlertThresholds();
        var options = new DeadLetterOptions { AlertThresholds = thresholds };

        options.AlertThresholds.ShouldBe(thresholds);
    }

    [Fact]
    public void AllowProductionConfiguration()
    {
        var options = new DeadLetterOptions
        {
            DeadLetterQueueName = "prod-orders-dlq",
            MaxDeliveryAttempts = 3,
            MessageRetentionPeriod = TimeSpan.FromDays(30),
            EnableAutomaticDeadLettering = true,
            IncludeStackTrace = false,
            MaxQueueSizeInBytes = 5_368_709_120L,
            EnableMonitoring = true,
            MonitoringInterval = TimeSpan.FromMinutes(1)
        };

        options.DeadLetterQueueName.ShouldBe("prod-orders-dlq");
        options.MaxDeliveryAttempts.ShouldBe(3);
        options.MessageRetentionPeriod.ShouldBe(TimeSpan.FromDays(30));
        options.EnableAutomaticDeadLettering.ShouldBeTrue();
        options.IncludeStackTrace.ShouldBeFalse();
        options.MaxQueueSizeInBytes.ShouldBe(5_368_709_120L);
        options.EnableMonitoring.ShouldBeTrue();
        options.MonitoringInterval.ShouldBe(TimeSpan.FromMinutes(1));
    }

    [Fact]
    public void AllowDevelopmentConfiguration()
    {
        var options = new DeadLetterOptions
        {
            DeadLetterQueueName = "dev-dlq",
            MaxDeliveryAttempts = 1,
            MessageRetentionPeriod = TimeSpan.FromHours(1),
            EnableAutomaticDeadLettering = true,
            IncludeStackTrace = true,
            MaxQueueSizeInBytes = 104_857_600L,
            EnableMonitoring = false
        };

        options.DeadLetterQueueName.ShouldBe("dev-dlq");
        options.MaxDeliveryAttempts.ShouldBe(1);
        options.MessageRetentionPeriod.ShouldBe(TimeSpan.FromHours(1));
        options.IncludeStackTrace.ShouldBeTrue();
        options.MaxQueueSizeInBytes.ShouldBe(104_857_600L);
        options.EnableMonitoring.ShouldBeFalse();
    }
}
