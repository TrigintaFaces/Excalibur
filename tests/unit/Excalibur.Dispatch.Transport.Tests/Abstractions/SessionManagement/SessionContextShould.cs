// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport;

namespace Excalibur.Dispatch.Transport.Tests.Abstractions.SessionManagement;

/// <summary>
/// Unit tests for <see cref="SessionContext"/> class.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Transport.Abstractions")]
public sealed class SessionContextShould
{
	[Fact]
	public void HaveEmptySessionId_ByDefault()
	{
		// Arrange & Act
		var context = new SessionContext();

		// Assert
		context.SessionId.ShouldBe(string.Empty);
	}

	[Fact]
	public void HaveDefaultSessionInfo_ByDefault()
	{
		// Arrange & Act
		var context = new SessionContext();

		// Assert
		context.Info.ShouldNotBeNull();
	}

	[Fact]
	public void HaveDefaultSessionOptions_ByDefault()
	{
		// Arrange & Act
		var context = new SessionContext();

		// Assert
		context.Options.ShouldNotBeNull();
	}

	[Fact]
	public void HaveNullStateData_ByDefault()
	{
		// Arrange & Act
		var context = new SessionContext();

		// Assert
		context.StateData.ShouldBeNull();
	}

	[Fact]
	public void HaveNullOnCompletion_ByDefault()
	{
		// Arrange & Act
		var context = new SessionContext();

		// Assert
		context.OnCompletion.ShouldBeNull();
	}

	[Fact]
	public void AllowSettingSessionId()
	{
		// Arrange & Act
		var context = new SessionContext { SessionId = "test-session-123" };

		// Assert
		context.SessionId.ShouldBe("test-session-123");
	}

	[Fact]
	public void AllowSettingInfo()
	{
		// Arrange
		var info = new SessionInfo { SessionId = "info-session" };

		// Act
		var context = new SessionContext { Info = info };

		// Assert
		context.Info.ShouldBe(info);
	}

	[Fact]
	public void AllowSettingOptions()
	{
		// Arrange
		var options = new SessionOptions { LockTimeout = TimeSpan.FromMinutes(5) };

		// Act
		var context = new SessionContext { Options = options };

		// Assert
		context.Options.ShouldBe(options);
	}

	[Fact]
	public void AllowSettingStateData()
	{
		// Arrange
		var context = new SessionContext();
		var stateData = new { Key = "value" };

		// Act
		context.StateData = stateData;

		// Assert
		context.StateData.ShouldBe(stateData);
	}

	[Fact]
	public void AllowSettingOnCompletion()
	{
		// Arrange
		var context = new SessionContext();
		Func<SessionContext, Task> callback = _ => Task.CompletedTask;

		// Act
		context.OnCompletion = callback;

		// Assert
		context.OnCompletion.ShouldBe(callback);
	}

	[Fact]
	public void ImplementIDisposable()
	{
		// Assert
		typeof(IDisposable).IsAssignableFrom(typeof(SessionContext)).ShouldBeTrue();
	}

	[Fact]
	public void ImplementIAsyncDisposable()
	{
		// Assert
		typeof(IAsyncDisposable).IsAssignableFrom(typeof(SessionContext)).ShouldBeTrue();
	}

	[Fact]
	public void Dispose_NotThrowWithoutCallback()
	{
		// Arrange
		var context = new SessionContext();

		// Act & Assert - should not throw
		context.Dispose();
	}

	[Fact]
	public void Dispose_InvokeCompletionCallback()
	{
		// Arrange
		var callbackInvoked = false;
		var context = new SessionContext
		{
			OnCompletion = _ =>
			{
				callbackInvoked = true;
				return Task.CompletedTask;
			},
		};

		// Act
		context.Dispose();

		// Assert
		callbackInvoked.ShouldBeTrue();
	}

	[Fact]
	public void Dispose_NotInvokeCallbackTwice()
	{
		// Arrange
		var callbackCount = 0;
		var context = new SessionContext
		{
			OnCompletion = _ =>
			{
				callbackCount++;
				return Task.CompletedTask;
			},
		};

		// Act
		context.Dispose();
		context.Dispose();

		// Assert
		callbackCount.ShouldBe(1);
	}

	[Fact]
	public async Task DisposeAsync_NotThrowWithoutCallback()
	{
		// Arrange
		var context = new SessionContext();

		// Act & Assert - should not throw
		await context.DisposeAsync();
	}

	[Fact]
	public async Task DisposeAsync_InvokeCompletionCallback()
	{
		// Arrange
		var callbackInvoked = false;
		var context = new SessionContext
		{
			OnCompletion = _ =>
			{
				callbackInvoked = true;
				return Task.CompletedTask;
			},
		};

		// Act
		await context.DisposeAsync();

		// Assert
		callbackInvoked.ShouldBeTrue();
	}

	[Fact]
	public async Task DisposeAsync_NotInvokeCallbackTwice()
	{
		// Arrange
		var callbackCount = 0;
		var context = new SessionContext
		{
			OnCompletion = _ =>
			{
				callbackCount++;
				return Task.CompletedTask;
			},
		};

		// Act
		await context.DisposeAsync();
		await context.DisposeAsync();

		// Assert
		callbackCount.ShouldBe(1);
	}

	[Fact]
	public async Task DisposeAsync_PassContextToCallback()
	{
		// Arrange
		SessionContext? receivedContext = null;
		var context = new SessionContext
		{
			SessionId = "test-id",
			OnCompletion = ctx =>
			{
				receivedContext = ctx;
				return Task.CompletedTask;
			},
		};

		// Act
		await context.DisposeAsync();

		// Assert
		receivedContext.ShouldBe(context);
		receivedContext.SessionId.ShouldBe("test-id");
	}
}
