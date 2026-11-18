// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Threading.Tasks.Sources;

using Excalibur.Dispatch.Delivery;
using Excalibur.Dispatch.Delivery.Handlers;
using Excalibur.Dispatch.Messaging;

namespace Excalibur.Dispatch.Tests.Messaging.Delivery.Handlers;

[Collection("HandlerInvokerRegistry")]
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Core)]
[Trait("Pattern", "Regression")]
public sealed class HandlerValueTaskConsumptionShould
{
	static HandlerValueTaskConsumptionShould()
	{
		PrecompiledDirectDispatchRegistry.Register(
			static type => type == typeof(PrecompiledMessage),
			static (Type type, out bool expectsResponse, out bool requiresContext) =>
			{
				expectsResponse = false;
				requiresContext = false;
				return type == typeof(PrecompiledMessage);
			},
			static (action, _, _, _) =>
			{
				var source = ((PrecompiledMessage)action).Source;
				return new ValueTask<object?>(source, source.Token);
			});
	}

	[Theory]
	[InlineData(true, false, false)]
	[InlineData(false, false, false)]
	[InlineData(true, true, false)]
	[InlineData(false, true, false)]
	[InlineData(true, false, true)]
	[InlineData(false, false, true)]
	[InlineData(true, true, true)]
	[InlineData(false, true, true)]
	public async Task ConsumePrecompiledFaultOrCancellation(bool completedBeforeInvocation, bool canceled, bool contextLess)
	{
		using var cancellation = new CancellationTokenSource();
		cancellation.Cancel();
		Exception failure = canceled
			? new OperationCanceledException(cancellation.Token)
			: new InvalidOperationException("precompiled source failure");
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddDispatch();
		await using var provider = services.BuildServiceProvider();
		var dispatcher = (Dispatcher)provider.GetRequiredService<IDispatcher>();
		var source = new ObservedSource();
		if (completedBeforeInvocation)
		{
			source.Fail(failure);
		}

		var message = new PrecompiledMessage(source);
		var operation = contextLess
			? dispatcher.DispatchAsync(message, CancellationToken.None)
			: dispatcher.DispatchAsync(message, new MessageContext { RequestServices = provider }, CancellationToken.None);
		if (!completedBeforeInvocation)
		{
			operation.IsCompleted.ShouldBeFalse();
			source.ConsumptionCount.ShouldBe(0);
			source.Fail(failure);
		}

		if (canceled)
		{
			var exception = await Should.ThrowAsync<OperationCanceledException>(
				async () => await operation.WaitAsync(TimeSpan.FromSeconds(10)));
			exception.CancellationToken.ShouldBe(cancellation.Token);
		}
		else
		{
			var exception = await Should.ThrowAsync<InvalidOperationException>(
				async () => await operation.WaitAsync(TimeSpan.FromSeconds(10)));
			exception.ShouldBeSameAs(failure);
		}

		source.ConsumptionCount.ShouldBe(1);
		source.ConsumedToken.ShouldBe(source.Token);
	}

	[Theory]
	[InlineData(true, false)]
	[InlineData(false, false)]
	[InlineData(true, true)]
	[InlineData(false, true)]
	public async Task ConsumePrecompiledSourceThroughPublicDispatcher(bool completedBeforeInvocation, bool contextLess)
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddDispatch();
		await using var provider = services.BuildServiceProvider();
		var dispatcher = (Dispatcher)provider.GetRequiredService<IDispatcher>();
		var source = new ObservedSource();
		if (completedBeforeInvocation)
		{
			source.Complete();
		}

		var message = new PrecompiledMessage(source);
		var operation = contextLess
			? dispatcher.DispatchAsync(message, CancellationToken.None)
			: dispatcher.DispatchAsync(message, new MessageContext { RequestServices = provider }, CancellationToken.None);
		if (!completedBeforeInvocation)
		{
			operation.IsCompleted.ShouldBeFalse();
			source.ConsumptionCount.ShouldBe(0);
			source.Complete();
		}

		var result = await operation.WaitAsync(TimeSpan.FromSeconds(10));
		result.Succeeded.ShouldBeTrue();
		source.ConsumptionCount.ShouldBe(1);
		source.ConsumedToken.ShouldBe(source.Token);
	}

	[Theory]
	[InlineData(true, false)]
	[InlineData(false, false)]
	[InlineData(true, true)]
	[InlineData(false, true)]
	public async Task ConsumeSuccessfulSourceExactlyOnce(bool completedBeforeInvocation, bool generic)
	{
		var source = new ObservedSource();
		if (completedBeforeInvocation)
		{
			source.Complete();
		}

		object handler = generic ? new GenericHandler(source) : new Handler(source);
		var operation = new HandlerInvoker().InvokeAsync(handler, new Message(), CancellationToken.None);
		if (!completedBeforeInvocation)
		{
			operation.IsCompleted.ShouldBeFalse();
			source.ConsumptionCount.ShouldBe(0);
			source.Complete();
		}

		var result = await operation.WaitAsync(TimeSpan.FromSeconds(10));
		result.ShouldBe(generic ? 42 : null);
		source.ConsumptionCount.ShouldBe(1);
		source.ConsumedToken.ShouldBe(source.Token);
		source.Token.ShouldNotBe((short)0);
	}

	[Theory]
	[InlineData(true, false)]
	[InlineData(false, false)]
	[InlineData(true, true)]
	[InlineData(false, true)]
	public async Task ConsumeFaultOrCancellationExactlyOnce(bool completedBeforeInvocation, bool canceled)
	{
		using var cancellation = new CancellationTokenSource();
		cancellation.Cancel();
		Exception failure = canceled
			? new OperationCanceledException(cancellation.Token)
			: new InvalidOperationException("source failure");
		var source = new ObservedSource();
		if (completedBeforeInvocation)
		{
			source.Fail(failure);
		}

		var operation = new HandlerInvoker().InvokeAsync(new Handler(source), new Message(), CancellationToken.None);
		if (!completedBeforeInvocation)
		{
			operation.IsCompleted.ShouldBeFalse();
			source.ConsumptionCount.ShouldBe(0);
			source.Fail(failure);
		}

		if (canceled)
		{
			var exception = await Should.ThrowAsync<OperationCanceledException>(
				async () => await operation.WaitAsync(TimeSpan.FromSeconds(10)));
			exception.CancellationToken.ShouldBe(cancellation.Token);
		}
		else
		{
			var exception = await Should.ThrowAsync<InvalidOperationException>(
				async () => await operation.WaitAsync(TimeSpan.FromSeconds(10)));
			exception.ShouldBeSameAs(failure);
		}

		source.ConsumptionCount.ShouldBe(1);
		source.ConsumedToken.ShouldBe(source.Token);
	}

	private sealed class Message : IDispatchAction;

	private sealed class PrecompiledMessage(ObservedSource source) : IDispatchAction
	{
		public ObservedSource Source { get; } = source;
	}

	private sealed class Handler(ObservedSource source)
	{
		public ValueTask HandleAsync(Message message, CancellationToken cancellationToken) => new(source, source.Token);
	}

	private sealed class GenericHandler(ObservedSource source)
	{
		public ValueTask<object?> HandleAsync(Message message, CancellationToken cancellationToken) => new(source, source.Token);
	}

	private sealed class ObservedSource : IValueTaskSource, IValueTaskSource<object?>
	{
		private ManualResetValueTaskSourceCore<bool> _core = new() { RunContinuationsAsynchronously = true };
		private int _consumptionCount;

		public ObservedSource() => _core.Reset();

		public short Token => _core.Version;
		public short ConsumedToken { get; private set; }
		public int ConsumptionCount => Volatile.Read(ref _consumptionCount);

		public void Complete() => _core.SetResult(true);
		public void Fail(Exception error) => _core.SetException(error);
		public ValueTaskSourceStatus GetStatus(short token) => _core.GetStatus(token);
		public void OnCompleted(Action<object?> continuation, object? state, short token, ValueTaskSourceOnCompletedFlags flags) =>
			_core.OnCompleted(continuation, state, token, flags);

		void IValueTaskSource.GetResult(short token) => Consume(token);

		object? IValueTaskSource<object?>.GetResult(short token)
		{
			Consume(token);
			return 42;
		}

		private void Consume(short token)
		{
			ConsumedToken = token;
			if (Interlocked.Increment(ref _consumptionCount) != 1)
			{
				throw new InvalidOperationException("Source consumed more than once");
			}

			_ = _core.GetResult(token);
		}
	}
}
