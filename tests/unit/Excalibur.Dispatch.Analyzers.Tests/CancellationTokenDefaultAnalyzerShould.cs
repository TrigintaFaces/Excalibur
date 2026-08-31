// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Xunit;

namespace Excalibur.Dispatch.Analyzers.Tests;

/// <summary>
/// Locks for DISP103 — a framework interface must require its <c>CancellationToken</c>, because an
/// optional one lets a caller build a non-cancellable chain without ever writing the omission down.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class CancellationTokenDefaultAnalyzerShould
{
	[Fact]
	public async Task Flag_AnOptionalCancellationTokenOnAnInterfaceMethod()
	{
		const string source = """
			using System.Threading;
			using System.Threading.Tasks;

			namespace Excalibur.Sample
			{
				public interface IWidgetStore
				{
					Task SaveAsync(string id, CancellationToken {|#0:cancellationToken|} = default);
				}
			}
			""";

		var test = AnalyzerTestHarness.For<CancellationTokenDefaultAnalyzer>(source);
		test.ExpectedDiagnostics.Add(
			AnalyzerTestHarness.Expect(
				AnalyzerTestHarness.CancellationTokenOptionalInInterface,
				0,
				"cancellationToken",
				"IWidgetStore",
				"SaveAsync"));

		await test.RunAsync();
	}

	/// <summary>
	/// The liveness arm. Without it, an analyzer that reports on every <c>CancellationToken</c> parameter —
	/// or on none at all — passes the safety arm just as convincingly as a correct one.
	/// </summary>
	[Fact]
	public async Task StaySilent_WhenTheCancellationTokenIsRequired()
	{
		const string source = """
			using System.Threading;
			using System.Threading.Tasks;

			namespace Excalibur.Sample
			{
				public interface IWidgetStore
				{
					Task SaveAsync(string id, CancellationToken cancellationToken);
				}
			}
			""";

		await AnalyzerTestHarness.ShouldReportNothingAsync<CancellationTokenDefaultAnalyzer>(source);
	}

	/// <summary>
	/// The rule is scoped to interfaces. A concrete class may carry a defaulted token — that is how an
	/// implementation offers a convenience overload without weakening the contract it implements.
	/// </summary>
	[Fact]
	public async Task StaySilent_ForADefaultedTokenOnAConcreteClass()
	{
		const string source = """
			using System.Threading;
			using System.Threading.Tasks;

			namespace Excalibur.Sample
			{
				public sealed class WidgetStore
				{
					public Task SaveAsync(string id, CancellationToken cancellationToken = default) => Task.CompletedTask;
				}
			}
			""";

		await AnalyzerTestHarness.ShouldReportNothingAsync<CancellationTokenDefaultAnalyzer>(source);
	}

	/// <summary>
	/// The rule is a framework convention, not a universal one — it must not fire on a consumer's own
	/// interfaces, where a defaulted token is an ordinary and reasonable choice.
	/// </summary>
	[Fact]
	public async Task StaySilent_ForAnInterfaceOutsideTheFrameworkNamespaces()
	{
		const string source = """
			using System.Threading;
			using System.Threading.Tasks;

			namespace Contoso.Ordering
			{
				public interface IOrderStore
				{
					Task SaveAsync(string id, CancellationToken cancellationToken = default);
				}
			}
			""";

		await AnalyzerTestHarness.ShouldReportNothingAsync<CancellationTokenDefaultAnalyzer>(source);
	}

	/// <summary>
	/// A method may carry more than one defaulted parameter; only the token is this rule's concern.
	/// </summary>
	[Fact]
	public async Task Flag_OnlyTheTokenWhenOtherParametersAlsoCarryDefaults()
	{
		const string source = """
			using System.Threading;
			using System.Threading.Tasks;

			namespace Excalibur.Sample
			{
				public interface IWidgetStore
				{
					Task SaveAsync(string id, int retries = 3, CancellationToken {|#0:cancellationToken|} = default);
				}
			}
			""";

		var test = AnalyzerTestHarness.For<CancellationTokenDefaultAnalyzer>(source);
		test.ExpectedDiagnostics.Add(
			AnalyzerTestHarness.Expect(
				AnalyzerTestHarness.CancellationTokenOptionalInInterface,
				0,
				"cancellationToken",
				"IWidgetStore",
				"SaveAsync"));

		await test.RunAsync();
	}
}
