// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace Excalibur.Dispatch.Extensions;

/// <summary>
/// Reflection helpers shared by the assembly-scanning entry points.
/// </summary>
internal static class AssemblyExtensions
{
	/// <summary>
	/// Gets the types an assembly can surface, keeping the ones that did load when some of its dependencies are missing.
	/// </summary>
	/// <param name="assembly"> The assembly to enumerate. </param>
	/// <returns> Every type that resolved. A partially loadable assembly yields its loadable subset instead of throwing. </returns>
	/// <remarks>
	/// <see cref="Assembly.GetTypes" /> throws <see cref="ReflectionTypeLoadException" /> outright when the assembly references a type it
	/// cannot load -- a plugin directory, a mismatched transitive dependency, an optional dependency the host did not deploy. Dropping the
	/// whole assembly there is how a handler or message type goes missing for a reason nothing reports.
	/// </remarks>
	[RequiresUnreferencedCode("Enumerates every type in the assembly, which trimming cannot statically preserve.")]
	public static IEnumerable<Type> GetLoadableTypes(this Assembly assembly)
	{
		ArgumentNullException.ThrowIfNull(assembly);

		try
		{
			return assembly.GetTypes();
		}
		catch (ReflectionTypeLoadException ex)
		{
			return ex.Types.Where(static t => t is not null)!;
		}
	}
}
