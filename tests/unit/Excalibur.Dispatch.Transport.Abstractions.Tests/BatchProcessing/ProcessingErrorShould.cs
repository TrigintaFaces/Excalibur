using Excalibur.Dispatch.Transport;

namespace Excalibur.Dispatch.Transport.Abstractions.Tests.BatchProcessing;

public class ProcessingErrorShould
{
    [Fact]
    public void Should_Default_Code_To_Empty()
    {
        var error = new ProcessingError();

        error.Code.ShouldBe(string.Empty);
    }

    [Fact]
    public void Should_Default_Message_To_Empty()
    {
        var error = new ProcessingError();

        error.Message.ShouldBe(string.Empty);
    }

    [Fact]
    public void Should_Default_Severity_To_Info()
    {
        var error = new ProcessingError();

        error.Severity.ShouldBe(ErrorSeverity.Info);
    }

    [Fact]
    public void Should_Set_OccurredAt_To_Now()
    {
        var before = DateTimeOffset.UtcNow;
        var error = new ProcessingError();
        var after = DateTimeOffset.UtcNow;

        error.OccurredAt.ShouldBeGreaterThanOrEqualTo(before);
        error.OccurredAt.ShouldBeLessThanOrEqualTo(after);
    }

    [Fact]
    public void Should_Default_MessageId_To_Null()
    {
        var error = new ProcessingError();

        error.MessageId.ShouldBeNull();
    }

    [Fact]
    public void Should_Default_Exception_To_Null()
    {
        var error = new ProcessingError();

        error.Exception.ShouldBeNull();
    }

    [Fact]
    public void Should_Allow_Setting_All_Properties()
    {
        var ex = new InvalidOperationException("fail");
        var error = new ProcessingError
        {
            Code = "ERR_001",
            Message = "Processing failed",
            Severity = ErrorSeverity.Critical,
            MessageId = "msg-42",
            Exception = ex,
        };

        error.Code.ShouldBe("ERR_001");
        error.Message.ShouldBe("Processing failed");
        error.Severity.ShouldBe(ErrorSeverity.Critical);
        error.MessageId.ShouldBe("msg-42");
        error.Exception.ShouldBe(ex);
    }
}
