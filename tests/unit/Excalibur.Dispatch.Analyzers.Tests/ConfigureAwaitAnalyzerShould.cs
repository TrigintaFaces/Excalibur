// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Xunit;

namespace Excalibur.Dispatch.Analyzers.Tests;

/// <summary>
/// Locks for DISP105 — library code awaits with <c>ConfigureAwait(false)</c> so a continuation is never
/// forced back onto a caller's synchronization context.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class ConfigureAwaitAnalyzerShould
{
	[Fact]
	public async Task Flag_AnAwaitOnATask_WithoutConfigureAwait()
	{
		const string source = """
			using System.Threading.Tasks;

			namespace Excalibur.Sample
			{
				public sealed class Pipeline
				{
					public async Task RunAsync()
					{
						{|#0:await WorkAsync()|};
					}

					private static Task WorkAsync() => Task.CompletedTask;
				}
			}
			""";

		var test = AnalyzerTestHarness.For<ConfigureAwaitAnalyzer>(source);
		test.ExpectedDiagnostics.Add(
			AnalyzerTestHarness.Expect(AnalyzerTestHarness.MissingConfigureAwait, 0));

		await test.RunAsync();
	}

	/// <summary>
	/// <c>ValueTask</c> carries the same context-capture hazard as <c>Task</c>, and the framework's hot
	/// paths return it. An analyzer covering only <c>Task</c> would be silent exactly where allocation
	/// pressure pushed the code.
	/// </summary>
	[Fact]
	public async Task Flag_AnAwaitOnAValueTask_WithoutConfigureAwait()
	{
		const string source = """
			using System.Threading.Tasks;

			namespace Excalibur.Sample
			{
				public sealed class Pipeline
				{
					public async Task RunAsync()
					{
						{|#0:await WorkAsync()|};
					}

					private static ValueTask WorkAsync() => default;
				}
			}
			""";

		var test = AnalyzerTestHarness.For<ConfigureAwaitAnalyzer>(source);
		test.ExpectedDiagnostics.Add(
			AnalyzerTestHarness.Expect(AnalyzerTestHarness.MissingConfigureAwait, 0));

		await test.RunAsync();
	}

	/// <summary>
	/// The liveness arm, and the one that matters most here: an analyzer that reported on <em>every</em>
	/// await would satisfy the two arms above perfectly while making the rule unusable.
	/// </summary>
	[Fact]
	public async Task StaySilent_WhenTheAwaitAlreadyConfiguresAwait()
	{
		const string source = """
			using System.Threading.Tasks;

			namespace Excalibur.Sample
			{
				public sealed class Pipeline
				{
					public async Task RunAsync()
					{
						await WorkAsync().ConfigureAwait(false);
					}

					private static Task WorkAsync() => Task.CompletedTask;
				}
			}
			""";

		await AnalyzerTestHarness.ShouldReportNothingAsync<ConfigureAwaitAnalyzer>(source);
	}

	/// <summary>
	/// <c>ConfigureAwait(false)</c> is a library obligation, not a universal one. Consumer application code
	/// is entitled to capture its context, and a shipped analyzer that said otherwise would be noise in
	/// every build that installed it.
	/// </summary>
	[Fact]
	public async Task StaySilent_ForAwaitsOutsideTheFrameworkNamespaces()
	{
		const string source = """
			using System.Threading.Tasks;

			namespace Contoso.Ordering
			{
				public sealed class Pipeline
				{
					public async Task RunAsync()
					{
						await WorkAsync();
					}

					private static Task WorkAsync() => Task.CompletedTask;
				}
			}
			""";

		await AnalyzerTestHarness.ShouldReportNothingAsync<ConfigureAwaitAnalyzer>(source);
	}
}
