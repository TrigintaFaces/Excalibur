// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.LeaderElection;

using Excalibur.LeaderElection.InMemory;
using Excalibur.Testing.Conformance;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Xunit;

namespace Excalibur.Tests.Testing.Conformance;

/// <summary>
/// Conformance tests for <see cref="InMemoryLeaderElection"/> validating ILeaderElection contract compliance.
/// </summary>
/// <remarks>
/// <para>
/// InMemoryLeaderElection uses static dictionaries for leadership state, which enables
/// multi-instance testing within a single process. Each test uses a unique resource name
/// to ensure test isolation.
/// </para>
/// </remarks>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1707:Identifiers should not contain underscores", Justification = "Test method naming convention")]
[Trait("Category", "Integration")]
[Trait("Component", "Core")]
[Trait("Pattern", "LEADER-ELECTION")]
public class InMemoryLeaderElectionConformanceTests : LeaderElectionConformanceTestKit
{
	/// <inheritdoc />
	protected override ILeaderElection CreateElection(string resourceName, string? candidateId)
	{
		var options = Options.Create(new LeaderElectionOptions
		{
			InstanceId = candidateId ?? GenerateCandidateId(),
			LeaseDuration = TimeSpan.FromSeconds(5),
			RenewInterval = TimeSpan.FromSeconds(1),
		});

		return new InMemoryLeaderElection(
			resourceName,
			options,
			NullLogger<InMemoryLeaderElection>.Instance);
	}

	#region Lifecycle Tests

	[Fact]
	public Task StartAsync_ShouldInitiateParticipation_Test() =>
		StartAsync_ShouldInitiateParticipation();

	[Fact]
	public Task StopAsync_ShouldRelinquishLeadership_Test() =>
		StopAsync_ShouldRelinquishLeadership();

	[Fact]
	public Task StartAsync_AfterStop_ShouldRestartElection_Test() =>
		StartAsync_AfterStop_ShouldRestartElection();

	#endregion Lifecycle Tests

	#region Single-Candidate Leadership Tests

	[Fact]
	public Task StartAsync_SingleCandidate_ShouldBecomeLeader_Test() =>
		StartAsync_SingleCandidate_ShouldBecomeLeader();

	[Fact]
	public Task StartAsync_SingleCandidate_IsLeaderShouldBeTrue_Test() =>
		StartAsync_SingleCandidate_IsLeaderShouldBeTrue();

	[Fact]
	public Task StartAsync_SingleCandidate_CurrentLeaderIdShouldMatchCandidateId_Test() =>
		StartAsync_SingleCandidate_CurrentLeaderIdShouldMatchCandidateId();

	#endregion Single-Candidate Leadership Tests

	#region Multi-Candidate Tests

	[Fact]
	public Task MultipleCandidate_OnlyOneBecomesLeader_Test() =>
		MultipleCandidate_OnlyOneBecomesLeader();

	[Fact]
	public Task MultipleCandidate_AllSeeSameCurrentLeaderId_Test() =>
		MultipleCandidate_AllSeeSameCurrentLeaderId();

	[Fact]
	public Task MultipleCandidate_FirstToStartBecomesLeader_Test() =>
		MultipleCandidate_FirstToStartBecomesLeader();

	#endregion Multi-Candidate Tests

	#region Event Tests

	[Fact]
	public Task BecameLeader_ShouldFireWhenElected_Test() =>
		BecameLeader_ShouldFireWhenElected();

	[Fact]
	public Task LostLeadership_ShouldFireWhenStopped_Test() =>
		LostLeadership_ShouldFireWhenStopped();

	[Fact]
	public Task LeaderChanged_ShouldFireOnLeadershipChange_Test() =>
		LeaderChanged_ShouldFireOnLeadershipChange();

	#endregion Event Tests

	#region Property Tests

	[Fact]
	public Task CandidateId_ShouldBeUniquePerInstance_Test() =>
		CandidateId_ShouldBeUniquePerInstance();

	[Fact]
	public Task CurrentLeaderId_AfterStop_ShouldBeNullOrEmpty_Test() =>
		CurrentLeaderId_AfterStop_ShouldBeNullOrEmpty();

	#endregion Property Tests
}
