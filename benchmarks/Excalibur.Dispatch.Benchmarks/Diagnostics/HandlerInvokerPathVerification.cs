// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Reflection;

using Excalibur.Dispatch.Delivery.Handlers;

namespace Excalibur.Dispatch.Benchmarks.Diagnostics;

/// <summary>Managed fixture controls; these are not performance or Native AOT measurements.</summary>
internal static class HandlerInvokerPathVerification
{
	private static readonly MethodInfo Lookup = typeof(HandlerInvokerRegistry).GetMethod(
		"TryGetRegisteredInvoker", BindingFlags.NonPublic | BindingFlags.Static, null,
		[typeof(Type), typeof(Type), typeof(Func<object, IDispatchMessage, CancellationToken, Task<object?>>).MakeByRefType()], null)
		?? throw new InvalidOperationException("The exact registered-pair lookup contract changed.");

	internal static async Task RunAsync(string mode)
	{
		Require(!HandlerInvokerRegistry.IsCacheFrozen && !HandlerInvoker.IsCacheFrozen, "fresh mutable process");
		var sentinelCalls = 0;
		HandlerInvokerRegistry.RegisterInvoker<SentinelHandler, IDispatchMessage, object?>((_, _, _) =>
		{
			sentinelCalls++;
			return Task.FromResult<object?>(19);
		});
		var sentinel = GetRegistered((typeof(SentinelHandler), typeof(IDispatchMessage)))
			?? throw new InvalidOperationException("Sentinel was not registered.");
		var fixture = new HandlerInvokerPathBenchmarks();
		if (mode == "normal")
		{
			ExpectInvalidOperation(HandlerInvokerPathBenchmarks.VerifyFallbackGuard);
			for (var iteration = 0; iteration < 12; iteration++)
			{
				// Repeated setup without cleanup must remain bounded to the same private pair.
				await fixture.GlobalSetup().ConfigureAwait(false);
				await fixture.GlobalSetup().ConfigureAwait(false);
				var registered = GetRegistered(HandlerInvokerPathBenchmarks.RegisteredPair);
				Require(registered is not null, "owned pair registered");
				Require(GetRegistered(HandlerInvokerPathBenchmarks.RuntimePair) is null, "runtime pair remains unregistered");
				Require(Equals(await fixture.Invoke_PrecompiledCacheHit().ConfigureAwait(false), 1), "registered result");
				Require(Equals(await fixture.Invoke_RuntimeFallbackCached().ConfigureAwait(false), 2), "runtime result");
				Require(ReferenceEquals(registered, GetRegistered(HandlerInvokerPathBenchmarks.RegisteredPair)), "owned delegate identity retained during calls");
				Require(GetRegistered(HandlerInvokerPathBenchmarks.RuntimePair) is null, "runtime pair still unregistered after calls");
				fixture.GlobalCleanup();
				fixture.GlobalCleanup();
				Require(!fixture.HoldsFixtureState, "cleanup releases fixture references");
				Require(ReferenceEquals(registered, GetRegistered(HandlerInvokerPathBenchmarks.RegisteredPair)), "cleanup preserves owned registration");
				Require(ReferenceEquals(sentinel, GetRegistered((typeof(SentinelHandler), typeof(IDispatchMessage)))), "foreign registration survives lifecycle");
			}
		}
		else if (mode is "frozen-registry" or "frozen-invoker" or "cleanup-frozen-registry" or "cleanup-frozen-invoker")
		{
			var cleanup = mode.StartsWith("cleanup-", StringComparison.Ordinal);
			if (cleanup)
			{
				await fixture.GlobalSetup().ConfigureAwait(false);
			}

			if (mode.EndsWith("registry", StringComparison.Ordinal))
			{
				HandlerInvokerRegistry.FreezeCache();
			}
			else
			{
				HandlerInvoker.FreezeCache();
			}

			var registryFrozen = HandlerInvokerRegistry.IsCacheFrozen;
			var invokerFrozen = HandlerInvoker.IsCacheFrozen;
			if (!cleanup)
			{
				var refused = false;
				try
				{
					await fixture.GlobalSetup().ConfigureAwait(false);
				}
				catch (InvalidOperationException)
				{
					refused = true;
				}

				Require(refused && !fixture.HoldsFixtureState, "setup refuses frozen caches before allocating fixture state");
				Require(GetRegistered(HandlerInvokerPathBenchmarks.RegisteredPair) is null, "refused setup did not register owned pair");
			}

			ExpectInvalidOperation(fixture.GlobalCleanup);
			Require(!fixture.HoldsFixtureState, "failed cleanup still releases fixture references");
			Require(registryFrozen == HandlerInvokerRegistry.IsCacheFrozen && invokerFrozen == HandlerInvoker.IsCacheFrozen, "freeze state preserved");
		}
		else
		{
			throw new ArgumentException("Expected normal, frozen-registry, frozen-invoker, cleanup-frozen-registry, or cleanup-frozen-invoker.", nameof(mode));
		}

		Require(ReferenceEquals(sentinel, GetRegistered((typeof(SentinelHandler), typeof(IDispatchMessage)))), "sentinel identity preserved");
		Require(Equals(await sentinel(new SentinelHandler(), null!, CancellationToken.None).ConfigureAwait(false), 19) && sentinelCalls == 1, "sentinel behavior preserved");
		Console.WriteLine($"HandlerInvoker fixture controls passed: {mode}; managed functional controls only.");
	}

	private static Func<object, IDispatchMessage, CancellationToken, Task<object?>>? GetRegistered((Type Handler, Type Message) pair)
	{
		object?[] arguments = [pair.Handler, pair.Message, null];
		var found = (bool)(Lookup.Invoke(null, arguments) ?? throw new InvalidOperationException("Lookup returned null."));
		var result = arguments[2] as Func<object, IDispatchMessage, CancellationToken, Task<object?>>;
		Require(found == (result is not null), "lookup result consistency");
		return result;
	}

	private static void ExpectInvalidOperation(Action action)
	{
		try
		{
			action();
		}
		catch (InvalidOperationException)
		{
			return;
		}

		throw new InvalidOperationException("Expected InvalidOperationException.");
	}

	private static void Require(bool condition, string description)
	{
		if (!condition)
		{
			throw new InvalidOperationException($"Fixture control failed: {description}");
		}
	}

	private sealed class SentinelHandler
	{
	}
}
