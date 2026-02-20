using Excalibur.Data.Abstractions.Transactions;

namespace Excalibur.Data.Tests.Abstractions;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class DistributedTransactionOptionsShould
{
    [Fact]
    public void HaveCorrectDefaults()
    {
        // Arrange & Act
        var options = new DistributedTransactionOptions();

        // Assert
        options.Timeout.ShouldBe(TimeSpan.FromSeconds(30));
        options.IsolationLevel.ShouldBe(IsolationLevel.ReadCommitted);
        options.MaxParticipants.ShouldBe(10);
        options.AutoRollbackOnPrepareFailure.ShouldBeTrue();
    }

    [Fact]
    public void AllowSettingTimeout()
    {
        // Arrange
        var timeout = TimeSpan.FromMinutes(5);

        // Act
        var options = new DistributedTransactionOptions { Timeout = timeout };

        // Assert
        options.Timeout.ShouldBe(timeout);
    }

    [Fact]
    public void AllowSettingIsolationLevel()
    {
        // Arrange & Act
        var options = new DistributedTransactionOptions { IsolationLevel = IsolationLevel.Serializable };

        // Assert
        options.IsolationLevel.ShouldBe(IsolationLevel.Serializable);
    }

    [Fact]
    public void AllowSettingMaxParticipants()
    {
        // Arrange & Act
        var options = new DistributedTransactionOptions { MaxParticipants = 50 };

        // Assert
        options.MaxParticipants.ShouldBe(50);
    }

    [Fact]
    public void AllowDisablingAutoRollback()
    {
        // Arrange & Act
        var options = new DistributedTransactionOptions { AutoRollbackOnPrepareFailure = false };

        // Assert
        options.AutoRollbackOnPrepareFailure.ShouldBeFalse();
    }
}
