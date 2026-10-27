// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch.Configuration;
using Excalibur.Dispatch.Delivery;
using Excalibur.Dispatch.Delivery.Handlers;
using Excalibur.Dispatch.Messaging;
using Excalibur.Dispatch.Threading;
using Excalibur.Dispatch.Options.Threading;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Dispatch.Aot.Sample;

internal static class PipelineVerification
{
	internal static async Task RunAsync(IDispatcher dispatcher, IServiceProvider services)
	{
		var first = new VerifyFirst();
		var firstContext = new MessageContext(first, services);
		var firstResult = await dispatcher.DispatchAsync(first, firstContext, CancellationToken.None).ConfigureAwait(false);
		var second = new VerifySecond();
		var secondContext = new MessageContext(second, services);
		var secondResult = await dispatcher.DispatchAsync<VerifySecond, string>(second, secondContext, CancellationToken.None).ConfigureAwait(false);
		if (!firstResult.Succeeded || !secondResult.Succeeded || secondResult.ReturnValue != "second"
			|| first.Calls != 1 || second.Calls != 1
			|| !firstContext.Items.ContainsKey("verified") || !secondContext.Items.ContainsKey("verified"))
		{
			throw new InvalidOperationException("Generated handler pairs and configured middleware must both execute.");
		}

		// Exercise the exact AOT invoker registry as well as dispatcher optimizations.
		var invoker = new HandlerInvokerAot();
		var handler = new VerificationHandler();
		await invoker.InvokeAsync(handler, first, CancellationToken.None).ConfigureAwait(false);
		var response = await invoker.InvokeAsync(handler, second, CancellationToken.None).ConfigureAwait(false);
		if (first.Calls != 2 || second.Calls != 2 || !Equals(response, "second"))
		{
			throw new InvalidOperationException("The AOT invoker must select the exact handler/message pair.");
		}

		// Factory middleware must be resolved anew in each dispatch's owned scope.
		await using var scopedProvider = new ServiceCollection().AddScoped<ScopedVerificationMiddleware>().BuildServiceProvider(
			new ServiceProviderOptions { ValidateScopes = true });
		var builder = new PipelineBuilder("ScopedVerification", scopedProvider);
		builder.UseDeferred(static provider => provider.GetRequiredService<ScopedVerificationMiddleware>(),
			DispatchMiddlewareStage.PreProcessing, MessageKinds.Action);
		var pipeline = builder.Build();
		var observed = new List<ScopedVerificationMiddleware>();
		for (var i = 0; i < 2; i++)
		{
			var result = await pipeline.ExecuteAsync(first, new MessageContext(), (_, context, _) =>
			{
				var middleware = context.RequestServices.GetRequiredService<ScopedVerificationMiddleware>();
				ObjectDisposedException.ThrowIf(middleware.Disposed, middleware);
				observed.Add(middleware);
				return ValueTask.FromResult<IMessageResult>(MessageResult.Success());
			}, CancellationToken.None).ConfigureAwait(false);
			if (!result.Succeeded)
			{
				throw new InvalidOperationException("Scoped pipeline verification failed.");
			}
		}
		if (ReferenceEquals(observed[0], observed[1]) || observed.Any(static middleware => !middleware.Disposed))
		{
			throw new InvalidOperationException("Dispatch scopes must own distinct middleware instances and dispose them.");
		}
		var background = new BackgroundExecutionMiddleware(
			Microsoft.Extensions.Options.Options.Create(new BackgroundExecutionOptions()),
			NullLogger<BackgroundExecutionMiddleware>.Instance);
		var completed = new TaskCompletionSource<ScopedVerificationMiddleware>(TaskCreationOptions.RunContinuationsAsynchronously);
		var accepted = await background.InvokeAsync(new VerifyBackground(), new MessageContext(first, scopedProvider),
			(_, context, _) =>
			{
				completed.SetResult(context.RequestServices.GetRequiredService<ScopedVerificationMiddleware>());
				return ValueTask.FromResult<IMessageResult>(MessageResult.Success());
			}, CancellationToken.None).ConfigureAwait(false);
		if (accepted.Disposition != MessageDisposition.AcceptedForBackgroundExecution)
		{
			throw new InvalidOperationException("Background work was not accepted.");
		}
		var backgroundDependency = await completed.Task.WaitAsync(TimeSpan.FromSeconds(10)).ConfigureAwait(false);
		await backgroundDependency.DisposedSignal.Task.WaitAsync(TimeSpan.FromSeconds(10)).ConfigureAwait(false);
		var rejected = await background.InvokeAsync(new VerifyTypedBackground(), new MessageContext(),
			static (_, _, _) => throw new InvalidOperationException("Typed background message must not reach its terminal."),
			CancellationToken.None).ConfigureAwait(false);
		if (rejected.Succeeded)
		{
			throw new InvalidOperationException("Typed background work must be refused under Native AOT too.");
		}

		Console.WriteLine($"Pipeline verification PASS; dynamic code supported: {System.Runtime.CompilerServices.RuntimeFeature.IsDynamicCodeSupported}");
	}
}

internal sealed class VerifyBackground : IDispatchAction, IExecuteInBackground;
internal sealed class VerifyTypedBackground : IDispatchAction<string>, IExecuteInBackground;

internal sealed class VerifyFirst : IDispatchAction
{
	public int Calls { get; set; }
}

internal sealed class VerifySecond : IDispatchAction<string>
{
	public int Calls { get; set; }
}

internal sealed class VerificationHandler : IActionHandler<VerifyFirst>, IActionHandler<VerifySecond, string>
{
	Task IActionHandler<VerifyFirst>.HandleAsync(VerifyFirst action, CancellationToken cancellationToken)
	{
		action.Calls++;
		return Task.CompletedTask;
	}
	Task<string> IActionHandler<VerifySecond, string>.HandleAsync(VerifySecond action, CancellationToken cancellationToken)
	{
		action.Calls++;
		return Task.FromResult("second");
	}
}

internal sealed class VerificationMiddleware : IDispatchMiddleware
{
	public DispatchMiddlewareStage? Stage => DispatchMiddlewareStage.PreProcessing;
	public ValueTask<IMessageResult> InvokeAsync(IDispatchMessage message, IMessageContext context,
		DispatchRequestDelegate nextDelegate, CancellationToken cancellationToken)
	{
		if (message is VerifyFirst or VerifySecond)
		{
			context.Items["verified"] = true;
		}
		return nextDelegate(message, context, cancellationToken);
	}
}

internal sealed class ScopedVerificationMiddleware : IDispatchMiddleware, IAsyncDisposable
{
	public bool Disposed { get; private set; }
	public TaskCompletionSource DisposedSignal { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
	public DispatchMiddlewareStage? Stage => DispatchMiddlewareStage.PreProcessing;
	public ValueTask<IMessageResult> InvokeAsync(IDispatchMessage message, IMessageContext context,
		DispatchRequestDelegate nextDelegate, CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(Disposed, this);
		return nextDelegate(message, context, cancellationToken);
	}
	public ValueTask DisposeAsync()
	{
		Disposed = true;
		DisposedSignal.TrySetResult();
		return ValueTask.CompletedTask;
	}
}
