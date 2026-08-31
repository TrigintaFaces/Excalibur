// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing;

namespace Excalibur.Dispatch.Analyzers.Tests;

/// <summary>
/// Shared harness for the Dispatch convention analyzers.
/// </summary>
/// <remarks>
/// <para>
/// The reference set is pinned deliberately. The testing harness otherwise defaults to a reference set
/// that predates APIs these analyzers reason about, and the resulting failure is not a missing test but a
/// <em>passing</em> one: the snippet fails to compile, the harness offers a ready-made
/// <c>DiagnosticResult.CompilerError(...)</c> to paste in, and pasting it turns the test green while the
/// analyzer was never consulted at all.
/// </para>
/// <para>
/// Diagnostic IDs are referenced here as string literals on purpose. The descriptors are
/// <c>internal</c> to the analyzer assembly, but the ID is the part a consumer pins in their
/// <c>.editorconfig</c> and their build log — so the ID string, not the descriptor field, is the contract
/// these locks are meant to bind.
/// </para>
/// </remarks>
internal static class AnalyzerTestHarness
{
	public const string DiExtensionWrongNamespace = "DISP101";
	public const string ExtensionClassIPrefix = "DISP102";
	public const string CancellationTokenOptionalInInterface = "DISP103";
	public const string NamespaceContainsCoreSegment = "DISP104";
	public const string MissingConfigureAwait = "DISP105";
	public const string BlockingCallInAsyncMethod = "DISP106";

	/// <summary>
	/// A minimal <c>IServiceCollection</c> declaration. <see cref="DiExtensionNamespaceAnalyzer"/> resolves
	/// the type by metadata name and bails out entirely when it is absent, so a snippet that omits this
	/// stub produces a vacuous green regardless of what else it contains.
	/// </summary>
	public const string ServiceCollectionStub = """
		namespace Microsoft.Extensions.DependencyInjection
		{
			public interface IServiceCollection { }
		}
		""";

	public static ReferenceAssemblies ReferenceAssemblies => Microsoft.CodeAnalysis.Testing.ReferenceAssemblies.Net.Net90;

	/// <summary>
	/// Builds a single-analyzer test over the supplied sources with the pinned reference set.
	/// </summary>
	public static CSharpAnalyzerTest<TAnalyzer, DefaultVerifier> For<TAnalyzer>(params string[] sources)
		where TAnalyzer : DiagnosticAnalyzer, new()
	{
		var test = new CSharpAnalyzerTest<TAnalyzer, DefaultVerifier>
		{
			ReferenceAssemblies = ReferenceAssemblies,
		};

		foreach (var source in sources)
		{
			test.TestState.Sources.Add(source);
		}

		return test;
	}

	/// <summary>
	/// Asserts the analyzer reports nothing at all for the supplied sources — the liveness half of every
	/// lock in this project. A guard asserted only on its safety half is satisfied by an analyzer that has
	/// been accidentally disabled, mis-registered, or short-circuited to a no-op.
	/// </summary>
	public static Task ShouldReportNothingAsync<TAnalyzer>(params string[] sources)
		where TAnalyzer : DiagnosticAnalyzer, new()
		=> For<TAnalyzer>(sources).RunAsync();

	public static DiagnosticResult Expect(string id, int location, params string[] arguments)
		=> new DiagnosticResult(id, DiagnosticSeverity.Warning)
			.WithLocation(location)
			.WithArguments(arguments);
}
