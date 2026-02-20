using Excalibur.Dispatch.Transport;

namespace Excalibur.Dispatch.Transport.Abstractions.Tests.Messaging;

public class SendErrorShould
{
    [Fact]
    public void Should_Default_Code_To_Empty()
    {
        var error = new SendError();

        error.Code.ShouldBe(string.Empty);
    }

    [Fact]
    public void Should_Default_Message_To_Empty()
    {
        var error = new SendError();

        error.Message.ShouldBe(string.Empty);
    }

    [Fact]
    public void Should_Default_IsRetryable_To_False()
    {
        var error = new SendError();

        error.IsRetryable.ShouldBeFalse();
    }

    [Fact]
    public void Should_Default_Exception_To_Null()
    {
        var error = new SendError();

        error.Exception.ShouldBeNull();
    }

    [Fact]
    public void FromException_Should_Create_Error_From_Exception()
    {
        var ex = new InvalidOperationException("something went wrong");

        var error = SendError.FromException(ex);

        error.Code.ShouldBe("InvalidOperationException");
        error.Message.ShouldBe("something went wrong");
        error.Exception.ShouldBe(ex);
        error.IsRetryable.ShouldBeFalse();
    }

    [Fact]
    public void FromException_Should_Set_IsRetryable_When_Specified()
    {
        var ex = new TimeoutException("timed out");

        var error = SendError.FromException(ex, isRetryable: true);

        error.IsRetryable.ShouldBeTrue();
    }

    [Fact]
    public void FromException_Should_Throw_On_Null_Exception()
    {
        Should.Throw<ArgumentNullException>(() => SendError.FromException(null!));
    }

    [Fact]
    public void Should_Allow_Init_Properties()
    {
        var ex = new InvalidOperationException("test");
        var error = new SendError
        {
            Code = "CONN_REFUSED",
            Message = "Connection refused",
            Exception = ex,
            IsRetryable = true,
        };

        error.Code.ShouldBe("CONN_REFUSED");
        error.Message.ShouldBe("Connection refused");
        error.Exception.ShouldBe(ex);
        error.IsRetryable.ShouldBeTrue();
    }
}
