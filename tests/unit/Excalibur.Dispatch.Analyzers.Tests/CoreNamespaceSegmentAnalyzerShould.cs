// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Xunit;

namespace Excalibur.Dispatch.Analyzers.Tests;

/// <summary>
/// Locks for DISP104 — framework namespaces carry no <c>.Core.</c> segment.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class CoreNamespaceSegmentAnalyzerShould
{
	[Fact]
	public async Task Flag_AnInteriorCoreSegment_AndSuggestTheNamespaceWithoutIt()
	{
		const string source = """
			namespace {|#0:Excalibur.Core.Messaging|}
			{
				public sealed class Widget { }
			}
			""";

		var test = AnalyzerTestHarness.For<CoreNamespaceSegmentAnalyzer>(source);
		test.ExpectedDiagnostics.Add(
			AnalyzerTestHarness.Expect(
				AnalyzerTestHarness.NamespaceContainsCoreSegment,
				0,
				"Excalibur.Core.Messaging",
				"Excalibur.Messaging"));

		await test.RunAsync();
	}

	[Fact]
	public async Task Flag_ATrailingCoreSegment()
	{
		const string source = """
			namespace {|#0:Excalibur.Messaging.Core|}
			{
				public sealed class Widget { }
			}
			""";

		var test = AnalyzerTestHarness.For<CoreNamespaceSegmentAnalyzer>(source);
		test.ExpectedDiagnostics.Add(
			AnalyzerTestHarness.Expect(
				AnalyzerTestHarness.NamespaceContainsCoreSegment,
				0,
				"Excalibur.Messaging.Core",
				"Excalibur.Messaging"));

		await test.RunAsync();
	}

	/// <summary>
	/// File-scoped and block-bodied namespaces are separate syntax kinds; the analyzer registers for both,
	/// and this arm is what keeps that true. Modern framework files use the file-scoped form almost
	/// exclusively, so a registration covering only the block form would be inert where it matters most.
	/// </summary>
	[Fact]
	public async Task Flag_ACoreSegment_InAFileScopedNamespace()
	{
		const string source = """
			namespace {|#0:Dispatch.Core.Pipeline|};

			public sealed class Widget { }
			""";

		var test = AnalyzerTestHarness.For<CoreNamespaceSegmentAnalyzer>(source);
		test.ExpectedDiagnostics.Add(
			AnalyzerTestHarness.Expect(
				AnalyzerTestHarness.NamespaceContainsCoreSegment,
				0,
				"Dispatch.Core.Pipeline",
				"Dispatch.Pipeline"));

		await test.RunAsync();
	}

	/// <summary>
	/// The liveness arm: a conventional framework namespace draws no diagnostic.
	/// </summary>
	[Fact]
	public async Task StaySilent_ForANamespaceWithNoCoreSegment()
	{
		const string source = """
			namespace Excalibur.Messaging.Pipeline;

			public sealed class Widget { }
			""";

		await AnalyzerTestHarness.ShouldReportNothingAsync<CoreNamespaceSegmentAnalyzer>(source);
	}

	/// <summary>
	/// This is a framework naming convention, and it must not be imposed on a consumer who has every right
	/// to a <c>.Core.</c> segment of their own.
	/// </summary>
	[Fact]
	public async Task StaySilent_ForACoreSegmentOutsideTheFrameworkNamespaces()
	{
		const string source = """
			namespace Contoso.Core.Ordering;

			public sealed class Widget { }
			""";

		await AnalyzerTestHarness.ShouldReportNothingAsync<CoreNamespaceSegmentAnalyzer>(source);
	}

	/// <summary>
	/// <c>Core</c> must be matched as a whole segment. A namespace whose segment merely begins with those
	/// four letters — <c>CoreographyEngine</c> here — contains no <c>.Core.</c> segment at all, and a rule
	/// that fired on it would be a substring match wearing a convention's clothes.
	/// </summary>
	[Fact]
	public async Task StaySilent_WhenCoreIsOnlyThePrefixOfALongerSegment()
	{
		const string source = """
			namespace Excalibur.CoreographyEngine.Steps;

			public sealed class Widget { }
			""";

		await AnalyzerTestHarness.ShouldReportNothingAsync<CoreNamespaceSegmentAnalyzer>(source);
	}
}
