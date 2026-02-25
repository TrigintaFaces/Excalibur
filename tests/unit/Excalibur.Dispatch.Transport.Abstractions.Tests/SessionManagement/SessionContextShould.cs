using Excalibur.Dispatch.Transport;

namespace Excalibur.Dispatch.Transport.Abstractions.Tests.SessionManagement;

[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
public sealed class SessionContextShould
{
    [Fact]
    public void Have_default_values()
    {
        // Arrange & Act
        var sut = new SessionContext();

        // Assert
        sut.SessionId.ShouldBe(string.Empty);
        sut.Info.ShouldNotBeNull();
        sut.Options.ShouldNotBeNull();
        sut.StateData.ShouldBeNull();
        sut.OnCompletion.ShouldBeNull();
    }

    [Fact]
    public void Allow_setting_session_id()
    {
        // Arrange & Act
        var sut = new SessionContext { SessionId = "test-session-123" };

        // Assert
        sut.SessionId.ShouldBe("test-session-123");
    }

    [Fact]
    public void Allow_setting_state_data()
    {
        // Arrange
        var sut = new SessionContext();
        var stateData = new { Counter = 42 };

        // Act
        sut.StateData = stateData;

        // Assert
        sut.StateData.ShouldBe(stateData);
    }

    [Fact]
    public void Allow_setting_info_via_init()
    {
        // Arrange
        var info = new SessionInfo
        {
            SessionId = "s1",
            State = DispatchSessionState.Active,
            MessageCount = 10
        };

        // Act
        var sut = new SessionContext { Info = info };

        // Assert
        sut.Info.SessionId.ShouldBe("s1");
        sut.Info.State.ShouldBe(DispatchSessionState.Active);
        sut.Info.MessageCount.ShouldBe(10);
    }

    [Fact]
    public void Allow_setting_options_via_init()
    {
        // Arrange
        var options = new SessionOptions
        {
            SessionTimeout = TimeSpan.FromMinutes(10),
            AutoRenew = false
        };

        // Act
        var sut = new SessionContext { Options = options };

        // Assert
        sut.Options.SessionTimeout.ShouldBe(TimeSpan.FromMinutes(10));
        sut.Options.AutoRenew.ShouldBeFalse();
    }

    [Fact]
    public void Dispose_synchronously_without_callback()
    {
        // Arrange
        var sut = new SessionContext();

        // Act & Assert - should not throw
        sut.Dispose();
    }

    [Fact]
    public void Dispose_synchronously_with_callback()
    {
        // Arrange
        var callbackInvoked = false;
        var sut = new SessionContext
        {
            OnCompletion = _ =>
            {
                callbackInvoked = true;
                return Task.CompletedTask;
            }
        };

        // Act
        sut.Dispose();

        // Assert
        callbackInvoked.ShouldBeTrue();
    }

    [Fact]
    public async Task Dispose_asynchronously_without_callback()
    {
        // Arrange
        var sut = new SessionContext();

        // Act & Assert - should not throw
        await sut.DisposeAsync().ConfigureAwait(false);
    }

    [Fact]
    public async Task Dispose_asynchronously_with_callback()
    {
        // Arrange
        var callbackInvoked = false;
        var sut = new SessionContext
        {
            OnCompletion = _ =>
            {
                callbackInvoked = true;
                return Task.CompletedTask;
            }
        };

        // Act
        await sut.DisposeAsync().ConfigureAwait(false);

        // Assert
        callbackInvoked.ShouldBeTrue();
    }

    [Fact]
    public async Task Invoke_callback_only_once_on_double_async_dispose()
    {
        // Arrange
        var invocationCount = 0;
        var sut = new SessionContext
        {
            OnCompletion = _ =>
            {
                invocationCount++;
                return Task.CompletedTask;
            }
        };

        // Act
        await sut.DisposeAsync().ConfigureAwait(false);
        await sut.DisposeAsync().ConfigureAwait(false);

        // Assert
        invocationCount.ShouldBe(1);
    }

    [Fact]
    public async Task Dispose_synchronously_does_not_wait_for_async_callback_completion()
    {
        // Arrange
        var callbackStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var allowCallbackCompletion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var callbackCompleted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var sut = new SessionContext
        {
            OnCompletion = async _ =>
            {
                callbackStarted.TrySetResult();
                await allowCallbackCompletion.Task;
                callbackCompleted.TrySetResult();
            }
        };

        // Act
        sut.Dispose();

        // Assert
        var callbackStartedObserved = await global::Tests.Shared.Infrastructure.WaitHelpers.WaitUntilAsync(
            () => callbackStarted.Task.IsCompleted,
            TimeSpan.FromSeconds(10),
            TimeSpan.FromMilliseconds(20));
        callbackStartedObserved.ShouldBeTrue("callback should start when disposing synchronously");
        callbackCompleted.Task.IsCompleted.ShouldBeFalse();

        allowCallbackCompletion.TrySetResult();
        var callbackCompletedObserved = await global::Tests.Shared.Infrastructure.WaitHelpers.WaitUntilAsync(
            () => callbackCompleted.Task.IsCompleted,
            TimeSpan.FromSeconds(10),
            TimeSpan.FromMilliseconds(20));
        callbackCompletedObserved.ShouldBeTrue("callback should complete after being released");
    }

    [Fact]
    public async Task Dispose_async_waits_for_async_callback_completion()
    {
        // Arrange
        var callbackStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var allowCallbackCompletion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var sut = new SessionContext
        {
            OnCompletion = async _ =>
            {
                callbackStarted.TrySetResult();
                await allowCallbackCompletion.Task;
            }
        };

        // Act
        var disposeTask = sut.DisposeAsync().AsTask();
        var callbackStartedObserved = await global::Tests.Shared.Infrastructure.WaitHelpers.WaitUntilAsync(
            () => callbackStarted.Task.IsCompleted,
            TimeSpan.FromSeconds(10),
            TimeSpan.FromMilliseconds(20));
        callbackStartedObserved.ShouldBeTrue("callback should start before dispose async completes");

        // Assert
        disposeTask.IsCompleted.ShouldBeFalse();
        allowCallbackCompletion.TrySetResult();
        await disposeTask;
    }

    [Fact]
    public async Task Dispose_then_DisposeAsync_invokes_callback_only_once()
    {
        // Arrange
        var callbackInvocations = 0;
        var allowCallbackCompletion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var callbackStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var sut = new SessionContext
        {
            OnCompletion = async _ =>
            {
                callbackInvocations++;
                callbackStarted.TrySetResult();
                await allowCallbackCompletion.Task;
            }
        };

        // Act
        sut.Dispose();
        var callbackStartedObserved = await global::Tests.Shared.Infrastructure.WaitHelpers.WaitUntilAsync(
            () => callbackStarted.Task.IsCompleted,
            TimeSpan.FromSeconds(10),
            TimeSpan.FromMilliseconds(20));
        callbackStartedObserved.ShouldBeTrue("callback should start on synchronous dispose");
        await sut.DisposeAsync();

        // Assert
        callbackInvocations.ShouldBe(1);

        allowCallbackCompletion.TrySetResult();
    }

    [Fact]
    public void Dispose_propagates_synchronous_callback_exception()
    {
        // Arrange
        var sut = new SessionContext
        {
            OnCompletion = _ => throw new InvalidOperationException("sync callback failure")
        };

        // Act & Assert
        var ex = Should.Throw<InvalidOperationException>(() => sut.Dispose());
        ex.Message.ShouldBe("sync callback failure");
    }

    [Fact]
    public void Pass_self_to_completion_callback()
    {
        // Arrange
        SessionContext? capturedContext = null;
        var sut = new SessionContext
        {
            SessionId = "callback-test",
            OnCompletion = ctx =>
            {
                capturedContext = ctx;
                return Task.CompletedTask;
            }
        };

        // Act
        sut.Dispose();

        // Assert
        capturedContext.ShouldNotBeNull();
        capturedContext!.SessionId.ShouldBe("callback-test");
    }
}
