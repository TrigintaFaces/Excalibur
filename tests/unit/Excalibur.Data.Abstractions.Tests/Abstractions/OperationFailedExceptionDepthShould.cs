namespace Excalibur.Data.Abstractions.Tests.Abstractions;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class OperationFailedExceptionDepthShould
{
    [Fact]
    public void Constructor_WithOperationAndResource_SetProperties()
    {
        // Act
        var ex = new OperationFailedException("Save", "Order");

        // Assert
        ex.Operation.ShouldBe("Save");
        ex.Resource.ShouldBe("Order");
        ex.StatusCode.ShouldBe(OperationFailedException.DefaultStatusCode);
        ex.Message.ShouldBe(OperationFailedException.DefaultMessage);
        ex.InnerException.ShouldBeNull();
    }

    [Fact]
    public void Constructor_WithCustomStatusCode_SetStatusCode()
    {
        // Act
        var ex = new OperationFailedException("Delete", "Product", statusCode: 409);

        // Assert
        ex.StatusCode.ShouldBe(409);
    }

    [Fact]
    public void Constructor_WithCustomMessage_SetMessage()
    {
        // Act
        var ex = new OperationFailedException("Update", "User", message: "Custom error");

        // Assert
        ex.Message.ShouldBe("Custom error");
    }

    [Fact]
    public void Constructor_WithInnerException_SetInnerException()
    {
        // Arrange
        var inner = new InvalidOperationException("inner");

        // Act
        var ex = new OperationFailedException("Create", "Item", innerException: inner);

        // Assert
        ex.InnerException.ShouldBe(inner);
    }

    [Fact]
    public void Constructor_WithAllParameters_SetCorrectly()
    {
        // Arrange
        var inner = new TimeoutException("timed out");

        // Act
        var ex = new OperationFailedException("Query", "Report", 504, "Gateway timeout", inner);

        // Assert
        ex.Operation.ShouldBe("Query");
        ex.Resource.ShouldBe("Report");
        ex.StatusCode.ShouldBe(504);
        ex.Message.ShouldBe("Gateway timeout");
        ex.InnerException.ShouldBe(inner);
    }

    [Fact]
    public void Constructor_ThrowOnNullOperation()
    {
        // Act & Assert
        Should.Throw<ArgumentNullException>(() =>
            new OperationFailedException(null!, "Resource"));
    }

    [Fact]
    public void Constructor_ThrowOnNullResource()
    {
        // Act & Assert
        Should.Throw<ArgumentNullException>(() =>
            new OperationFailedException(operation: "Op", resource: null!));
    }

    [Fact]
    public void DefaultStatusCode_Is500()
    {
        OperationFailedException.DefaultStatusCode.ShouldBe(500);
    }

    [Fact]
    public void DefaultMessage_IsOperationFailed()
    {
        OperationFailedException.DefaultMessage.ShouldBe("The operation failed.");
    }

    [Fact]
    public void AlternateConstructor_WorksWithResourceOnly()
    {
        // Act
        var ex = new OperationFailedException("resource", 404, "Not found");

        // Assert
        ex.Resource.ShouldBe("resource");
        ex.StatusCode.ShouldBe(404);
        ex.Message.ShouldBe("Not found");
    }

    [Fact]
    public void Operation_CanBeModified()
    {
        // Arrange
        var ex = new OperationFailedException("Save", "Order");

        // Act
        ex.Operation = "Update";

        // Assert
        ex.Operation.ShouldBe("Update");
    }
}
