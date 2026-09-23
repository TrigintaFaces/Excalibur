// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

namespace Excalibur.Testing.Conformance;

/// <summary>
/// Fails a conformance run in which arms were attempted and NOT ONE of them executed.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="ConformanceArmLedger"/> says in its own summary that it is "a reporting surface, not an
/// assertion: a kit records into it, and the consumer decides what an unverified arm means". This type is
/// that decision, supplied so a consumer does not have to write it: a run that verified NOTHING is a
/// failure, because it is otherwise indistinguishable from a run in which every store conformed.
/// </para>
/// <para>
/// <b>Why the ledger alone is not enough.</b> An arm that returns early because the store lacks the
/// capability it exercises reports a PASS in every test runner, exactly as an arm that ran and passed does.
/// The ledger makes the difference observable; nothing makes it fail. So a suite in which every arm skipped
/// exits zero, and the run is reported as conformant while having verified nothing at all.
/// </para>
/// <para>
/// <b>It is silent when nothing was attempted.</b> A filtered run that selected no conformance arm has an
/// empty ledger, and an unconditional demand would fail a run that was never asked to verify anything. The
/// gate speaks only when arms were attempted and every one of them skipped. A gate that produces a false
/// failure is worse than the gap it closes, because the next person to see one deletes the gate.
/// </para>
/// <para>
/// <b>Wire it as an assembly-level fixture, never as a test.</b> A test that reads the ledger is
/// ORDER-DEPENDENT: runners execute collections in parallel with no ordering guarantee, so such a test can
/// run before the suites it is judging and report a failure in a healthy run. An assembly fixture is
/// disposed after the assembly's tests have run, which is the only point at which "did anything execute?"
/// has an answer. With xUnit:
/// </para>
/// <code>
/// [assembly: AssemblyFixture(typeof(Excalibur.Testing.Conformance.ConformanceArmLivenessGate))]
/// </code>
/// <para>
/// The type itself takes no dependency on any test framework, so a consumer on a different runner wires it
/// to whatever their framework calls an end-of-run hook, or calls <see cref="EvaluateFailure()"/> directly.
/// </para>
/// </remarks>
public sealed class ConformanceArmLivenessGate : IAsyncDisposable
{
	/// <summary>
	/// Evaluates the liveness property against the live ledger.
	/// </summary>
	/// <returns>
	/// The failure message, or <see langword="null"/> when the run is live or nothing was attempted.
	/// </returns>
	public static string? EvaluateFailure() =>
		EvaluateFailure(
			ConformanceArmLedger.Executed.Count,
			ConformanceArmLedger.Skipped.Count,
			ConformanceArmLedger.Describe());

	/// <summary>
	/// The liveness rule, as a pure function of the counts.
	/// </summary>
	/// <param name="armsExecuted"> How many distinct arms ran their bodies. </param>
	/// <param name="armsSkipped"> How many distinct arms returned early for want of a capability. </param>
	/// <param name="description"> The ledger's own account of what ran and what did not. </param>
	/// <returns>
	/// The failure message, or <see langword="null"/> when the run is live or nothing was attempted.
	/// </returns>
	/// <remarks>
	/// Pure on purpose. The ledger is process-wide state shared with the live run, so a proof that drove
	/// this gate by mutating it would corrupt the ledger the real gate reads -- the proof could turn a
	/// healthy run red, or erase the evidence of a broken one. Passing the counts in means this gate's own
	/// tests cannot touch the run they execute inside.
	/// </remarks>
	public static string? EvaluateFailure(int armsExecuted, int armsSkipped, string description)
	{
		if (armsExecuted == 0 && armsSkipped == 0)
		{
			// Nothing was attempted. A filtered run that selected no conformance arm has nothing to assert.
			return null;
		}

		if (armsExecuted > 0)
		{
			return null;
		}

		return "CONFORMANCE VERIFIED NOTHING IN THIS RUN." + Environment.NewLine
			+ $"{armsSkipped} conformance arm(s) were attempted and NOT ONE executed -- every one returned "
			+ "early because the store under test did not present the capability the arm exercises."
			+ Environment.NewLine + Environment.NewLine
			+ "This is reported as a FAILURE because the alternative is silence. A skipped arm reports a "
			+ "pass, so a run in which nothing was verified reports exactly what a run in which every store "
			+ "conformed reports. That is the state this gate exists to make visible."
			+ Environment.NewLine + Environment.NewLine
			+ "Two causes are worth separating before changing anything. The stores under test may "
			+ "genuinely lack these capabilities, in which case the suite is certifying nothing and should "
			+ "say so rather than pass. Or the capability probe is not finding a capability the store does "
			+ "have -- a decorator in the way, a registration that did not run, an unstarted container -- "
			+ "in which case the store is fine and the run is blind."
			+ Environment.NewLine + Environment.NewLine
			+ description;
	}

	/// <summary>
	/// Evaluates the gate at the end of the run and throws when nothing was verified.
	/// </summary>
	/// <returns> A completed task when the run is live or nothing was attempted. </returns>
	/// <exception cref="InvalidOperationException"> Thrown when arms were attempted and none executed. </exception>
	/// <remarks>
	/// <b>How this surfaces depends on the runner front-end, and one of the two is misleading. Measured, both
	/// arms, same binaries.</b> Run the test executable directly and the throw is reported as its own
	/// <c>Errors: 1</c> alongside <c>Failed: n</c>, with the message and stack printed -- an operator sees
	/// what happened. Run the same assembly through <c>dotnet test</c>'s bridge and the throw becomes a
	/// "Test Assembly Cleanup Failure" recorded OUTSIDE the failure count: the summary line still reads
	/// <c>Passed!</c> with zero failures and the message does not appear at all. <b>The process exit code is
	/// non-zero either way</b>, so CI is correct in both, but a human reading a <c>dotnet test</c> console
	/// sees a green run with no reason. Read the exit code, not the summary line.
	/// </remarks>
	public ValueTask DisposeAsync()
	{
		var failure = EvaluateFailure();

		return failure is null
			? ValueTask.CompletedTask
			: throw new InvalidOperationException(failure);
	}
}
