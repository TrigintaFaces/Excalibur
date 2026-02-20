// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Runtime.CompilerServices;

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;

namespace Excalibur.Dispatch.Benchmarks.Optimization;

/// <summary>
/// Benchmarks comparing LINQ-based operations with manual loop replacements in dispatch hot path.
/// </summary>
/// <remarks>
/// <para>
/// These benchmarks compare LINQ-based operations with manual loop replacements
/// used in the dispatch hot path.
/// </para>
/// <para>
/// Optimizations validated:
/// - FilteredDispatchMiddlewareInvoker: ContainsInterfaceWithName helper
/// - DefaultMiddlewareApplicabilityStrategy: ImplementsGenericInterface helper
/// - PipelineProfile: ImplementsGenericActionInterface helper
/// </para>
/// </remarks>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.HostProcess)]
public class LinqEliminationBenchmarks
{
	private Type[] _interfaces = null!;
	private Type _testType = null!;
	private Type _genericInterfaceDefinition = null!;

	[GlobalSetup]
	public void Setup()
	{
		_testType = typeof(TestActionHandler);
		_interfaces = _testType.GetInterfaces();
		_genericInterfaceDefinition = typeof(IGenericHandler<>);
	}

	#region ContainsInterfaceWithName Benchmarks

	/// <summary>
	/// LINQ .Any() with Contains check (what we replaced).
	/// </summary>
	[Benchmark(Baseline = true)]
	public bool ContainsInterface_Linq()
	{
		return _interfaces.Any(iface => iface.Name.Contains("Action", StringComparison.Ordinal));
	}

	/// <summary>
	/// Manual loop replacement (optimized).
	/// </summary>
	[Benchmark]
	public bool ContainsInterface_ManualLoop()
	{
		return ContainsInterfaceWithName(_interfaces, "Action");
	}

	/// <summary>
	/// Batch of 1000 LINQ checks.
	/// </summary>
	[Benchmark]
	public int ContainsInterface_Linq_Batch1000()
	{
		var count = 0;
		for (var i = 0; i < 1000; i++)
		{
			if (_interfaces.Any(iface => iface.Name.Contains("Action", StringComparison.Ordinal)))
			{
				count++;
			}
		}

		return count;
	}

	/// <summary>
	/// Batch of 1000 manual loop checks.
	/// </summary>
	[Benchmark]
	public int ContainsInterface_ManualLoop_Batch1000()
	{
		var count = 0;
		for (var i = 0; i < 1000; i++)
		{
			if (ContainsInterfaceWithName(_interfaces, "Action"))
			{
				count++;
			}
		}

		return count;
	}

	#endregion

	#region ImplementsGenericInterface Benchmarks

	/// <summary>
	/// LINQ .Any() with generic interface check (what we replaced).
	/// </summary>
	[Benchmark]
	public bool ImplementsGeneric_Linq()
	{
		return _interfaces.Any(iface =>
			iface.IsGenericType && iface.GetGenericTypeDefinition() == _genericInterfaceDefinition);
	}

	/// <summary>
	/// Manual loop replacement (optimized).
	/// </summary>
	[Benchmark]
	public bool ImplementsGeneric_ManualLoop()
	{
		return ImplementsGenericInterface(_testType, _genericInterfaceDefinition);
	}

	/// <summary>
	/// Batch of 1000 LINQ generic interface checks.
	/// </summary>
	[Benchmark]
	public int ImplementsGeneric_Linq_Batch1000()
	{
		var count = 0;
		for (var i = 0; i < 1000; i++)
		{
			if (_interfaces.Any(iface =>
					iface.IsGenericType && iface.GetGenericTypeDefinition() == _genericInterfaceDefinition))
			{
				count++;
			}
		}

		return count;
	}

	/// <summary>
	/// Batch of 1000 manual loop generic interface checks.
	/// </summary>
	[Benchmark]
	public int ImplementsGeneric_ManualLoop_Batch1000()
	{
		var count = 0;
		for (var i = 0; i < 1000; i++)
		{
			if (ImplementsGenericInterface(_testType, _genericInterfaceDefinition))
			{
				count++;
			}
		}

		return count;
	}

	#endregion

	#region Helper Methods (copied from production code)

	/// <summary>
	/// Checks if any interface name contains the specified substring.
	/// Uses manual loop to avoid LINQ iterator allocation.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static bool ContainsInterfaceWithName(Type[] interfaces, string nameSubstring)
	{
		foreach (var iface in interfaces)
		{
			if (iface.Name.Contains(nameSubstring, StringComparison.Ordinal))
			{
				return true;
			}
		}

		return false;
	}

	/// <summary>
	/// Checks if a type implements a specific generic interface definition.
	/// Uses manual loop to avoid LINQ iterator allocation.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static bool ImplementsGenericInterface(Type type, Type genericInterfaceDefinition)
	{
		var interfaces = type.GetInterfaces();
		foreach (var iface in interfaces)
		{
			if (iface.IsGenericType && iface.GetGenericTypeDefinition() == genericInterfaceDefinition)
			{
				return true;
			}
		}

		return false;
	}

	#endregion

	// Test types
	private interface IGenericHandler<T>;

	private interface IActionHandler;

	private sealed class TestActionHandler : IActionHandler, IGenericHandler<string>;
}
