// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Excalibur.AuditLogging;
using Excalibur.Compliance;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace Excalibur.AuditLogging.Tests;

[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class AuditContextMiddlewareShould
{
    private readonly FakeTimeProvider _timeProvider;
    private readonly IAuditActorProvider _fakeActorProvider;
    private readonly ILogger<AuditContextMiddleware> _logger;
    private readonly AuditContextMiddleware _sut;

    public AuditContextMiddlewareShould()
    {
        _timeProvider = new FakeTimeProvider(new DateTimeOffset(2026, 4, 11, 12, 0, 0, TimeSpan.Zero));
        _fakeActorProvider = A.Fake<IAuditActorProvider>();
        _logger = NullLogger<AuditContextMiddleware>.Instance;

        A.CallTo(() => _fakeActorProvider.GetCurrentActorIdAsync(A<CancellationToken>._))
            .Returns("actor-1");

        _sut = new AuditContextMiddleware(_timeProvider, _fakeActorProvider, _logger);
    }

    // ========================================
    // Constructor validation
    // ========================================

    [Fact]
    public void Throw_when_time_provider_is_null()
    {
        Should.Throw<ArgumentNullException>(() =>
            new AuditContextMiddleware(null!, _fakeActorProvider, _logger));
    }

    [Fact]
    public void Throw_when_logger_is_null()
    {
        Should.Throw<ArgumentNullException>(() =>
            new AuditContextMiddleware(_timeProvider, _fakeActorProvider, null!));
    }

    [Fact]
    public void Accept_null_actor_provider()
    {
        var middleware = new AuditContextMiddleware(_timeProvider, null, _logger);
        middleware.ShouldNotBeNull();
    }

    // ========================================
    // Stage
    // ========================================

    [Fact]
    public void Have_pre_processing_stage()
    {
        _sut.Stage.ShouldBe(DispatchMiddlewareStage.PreProcessing);
    }

    // ========================================
    // InvokeAsync: argument validation
    // ========================================

    [Fact]
    public async Task Throw_when_message_is_null()
    {
        var context = A.Fake<IMessageContext>();
        DispatchRequestDelegate next = (_, _, _) => ValueTask.FromResult(A.Fake<IMessageResult>());

        await Should.ThrowAsync<ArgumentNullException>(
            () => _sut.InvokeAsync(null!, context, next, CancellationToken.None).AsTask());
    }

    [Fact]
    public async Task Throw_when_context_is_null()
    {
        var message = A.Fake<IDispatchMessage>();
        DispatchRequestDelegate next = (_, _, _) => ValueTask.FromResult(A.Fake<IMessageResult>());

        await Should.ThrowAsync<ArgumentNullException>(
            () => _sut.InvokeAsync(message, null!, next, CancellationToken.None).AsTask());
    }

    [Fact]
    public async Task Throw_when_next_delegate_is_null()
    {
        var message = A.Fake<IDispatchMessage>();
        var context = A.Fake<IMessageContext>();

        await Should.ThrowAsync<ArgumentNullException>(
            () => _sut.InvokeAsync(message, context, null!, CancellationToken.None).AsTask());
    }

    // ========================================
    // InvokeAsync: pass-through when no IAuditContext
    // ========================================

    [Fact]
    public async Task Pass_through_when_no_audit_context_registered()
    {
        var message = A.Fake<IDispatchMessage>();
        var context = CreateFakeContext(auditContext: null);
        var expectedResult = A.Fake<IMessageResult>();
        var nextCalled = false;

        DispatchRequestDelegate next = (_, _, _) =>
        {
            nextCalled = true;
            return ValueTask.FromResult(expectedResult);
        };

        var result = await _sut.InvokeAsync(message, context, next, CancellationToken.None);

        nextCalled.ShouldBeTrue();
        result.ShouldBe(expectedResult);
    }

    [Fact]
    public async Task Pass_through_when_audit_context_is_not_default_implementation()
    {
        var message = A.Fake<IDispatchMessage>();
        var fakeAuditContext = A.Fake<IAuditContext>();
        var context = CreateFakeContext(auditContext: fakeAuditContext);
        var expectedResult = A.Fake<IMessageResult>();

        DispatchRequestDelegate next = (_, _, _) => ValueTask.FromResult(expectedResult);

        var result = await _sut.InvokeAsync(message, context, next, CancellationToken.None);

        result.ShouldBe(expectedResult);
    }

    // ========================================
    // InvokeAsync: populates DefaultAuditContext
    // ========================================

    [Fact]
    public async Task Populate_default_audit_context_with_pipeline_data()
    {
        var message = A.Fake<IDispatchMessage>();
        var auditLogger = A.Fake<IAuditLogger>();
        var defaultAuditContext = new DefaultAuditContext(
            auditLogger,
            _timeProvider,
            Microsoft.Extensions.Options.Options.Create(new AuditContextOptions()),
            NullLogger<DefaultAuditContext>.Instance);

        var context = CreateFakeContext(auditContext: defaultAuditContext, correlationId: "corr-123");
        var expectedResult = A.Fake<IMessageResult>();

        DispatchRequestDelegate next = (_, _, _) => ValueTask.FromResult(expectedResult);

        await _sut.InvokeAsync(message, context, next, CancellationToken.None);

        // Verify the context was initialized by asserting through it
        AuditEvent? captured = null;
        A.CallTo(() => auditLogger.LogAsync(A<AuditEvent>._, A<CancellationToken>._))
            .Invokes((AuditEvent e, CancellationToken _) => captured = e)
            .Returns(new AuditEventId
            {
                EventId = "test",
                EventHash = "hash",
                SequenceNumber = 1,
                RecordedAt = DateTimeOffset.UtcNow
            });

        await defaultAuditContext.AssertAsync(true, "test", AuditEventType.Compliance, CancellationToken.None);

        captured.ShouldNotBeNull();
        captured.CorrelationId.ShouldBe("corr-123");
        captured.ActorId.ShouldBe("actor-1");
    }

    // ========================================
    // InvokeAsync: actor resolution
    // ========================================

    [Fact]
    public async Task Default_actor_to_system_when_no_actor_provider()
    {
        var middlewareNoActor = new AuditContextMiddleware(_timeProvider, null, _logger);
        var message = A.Fake<IDispatchMessage>();
        var auditLogger = A.Fake<IAuditLogger>();
        var defaultAuditContext = new DefaultAuditContext(
            auditLogger,
            _timeProvider,
            Microsoft.Extensions.Options.Options.Create(new AuditContextOptions()),
            NullLogger<DefaultAuditContext>.Instance);

        var context = CreateFakeContext(auditContext: defaultAuditContext);
        DispatchRequestDelegate next = (_, _, _) => ValueTask.FromResult(A.Fake<IMessageResult>());

        await middlewareNoActor.InvokeAsync(message, context, next, CancellationToken.None);

        // Verify actor defaults to "system"
        AuditEvent? captured = null;
        A.CallTo(() => auditLogger.LogAsync(A<AuditEvent>._, A<CancellationToken>._))
            .Invokes((AuditEvent e, CancellationToken _) => captured = e)
            .Returns(new AuditEventId
            {
                EventId = "test",
                EventHash = "hash",
                SequenceNumber = 1,
                RecordedAt = DateTimeOffset.UtcNow
            });

        await defaultAuditContext.AssertAsync(true, "test", AuditEventType.Compliance, CancellationToken.None);

        captured.ShouldNotBeNull();
        captured.ActorId.ShouldBe("system");
    }

    [Fact]
    public async Task Default_actor_to_system_when_actor_provider_throws()
    {
        A.CallTo(() => _fakeActorProvider.GetCurrentActorIdAsync(A<CancellationToken>._))
            .Throws(new InvalidOperationException("Provider unavailable"));

        var message = A.Fake<IDispatchMessage>();
        var auditLogger = A.Fake<IAuditLogger>();
        var defaultAuditContext = new DefaultAuditContext(
            auditLogger,
            _timeProvider,
            Microsoft.Extensions.Options.Options.Create(new AuditContextOptions()),
            NullLogger<DefaultAuditContext>.Instance);

        var context = CreateFakeContext(auditContext: defaultAuditContext);
        DispatchRequestDelegate next = (_, _, _) => ValueTask.FromResult(A.Fake<IMessageResult>());

        await _sut.InvokeAsync(message, context, next, CancellationToken.None);

        // Verify actor defaults to "system" on provider failure
        AuditEvent? captured = null;
        A.CallTo(() => auditLogger.LogAsync(A<AuditEvent>._, A<CancellationToken>._))
            .Invokes((AuditEvent e, CancellationToken _) => captured = e)
            .Returns(new AuditEventId
            {
                EventId = "test",
                EventHash = "hash",
                SequenceNumber = 1,
                RecordedAt = DateTimeOffset.UtcNow
            });

        await defaultAuditContext.AssertAsync(true, "test", AuditEventType.Compliance, CancellationToken.None);

        captured.ShouldNotBeNull();
        captured.ActorId.ShouldBe("system");
    }

    // ========================================
    // InvokeAsync: always calls next delegate
    // ========================================

    [Fact]
    public async Task Always_invoke_next_delegate()
    {
        var message = A.Fake<IDispatchMessage>();
        var defaultAuditContext = new DefaultAuditContext(
            A.Fake<IAuditLogger>(),
            _timeProvider,
            Microsoft.Extensions.Options.Options.Create(new AuditContextOptions()),
            NullLogger<DefaultAuditContext>.Instance);

        var context = CreateFakeContext(auditContext: defaultAuditContext);
        var nextCalled = false;
        var expectedResult = A.Fake<IMessageResult>();

        DispatchRequestDelegate next = (_, _, _) =>
        {
            nextCalled = true;
            return ValueTask.FromResult(expectedResult);
        };

        var result = await _sut.InvokeAsync(message, context, next, CancellationToken.None);

        nextCalled.ShouldBeTrue();
        result.ShouldBe(expectedResult);
    }

    // ========================================
    // Helpers
    // ========================================

    private static IMessageContext CreateFakeContext(
        IAuditContext? auditContext = null,
        string? correlationId = null)
    {
        var context = A.Fake<IMessageContext>();
        var serviceProvider = A.Fake<IServiceProvider>();

        A.CallTo(() => context.RequestServices).Returns(serviceProvider);
        A.CallTo(() => context.CorrelationId).Returns(correlationId);
        A.CallTo(() => serviceProvider.GetService(typeof(IAuditContext))).Returns(auditContext);

        return context;
    }
}
