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
