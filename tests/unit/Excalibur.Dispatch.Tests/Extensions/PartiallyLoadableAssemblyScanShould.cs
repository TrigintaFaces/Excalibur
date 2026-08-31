// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Reflection;

using Excalibur.Dispatch.Extensions;

namespace Excalibur.Dispatch.Tests.Extensions;

/// <summary>
/// Assembly scanning must survive an assembly it cannot fully load.
/// </summary>
/// <remarks>
/// <see cref="Assembly.GetTypes" /> throws outright when an assembly references a type it cannot load --
/// a plugin directory, a mismatched transitive dependency, an optional dependency the host did not
/// deploy. Unguarded, that takes down registration at startup and names a type the consumer may never
/// have heard of, instead of registering the handlers that did load.
/// </remarks>
public sealed class PartiallyLoadableAssemblyScanShould
{
	private sealed class PartiallyLoadableAssembly : Assembly
	{
		public override Type[] GetTypes() =>
			throw new ReflectionTypeLoadException(
				[typeof(string), null, typeof(int)],
				[null, new TypeLoadException("the missing one"), null]);
	}

	private sealed class FullyLoadableAssembly : Assembly
	{
		public override Type[] GetTypes() => [typeof(string), typeof(int)];
	}

	[Fact]
	public void KeepTheTypesThatDidLoad()
	{
		// SAFETY. The unguarded call propagates ReflectionTypeLoadException and the whole scan dies.
		Type[] loaded = [.. new PartiallyLoadableAssembly().GetLoadableTypes()];

		loaded.ShouldBe([typeof(string), typeof(int)]);
	}

	[Fact]
	public void StillReturnEveryTypeWhenTheAssemblyLoadsCleanly()
	{
		// LIVENESS. A guard that swallowed everything, or returned empty, would pass the arm above.
		Type[] loaded = [.. new FullyLoadableAssembly().GetLoadableTypes()];

		loaded.ShouldBe([typeof(string), typeof(int)]);
	}
}
