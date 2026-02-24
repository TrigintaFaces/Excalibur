// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

#pragma warning disable CA2012 // Use ValueTasks correctly - acceptable in tests

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Delivery;
using Excalibur.Dispatch.Messaging;
using Excalibur.Dispatch.Middleware.Timeout;
using Excalibur.Dispatch.Tests.TestFakes;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using MessageResult = Excalibur.Dispatch.Abstractions.MessageResult;

namespace Excalibur.Dispatch.Tests.Messaging.Middleware;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class HandlerTimeoutMiddlewareShould
{
	[Fact]
	public async Task PassThroughWhenDisabled()
	{
		// Arrange
		var options = new HandlerTimeoutOptions
		{
			Enabled = false,
			DefaultTimeout = TimeSpan.FromMilliseconds(1)
		};
		var sut = CreateSut(options);
		var message = new FakeDispatchMessage();
		var context = new MessageContext();
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
	public async Task UseContextOverrideTimeoutWhenProvided()
	{
		// Arrange
		var options = new HandlerTimeoutOptions
		{
			Enabled = true,
			DefaultTimeout = TimeSpan.FromSeconds(5),
			ThrowOnTimeout = false
		};
		var sut = CreateSut(options);
		var context = new MessageContext();
		context.SetItem("HandlerTimeout.Override", TimeSpan.FromMilliseconds(20));
		var message = new FakeDispatchMessage();

		// Act
		var result = await sut.InvokeAsync(
			message,
			context,
			async (_, _, ct) =>
			{
				await global::Tests.Shared.Infrastructure.TestTiming.DelayAsync(Timeout.InfiniteTimeSpan, ct).ConfigureAwait(false);
				return MessageResult.Success();
			},
			CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.Succeeded.ShouldBeFalse();
		context.TimeoutExceeded.ShouldBeTrue();
		context.TimeoutElapsed.ShouldBe(TimeSpan.FromMilliseconds(20));
	}

	[Fact]
	public async Task UsePerHandlerTimeoutWhenNoContextOverride()
	{
		// Arrange
		var options = new HandlerTimeoutOptions
		{
			Enabled = true,
			DefaultTimeout = TimeSpan.FromSeconds(5),
			ThrowOnTimeout = false
		};
		options.HandlerTimeouts[nameof(FakeDispatchMessage)] = TimeSpan.FromMilliseconds(30);
		var sut = CreateSut(options);
		var context = new MessageContext();
		var message = new FakeDispatchMessage();

		// Act
		var result = await sut.InvokeAsync(
			message,
			context,
			async (_, _, ct) =>
			{
				await global::Tests.Shared.Infrastructure.TestTiming.DelayAsync(Timeout.InfiniteTimeSpan, ct).ConfigureAwait(false);
				return MessageResult.Success();
			},
			CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.Succeeded.ShouldBeFalse();
		context.TimeoutExceeded.ShouldBeTrue();
		context.TimeoutElapsed.ShouldBe(TimeSpan.FromMilliseconds(30));
	}

	[Fact]
	public async Task IgnoreNonPositiveContextOverrideAndUsePerHandlerTimeout()
	{
		// Arrange
		var options = new HandlerTimeoutOptions
		{
			Enabled = true,
			DefaultTimeout = TimeSpan.FromSeconds(5),
			ThrowOnTimeout = false
		};
		options.HandlerTimeouts[nameof(FakeDispatchMessage)] = TimeSpan.FromMilliseconds(25);
		var sut = CreateSut(options);
		var context = new MessageContext();
		context.SetItem("HandlerTimeout.Override", TimeSpan.Zero);
		var message = new FakeDispatchMessage();

		// Act
		var result = await sut.InvokeAsync(
			message,
			context,
			async (_, _, ct) =>
			{
				await global::Tests.Shared.Infrastructure.TestTiming.DelayAsync(Timeout.InfiniteTimeSpan, ct).ConfigureAwait(false);
				return MessageResult.Success();
			},
			CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.Succeeded.ShouldBeFalse();
		context.TimeoutExceeded.ShouldBeTrue();
		context.TimeoutElapsed.ShouldBe(TimeSpan.FromMilliseconds(25));
	}

	[Fact]
	public async Task ThrowTimeoutExceptionWhenTimedOutAndThrowOnTimeoutEnabled()
	{
		// Arrange
		var options = new HandlerTimeoutOptions
		{
			Enabled = true,
			DefaultTimeout = TimeSpan.FromMilliseconds(20),
			ThrowOnTimeout = true
		};
		var sut = CreateSut(options);
		var context = new MessageContext();
		var message = new FakeDispatchMessage();

		// Act/Assert
		await Should.ThrowAsync<TimeoutException>(
			() => sut.InvokeAsync(
					message,
					context,
					async (_, _, ct) =>
					{
						await global::Tests.Shared.Infrastructure.TestTiming.DelayAsync(Timeout.InfiniteTimeSpan, ct).ConfigureAwait(false);
						return MessageResult.Success();
					},
					CancellationToken.None)
				.AsTask()).ConfigureAwait(false);
	}

	[Fact]
	public async Task PropagateExternalCancellation()
	{
		// Arrange
		var options = new HandlerTimeoutOptions
		{
			Enabled = true,
			DefaultTimeout = TimeSpan.FromSeconds(5),
			ThrowOnTimeout = true
		};
		var sut = CreateSut(options);
		var context = new MessageContext();
		var message = new FakeDispatchMessage();
		using var cts = new CancellationTokenSource();
		await cts.CancelAsync().ConfigureAwait(false);

		// Act/Assert
		await Should.ThrowAsync<OperationCanceledException>(
			() => sut.InvokeAsync(
					message,
					context,
					async (_, _, ct) =>
					{
						ct.ThrowIfCancellationRequested();
						await global::Tests.Shared.Infrastructure.TestTiming.DelayAsync(TimeSpan.FromMilliseconds(1), ct).ConfigureAwait(false);
						return MessageResult.Success();
					},
					cts.Token)
				.AsTask()).ConfigureAwait(false);
	}

	[Fact]
	public void RegisterHandlerTimeoutMiddlewareWithDefaultOptions()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddHandlerTimeoutMiddleware();

		// Assert
		var descriptor = services.SingleOrDefault(sd => sd.ServiceType == typeof(HandlerTimeoutMiddleware));
		descriptor.ShouldNotBeNull();
		descriptor.Lifetime.ShouldBe(ServiceLifetime.Singleton);
	}

	[Fact]
	public void RegisterHandlerTimeoutMiddlewareWithConfiguredOptions()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddHandlerTimeoutMiddleware(opts =>
		{
			opts.Enabled = true;
			opts.ThrowOnTimeout = false;
			opts.DefaultTimeout = TimeSpan.FromSeconds(9);
		});

		// Assert
		using var provider = services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptions<HandlerTimeoutOptions>>().Value;
		options.DefaultTimeout.ShouldBe(TimeSpan.FromSeconds(9));
		options.ThrowOnTimeout.ShouldBeFalse();
	}

	[Fact]
	public void ThrowWhenServiceCollectionIsNull()
	{
		// Act/Assert
		Should.Throw<ArgumentNullException>(() => HandlerTimeoutServiceCollectionExtensions.AddHandlerTimeoutMiddleware(null!));
		Should.Throw<ArgumentNullException>(() =>
			HandlerTimeoutServiceCollectionExtensions.AddHandlerTimeoutMiddleware(null!, _ => { }));
	}

	[Fact]
	public void ThrowWhenConfigureDelegateIsNull()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act/Assert
		Should.Throw<ArgumentNullException>(() => services.AddHandlerTimeoutMiddleware(null!));
	}

	private static HandlerTimeoutMiddleware CreateSut(HandlerTimeoutOptions options)
	{
		return new HandlerTimeoutMiddleware(
			Microsoft.Extensions.Options.Options.Create(options),
			NullLogger<HandlerTimeoutMiddleware>.Instance);
	}
}
