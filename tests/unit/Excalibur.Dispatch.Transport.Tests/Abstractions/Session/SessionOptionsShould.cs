// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport;
using Shouldly;

namespace Excalibur.Dispatch.Transport.Tests.Abstractions.Session;

[Trait("Category", "Unit")]
[Trait("Component", "Transport.Abstractions")]
public class SessionOptionsShould
{
    [Fact]
    public void HaveCorrectDefaultValues()
    {
        var options = new SessionOptions();

        options.SessionTimeout.ShouldBe(TimeSpan.FromMinutes(5));
        options.LockTimeout.ShouldBe(TimeSpan.FromSeconds(30));
        options.AutoRenew.ShouldBeTrue();
        options.AutoRenewInterval.ShouldBe(TimeSpan.FromMinutes(1));
        options.MaxMessagesPerSession.ShouldBeNull();
        options.PreserveOrder.ShouldBeTrue();
        options.Metadata.ShouldNotBeNull();
        options.Metadata.ShouldBeEmpty();
    }

    [Fact]
    public void AllowSettingAllProperties()
    {
        var options = new SessionOptions
        {
            SessionTimeout = TimeSpan.FromMinutes(10),
            LockTimeout = TimeSpan.FromSeconds(60),
            AutoRenew = false,
            AutoRenewInterval = TimeSpan.FromMinutes(2),
            MaxMessagesPerSession = 1000,
            PreserveOrder = false
        };

        options.SessionTimeout.ShouldBe(TimeSpan.FromMinutes(10));
        options.LockTimeout.ShouldBe(TimeSpan.FromSeconds(60));
        options.AutoRenew.ShouldBeFalse();
        options.AutoRenewInterval.ShouldBe(TimeSpan.FromMinutes(2));
        options.MaxMessagesPerSession.ShouldBe(1000);
        options.PreserveOrder.ShouldBeFalse();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void AllowSettingAutoRenew(bool autoRenew)
    {
        var options = new SessionOptions { AutoRenew = autoRenew };

        options.AutoRenew.ShouldBe(autoRenew);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void AllowSettingPreserveOrder(bool preserveOrder)
    {
        var options = new SessionOptions { PreserveOrder = preserveOrder };

        options.PreserveOrder.ShouldBe(preserveOrder);
    }

    [Fact]
    public void AllowNullMaxMessagesPerSession()
    {
        var options = new SessionOptions { MaxMessagesPerSession = null };

        options.MaxMessagesPerSession.ShouldBeNull();
    }

    [Theory]
    [InlineData(1)]
    [InlineData(100)]
    [InlineData(10000)]
    public void AllowSettingMaxMessagesPerSession(int maxMessages)
    {
        var options = new SessionOptions { MaxMessagesPerSession = maxMessages };

        options.MaxMessagesPerSession.ShouldBe(maxMessages);
    }

    [Fact]
    public void AllowAddingMetadata()
    {
        var options = new SessionOptions();
        options.Metadata["key1"] = "value1";
        options.Metadata["key2"] = "value2";

        options.Metadata.Count.ShouldBe(2);
        options.Metadata["key1"].ShouldBe("value1");
        options.Metadata["key2"].ShouldBe("value2");
    }

    [Fact]
    public void AllowMetadataInitialization()
    {
        var options = new SessionOptions
        {
            Metadata =
            {
                ["session-type"] = "fifo",
                ["region"] = "us-east-1"
            }
        };

        options.Metadata.Count.ShouldBe(2);
        options.Metadata["session-type"].ShouldBe("fifo");
    }

    [Fact]
    public void AllowLongSessionTimeout()
    {
        var options = new SessionOptions
        {
            SessionTimeout = TimeSpan.FromHours(24)
        };

        options.SessionTimeout.TotalHours.ShouldBe(24);
    }

    [Fact]
    public void AllowShortLockTimeout()
    {
        var options = new SessionOptions
        {
            LockTimeout = TimeSpan.FromSeconds(5)
        };

        options.LockTimeout.TotalSeconds.ShouldBe(5);
    }

    [Fact]
    public void AllowAzureServiceBusStyleConfiguration()
    {
        var options = new SessionOptions
        {
            SessionTimeout = TimeSpan.FromMinutes(5),
            LockTimeout = TimeSpan.FromSeconds(30),
            AutoRenew = true,
            AutoRenewInterval = TimeSpan.FromSeconds(25), // Renew before lock expires
            MaxMessagesPerSession = 100,
            PreserveOrder = true
        };

        options.SessionTimeout.ShouldBe(TimeSpan.FromMinutes(5));
        options.AutoRenewInterval.ShouldBeLessThan(options.LockTimeout);
    }

    [Fact]
    public void AllowHighThroughputConfiguration()
    {
        var options = new SessionOptions
        {
            SessionTimeout = TimeSpan.FromMinutes(1),
            LockTimeout = TimeSpan.FromSeconds(10),
            AutoRenew = false, // Manual control
            MaxMessagesPerSession = 10000,
            PreserveOrder = false // Allow parallel processing
        };

        options.MaxMessagesPerSession.ShouldBe(10000);
        options.PreserveOrder.ShouldBeFalse();
    }

    [Fact]
    public void AllowStrictOrderingConfiguration()
    {
        var options = new SessionOptions
        {
            SessionTimeout = TimeSpan.FromMinutes(30),
            LockTimeout = TimeSpan.FromMinutes(2),
            AutoRenew = true,
            AutoRenewInterval = TimeSpan.FromMinutes(1),
            MaxMessagesPerSession = 1, // One message at a time
            PreserveOrder = true
        };

        options.MaxMessagesPerSession.ShouldBe(1);
        options.PreserveOrder.ShouldBeTrue();
    }
}
