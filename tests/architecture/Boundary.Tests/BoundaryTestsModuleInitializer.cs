// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Reflection;
using System.Runtime.CompilerServices;

namespace Boundary.Tests;

/// <summary>
/// Deterministically force-loads every <c>Excalibur.*</c> framework assembly into the test AppDomain BEFORE
/// any test runs, so the architecture rules — which enumerate <c>AppDomain.CurrentDomain.GetAssemblies()</c>
/// and NetArchTest's <c>Types.InCurrentDomain()</c> — evaluate over the COMPLETE, order-independent set.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why:</b> <c>GetAssemblies()</c> / <c>InCurrentDomain()</c> return only the assemblies ALREADY loaded
/// at the instant they are called, and a referenced assembly is not loaded until one of its types is first
/// used. Which assemblies were loaded therefore depended on test execution ORDER and PARALLELISM, so a
/// boundary rule ran over a PARTIAL type set and the verdict flipped across identical shard invocations —
/// and worse, several tests then <em>vacuously passed</em> by examining zero types when their target
/// assembly happened to be unloaded. A <see cref="ModuleInitializerAttribute">module initializer</see> runs
/// once, before any test, and force-loads the full framework set, making the enumeration complete and
/// deterministic regardless of order/parallelism.
/// </para>
/// <para>
/// The project references every framework project (see <c>Boundary.Tests.csproj</c>), so MSBuild copies the
/// framework assemblies AND their complete transitive dependency closure into this test's output directory.
/// Force-loading the <c>Excalibur.*</c> assemblies from that directory is therefore sufficient: reflecting
/// over their types (<c>GetTypes()</c>) resolves every transitive dependency from the same directory, so the
/// guards evaluate over the complete type set and never crash on a missing dependency.
/// </para>
/// </remarks>
internal static class BoundaryTestsModuleInitializer
{
    [ModuleInitializer]
    public static void ForceLoadFrameworkAssemblies()
    {
        var baseDir = AppContext.BaseDirectory;

        foreach (var dll in Directory.EnumerateFiles(baseDir, "Excalibur*.dll", SearchOption.TopDirectoryOnly))
        {
            var simpleName = Path.GetFileNameWithoutExtension(dll);
            var alreadyLoaded = AppDomain.CurrentDomain.GetAssemblies()
                .Any(a => string.Equals(a.GetName().Name, simpleName, StringComparison.OrdinalIgnoreCase));
            if (alreadyLoaded)
            {
                continue;
            }

            try
            {
                _ = Assembly.LoadFrom(dll);
            }
            catch (Exception ex) when (ex is BadImageFormatException or FileLoadException or IOException)
            {
                // A non-managed / unloadable DLL is not a framework assembly under test — skip it. A
                // genuinely MISSING target assembly is caught at the per-test fail-loud assertions, not here.
            }
        }
    }
}
