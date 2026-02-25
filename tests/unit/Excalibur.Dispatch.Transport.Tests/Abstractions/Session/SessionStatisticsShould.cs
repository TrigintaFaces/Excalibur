// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport;

namespace Excalibur.Dispatch.Transport.Tests.Abstractions.Session;

/// <summary>
/// Unit tests for <see cref="SessionStatistics"/> class.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Transport.Abstractions")]
public sealed class SessionStatisticsShould
{
	[Fact]
	public void HaveZeroTotalSessions_ByDefault()
	{
		// Arrange & Act
		var stats = new SessionStatistics();

		// Assert
		stats.TotalSessions.ShouldBe(0);
	}

	[Fact]
	public void HaveZeroActiveSessions_ByDefault()
	{
		// Arrange & Act
		var stats = new SessionStatistics();

		// Assert
		stats.ActiveSessions.ShouldBe(0);
	}

	[Fact]
	public void HaveZeroIdleSessions_ByDefault()
	{
		// Arrange & Act
		var stats = new SessionStatistics();

		// Assert
		stats.IdleSessions.ShouldBe(0);
	}

	[Fact]
	public void HaveZeroLockedSessions_ByDefault()
	{
		// Arrange & Act
		var stats = new SessionStatistics();

		// Assert
		stats.LockedSessions.ShouldBe(0);
	}

	[Fact]
	public void HaveZeroTotalMessagesProcessed_ByDefault()
	{
		// Arrange & Act
		var stats = new SessionStatistics();

		// Assert
		stats.TotalMessagesProcessed.ShouldBe(0);
	}

	[Fact]
	public void HaveZeroAverageMessagesPerSession_ByDefault()
	{
		// Arrange & Act
		var stats = new SessionStatistics();

		// Assert
		stats.AverageMessagesPerSession.ShouldBe(0.0);
	}

	[Fact]
	public void HaveZeroAverageSessionDuration_ByDefault()
	{
		// Arrange & Act
		var stats = new SessionStatistics();

		// Assert
		stats.AverageSessionDuration.ShouldBe(TimeSpan.Zero);
	}

	[Fact]
	public void HaveDefaultGeneratedAt_ByDefault()
	{
		// Arrange & Act
		var stats = new SessionStatistics();

		// Assert
		stats.GeneratedAt.ShouldBe(default(DateTimeOffset));
	}

	[Fact]
	public void AllowSettingTotalSessions()
	{
		// Arrange
		var stats = new SessionStatistics();

		// Act
		stats.TotalSessions = 100;

		// Assert
		stats.TotalSessions.ShouldBe(100);
	}

	[Fact]
	public void AllowSettingActiveSessions()
	{
		// Arrange
		var stats = new SessionStatistics();

		// Act
		stats.ActiveSessions = 50;

		// Assert
		stats.ActiveSessions.ShouldBe(50);
	}

	[Fact]
	public void AllowSettingIdleSessions()
	{
		// Arrange
		var stats = new SessionStatistics();

		// Act
		stats.IdleSessions = 30;

		// Assert
		stats.IdleSessions.ShouldBe(30);
	}

	[Fact]
	public void AllowSettingLockedSessions()
	{
		// Arrange
		var stats = new SessionStatistics();

		// Act
		stats.LockedSessions = 20;

		// Assert
		stats.LockedSessions.ShouldBe(20);
	}

	[Fact]
	public void AllowSettingTotalMessagesProcessed()
	{
		// Arrange
		var stats = new SessionStatistics();

		// Act
		stats.TotalMessagesProcessed = 100000;

		// Assert
		stats.TotalMessagesProcessed.ShouldBe(100000);
	}

	[Fact]
	public void AllowSettingAverageMessagesPerSession()
	{
		// Arrange
		var stats = new SessionStatistics();

		// Act
		stats.AverageMessagesPerSession = 1000.5;

		// Assert
		stats.AverageMessagesPerSession.ShouldBe(1000.5);
	}

	[Fact]
	public void AllowSettingAverageSessionDuration()
	{
		// Arrange
		var stats = new SessionStatistics();

		// Act
		stats.AverageSessionDuration = TimeSpan.FromMinutes(15);

		// Assert
		stats.AverageSessionDuration.ShouldBe(TimeSpan.FromMinutes(15));
	}

	[Fact]
	public void AllowSettingGeneratedAt()
	{
		// Arrange
		var stats = new SessionStatistics();
		var generatedAt = DateTimeOffset.UtcNow;

		// Act
		stats.GeneratedAt = generatedAt;

		// Assert
		stats.GeneratedAt.ShouldBe(generatedAt);
	}

	[Fact]
	public void AllowCreatingFullyPopulatedStatistics()
	{
		// Arrange
		var generatedAt = DateTimeOffset.UtcNow;

		// Act
		var stats = new SessionStatistics
		{
			TotalSessions = 100,
			ActiveSessions = 50,
			IdleSessions = 30,
			LockedSessions = 20,
			TotalMessagesProcessed = 100000,
			AverageMessagesPerSession = 1000.0,
			AverageSessionDuration = TimeSpan.FromMinutes(15),
			GeneratedAt = generatedAt,
		};

		// Assert
		stats.TotalSessions.ShouldBe(100);
		stats.ActiveSessions.ShouldBe(50);
		stats.IdleSessions.ShouldBe(30);
		stats.LockedSessions.ShouldBe(20);
		stats.TotalMessagesProcessed.ShouldBe(100000);
		stats.AverageMessagesPerSession.ShouldBe(1000.0);
		stats.AverageSessionDuration.ShouldBe(TimeSpan.FromMinutes(15));
		stats.GeneratedAt.ShouldBe(generatedAt);
	}
}
