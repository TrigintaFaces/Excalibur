using Excalibur.Dispatch.Transport;

namespace Excalibur.Dispatch.Transport.Abstractions.Tests.SessionManagement;

[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
public sealed class SessionStatisticsShould
{
    [Fact]
    public void Have_default_values()
    {
        // Arrange & Act
        var sut = new SessionStatistics();

        // Assert
        sut.TotalSessions.ShouldBe(0);
        sut.ActiveSessions.ShouldBe(0);
        sut.IdleSessions.ShouldBe(0);
        sut.LockedSessions.ShouldBe(0);
        sut.TotalMessagesProcessed.ShouldBe(0);
        sut.AverageMessagesPerSession.ShouldBe(0);
        sut.AverageSessionDuration.ShouldBe(default);
        sut.GeneratedAt.ShouldBe(default);
    }

    [Fact]
    public void Allow_setting_all_properties()
    {
        // Arrange
        var now = DateTimeOffset.UtcNow;

        // Act
        var sut = new SessionStatistics
        {
            TotalSessions = 50,
            ActiveSessions = 10,
            IdleSessions = 30,
            LockedSessions = 5,
            TotalMessagesProcessed = 100000,
            AverageMessagesPerSession = 2000,
            AverageSessionDuration = TimeSpan.FromMinutes(15),
            GeneratedAt = now
        };

        // Assert
        sut.TotalSessions.ShouldBe(50);
        sut.ActiveSessions.ShouldBe(10);
        sut.IdleSessions.ShouldBe(30);
        sut.LockedSessions.ShouldBe(5);
        sut.TotalMessagesProcessed.ShouldBe(100000);
        sut.AverageMessagesPerSession.ShouldBe(2000);
        sut.AverageSessionDuration.ShouldBe(TimeSpan.FromMinutes(15));
        sut.GeneratedAt.ShouldBe(now);
    }
}
