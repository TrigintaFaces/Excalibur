// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Xunit;

[assembly: AssemblyFixture(typeof(Excalibur.Dispatch.Tests.Conformance.Transport.ConformanceLivenessGate))]

namespace Excalibur.Dispatch.Tests.Conformance.Transport;

/// <summary>
/// Fails the run when external-broker transport conformance suites were selected and NOT ONE of their arms
/// executed. This is the liveness half of the suite: every other assertion here says "the bad thing did not
/// happen", and all of them are satisfied by a suite that did nothing at all.
/// </summary>
/// <remarks>
/// <para>
/// This is an assembly fixture rather than a <c>[Fact]</c> deliberately. A fact that reads the execution
/// ledger is ORDER-DEPENDENT: xUnit runs test collections in parallel and guarantees no ordering between
/// them, so such a fact can run before the broker suites and report a liveness failure in a perfectly
/// healthy run. A false RED on a gate is worse than the gap it closes, because the next person to see it
/// deletes the gate. An assembly fixture is disposed after the assembly's tests have run, which is the only
/// point at which the question "did anything execute?" has an answer.
/// </para>
/// <para>
/// It is silent when no broker suite was selected. The conformance CI matrix runs one transport at a time by
/// name, so an unconditional demand would fail a job that was never asked to run a broker at all. The gate
/// speaks only when broker suites were attempted and produced nothing.
/// </para>
/// </remarks>
public sealed class ConformanceLivenessGate : IAsyncDisposable
{
	/// <summary>
	/// Evaluates the liveness property and returns the failure message, or <see langword="null" /> when the
	/// run is live.
	/// </summary>
	/// <remarks>
	/// Separated from disposal so the gate's own non-vacuity proof can drive it directly, rather than
	/// asserting on a disposal side effect it cannot observe.
	/// </remarks>
	internal static string? EvaluateFailure() =>
		EvaluateFailure(
			ConformanceExecutionLedger.BrokerSuitesSelected,
			ConformanceExecutionLedger.BrokerArmsExecuted,
			ConformanceExecutionLedger.Describe());

	/// <summary>
	/// The liveness rule itself, as a pure function of the counts.
	/// </summary>
	/// <remarks>
	/// Pure on purpose. The ledger is process-wide static state shared with the live run, so a proof that
	/// drove the gate by mutating it would corrupt the very ledger the real gate reads -- the proof could
	/// turn a healthy run RED, or erase the evidence of a broken one. Passing the counts in means the gate's
	/// own tests cannot touch the run they are executing inside.
	/// </remarks>
	internal static string? EvaluateFailure(int brokerSuitesSelected, int brokerArmsExecuted, string description)
	{
		if (brokerSuitesSelected == 0)
		{
			// No external-broker suite was selected (a filtered run). Nothing to assert.
			return null;
		}

		if (brokerArmsExecuted > 0)
		{
			return null;
		}

		return "TRANSPORT CONFORMANCE DID NOT VERIFY ANYTHING." + Environment.NewLine
			+ $"{brokerSuitesSelected} external-broker conformance suite(s) were "
			+ "selected in this run and NOT ONE conformance arm executed -- every one skipped because its "
			+ "transport could not be initialized." + Environment.NewLine + Environment.NewLine
			+ "This is reported as a FAILURE because the alternative is silence. Skips are not failures, so "
			+ "a run in which nothing was verified reports the same result as a run in which every transport "
			+ "conformed. That is the state this gate exists to make visible." + Environment.NewLine
			+ Environment.NewLine
			+ "Usually this means the container runtime is not running. Start it and re-run; the arms then "
			+ "execute and this gate goes quiet." + Environment.NewLine + Environment.NewLine
			+ description;
	}

	/// <inheritdoc />
	public ValueTask DisposeAsync()
	{
		var failure = EvaluateFailure();

		return failure is null
			? ValueTask.CompletedTask
			: throw new InvalidOperationException(failure);
	}
}
