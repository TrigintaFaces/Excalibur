using Excalibur.Data.Abstractions.Transactions;

namespace Excalibur.Data.Tests.Abstractions;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class DistributedTransactionExceptionShould
{
    [Fact]
    public void CreateWithDefaultConstructor()
    {
        // Arrange & Act
        var exception = new DistributedTransactionException();

        // Assert
        exception.Message.ShouldNotBeNullOrEmpty();
        exception.InnerException.ShouldBeNull();
        exception.TransactionId.ShouldBeNull();
        exception.FailedParticipantIds.ShouldBeEmpty();
    }

    [Fact]
    public void CreateWithMessage()
    {
        // Arrange
        const string message = "Transaction failed";

        // Act
        var exception = new DistributedTransactionException(message);

        // Assert
        exception.Message.ShouldBe(message);
    }

    [Fact]
    public void CreateWithMessageAndInnerException()
    {
        // Arrange
        const string message = "Transaction failed";
        var inner = new TimeoutException("Timed out");

        // Act
        var exception = new DistributedTransactionException(message, inner);

        // Assert
        exception.Message.ShouldBe(message);
        exception.InnerException.ShouldBeSameAs(inner);
    }

    [Fact]
    public void SupportTransactionIdProperty()
    {
        // Arrange & Act
        var exception = new DistributedTransactionException("Error") { TransactionId = "txn-123" };

        // Assert
        exception.TransactionId.ShouldBe("txn-123");
    }

    [Fact]
    public void SupportFailedParticipantIdsProperty()
    {
        // Arrange
        var failedIds = new List<string> { "participant-1", "participant-2" };

        // Act
        var exception = new DistributedTransactionException("Error") { FailedParticipantIds = failedIds };

        // Assert
        exception.FailedParticipantIds.Count.ShouldBe(2);
        exception.FailedParticipantIds.ShouldContain("participant-1");
        exception.FailedParticipantIds.ShouldContain("participant-2");
    }

    [Fact]
    public void BeAssignableToException()
    {
        // Arrange & Act
        var exception = new DistributedTransactionException();

        // Assert
        exception.ShouldBeAssignableTo<Exception>();
    }

    [Fact]
    public void DefaultFailedParticipantIdsToEmptyList()
    {
        // Arrange & Act
        var exception = new DistributedTransactionException("Error");

        // Assert
        exception.FailedParticipantIds.ShouldNotBeNull();
        exception.FailedParticipantIds.ShouldBeEmpty();
    }
}
