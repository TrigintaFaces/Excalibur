// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Delivery;
using Excalibur.Dispatch.Messaging;
using Excalibur.Dispatch.Delivery;
using Excalibur.Dispatch.Delivery.Handlers;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Dispatch.Benchmarks.Diagnostics.Support;

internal sealed partial class DiagnosticBenchmarkFixture
{
}

public enum HandlerLifetimeMode
{
	Transient,
	Scoped,
	Singleton,
}

public enum DispatchScenario
{
	Command,
	Query,
	Event,
}

internal sealed record DiagnosticCommand(int Value) : IDispatchAction;

internal sealed record DiagnosticQuery(int Value) : IDispatchAction<int>;

internal sealed record DiagnosticEvent(int Value, string Name) : IDispatchEvent;

internal sealed record FaultingCommand(int Value) : IDispatchAction;

internal sealed record CancelableCommand(int Value, int DelayMs) : IDispatchAction;

internal sealed record UnregisteredDiagnosticCommand(int Value) : IDispatchAction;
internal sealed record UnregisteredDiagnosticCommand2(int Value) : IDispatchAction;
internal sealed record UnregisteredDiagnosticCommand3(int Value) : IDispatchAction;
internal sealed record UnregisteredDiagnosticCommand4(int Value) : IDispatchAction;

internal sealed class DiagnosticCommandHandler : IActionHandler<DiagnosticCommand>
{
	public Task HandleAsync(DiagnosticCommand action, CancellationToken cancellationToken)
	{
		_ = action.Value * 2;
		return Task.CompletedTask;
	}
}

internal sealed class DiagnosticQueryHandler : IActionHandler<DiagnosticQuery, int>
{
	public Task<int> HandleAsync(DiagnosticQuery action, CancellationToken cancellationToken)
	{
		return Task.FromResult(action.Value + 1);
	}
}

internal sealed class DiagnosticEventHandler : IEventHandler<DiagnosticEvent>
{
	public Task HandleAsync(DiagnosticEvent eventMessage, CancellationToken cancellationToken)
	{
		_ = eventMessage.Value + eventMessage.Name.Length;
		return Task.CompletedTask;
	}
}

internal sealed class FanOutEventHandler<TMarker> : IEventHandler<DiagnosticEvent>
	where TMarker : class
{
	public Task HandleAsync(DiagnosticEvent eventMessage, CancellationToken cancellationToken)
	{
		_ = eventMessage.Value + eventMessage.Name.Length;
		return Task.CompletedTask;
	}
}

internal sealed class FaultingCommandHandler : IActionHandler<FaultingCommand>
{
	public Task HandleAsync(FaultingCommand action, CancellationToken cancellationToken)
	{
		throw new InvalidOperationException($"Synthetic benchmark failure for {action.Value}");
	}
}

internal sealed class CancelableCommandHandler : IActionHandler<CancelableCommand>
{
	public async Task HandleAsync(CancelableCommand action, CancellationToken cancellationToken)
	{
		await Task.Delay(action.DelayMs, cancellationToken).ConfigureAwait(false);
	}
}

internal sealed class BenchmarkPassThroughMiddleware(int order) : IDispatchMiddleware
{
	public DispatchMiddlewareStage? Stage => DispatchMiddlewareStage.PreProcessing;

	public MessageKinds ApplicableMessageKinds => MessageKinds.All;

	public ValueTask<IMessageResult> InvokeAsync(
		IDispatchMessage message,
		IMessageContext context,
		DispatchRequestDelegate nextDelegate,
		CancellationToken cancellationToken)
	{
		context.Items[$"Benchmark:Middleware:{order}"] = order;
		return nextDelegate(message, context, cancellationToken);
	}
}

internal sealed class DelayMiddleware(TimeSpan delay) : IDispatchMiddleware
{
	public DispatchMiddlewareStage? Stage => DispatchMiddlewareStage.PreProcessing;

	public MessageKinds ApplicableMessageKinds => MessageKinds.Action;

	public async ValueTask<IMessageResult> InvokeAsync(
		IDispatchMessage message,
		IMessageContext context,
		DispatchRequestDelegate nextDelegate,
		CancellationToken cancellationToken)
	{
		await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
		return await nextDelegate(message, context, cancellationToken).ConfigureAwait(false);
	}
}

internal sealed partial class DiagnosticBenchmarkFixture : IDisposable
{
	private static readonly Type[] FanOutMarkerTypes =
	[
		typeof(FanOutMarker01), typeof(FanOutMarker02), typeof(FanOutMarker03), typeof(FanOutMarker04), typeof(FanOutMarker05),
		typeof(FanOutMarker06), typeof(FanOutMarker07), typeof(FanOutMarker08), typeof(FanOutMarker09), typeof(FanOutMarker10),
		typeof(FanOutMarker11), typeof(FanOutMarker12), typeof(FanOutMarker13), typeof(FanOutMarker14), typeof(FanOutMarker15),
		typeof(FanOutMarker16), typeof(FanOutMarker17), typeof(FanOutMarker18), typeof(FanOutMarker19), typeof(FanOutMarker20),
		typeof(FanOutMarker21), typeof(FanOutMarker22), typeof(FanOutMarker23), typeof(FanOutMarker24), typeof(FanOutMarker25),
		typeof(FanOutMarker26), typeof(FanOutMarker27), typeof(FanOutMarker28), typeof(FanOutMarker29), typeof(FanOutMarker30),
		typeof(FanOutMarker31), typeof(FanOutMarker32), typeof(FanOutMarker33), typeof(FanOutMarker34), typeof(FanOutMarker35),
		typeof(FanOutMarker36), typeof(FanOutMarker37), typeof(FanOutMarker38), typeof(FanOutMarker39), typeof(FanOutMarker40),
		typeof(FanOutMarker41), typeof(FanOutMarker42), typeof(FanOutMarker43), typeof(FanOutMarker44), typeof(FanOutMarker45),
		typeof(FanOutMarker46), typeof(FanOutMarker47), typeof(FanOutMarker48), typeof(FanOutMarker49), typeof(FanOutMarker50),
	];

	internal static IReadOnlyList<Type> GetFanOutMarkerTypes(int count)
	{
		if (count < 1 || count > FanOutMarkerTypes.Length)
		{
			throw new ArgumentOutOfRangeException(nameof(count), count, $"count must be in range [1, {FanOutMarkerTypes.Length}].");
		}

		var markers = new Type[count];
		Array.Copy(FanOutMarkerTypes, markers, count);
		return markers;
	}

	private readonly ServiceProvider _provider;

	public DiagnosticBenchmarkFixture(
		int middlewareCount = 0,
		int eventHandlerCount = 1,
		HandlerLifetimeMode commandHandlerLifetime = HandlerLifetimeMode.Transient,
		bool includeFaultingHandler = false,
		bool includeCancelableHandler = false,
		bool includeDelayMiddleware = false,
		TimeSpan? delayMiddlewareDuration = null)
	{
		if (eventHandlerCount is < 1 or > 50)
		{
			throw new ArgumentOutOfRangeException(nameof(eventHandlerCount), eventHandlerCount,
				"eventHandlerCount must be in range [1, 50].");
		}

		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddBenchmarkDispatch();

		RegisterCommandHandlers(services, commandHandlerLifetime);
		RegisterEventHandlers(services, eventHandlerCount);

		if (includeFaultingHandler)
		{
			_ = services.AddTransient<IActionHandler<FaultingCommand>, FaultingCommandHandler>();
		}

		if (includeCancelableHandler)
		{
			_ = services.AddTransient<IActionHandler<CancelableCommand>, CancelableCommandHandler>();
		}

		for (var i = 0; i < middlewareCount; i++)
		{
			_ = services.AddSingleton<IDispatchMiddleware>(new BenchmarkPassThroughMiddleware(i));
		}

		if (includeDelayMiddleware)
		{
			_ = services.AddSingleton<IDispatchMiddleware>(new DelayMiddleware(delayMiddlewareDuration ?? TimeSpan.FromMilliseconds(2)));
		}

		_provider = services.BuildServiceProvider();
		Dispatcher = _provider.GetRequiredService<IDispatcher>();
		ContextFactory = _provider.GetRequiredService<IMessageContextFactory>();
		MiddlewareInvoker = _provider.GetRequiredService<IDispatchMiddlewareInvoker>();
		FinalDispatchHandler = _provider.GetRequiredService<FinalDispatchHandler>();
		LocalMessageBus = _provider.GetRequiredService<LocalMessageBus>();
		HandlerActivator = _provider.GetRequiredService<IHandlerActivator>();
		HandlerInvoker = _provider.GetRequiredService<IHandlerInvoker>();
		HandlerRegistry = _provider.GetRequiredService<IHandlerRegistry>();
	}

	public IServiceProvider Services => _provider;

	public IDispatcher Dispatcher { get; }

	public IMessageContextFactory ContextFactory { get; }

	public IDispatchMiddlewareInvoker MiddlewareInvoker { get; }

	public FinalDispatchHandler FinalDispatchHandler { get; }

	public LocalMessageBus LocalMessageBus { get; }

	public IHandlerActivator HandlerActivator { get; }

	public IHandlerInvoker HandlerInvoker { get; }

	public IHandlerRegistry HandlerRegistry { get; }

	public IMessageContext CreateContext(bool cacheHit = false, object? cachedResult = null)
	{
		var context = ContextFactory.CreateContext();
		if (cacheHit)
		{
			context.Items["Dispatch:CacheHit"] = true;
			context.Items["Dispatch:Result"] = cachedResult ?? 0;
		}

		return context;
	}

	public void ReturnContext(IMessageContext context)
	{
		ArgumentNullException.ThrowIfNull(context);
		ContextFactory.Return(context);
	}

	public void Dispose()
	{
		_provider.Dispose();
	}

	private static void RegisterCommandHandlers(IServiceCollection services, HandlerLifetimeMode lifetime)
	{
		switch (lifetime)
		{
			case HandlerLifetimeMode.Transient:
				_ = services.AddTransient<IActionHandler<DiagnosticCommand>, DiagnosticCommandHandler>();
				_ = services.AddTransient<IActionHandler<DiagnosticQuery, int>, DiagnosticQueryHandler>();
				break;
			case HandlerLifetimeMode.Scoped:
				_ = services.AddScoped<IActionHandler<DiagnosticCommand>, DiagnosticCommandHandler>();
				_ = services.AddScoped<IActionHandler<DiagnosticQuery, int>, DiagnosticQueryHandler>();
				break;
			case HandlerLifetimeMode.Singleton:
				_ = services.AddSingleton<IActionHandler<DiagnosticCommand>, DiagnosticCommandHandler>();
				_ = services.AddSingleton<IActionHandler<DiagnosticQuery, int>, DiagnosticQueryHandler>();
				break;
			default:
				throw new ArgumentOutOfRangeException(nameof(lifetime), lifetime, "Unknown handler lifetime mode.");
		}
	}

	private static void RegisterEventHandlers(IServiceCollection services, int eventHandlerCount)
	{
		_ = services.AddTransient<IEventHandler<DiagnosticEvent>, DiagnosticEventHandler>();

		for (var i = 1; i < eventHandlerCount; i++)
		{
			var markerType = FanOutMarkerTypes[i - 1];
			var handlerType = typeof(FanOutEventHandler<>).MakeGenericType(markerType);
			_ = services.AddTransient(typeof(IEventHandler<DiagnosticEvent>), handlerType);
		}
	}

	#pragma warning disable SA1502
	internal sealed class FanOutMarker01 { }
	internal sealed class FanOutMarker02 { }
	internal sealed class FanOutMarker03 { }
	internal sealed class FanOutMarker04 { }
	internal sealed class FanOutMarker05 { }
	internal sealed class FanOutMarker06 { }
	internal sealed class FanOutMarker07 { }
	internal sealed class FanOutMarker08 { }
	internal sealed class FanOutMarker09 { }
	internal sealed class FanOutMarker10 { }
	internal sealed class FanOutMarker11 { }
	internal sealed class FanOutMarker12 { }
	internal sealed class FanOutMarker13 { }
	internal sealed class FanOutMarker14 { }
	internal sealed class FanOutMarker15 { }
	internal sealed class FanOutMarker16 { }
	internal sealed class FanOutMarker17 { }
	internal sealed class FanOutMarker18 { }
	internal sealed class FanOutMarker19 { }
	internal sealed class FanOutMarker20 { }
	internal sealed class FanOutMarker21 { }
	internal sealed class FanOutMarker22 { }
	internal sealed class FanOutMarker23 { }
	internal sealed class FanOutMarker24 { }
	internal sealed class FanOutMarker25 { }
	internal sealed class FanOutMarker26 { }
	internal sealed class FanOutMarker27 { }
	internal sealed class FanOutMarker28 { }
	internal sealed class FanOutMarker29 { }
	internal sealed class FanOutMarker30 { }
	internal sealed class FanOutMarker31 { }
	internal sealed class FanOutMarker32 { }
	internal sealed class FanOutMarker33 { }
	internal sealed class FanOutMarker34 { }
	internal sealed class FanOutMarker35 { }
	internal sealed class FanOutMarker36 { }
	internal sealed class FanOutMarker37 { }
	internal sealed class FanOutMarker38 { }
	internal sealed class FanOutMarker39 { }
	internal sealed class FanOutMarker40 { }
	internal sealed class FanOutMarker41 { }
	internal sealed class FanOutMarker42 { }
	internal sealed class FanOutMarker43 { }
	internal sealed class FanOutMarker44 { }
	internal sealed class FanOutMarker45 { }
	internal sealed class FanOutMarker46 { }
	internal sealed class FanOutMarker47 { }
	internal sealed class FanOutMarker48 { }
	internal sealed class FanOutMarker49 { }
	internal sealed class FanOutMarker50 { }
	#pragma warning restore SA1502
}

