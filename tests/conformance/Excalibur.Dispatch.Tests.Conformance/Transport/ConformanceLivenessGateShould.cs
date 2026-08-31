// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Shouldly;

using Xunit;

namespace Excalibur.Dispatch.Tests.Conformance.Transport;

/// <summary>
/// Non-vacuity proof for <see cref="ConformanceLivenessGate" />. A gate that cannot fail proves nothing, and
/// a gate that fails when it should not is deleted by the next person who sees it -- so both directions are
/// pinned here.
/// </summary>
/// <remarks>
/// Drives the pure overload rather than the process-wide ledger: these tests execute inside the very run the
/// gate is judging, so mutating that ledger would corrupt the result it reports.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class ConformanceLivenessGateShould
{
	private const string Description = "(ledger description)";

	/// <summary>
	/// SAFETY: broker suites ran and produced nothing -- the state a green run is otherwise
	/// indistinguishable from. This is the arm that is RED with the container runtime stopped.
	/// </summary>
	[Fact]
	public void Fail_When_Broker_Suites_Were_Selected_And_No_Arm_Executed()
	{
		var failure = ConformanceLivenessGate.EvaluateFailure(
			brokerSuitesSelected: 8,
			brokerArmsExecuted: 0,
			description: Description);

		_ = failure.ShouldNotBeNull(
			"8 broker suites ran and not one arm executed: the run verified nothing and MUST NOT report success.");
		failure.ShouldContain("DID NOT VERIFY ANYTHING");
		failure.ShouldContain(Description, Case.Sensitive, "the failure must carry the ledger so it is diagnosable");
	}

	/// <summary>
	/// LIVENESS: the gate stays quiet on a healthy run. Without this arm, a gate that always failed would
	/// satisfy the arm above and still be worthless.
	/// </summary>
	[Fact]
	public void Pass_When_At_Least_One_Broker_Arm_Executed() =>
		ConformanceLivenessGate.EvaluateFailure(
			brokerSuitesSelected: 8,
			brokerArmsExecuted: 1,
			description: Description)
			.ShouldBeNull("one executed broker arm is enough to prove the run was not vacuous.");

	/// <summary>
	/// LIVENESS: a filtered run that selected no broker suite has nothing to answer for. The conformance CI
	/// matrix runs one transport by name, so a gate without this arm would fail a job that was never asked to
	/// start a broker.
	/// </summary>
	[Fact]
	public void Stay_Silent_When_No_Broker_Suite_Was_Selected() =>
		ConformanceLivenessGate.EvaluateFailure(
			brokerSuitesSelected: 0,
			brokerArmsExecuted: 0,
			description: Description)
			.ShouldBeNull("a run that selected no broker suite is not a run that failed to verify one.");
}
