// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch.Delivery;
using Excalibur.Testing;
using Excalibur.Testing.Conformance;
using Shouldly;
using Xunit;

namespace Excalibur.Tests.Testing.Conformance;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
[Trait("Pattern", "STORE")]
public sealed class ScheduleRenewalConformanceWiringShould
{
	[Fact]
	public async Task AcceptAllTwentyFourInheritedAndRenewalArms() =>
		await new FullyWiredRenewalProbe().ConformanceSuite_ShouldWireEveryArm();

	[Fact]
	public async Task RejectExactlyOneMissingRenewalArm()
	{
		var error = await Should.ThrowAsync<TestFixtureAssertionException>(() => new MissingRenewalArmProbe().ConformanceSuite_ShouldWireEveryArm());
		error.Message.ShouldContain(nameof(ScheduleExecutionLeaseRenewalConformanceTestKit.RenewalExtendsCurrentOwnership));
		error.Message.ShouldContain("1 of the 24 arms");
	}

	private sealed class FullyWiredRenewalProbe : ScheduleExecutionLeaseRenewalConformanceTestKit
	{
		protected override IScheduleExecutionLeaseRenewal CreateStore() => throw new InvalidOperationException("The wiring guard must not resolve the store.");

		public override Task StoreAsync_ShouldPersistMessage() => base.StoreAsync_ShouldPersistMessage();

		public override Task StoreAsync_WithNullMessage_ShouldThrow() => base.StoreAsync_WithNullMessage_ShouldThrow();

		public override Task StoreAsync_SameId_ShouldUpsert() => base.StoreAsync_SameId_ShouldUpsert();

		public override Task StoreAsync_MultipleMessages_ShouldPersistAll() => base.StoreAsync_MultipleMessages_ShouldPersistAll();

		public override Task GetAllAsync_EmptyStore_ShouldReturnEmpty() => base.GetAllAsync_EmptyStore_ShouldReturnEmpty();

		public override Task GetAllAsync_AfterStore_ShouldReturnMessage() => base.GetAllAsync_AfterStore_ShouldReturnMessage();

		public override Task GetAllAsync_ShouldReturnAllMessages() => base.GetAllAsync_ShouldReturnAllMessages();

		public override Task CompleteAsync_ShouldSetEnabledFalse() => base.CompleteAsync_ShouldSetEnabledFalse();

		public override Task CompleteAsync_NonExistent_ShouldBeIdempotent() => base.CompleteAsync_NonExistent_ShouldBeIdempotent();

		public override Task CompleteAsync_AlreadyCompleted_ShouldBeIdempotent() => base.CompleteAsync_AlreadyCompleted_ShouldBeIdempotent();

		public override Task StoreAsync_ThenComplete_MessageRemainsPersisted() => base.StoreAsync_ThenComplete_MessageRemainsPersisted();

		public override Task MultipleMessages_CompleteOne_OthersUnaffected() => base.MultipleMessages_CompleteOne_OthersUnaffected();

		public override Task StoreInputRemainsDetached() => base.StoreInputRemainsDetached();

		public override Task ReturnedSnapshotsRemainDetached() => base.ReturnedSnapshotsRemainDetached();

		public override Task SimultaneousAdmissionHasOneWinner() => base.SimultaneousAdmissionHasOneWinner();

		public override Task AdministrativeChangesInvalidateOldAuthority() => base.AdministrativeChangesInvalidateOldAuthority();

		public override Task ProgressAndReleaseRespectCurrentAuthority() => base.ProgressAndReleaseRespectCurrentAuthority();

		public override Task CanceledOperationsLeaveAuthorityUnchanged() => base.CanceledOperationsLeaveAuthorityUnchanged();

		public override Task ConformanceSuite_ShouldWireEveryArm() => base.ConformanceSuite_ShouldWireEveryArm();
		public override Task RenewalExtendsCurrentOwnership() => base.RenewalExtendsCurrentOwnership();

		public override Task ExpiredAuthorityCannotRenewOrProgress() => base.ExpiredAuthorityCannotRenewOrProgress();

		public override Task ReleasedAndForeignAuthorityCannotRenewSuccessor() => base.ReleasedAndForeignAuthorityCannotRenewSuccessor();

		public override Task AdministrativeMutationInvalidatesRenewal() => base.AdministrativeMutationInvalidatesRenewal();

		public override Task RenewalPreservesProgressOnceIncludingAfterCommit() => base.RenewalPreservesProgressOnceIncludingAfterCommit();

		public override Task CanceledRenewalLeavesOwnershipUnchanged() => base.CanceledRenewalLeavesOwnershipUnchanged();

	}

	private sealed class MissingRenewalArmProbe : ScheduleExecutionLeaseRenewalConformanceTestKit
	{
		protected override IScheduleExecutionLeaseRenewal CreateStore() => throw new InvalidOperationException("The wiring guard must not resolve the store.");

		public override Task StoreAsync_ShouldPersistMessage() => base.StoreAsync_ShouldPersistMessage();

		public override Task StoreAsync_WithNullMessage_ShouldThrow() => base.StoreAsync_WithNullMessage_ShouldThrow();

		public override Task StoreAsync_SameId_ShouldUpsert() => base.StoreAsync_SameId_ShouldUpsert();

		public override Task StoreAsync_MultipleMessages_ShouldPersistAll() => base.StoreAsync_MultipleMessages_ShouldPersistAll();

		public override Task GetAllAsync_EmptyStore_ShouldReturnEmpty() => base.GetAllAsync_EmptyStore_ShouldReturnEmpty();

		public override Task GetAllAsync_AfterStore_ShouldReturnMessage() => base.GetAllAsync_AfterStore_ShouldReturnMessage();

		public override Task GetAllAsync_ShouldReturnAllMessages() => base.GetAllAsync_ShouldReturnAllMessages();

		public override Task CompleteAsync_ShouldSetEnabledFalse() => base.CompleteAsync_ShouldSetEnabledFalse();

		public override Task CompleteAsync_NonExistent_ShouldBeIdempotent() => base.CompleteAsync_NonExistent_ShouldBeIdempotent();

		public override Task CompleteAsync_AlreadyCompleted_ShouldBeIdempotent() => base.CompleteAsync_AlreadyCompleted_ShouldBeIdempotent();

		public override Task StoreAsync_ThenComplete_MessageRemainsPersisted() => base.StoreAsync_ThenComplete_MessageRemainsPersisted();

		public override Task MultipleMessages_CompleteOne_OthersUnaffected() => base.MultipleMessages_CompleteOne_OthersUnaffected();

		public override Task StoreInputRemainsDetached() => base.StoreInputRemainsDetached();

		public override Task ReturnedSnapshotsRemainDetached() => base.ReturnedSnapshotsRemainDetached();

		public override Task SimultaneousAdmissionHasOneWinner() => base.SimultaneousAdmissionHasOneWinner();

		public override Task AdministrativeChangesInvalidateOldAuthority() => base.AdministrativeChangesInvalidateOldAuthority();

		public override Task ProgressAndReleaseRespectCurrentAuthority() => base.ProgressAndReleaseRespectCurrentAuthority();

		public override Task CanceledOperationsLeaveAuthorityUnchanged() => base.CanceledOperationsLeaveAuthorityUnchanged();

		public override Task ConformanceSuite_ShouldWireEveryArm() => base.ConformanceSuite_ShouldWireEveryArm();
		public override Task ExpiredAuthorityCannotRenewOrProgress() => base.ExpiredAuthorityCannotRenewOrProgress();

		public override Task ReleasedAndForeignAuthorityCannotRenewSuccessor() => base.ReleasedAndForeignAuthorityCannotRenewSuccessor();

		public override Task AdministrativeMutationInvalidatesRenewal() => base.AdministrativeMutationInvalidatesRenewal();

		public override Task RenewalPreservesProgressOnceIncludingAfterCommit() => base.RenewalPreservesProgressOnceIncludingAfterCommit();

		public override Task CanceledRenewalLeavesOwnershipUnchanged() => base.CanceledRenewalLeavesOwnershipUnchanged();

	}

}
