// Functional tests for InputValidationMiddleware — injection detection, size limits, custom validators, context validation

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Security;

namespace Excalibur.Dispatch.Security.Tests.Security.Functional;

[Trait("Category", "Unit")]
public sealed class InputValidationMiddlewareFunctionalShould
{
    private static InputValidationMiddleware CreateMiddleware(
        InputValidationOptions? opts = null,
        IEnumerable<IInputValidator>? validators = null,
        ISecurityEventLogger? eventLogger = null)
    {
        opts ??= new InputValidationOptions
        {
            EnableValidation = true,
            BlockSqlInjection = true,
            BlockNoSqlInjection = true,
            BlockCommandInjection = true,
            BlockPathTraversal = true,
            BlockLdapInjection = true,
            BlockHtmlContent = true,
            BlockControlCharacters = true,
            MaxStringLength = 10000,
            MaxMessageSizeBytes = 1_048_576,
            RequireCorrelationId = true,
        };

        eventLogger ??= A.Fake<ISecurityEventLogger>();

        return new InputValidationMiddleware(
            NullLogger<InputValidationMiddleware>.Instance,
            opts,
            validators?.ToList() ?? [],
            eventLogger);
    }

    private static (IMessageContext Context, DispatchRequestDelegate Next, IMessageResult SuccessResult) CreatePipelineFakes()
    {
        var context = A.Fake<IMessageContext>();
        var items = new Dictionary<string, object>
        {
            ["CorrelationId"] = Guid.NewGuid().ToString(),
            ["MessageId"] = Guid.NewGuid().ToString(),
        };
        A.CallTo(() => context.Items).Returns(items);
        A.CallTo(() => context.Properties).Returns(new Dictionary<string, object?>());
        A.CallTo(() => context.CorrelationId).Returns(Guid.NewGuid().ToString());
        A.CallTo(() => context.MessageId).Returns(Guid.NewGuid().ToString());
        A.CallTo(() => context.ReceivedTimestampUtc).Returns(DateTimeOffset.UtcNow);

        var successResult = A.Fake<IMessageResult>();
        A.CallTo(() => successResult.Succeeded).Returns(true);

        DispatchRequestDelegate next = (msg, ctx, ct) => new ValueTask<IMessageResult>(successResult);

        return (context, next, successResult);
    }

    [Fact]
    public async Task PassThroughWhenDisabled()
    {
        var middleware = CreateMiddleware(new InputValidationOptions { EnableValidation = false });
        var message = A.Fake<IDispatchMessage>();
        var (context, next, successResult) = CreatePipelineFakes();

        var result = await middleware.InvokeAsync(message, context, next, CancellationToken.None);

        result.ShouldBe(successResult);
    }

    [Fact]
    public async Task PassValidMessageThrough()
    {
        var middleware = CreateMiddleware();
        var message = new ValidTestMessage { Name = "Hello", Value = 42 };
        var (context, next, successResult) = CreatePipelineFakes();

        var result = await middleware.InvokeAsync(message, context, next, CancellationToken.None);

        result.Succeeded.ShouldBeTrue();
    }

    [Fact]
    public async Task ThrowOnNullMessage()
    {
        var middleware = CreateMiddleware();
        var (context, next, _) = CreatePipelineFakes();

        await Should.ThrowAsync<ArgumentNullException>(async () =>
            await middleware.InvokeAsync(null!, context, next, CancellationToken.None));
    }

    [Fact]
    public async Task ThrowOnNullContext()
    {
        var middleware = CreateMiddleware();

        await Should.ThrowAsync<ArgumentNullException>(async () =>
            await middleware.InvokeAsync(A.Fake<IDispatchMessage>(), null!, (msg, ctx, ct) => default, CancellationToken.None));
    }

    [Fact]
    public async Task ThrowOnNullNextDelegate()
    {
        var middleware = CreateMiddleware();
        var (context, _, _) = CreatePipelineFakes();

        await Should.ThrowAsync<ArgumentNullException>(async () =>
            await middleware.InvokeAsync(A.Fake<IDispatchMessage>(), context, null!, CancellationToken.None));
    }

    [Fact]
    public async Task RunCustomValidatorAndRejectOnFailure()
    {
        var validator = A.Fake<IInputValidator>();
        A.CallTo(() => validator.ValidateAsync(A<IDispatchMessage>._, A<IMessageContext>._))
            .Returns(Task.FromResult(InputValidationResult.Failure("Custom validation failed")));

        var middleware = CreateMiddleware(validators: [validator]);
        var message = new ValidTestMessage { Name = "Test", Value = 1 };
        var (context, next, _) = CreatePipelineFakes();

        var ex = await Should.ThrowAsync<InputValidationException>(async () =>
            await middleware.InvokeAsync(message, context, next, CancellationToken.None));

        ex.ValidationErrors.ShouldContain(e => e.Contains("Custom validation failed"));
    }

    [Fact]
    public async Task RunCustomValidatorAndPassOnSuccess()
    {
        var validator = A.Fake<IInputValidator>();
        A.CallTo(() => validator.ValidateAsync(A<IDispatchMessage>._, A<IMessageContext>._))
            .Returns(Task.FromResult(InputValidationResult.Success()));

        var middleware = CreateMiddleware(validators: [validator]);
        var message = new ValidTestMessage { Name = "Test", Value = 1 };
        var (context, next, successResult) = CreatePipelineFakes();

        var result = await middleware.InvokeAsync(message, context, next, CancellationToken.None);

        result.Succeeded.ShouldBeTrue();
    }

    [Fact]
    public void SetStageToValidation()
    {
        var middleware = CreateMiddleware();
        middleware.Stage.ShouldBe(DispatchMiddlewareStage.Validation);
    }

    [Fact]
    public async Task LogSecurityEventOnValidationFailure()
    {
        var eventLogger = A.Fake<ISecurityEventLogger>();
        var validator = A.Fake<IInputValidator>();
        A.CallTo(() => validator.ValidateAsync(A<IDispatchMessage>._, A<IMessageContext>._))
            .Returns(Task.FromResult(InputValidationResult.Failure("Bad data")));

        var middleware = CreateMiddleware(eventLogger: eventLogger, validators: [validator]);
        var message = new ValidTestMessage { Name = "Test", Value = 1 };
        var (context, next, _) = CreatePipelineFakes();

        await Should.ThrowAsync<InputValidationException>(async () =>
            await middleware.InvokeAsync(message, context, next, CancellationToken.None));

        A.CallTo(() => eventLogger.LogSecurityEventAsync(
            SecurityEventType.ValidationFailure,
            A<string>._,
            A<SecuritySeverity>._,
            A<CancellationToken>._,
            A<IMessageContext?>._)).MustHaveHappened();
    }

    // Simple test message type
    private sealed class ValidTestMessage : IDispatchMessage
    {
        public string Name { get; set; } = string.Empty;
        public int Value { get; set; }
    }
}
