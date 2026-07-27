// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.CodeAnalysis.Testing;

namespace Excalibur.Workflows.Analyzers.Tests;

/// <summary>
/// Shared harness defaults for the workflow analyzer and code-fix tests.
/// </summary>
internal static class AnalyzerTestDefaults
{
	/// <summary>
	/// The reference assemblies every analyzer/code-fix test compiles its snippet against.
	/// </summary>
	/// <remarks>
	/// <para>
	/// This MUST be pinned to a modern .NET reference set. The testing harness otherwise defaults to a
	/// reference set that predates the very APIs this analyzer exists to flag: <c>Random.Shared</c> (.NET 6+),
	/// for example, simply does not exist there.
	/// </para>
	/// <para>
	/// The failure mode is not a missing test — it is a <em>passing</em> one. Against the unpinned default a
	/// snippet using <c>Random.Shared</c> fails to compile with <c>CS0117</c>, and the harness's own failure
	/// message offers the author a ready-made <c>DiagnosticResult.CompilerError("CS0117")</c> line to paste in.
	/// Pasting it turns the test green while asserting nothing whatsoever about the analyzer: the symbol under
	/// test never existed, so the analyzer was never consulted. A green test then certifies an analyzer that
	/// was never run against the API it is supposed to catch.
	/// </para>
	/// <para>
	/// Pinned to .NET 9, the highest reference set the pinned testing package provides.
	/// </para>
	/// </remarks>
	public static ReferenceAssemblies ReferenceAssemblies => Microsoft.CodeAnalysis.Testing.ReferenceAssemblies.Net.Net90;
}
