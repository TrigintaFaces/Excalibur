// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Toolchains.InProcess.Emit;

using Excalibur.Dispatch.Benchmarks.Comparative;
using Excalibur.Dispatch.Delivery;
using Excalibur.Dispatch.Delivery.Handlers;
using Excalibur.Dispatch.Messaging;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Dispatch.Benchmarks.Diagnostics;

#pragma warning disable CA1707

/// <summary>
/// DIAGNOSTIC ONLY. Decomposes the standard dispatch path (Dispatcher.DispatchAsync with a
/// caller-supplied context) against the ultra-local path (IDirectLocalDispatcher.DispatchLocalAsync)
/// so the delta between them can be attributed to named components.
/// </summary>
/// <remarks>
/// RUN ONE ARM PER PROCESS. Running the whole class in a single BenchmarkDotNet process gives wrong
/// absolute numbers: the arms share Dispatcher/LocalMessageBus/MessageContextHolder code, and the
/// in-process toolchain lets one arm's profile change another's codegen. Measured: arm A reports
/// 42.4 ns filtered to itself and 79.0 ns in a whole-class run, while the arms that share no code with
/// it (F, I, J, X*) are unchanged in both -- so it is cross-arm contamination, not machine load.
/// Use --filter "*DispatchPathDecompositionBenchmarks.&lt;ArmName&gt;" once per arm, and treat F/X3/X5
/// as load controls (about 1.06 / 1.36 / 1.24 ns on a quiet box) before believing any run.
/// </remarks>
[MemoryDiagnoser]
[Config(typeof(DecompositionConfig))]
public class DispatchPathDecompositionBenchmarks
{
	private readonly TestCommand _command = new() { Value = 42 };
	private ServiceProvider _provider = null!;
	private Dispatcher _dispatcher = null!;
	private IDirectLocalDispatcher _directLocal = null!;
	private IMessageContextFactory _factory = null!;
	private LocalMessageBus _bus = null!;
	private IActionHandler<TestCommand> _handler = null!;
	private MessageContext _pinnedContext = null!;
	private Task<IMessageResult> _cachedResultTask = null!;

	[GlobalSetup]
	public void Setup()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddDispatch();
		_ = services.AddTransient<DispatchTestCommandHandler>();
		_ = services.AddTransient<IActionHandler<TestCommand>, DispatchTestCommandHandler>();

		_provider = services.BuildServiceProvider();
		var dispatcher = _provider.GetRequiredService<IDispatcher>();
		_dispatcher = (Dispatcher)dispatcher;
		_directLocal = (IDirectLocalDispatcher)dispatcher;
		_factory = _provider.GetRequiredService<IMessageContextFactory>();
		_bus = _provider.GetRequiredService<LocalMessageBus>();
		_handler = _provider.GetRequiredService<IActionHandler<TestCommand>>();

		_pinnedContext = (MessageContext)_factory.CreateContext();

		// Non-vacuity: prove every arm below actually reaches the handler before publishing numbers.
		var warmResult = _dispatcher.DispatchAsync(_command, (IMessageContext)_pinnedContext, CancellationToken.None)
			.GetAwaiter().GetResult();
		_cachedResultTask = Task.FromResult(warmResult);
		_directLocal.DispatchLocalAsync(_command, CancellationToken.None).GetAwaiter().GetResult();
		if (!_bus.TryInvokeUltraLocalNoResponse(_command, CancellationToken.None, out _, out _))
		{
			throw new InvalidOperationException(
				"Ultra-local bus arm declined the command; the decomposition would measure a fallback path.");
		}
	}

	[GlobalCleanup]
	public void Cleanup() => _provider.Dispose();

	/// <summary>Replica of the published "Dispatch: Single command handler" arm.</summary>
	[Benchmark(Baseline = true, Description = "A. STANDARD published (rent + DispatchAsync + return)")]
	public Task<IMessageResult> A_Standard_Published()
	{
		var context = _factory.CreateContext();
		var task = _dispatcher.DispatchAsync(_command, context, CancellationToken.None);
		if (task.IsCompletedSuccessfully)
		{
			_factory.Return(context);
		}

		return task;
	}

	/// <summary>Replica of the published "Dispatch: Single command ultra-local API" arm.</summary>
	[Benchmark(Description = "B. ULTRA-LOCAL published (DispatchLocalAsync)")]
	public ValueTask B_UltraLocal_Published() => _directLocal.DispatchLocalAsync(_command, CancellationToken.None);

	/// <summary>A minus the context rent/return the CALLER (not the dispatcher) performs.</summary>
	[Benchmark(Description = "C. standard, pinned context (no rent/return)")]
	public Task<IMessageResult> C_Standard_PinnedContext()
		=> _dispatcher.DispatchAsync(_command, (IMessageContext)_pinnedContext, CancellationToken.None);

	/// <summary>C minus the IMessageContext to MessageContext type test and the extra interface frame.</summary>
	[Benchmark(Description = "D. standard, concrete-context overload")]
	public Task<IMessageResult> D_Standard_ConcreteOverload()
		=> _dispatcher.DispatchAsync(_command, _pinnedContext, CancellationToken.None);

	/// <summary>B minus the dispatcher frame: the bus call the ultra-local path terminates in.</summary>
	[Benchmark(Description = "E. ultra-local bus only (TryInvokeUltraLocalNoResponse)")]
	public ValueTask E_UltraLocal_BusOnly()
	{
		_ = _bus.TryInvokeUltraLocalNoResponse(_command, CancellationToken.None, out var invocation, out _);
		return invocation;
	}

	/// <summary>The handler itself, called directly. Everything above this is framework cost.</summary>
	[Benchmark(Description = "F. handler direct call (floor)")]
	public Task F_HandlerDirect() => _handler.HandleAsync(_command, CancellationToken.None);

	/// <summary>B called on the concrete Dispatcher, isolating the IDirectLocalDispatcher interface call.</summary>
	[Benchmark(Description = "G. ultra-local on concrete Dispatcher (no interface call)")]
	public ValueTask G_UltraLocal_Concrete() => _dispatcher.DispatchLocalAsync(_command, CancellationToken.None);

	/// <summary>
	/// The standard fast path's SHAPE, hand-built: ambient push, context init, handler, pop, cached Task.
	/// D minus this is the dispatch-info lookup + routing-fast check + invoker indirection + try/catch.
	/// </summary>
	[Benchmark(Description = "I. hand-built standard shape (push + ctx + handler + pop)")]
	public Task<IMessageResult> I_HandBuiltStandardShape()
	{
		var previous = MessageContextHolder.Current;
		MessageContextHolder.Current = _pinnedContext;
		try
		{
			((IMessageContext)_pinnedContext).Message = _command;
			var t = _handler.HandleAsync(_command, CancellationToken.None);
			if (t.IsCompletedSuccessfully)
			{
				MessageContextHolder.Current = previous;
				return _cachedResultTask;
			}

			MessageContextHolder.Current = previous;
			return _cachedResultTask;
		}
		catch (Exception)
		{
			MessageContextHolder.Current = previous;
			throw;
		}
	}

	/// <summary>I minus the ambient push/pop — prices the ambient write pair inside the real shape.</summary>
	[Benchmark(Description = "J. hand-built shape WITHOUT ambient push/pop")]
	public Task<IMessageResult> J_HandBuiltNoAmbient()
	{
		((IMessageContext)_pinnedContext).Message = _command;
		var t = _handler.HandleAsync(_command, CancellationToken.None);
		return t.IsCompletedSuccessfully ? _cachedResultTask : _cachedResultTask;
	}

	/// <summary>
	/// I, but the ambient POP is an ExecutionContext.Restore of the captured pre-push context instead of a
	/// second AsyncLocal write. Tests whether the pop's copy-on-write transition is avoidable.
	/// </summary>
	[Benchmark(Description = "K. hand-built shape, pop via ExecutionContext.Restore")]
	public Task<IMessageResult> K_HandBuiltRestorePop()
	{
		var ec = System.Threading.ExecutionContext.Capture();
		MessageContextHolder.Current = _pinnedContext;
		try
		{
			((IMessageContext)_pinnedContext).Message = _command;
			_ = _handler.HandleAsync(_command, CancellationToken.None);
			return _cachedResultTask;
		}
		finally
		{
			if (ec is not null)
			{
				System.Threading.ExecutionContext.Restore(ec);
			}
		}
	}

	/// <summary>ExecutionContext.Capture alone, to price K's added capture.</summary>
	[Benchmark(Description = "X6. ExecutionContext.Capture only")]
	public System.Threading.ExecutionContext? X6_EcCapture() => System.Threading.ExecutionContext.Capture();

	/// <summary>Caller-side context rent + return, alone.</summary>
	[Benchmark(Description = "X1. context rent + return only")]
	public IMessageContext X1_ContextRentReturn()
	{
		var context = _factory.CreateContext();
		_factory.Return(context);
		return context;
	}

	/// <summary>The ambient-context AsyncLocal push and pop, alone.</summary>
	[Benchmark(Description = "X2. ambient AsyncLocal push + pop only")]
	public IMessageContext? X2_AmbientPushPop()
	{
		var previous = MessageContextHolder.Current;
		MessageContextHolder.Current = _pinnedContext;
		MessageContextHolder.Current = previous;
		return previous;
	}

	/// <summary>The ambient-context read alone, to separate the read from the two writes in X2.</summary>
	[Benchmark(Description = "X3. ambient AsyncLocal read only")]
	public IMessageContext? X3_AmbientRead() => MessageContextHolder.Current;

	/// <summary>A single ambient write, to price one write against the pair in X2.</summary>
	[Benchmark(Description = "X4. ambient AsyncLocal single write")]
	public void X4_AmbientSingleWrite() => MessageContextHolder.Current = _pinnedContext;

	/// <summary>The context mutation InitializeDirectLocalContext performs under the Lean profile.</summary>
	[Benchmark(Description = "X5. context.Message assignment only")]
	public void X5_ContextMessageAssign() => ((IMessageContext)_pinnedContext).Message = _command;
}

internal sealed class DecompositionConfig : ManualConfig
{
	public DecompositionConfig() =>
		AddJob(Job.Default
			.WithId("decomp-inproc")
			.WithToolchain(InProcessEmitToolchain.Instance)
			.DontEnforcePowerPlan());
}
