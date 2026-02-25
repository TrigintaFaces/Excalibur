// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport;
using Shouldly;

namespace Excalibur.Dispatch.Transport.Tests.Abstractions.Statistics;

public class SessionStatisticsShould
{
    [Fact]
    public void HaveCorrectDefaultValues()
    {
        var stats = new SessionStatistics();

        stats.TotalSessions.ShouldBe(0);
        stats.ActiveSessions.ShouldBe(0);
        stats.IdleSessions.ShouldBe(0);
        stats.LockedSessions.ShouldBe(0);
        stats.TotalMessagesProcessed.ShouldBe(0);
        stats.AverageMessagesPerSession.ShouldBe(0);
        stats.AverageSessionDuration.ShouldBe(TimeSpan.Zero);
        stats.GeneratedAt.ShouldBe(default);
    }

    [Fact]
    public void AllowSettingAllProperties()
    {
        var generatedAt = DateTimeOffset.UtcNow;
        var avgDuration = TimeSpan.FromMinutes(15);

        var stats = new SessionStatistics
        {
            TotalSessions = 100,
            ActiveSessions = 50,
            IdleSessions = 30,
            LockedSessions = 20,
            TotalMessagesProcessed = 500000,
            AverageMessagesPerSession = 5000.0,
            AverageSessionDuration = avgDuration,
            GeneratedAt = generatedAt
        };

        stats.TotalSessions.ShouldBe(100);
        stats.ActiveSessions.ShouldBe(50);
        stats.IdleSessions.ShouldBe(30);
        stats.LockedSessions.ShouldBe(20);
        stats.TotalMessagesProcessed.ShouldBe(500000);
        stats.AverageMessagesPerSession.ShouldBe(5000.0);
        stats.AverageSessionDuration.ShouldBe(avgDuration);
        stats.GeneratedAt.ShouldBe(generatedAt);
    }

    [Fact]
    public void AllowHighVolumeMetrics()
    {
        var stats = new SessionStatistics
        {
            TotalSessions = 10_000,
            ActiveSessions = 5_000,
            TotalMessagesProcessed = 10_000_000_000,
            AverageMessagesPerSession = 1_000_000.0
        };

        stats.TotalSessions.ShouldBe(10_000);
        stats.TotalMessagesProcessed.ShouldBe(10_000_000_000);
    }

    [Fact]
    public void AllowLongSessionDurations()
    {
        var stats = new SessionStatistics
        {
            AverageSessionDuration = TimeSpan.FromHours(24)
        };

        stats.AverageSessionDuration.TotalHours.ShouldBe(24);
    }

    [Fact]
    public void AllowShortSessionDurations()
    {
        var stats = new SessionStatistics
        {
            AverageSessionDuration = TimeSpan.FromMilliseconds(100)
        };

        stats.AverageSessionDuration.TotalMilliseconds.ShouldBe(100);
    }

    [Fact]
    public void AllowSessionStateBreakdown()
    {
        var stats = new SessionStatistics
        {
            TotalSessions = 100,
            ActiveSessions = 40,
            IdleSessions = 35,
            LockedSessions = 25
        };

        // Total should equal sum of states
        stats.ActiveSessions.ShouldBe(40);
        stats.IdleSessions.ShouldBe(35);
        stats.LockedSessions.ShouldBe(25);
        (stats.ActiveSessions + stats.IdleSessions + stats.LockedSessions).ShouldBe(stats.TotalSessions);
    }

    [Fact]
    public void AllowNoActiveSessions()
    {
        var stats = new SessionStatistics
        {
            TotalSessions = 50,
            ActiveSessions = 0,
            IdleSessions = 50,
            LockedSessions = 0
        };

        stats.ActiveSessions.ShouldBe(0);
        stats.IdleSessions.ShouldBe(50);
    }

    [Fact]
    public void AllowAllSessionsLocked()
    {
        var stats = new SessionStatistics
        {
            TotalSessions = 10,
            ActiveSessions = 0,
            IdleSessions = 0,
            LockedSessions = 10
        };

        stats.LockedSessions.ShouldBe(10);
    }

    [Fact]
    public void AllowFractionalAverageMessages()
    {
        var stats = new SessionStatistics
        {
            AverageMessagesPerSession = 2.5
        };

        stats.AverageMessagesPerSession.ShouldBe(2.5);
    }

    [Fact]
    public void TrackGeneratedAtTimestamp()
    {
        var before = DateTimeOffset.UtcNow;
        var stats = new SessionStatistics
        {
            GeneratedAt = DateTimeOffset.UtcNow
        };
        var after = DateTimeOffset.UtcNow;

        stats.GeneratedAt.ShouldBeGreaterThanOrEqualTo(before);
        stats.GeneratedAt.ShouldBeLessThanOrEqualTo(after);
    }

    [Fact]
    public void AllowAzureServiceBusStyleSessionMetrics()
    {
        var stats = new SessionStatistics
        {
            TotalSessions = 50,
            ActiveSessions = 20,
            IdleSessions = 25,
            LockedSessions = 5,
            TotalMessagesProcessed = 100000,
            AverageMessagesPerSession = 2000.0,
            AverageSessionDuration = TimeSpan.FromMinutes(10),
            GeneratedAt = DateTimeOffset.UtcNow
        };

        stats.TotalSessions.ShouldBe(50);
        stats.AverageSessionDuration.TotalMinutes.ShouldBe(10);
    }
}
