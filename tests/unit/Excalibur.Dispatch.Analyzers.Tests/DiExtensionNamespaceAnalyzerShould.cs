// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Xunit;

namespace Excalibur.Dispatch.Analyzers.Tests;

/// <summary>
/// Locks for DISP101 — a static class carrying <c>IServiceCollection</c> extension methods belongs in the
/// <c>Microsoft.Extensions.DependencyInjection</c> namespace so consumers reach it without an extra using.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class DiExtensionNamespaceAnalyzerShould
{
	[Fact]
	public async Task Flag_AServiceCollectionExtensionClass_OutsideTheDependencyInjectionNamespace()
	{
		const string source = """
			using Microsoft.Extensions.DependencyInjection;

			namespace Excalibur.Sample
			{
				public static class {|#0:SampleServiceCollectionExtensions|}
				{
					public static IServiceCollection AddSample(this IServiceCollection services) => services;
				}
			}
			""";

		var test = AnalyzerTestHarness.For<DiExtensionNamespaceAnalyzer>(
			AnalyzerTestHarness.ServiceCollectionStub,
			source);
		test.ExpectedDiagnostics.Add(
			AnalyzerTestHarness.Expect(
				AnalyzerTestHarness.DiExtensionWrongNamespace,
				0,
				"SampleServiceCollectionExtensions",
				"Excalibur.Sample"));

		await test.RunAsync();
	}

	/// <summary>
	/// The liveness arm. An analyzer that reports on every static class — or one that has silently stopped
	/// running — is indistinguishable from a correct one under the safety arm alone.
	/// </summary>
	[Fact]
	public async Task StaySilent_WhenTheExtensionClassIsAlreadyInTheDependencyInjectionNamespace()
	{
		const string source = """
			namespace Microsoft.Extensions.DependencyInjection
			{
				public static class SampleServiceCollectionExtensions
				{
					public static IServiceCollection AddSample(this IServiceCollection services) => services;
				}
			}
			""";

		await AnalyzerTestHarness.ShouldReportNothingAsync<DiExtensionNamespaceAnalyzer>(
			AnalyzerTestHarness.ServiceCollectionStub,
			source);
	}

	/// <summary>
	/// Guards the first-parameter check. A static class full of extension methods on unrelated types must
	/// not be dragged into <c>Microsoft.Extensions.DependencyInjection</c>.
	/// </summary>
	[Fact]
	public async Task StaySilent_WhenTheExtendedTypeIsNotIServiceCollection()
	{
		const string source = """
			namespace Excalibur.Sample
			{
				public static class StringExtensions
				{
					public static string Shout(this string value) => value + "!";
				}
			}
			""";

		await AnalyzerTestHarness.ShouldReportNothingAsync<DiExtensionNamespaceAnalyzer>(
			AnalyzerTestHarness.ServiceCollectionStub,
			source);
	}

	/// <summary>
	/// Guards the <c>IsStatic</c> gate: an instance method that merely takes an <c>IServiceCollection</c>
	/// is a registration helper, not an extension, and is none of this analyzer's business.
	/// </summary>
	[Fact]
	public async Task StaySilent_ForANonStaticClassThatMerelyAcceptsAServiceCollection()
	{
		const string source = """
			using Microsoft.Extensions.DependencyInjection;

			namespace Excalibur.Sample
			{
				public sealed class Registrar
				{
					public IServiceCollection Register(IServiceCollection services) => services;
				}
			}
			""";

		await AnalyzerTestHarness.ShouldReportNothingAsync<DiExtensionNamespaceAnalyzer>(
			AnalyzerTestHarness.ServiceCollectionStub,
			source);
	}
}
