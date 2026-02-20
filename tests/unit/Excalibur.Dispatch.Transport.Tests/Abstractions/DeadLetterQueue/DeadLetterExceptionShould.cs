// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport;
using Shouldly;

namespace Excalibur.Dispatch.Transport.Tests.Abstractions.DeadLetterQueue;

[Trait("Category", "Unit")]
[Trait("Component", "Transport.Abstractions")]
public class DeadLetterExceptionShould
{
    [Fact]
    public void HaveCorrectDefaultValues()
    {
        var exception = new DeadLetterException();

        exception.Message.ShouldBe(string.Empty);
        exception.ExceptionType.ShouldBe(string.Empty);
        exception.OriginalStackTrace.ShouldBeNull();
        exception.InnerException.ShouldBeNull();
    }

    [Fact]
    public void AllowCreationWithMessageOnly()
    {
        var exception = new DeadLetterException("Processing failed");

        exception.Message.ShouldBe("Processing failed");
        exception.ExceptionType.ShouldBe(string.Empty);
        exception.OriginalStackTrace.ShouldBeNull();
    }

    [Fact]
    public void AllowCreationWithNullMessage()
    {
        var exception = new DeadLetterException(null);

        exception.Message.ShouldBe(string.Empty);
        exception.ExceptionType.ShouldBe(string.Empty);
    }

    [Fact]
    public void AllowCreationWithMessageAndInnerException()
    {
        var innerException = new InvalidOperationException("Something went wrong");
        var exception = new DeadLetterException("Processing failed", innerException);

        exception.Message.ShouldBe("Processing failed");
        exception.ExceptionType.ShouldBe("System.InvalidOperationException");
        exception.OriginalStackTrace.ShouldBe(innerException.StackTrace);
    }

    [Fact]
    public void AllowCreationWithNullInnerException()
    {
        var exception = new DeadLetterException("Processing failed", null);

        exception.Message.ShouldBe("Processing failed");
        exception.ExceptionType.ShouldBe(string.Empty);
        exception.OriginalStackTrace.ShouldBeNull();
    }

    [Fact]
    public void AllowCreationWithAllParameters()
    {
        var stackTrace = "   at Namespace.Class.Method() in File.cs:line 42";
        var exception = new DeadLetterException("Processing failed", "System.ArgumentException", stackTrace);

        exception.Message.ShouldBe("Processing failed");
        exception.ExceptionType.ShouldBe("System.ArgumentException");
        exception.OriginalStackTrace.ShouldBe(stackTrace);
    }

    [Fact]
    public void ReturnOriginalStackTraceWhenSet()
    {
        var stackTrace = "   at Namespace.Class.Method() in File.cs:line 42";
        var exception = new DeadLetterException("Processing failed", "System.ArgumentException", stackTrace);

        exception.StackTrace.ShouldBe(stackTrace);
    }

    [Fact]
    public void ReturnBaseStackTraceWhenOriginalNotSet()
    {
        var exception = new DeadLetterException("Processing failed", "System.ArgumentException", null);

        // When OriginalStackTrace is null, StackTrace property returns base.StackTrace
        // which will be null for a newly created exception that hasn't been thrown
        exception.StackTrace.ShouldBeNull();
    }

    [Fact]
    public void PreserveInnerExceptionDetails()
    {
        Exception thrownException;
        try
        {
            throw new ArgumentException("Invalid argument value");
        }
        catch (Exception ex)
        {
            thrownException = ex;
        }

        var deadLetterEx = new DeadLetterException("Message processing failed", thrownException);

        deadLetterEx.ExceptionType.ShouldBe("System.ArgumentException");
        deadLetterEx.OriginalStackTrace.ShouldNotBeNull();
        // Stack trace contains the method name where the exception was thrown
        deadLetterEx.OriginalStackTrace.ShouldContain("PreserveInnerExceptionDetails");
    }

    [Fact]
    public void AllowTimeoutException()
    {
        var innerException = new TimeoutException("Operation timed out after 30 seconds");
        var exception = new DeadLetterException("Message timed out", innerException);

        exception.Message.ShouldBe("Message timed out");
        exception.ExceptionType.ShouldBe("System.TimeoutException");
    }

    [Fact]
    public void AllowAggregateException()
    {
        var innerExceptions = new Exception[]
        {
            new InvalidOperationException("Error 1"),
            new TimeoutException("Error 2")
        };
        var innerException = new AggregateException("Multiple errors", innerExceptions);
        var exception = new DeadLetterException("Batch processing failed", innerException);

        exception.Message.ShouldBe("Batch processing failed");
        exception.ExceptionType.ShouldBe("System.AggregateException");
    }

    [Fact]
    public void BeDerivedException()
    {
        var exception = new DeadLetterException("Test message");

        exception.ShouldBeAssignableTo<Exception>();
    }

    [Fact]
    public void AllowSerializationStyleConstruction()
    {
        // Simulating construction from serialized data
        var exceptionType = "Namespace.CustomException";
        var message = "Custom error occurred";
        var stackTrace = @"   at Namespace.Handler.Handle() in Handler.cs:line 25
   at Namespace.Pipeline.Execute() in Pipeline.cs:line 100";

        var exception = new DeadLetterException(message, exceptionType, stackTrace);

        exception.Message.ShouldBe(message);
        exception.ExceptionType.ShouldBe(exceptionType);
        exception.OriginalStackTrace.ShouldBe(stackTrace);
        exception.StackTrace.ShouldBe(stackTrace);
    }
}
