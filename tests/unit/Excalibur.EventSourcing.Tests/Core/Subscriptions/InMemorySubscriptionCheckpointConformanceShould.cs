// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.EventSourcing.Subscriptions;
using Excalibur.Testing.Conformance;

namespace Excalibur.EventSourcing.Tests.Core.Subscriptions;

[Trait("Category", "Unit")]
[Trait("Component", "EventStore")]
[Trait("Pattern", "Conformance")]
public sealed class InMemorySubscriptionCheckpointConformanceShould : SubscriptionCheckpointStoreConformanceTestKit
{
    [Fact]
    public Task GenerationPointer_ShouldGateSwitchAndProtectReferencedLifetime_Test() => GenerationPointer_ShouldGateSwitchAndProtectReferencedLifetime();

    [Fact]
    public Task GenerationPointer_ConcurrentRetirement_ShouldNeverLeaveDanglingReference_Test() => GenerationPointer_ConcurrentRetirement_ShouldNeverLeaveDanglingReference();


	[Fact]
	public Task RetiredBindings_ShouldNeverAcknowledgeOrRetireAReplacement_Test() =>
		RetiredBindings_ShouldNeverAcknowledgeOrRetireAReplacement();

	private readonly ISubscriptionCheckpointStore _store = new InMemorySubscriptionCheckpointStore();

	protected override ISubscriptionCheckpointStore CreateStore() => _store;

	[Fact]
	public Task Bind_IdenticalRoster_ShouldJoinWithoutCreatingCheckpoint_Test() =>
		Bind_IdenticalRoster_ShouldJoinWithoutCreatingCheckpoint();

	[Fact]
	public Task Bind_NewMember_ShouldBeAdditive_AndLeaveSiblingCheckpointsUntouched_Test() =>
		Bind_NewMember_ShouldBeAdditive_AndLeaveSiblingCheckpointsUntouched();

	[Fact]
	public Task Bind_ChangedMemberFingerprint_ShouldRefuseNamingThatProjection_AndLeaveOthersBound_Test() =>
		Bind_ChangedMemberFingerprint_ShouldRefuseNamingThatProjection_AndLeaveOthersBound();

	[Fact]
	public Task Bind_ShouldReturnRosterIncludingMembersTheCallerDidNotSupply_Test() =>
		Bind_ShouldReturnRosterIncludingMembersTheCallerDidNotSupply();

	[Fact]
	public Task ConcurrentBindings_OfOneMember_WithDifferentFingerprints_ShouldAcceptExactlyOne_Test() =>
		ConcurrentBindings_OfOneMember_WithDifferentFingerprints_ShouldAcceptExactlyOne();

	[Fact]
	public Task Release_ShouldRemoveMemberAndItsCheckpoint_Atomically_Test() =>
		Release_ShouldRemoveMemberAndItsCheckpoint_Atomically();

	[Fact]
	public Task Release_AbsentMember_ShouldReturnFalse_AndWriteNothing_Test() =>
		Release_AbsentMember_ShouldReturnFalse_AndWriteNothing();

	[Fact]
	public Task Bind_CaseDistinctProjectionKeys_ShouldRemainIndependent_Test() =>
		Bind_CaseDistinctProjectionKeys_ShouldRemainIndependent();





	[Fact]
	public Task Bind_CaseDistinctNames_ShouldRemainIndependent_Test() => Bind_CaseDistinctNames_ShouldRemainIndependent();

	[Fact]
	public Task GetCheckpoint_ForUnknownSubscription_ShouldReturnNull_Test() => GetCheckpoint_ForUnknownSubscription_ShouldReturnNull();

	[Fact]
	public Task Advance_FromNoCheckpoint_ShouldBeAcceptedAndPersist_Test() => Advance_FromNoCheckpoint_ShouldBeAcceptedAndPersist();

	[Fact]
	public Task Advance_WithMatchingExpectedPosition_ShouldBeAccepted_Test() => Advance_WithMatchingExpectedPosition_ShouldBeAccepted();

	[Fact]
	public Task Advance_WithStaleExpectedPosition_ShouldBeSupersededAndChangeNothing_Test() => Advance_WithStaleExpectedPosition_ShouldBeSupersededAndChangeNothing();

	[Fact]
	public Task Advance_ExpectingNoCheckpoint_WhenOneExists_ShouldBeSuperseded_Test() => Advance_ExpectingNoCheckpoint_WhenOneExists_ShouldBeSuperseded();

	[Fact]
	public Task ConcurrentAdvances_FromTheSamePosition_ShouldAcceptExactlyOne_Test() => ConcurrentAdvances_FromTheSamePosition_ShouldAcceptExactlyOne();

	[Fact]
	public Task Advance_WithInvalidPositions_ShouldThrowAndChangeNothing_Test() => Advance_WithInvalidPositions_ShouldThrowAndChangeNothing();

	[Fact]
	public Task Advance_WithEqualPositions_ShouldCompareWithoutClaimingOwnership_Test() => Advance_WithEqualPositions_ShouldCompareWithoutClaimingOwnership();

	[Fact]
	public Task Advance_WhenNameAndKeyTogetherExceedTheNameWidth_ShouldRoundTrip_Test() => Advance_WhenNameAndKeyTogetherExceedTheNameWidth_ShouldRoundTrip();

	[Fact]
	public Task Advance_CaseDistinctProjectionKeys_ShouldRemainDistinctCursors_Test() => Advance_CaseDistinctProjectionKeys_ShouldRemainDistinctCursors();

	[Fact]
	public Task Enumerate_ShouldReportStoredCheckpoints_Test() => Enumerate_ShouldReportStoredCheckpoints();

	[Fact]
	public Task ConformanceSuite_ShouldWireEveryArm_Test() => ConformanceSuite_ShouldWireEveryArm();

}
