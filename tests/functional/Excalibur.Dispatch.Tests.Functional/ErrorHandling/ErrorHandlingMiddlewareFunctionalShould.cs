// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Options;

using Tests.Shared.Helpers;

// Disambiguate the conflicting types - use Shared implementations that match test expectations
using ErrorHandlingMiddleware = Tests.Shared.TestTypes.ErrorHandlingMiddleware;
using ErrorHandlingOptions = Tests.Shared.TestTypes.ErrorHandlingOptions;

namespace Excalibur.Dispatch.Tests.Functional.ErrorHandling;

/// <summary>
///     Functional tests for Excalibur.Core.Messaging.ErrorHandling.ErrorHandlingMiddleware in realistic scenarios.
/// </summary>
[Trait("Category", "Functional")]
public sealed class ErrorHandlingMiddlewareFunctionalShould : IDisposable
{
	private readonly IHost _host;
	private readonly IServiceProvider _serviceProvider;

	public ErrorHandlingMiddlewareFunctionalShould()
	{
		var builder = Host.CreateDefaultBuilder();

		_ = builder.ConfigureServices(static services =>
		{
			// Configure dispatch messaging
			_ = services.AddLogging(static logging =>
			{
				_ = logging.ClearProviders();
				_ = logging.AddDebug();
				_ = logging.SetMinimumLevel(LogLevel.Debug);
			});

			// Configure error handling
			_ = services.Configure<ErrorHandlingOptions>(static options =>
			{
				options.Enabled = true;
				options.ThrowExceptions = false;
				// ExceptionHandlers is init-only, already has default value
			});

			// Register middleware
			_ = services.AddSingleton<ErrorHandlingMiddleware>();
			_ = services.AddSingleton<IMessageContextAccessor, MessageContextAccessor>();

			// Register test handlers
			_ = services.AddTransient<SuccessfulMessageHandler>();
			_ = services.AddTransient<FailingMessageHandler>();
			_ = services.AddTransient<SlowMessageHandler>();
		});

		_host = builder.Build();
		_serviceProvider = _host.Services;
	}

	[Fact]
	public async Task HandleSuccessfulMessageProcessing()
	{
		// Arrange
		var errorHandling = _serviceProvider.GetRequiredService<ErrorHandlingMiddleware>();
		var handler = _serviceProvider.GetRequiredService<SuccessfulMessageHandler>();
		var message = new TestMessage { MessageId = Guid.NewGuid().ToString(), Content = "Test message" };
		var context = new MessageContext(message, _serviceProvider);

		static async Task<IMessageResult> Pipeline(IDispatchMessage msg, IMessageContext ctx, CancellationToken token)
		{
			return await SuccessfulMessageHandler.HandleAsync((TestMessage)msg, ctx, token).ConfigureAwait(true);
		}

		// Act
		var result = await errorHandling.InvokeAsync(message, context, Pipeline, CancellationToken.None).ConfigureAwait(true);

		// Assert
		result.Succeeded.ShouldBeTrue();
		result.ProblemDetails.ShouldBeNull();
		context.Items.ShouldNotContainKey("__Error");
		context.Items.ShouldNotContainKey("__Problem");
	}

	[Fact]
	public async Task HandleFailingMessageProcessing()
	{
		// Arrange
		var errorHandling = _serviceProvider.GetRequiredService<ErrorHandlingMiddleware>();
		var handler = _serviceProvider.GetRequiredService<FailingMessageHandler>();
		var message = new TestMessage { MessageId = Guid.NewGuid().ToString(), Content = "This will fail" };
		var context = new MessageContext(message, _serviceProvider)
		{
			MessageId = "fail-123",
			TraceParent = "00-0af7651916cd43dd8448eb211c80319c-b7ad6b7169203331-01",
		};

		static async Task<IMessageResult> Pipeline(IDispatchMessage msg, IMessageContext ctx, CancellationToken token)
		{
			return await FailingMessageHandler.HandleAsync((TestMessage)msg, ctx, token).ConfigureAwait(true);
		}

		// Act
		var result = await errorHandling.InvokeAsync(message, context, Pipeline, CancellationToken.None).ConfigureAwait(true);

		// Assert
		result.Succeeded.ShouldBeFalse();
		_ = result.ProblemDetails.ShouldNotBeNull();
		result.ProblemDetails.Title.ShouldBe("Unhandled dispatch exception");
		result.ProblemDetails.Detail.ShouldContain("Processing failed");
		result.ProblemDetails.Instance.ShouldContain(context.TraceParent);

		_ = context.Items["__Error"].ShouldBeOfType<InvalidOperationException>();
		_ = context.Items["__Problem"].ShouldBeOfType<MessageProblemDetails>();
		_ = context.Items["__ErrorId"].ShouldBeOfType<string>();
	}

	[Fact]
	public async Task HandleTimeoutScenario()
	{
		// Arrange
		var errorHandling = _serviceProvider.GetRequiredService<ErrorHandlingMiddleware>();
		var handler = _serviceProvider.GetRequiredService<SlowMessageHandler>();
		var message = new TestMessage { MessageId = Guid.NewGuid().ToString(), Content = "Slow message" };
		var context = new MessageContext(message, _serviceProvider);

		using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

		static async Task<IMessageResult> Pipeline(IDispatchMessage msg, IMessageContext ctx, CancellationToken token)
		{
			return await SlowMessageHandler.HandleAsync((TestMessage)msg, ctx, token).ConfigureAwait(true);
		}

		// Act
		var result = await errorHandling.InvokeAsync(message, context, Pipeline, cts.Token).ConfigureAwait(true);

		// Assert
		result.Succeeded.ShouldBeFalse();
		_ = result.ProblemDetails.ShouldNotBeNull();

		var error = context.Items["__Error"] as Exception;
		_ = error.ShouldNotBeNull();
		_ = error.ShouldBeAssignableTo<OperationCanceledException>();
	}

	[Fact]
	public async Task LogErrorWithFullContext()
#pragma warning restore CA1506
	{
		// Arrange
		using var loggerProvider = new CapturingLoggerProvider();
		var services = new ServiceCollection();

		_ = services.AddLogging(builder =>
		{
			_ = builder.ClearProviders();
			_ = builder.AddProvider(loggerProvider);
			_ = builder.SetMinimumLevel(LogLevel.Debug);
		});

		_ = services.Configure<ErrorHandlingOptions>(options =>
		{
			options.Enabled = true;
			options.ThrowExceptions = false;
			// ExceptionHandlers is init-only, already has default value
		});

		var provider = services.BuildServiceProvider();
		var errorHandling = new ErrorHandlingMiddleware(
			provider.GetRequiredService<IOptions<ErrorHandlingOptions>>(),
			provider.GetRequiredService<ILogger<ErrorHandlingMiddleware>>());

		var message = new TestMessage { MessageId = Guid.NewGuid().ToString(), Content = "Will fail for logging" };
		var context = new MessageContext(message, provider)
		{
			MessageId = "log-test-123",
			TraceParent = "00-trace-456",
			UserId = "user-789",
			Source = "TestSystem",
		};
		var expectedException = new InvalidOperationException("Test application error");

		Task<IMessageResult> Pipeline(IDispatchMessage msg, IMessageContext ctx, CancellationToken token)
		{
			throw expectedException;
		}

		// Act
		_ = await errorHandling.InvokeAsync(message, context, Pipeline, CancellationToken.None).ConfigureAwait(true);

		// Assert
		var loggedMessages = loggerProvider.Entries;
		loggedMessages.ShouldNotBeEmpty();

		var errorLog = loggedMessages.FirstOrDefault(m => m.Level == LogLevel.Error);
		_ = errorLog.ShouldNotBeNull();
		errorLog.Message.ShouldContain("log-test-123");
		errorLog.Message.ShouldContain("00-trace-456");
		errorLog.Exception.ShouldBe(expectedException);
	}

	/// <inheritdoc/>
	public void Dispose() => _host?.Dispose();
}

