// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

#pragma warning disable CA2012 // Use ValueTasks correctly - acceptable in tests

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Delivery;
using Excalibur.Dispatch.Messaging;
using Excalibur.Dispatch.Middleware.ParallelExecution;
using Excalibur.Dispatch.Tests.TestFakes;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using MessageResult = Excalibur.Dispatch.Abstractions.MessageResult;

namespace Excalibur.Dispatch.Tests.Messaging.Middleware;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class ParallelEventHandlerMiddlewareShould
{
	[Fact]
	public async Task PassThroughWhenDisabled()
	{
		// Arrange
		var sut = CreateSut(new ParallelEventHandlerOptions { Enabled = false });
		var context = new MessageContext();
		var message = new FakeDispatchMessage();
		var expected = MessageResult.Success();

		// Act
		var result = await sut.InvokeAsync(
			message,
			context,
			(_, _, _) => new ValueTask<IMessageResult>(expected),
			CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.ShouldBe(expected);
	}

	[Fact]
	public async Task PassThroughWhenNoParallelHandlersRegistered()
	{
		// Arrange
		var sut = CreateSut(new ParallelEventHandlerOptions { Enabled = true });
		var context = new MessageContext();
		var message = new FakeDispatchMessage();
		var expected = MessageResult.Success();

		// Act
		var result = await sut.InvokeAsync(
			message,
			context,
			(_, _, _) => new ValueTask<IMessageResult>(expected),
			CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.ShouldBe(expected);
	}

	[Fact]
	public async Task PassThroughWhenSingleParallelHandlerRegistered()
	{
		// Arrange
		var sut = CreateSut(new ParallelEventHandlerOptions { Enabled = true });
		var context = new MessageContext();
		var message = new FakeDispatchMessage();
		var expected = MessageResult.Success();
		context.SetItem<IReadOnlyList<DispatchRequestDelegate>>(
			"ParallelHandlers",
			[new DispatchRequestDelegate((_, _, _) => new ValueTask<IMessageResult>(MessageResult.Success()))]);

		// Act
		var result = await sut.InvokeAsync(
			message,
			context,
			(_, _, _) => new ValueTask<IMessageResult>(expected),
			CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.ShouldBe(expected);
	}

	[Fact]
	public async Task ExecuteAllHandlersAndReturnSuccessInWaitAllMode()
	{
		// Arrange
		var sut = CreateSut(new ParallelEventHandlerOptions
		{
			Enabled = true,
			MaxDegreeOfParallelism = 4,
			WhenAllStrategy = WhenAllStrategy.WaitAll
		});
		var context = new MessageContext { MessageId = "msg-1" };
		var message = new FakeDispatchMessage();
		var handlers = new[]
		{
			new DispatchRequestDelegate((_, _, _) => new ValueTask<IMessageResult>(MessageResult.Success())),
			new DispatchRequestDelegate((_, _, _) => new ValueTask<IMessageResult>(MessageResult.Success())),
			new DispatchRequestDelegate((_, _, _) => new ValueTask<IMessageResult>(MessageResult.Success()))
		};
		context.SetItem<IReadOnlyList<DispatchRequestDelegate>>("ParallelHandlers", handlers);

		// Act
		var result = await sut.InvokeAsync(
			message,
			context,
			(_, _, _) => new ValueTask<IMessageResult>(MessageResult.Success()),
			CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.Succeeded.ShouldBeTrue();
	}

	[Fact]
	public async Task ReturnFirstFailureResultInWaitAllMode()
	{
		// Arrange
		var sut = CreateSut(new ParallelEventHandlerOptions
		{
			Enabled = true,
			MaxDegreeOfParallelism = 4,
			WhenAllStrategy = WhenAllStrategy.WaitAll
		});
		var context = new MessageContext();
		var message = new FakeDispatchMessage();
		var handlers = new[]
		{
			new DispatchRequestDelegate((_, _, _) => new ValueTask<IMessageResult>(MessageResult.Success())),
			new DispatchRequestDelegate((_, _, _) => new ValueTask<IMessageResult>(MessageResult.Failed("handler-failed"))),
			new DispatchRequestDelegate((_, _, _) => new ValueTask<IMessageResult>(MessageResult.Success()))
		};
		context.SetItem<IReadOnlyList<DispatchRequestDelegate>>("ParallelHandlers", handlers);

		// Act
		var result = await sut.InvokeAsync(
			message,
			context,
			(_, _, _) => new ValueTask<IMessageResult>(MessageResult.Success()),
			CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.Succeeded.ShouldBeFalse();
	}

	[Fact]
	public async Task ThrowAggregateExceptionWhenMultipleHandlersFaultInWaitAllMode()
	{
		// Arrange
		var sut = CreateSut(new ParallelEventHandlerOptions
		{
			Enabled = true,
			MaxDegreeOfParallelism = 4,
			WhenAllStrategy = WhenAllStrategy.WaitAll
		});
		var context = new MessageContext();
		var message = new FakeDispatchMessage();
		var handlers = new[]
		{
			new DispatchRequestDelegate((_, _, _) => ValueTask.FromException<IMessageResult>(new InvalidOperationException("first"))),
			new DispatchRequestDelegate((_, _, _) => ValueTask.FromException<IMessageResult>(new NotSupportedException("second")))
		};
		context.SetItem<IReadOnlyList<DispatchRequestDelegate>>("ParallelHandlers", handlers);

		// Act/Assert
		var exception = await Should.ThrowAsync<AggregateException>(
			() => sut.InvokeAsync(
					message,
					context,
					(_, _, _) => new ValueTask<IMessageResult>(MessageResult.Success()),
					CancellationToken.None)
				.AsTask()).ConfigureAwait(false);
		exception.InnerExceptions.Count.ShouldBe(2);
	}

	[Fact]
	public async Task ThrowFirstExceptionInFirstFailureMode()
	{
		// Arrange
		var sut = CreateSut(new ParallelEventHandlerOptions
		{
			Enabled = true,
			MaxDegreeOfParallelism = 4,
			WhenAllStrategy = WhenAllStrategy.FirstFailure
		});
		var context = new MessageContext();
		var message = new FakeDispatchMessage();
		var handlers = new[]
		{
			new DispatchRequestDelegate((_, _, _) => ValueTask.FromException<IMessageResult>(new InvalidOperationException("boom"))),
			new DispatchRequestDelegate(async (_, _, ct) =>
			{
				await global::Tests.Shared.Infrastructure.TestTiming.PauseAsync(TimeSpan.FromMilliseconds(250), ct).ConfigureAwait(false);
				return MessageResult.Success();
			})
		};
		context.SetItem<IReadOnlyList<DispatchRequestDelegate>>("ParallelHandlers", handlers);

		// Act/Assert
		var exception = await Should.ThrowAsync<InvalidOperationException>(
			() => sut.InvokeAsync(
					message,
					context,
					(_, _, _) => new ValueTask<IMessageResult>(MessageResult.Success()),
					CancellationToken.None)
				.AsTask()).ConfigureAwait(false);
		exception.Message.ShouldContain("boom");
	}

	[Fact]
	public async Task FallBackToNextDelegateWhenStrategyIsUnknown()
	{
		// Arrange
		var sut = CreateSut(new ParallelEventHandlerOptions
		{
			Enabled = true,
			WhenAllStrategy = (WhenAllStrategy)99
		});
		var context = new MessageContext();
		var message = new FakeDispatchMessage();
		context.SetItem<IReadOnlyList<DispatchRequestDelegate>>(
			"ParallelHandlers",
			[
				new DispatchRequestDelegate((_, _, _) => new ValueTask<IMessageResult>(MessageResult.Failed("should-not-run"))),
				new DispatchRequestDelegate((_, _, _) => new ValueTask<IMessageResult>(MessageResult.Failed("should-not-run")))
			]);
		var expected = MessageResult.Success();

		// Act
		var result = await sut.InvokeAsync(
			message,
			context,
			(_, _, _) => new ValueTask<IMessageResult>(expected),
			CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.ShouldBe(expected);
	}

	[Fact]
	public void RegisterParallelEventHandlerMiddlewareWithDefaultOptions()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddParallelEventHandlerMiddleware();

		// Assert
		var descriptor = services.SingleOrDefault(sd => sd.ServiceType == typeof(ParallelEventHandlerMiddleware));
		descriptor.ShouldNotBeNull();
		descriptor.Lifetime.ShouldBe(ServiceLifetime.Singleton);
	}

	[Fact]
	public void RegisterParallelEventHandlerMiddlewareWithConfiguredOptions()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddParallelEventHandlerMiddleware(opts =>
		{
			opts.Enabled = true;
			opts.MaxDegreeOfParallelism = 12;
			opts.WhenAllStrategy = WhenAllStrategy.FirstFailure;
			opts.Timeout = TimeSpan.FromSeconds(4);
		});

		// Assert
		using var provider = services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptions<ParallelEventHandlerOptions>>().Value;
		options.Enabled.ShouldBeTrue();
		options.MaxDegreeOfParallelism.ShouldBe(12);
		options.WhenAllStrategy.ShouldBe(WhenAllStrategy.FirstFailure);
		options.Timeout.ShouldBe(TimeSpan.FromSeconds(4));
	}

	[Fact]
	public void ThrowWhenServiceCollectionOrConfigureDelegateIsNull()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act/Assert
		Should.Throw<ArgumentNullException>(() => ParallelEventHandlerServiceCollectionExtensions.AddParallelEventHandlerMiddleware(null!));
		Should.Throw<ArgumentNullException>(() =>
			ParallelEventHandlerServiceCollectionExtensions.AddParallelEventHandlerMiddleware(null!, _ => { }));
		Should.Throw<ArgumentNullException>(() => services.AddParallelEventHandlerMiddleware(null!));
	}

	[Fact]
	public void UseExpectedOptionDefaults()
	{
		// Arrange/Act
		var options = new ParallelEventHandlerOptions();

		// Assert
		options.Enabled.ShouldBeFalse();
		options.MaxDegreeOfParallelism.ShouldBe(Environment.ProcessorCount);
		options.WhenAllStrategy.ShouldBe(WhenAllStrategy.WaitAll);
		options.Timeout.ShouldBeNull();
	}

	private static ParallelEventHandlerMiddleware CreateSut(ParallelEventHandlerOptions options)
	{
		return new ParallelEventHandlerMiddleware(
			Microsoft.Extensions.Options.Options.Create(options),
			NullLogger<ParallelEventHandlerMiddleware>.Instance);
	}
}

