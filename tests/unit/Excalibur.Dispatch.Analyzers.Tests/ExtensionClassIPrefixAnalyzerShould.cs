// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Xunit;

namespace Excalibur.Dispatch.Analyzers.Tests;

/// <summary>
/// Locks for DISP102 — the <c>I</c> prefix is reserved for interfaces, so an extension-method container
/// named <c>IFooExtensions</c> is renamed to <c>FooExtensions</c>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class ExtensionClassIPrefixAnalyzerShould
{
	[Fact]
	public async Task Flag_AnExtensionClassWhoseNameUsesTheInterfacePrefix()
	{
		const string source = """
			namespace Excalibur.Sample
			{
				public static class {|#0:IDispatcherExtensions|}
				{
					public static string Describe(this string value) => value;
				}
			}
			""";

		var test = AnalyzerTestHarness.For<ExtensionClassIPrefixAnalyzer>(source);
		test.ExpectedDiagnostics.Add(
			AnalyzerTestHarness.Expect(
				AnalyzerTestHarness.ExtensionClassIPrefix,
				0,
				"IDispatcherExtensions",
				"DispatcherExtensions"));

		await test.RunAsync();
	}

	/// <summary>
	/// The liveness arm: a correctly named extension class must survive untouched.
	/// </summary>
	[Fact]
	public async Task StaySilent_ForACorrectlyNamedExtensionClass()
	{
		const string source = """
			namespace Excalibur.Sample
			{
				public static class DispatcherExtensions
				{
					public static string Describe(this string value) => value;
				}
			}
			""";

		await AnalyzerTestHarness.ShouldReportNothingAsync<ExtensionClassIPrefixAnalyzer>(source);
	}

	/// <summary>
	/// The analyzer's subject is the extension-method container, not every static class. A static holder
	/// with an <c>I</c> prefix and no extension methods is not what the rule is about.
	/// </summary>
	[Fact]
	public async Task StaySilent_ForAnIPrefixedStaticClassWithNoExtensionMethods()
	{
		const string source = """
			namespace Excalibur.Sample
			{
				public static class IdentityConstants
				{
					public const string Scheme = "excalibur";
				}

				public static class IReadOnlyMarkers
				{
					public static string Describe(string value) => value;
				}
			}
			""";

		await AnalyzerTestHarness.ShouldReportNothingAsync<ExtensionClassIPrefixAnalyzer>(source);
	}

	/// <summary>
	/// The rule is <c>I</c> followed by an upper-case letter. A name that merely begins with a lower-case
	/// <c>I</c>-word — <c>Instrumentation</c>, <c>Identity</c> — is ordinary English, not a prefix.
	/// </summary>
	[Fact]
	public async Task StaySilent_WhenTheLetterAfterIIsLowerCase()
	{
		const string source = """
			namespace Excalibur.Sample
			{
				public static class InstrumentationExtensions
				{
					public static string Describe(this string value) => value;
				}
			}
			""";

		await AnalyzerTestHarness.ShouldReportNothingAsync<ExtensionClassIPrefixAnalyzer>(source);
	}
}
