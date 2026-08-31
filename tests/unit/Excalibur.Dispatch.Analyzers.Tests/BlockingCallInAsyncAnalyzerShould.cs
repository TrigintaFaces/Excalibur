// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Xunit;

namespace Excalibur.Dispatch.Analyzers.Tests;

/// <summary>
/// Locks for DISP106 — an async method that blocks on a task starves the pool it is running on, and in a
/// dispatch pipeline that pool is the one serving every other message in flight.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class BlockingCallInAsyncAnalyzerShould
{
	[Fact]
	public async Task Flag_TaskResult_InsideAnAsyncMethod()
	{
		const string source = """
			using System.Threading.Tasks;

			namespace Excalibur.Sample
			{
				public sealed class Handler
				{
					public async Task<int> RunAsync()
					{
						await Task.Yield();
						return WorkAsync().{|#0:Result|};
					}

					private static Task<int> WorkAsync() => Task.FromResult(1);
				}
			}
			""";

		var test = AnalyzerTestHarness.For<BlockingCallInAsyncAnalyzer>(source);
		test.ExpectedDiagnostics.Add(
			AnalyzerTestHarness.Expect(AnalyzerTestHarness.BlockingCallInAsyncMethod, 0, "Result"));

		await test.RunAsync();
	}

	[Fact]
	public async Task Flag_TaskWait_InsideAnAsyncMethod()
	{
		const string source = """
			using System.Threading.Tasks;

			namespace Excalibur.Sample
			{
				public sealed class Handler
				{
					public async Task RunAsync()
					{
						await Task.Yield();
						WorkAsync().{|#0:Wait|}();
					}

					private static Task WorkAsync() => Task.CompletedTask;
				}
			}
			""";

		var test = AnalyzerTestHarness.For<BlockingCallInAsyncAnalyzer>(source);
		test.ExpectedDiagnostics.Add(
			AnalyzerTestHarness.Expect(AnalyzerTestHarness.BlockingCallInAsyncMethod, 0, "Wait"));

		await test.RunAsync();
	}

	/// <summary>
	/// <c>GetAwaiter().GetResult()</c> is the form people reach for precisely because it does not look
	/// like blocking. It blocks. This arm also pins that the intervening <c>GetAwaiter</c> draws nothing:
	/// exactly one diagnostic, on the call that does the harm.
	/// </summary>
	[Fact]
	public async Task Flag_GetAwaiterGetResult_InsideAnAsyncMethod()
	{
		const string source = """
			using System.Threading.Tasks;

			namespace Excalibur.Sample
			{
				public sealed class Handler
				{
					public async Task<int> RunAsync()
					{
						await Task.Yield();
						return WorkAsync().GetAwaiter().{|#0:GetResult|}();
					}

					private static Task<int> WorkAsync() => Task.FromResult(1);
				}
			}
			""";

		var test = AnalyzerTestHarness.For<BlockingCallInAsyncAnalyzer>(source);
		test.ExpectedDiagnostics.Add(
			AnalyzerTestHarness.Expect(AnalyzerTestHarness.BlockingCallInAsyncMethod, 0, "GetResult"));

		await test.RunAsync();
	}

	/// <summary>
	/// The liveness arm. A synchronous method has no <c>await</c> available to it, so blocking there is
	/// the only thing it can do; the rule is about async methods that had a choice.
	/// </summary>
	[Fact]
	public async Task StaySilent_ForBlockingCallsInASynchronousMethod()
	{
		const string source = """
			using System.Threading.Tasks;

			namespace Excalibur.Sample
			{
				public sealed class Handler
				{
					public int Run() => WorkAsync().Result;

					private static Task<int> WorkAsync() => Task.FromResult(1);
				}
			}
			""";

		await AnalyzerTestHarness.ShouldReportNothingAsync<BlockingCallInAsyncAnalyzer>(source);
	}

	/// <summary>
	/// A member merely <em>named</em> <c>Result</c> on an unrelated type is not a blocking call. Without
	/// this arm the analyzer could degrade to a bare name match and every arm above would still pass.
	/// </summary>
	[Fact]
	public async Task StaySilent_ForAPropertyNamedResultOnANonTaskType()
	{
		const string source = """
			using System.Threading.Tasks;

			namespace Excalibur.Sample
			{
				public sealed class Outcome
				{
					public int Result { get; init; }
				}

				public sealed class Handler
				{
					public async Task<int> RunAsync()
					{
						await Task.Yield();
						return new Outcome().Result;
					}
				}
			}
			""";

		await AnalyzerTestHarness.ShouldReportNothingAsync<BlockingCallInAsyncAnalyzer>(source);
	}

	/// <summary>
	/// The rule is scoped to framework code. Blocking in a consumer's own async method is their call to
	/// make, and a shipped analyzer that graded it would be lecturing from inside their build.
	/// </summary>
	[Fact]
	public async Task StaySilent_ForBlockingCallsOutsideTheFrameworkNamespaces()
	{
		const string source = """
			using System.Threading.Tasks;

			namespace Contoso.Ordering
			{
				public sealed class Handler
				{
					public async Task<int> RunAsync()
					{
						await Task.Yield();
						return WorkAsync().Result;
					}

					private static Task<int> WorkAsync() => Task.FromResult(1);
				}
			}
			""";

		await AnalyzerTestHarness.ShouldReportNothingAsync<BlockingCallInAsyncAnalyzer>(source);
	}
}
