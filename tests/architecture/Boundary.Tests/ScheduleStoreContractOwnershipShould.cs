// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Data.SqlServer.Scheduling;
using Excalibur.Dispatch;
using Excalibur.Dispatch.Delivery;

namespace Boundary.Tests;

[Trait("Category", "Unit")]
[Trait("Component", "Architecture")]
[Trait("Pattern", "Boundary")]
public sealed class ScheduleStoreContractOwnershipShould
{
    [Theory]
    [InlineData(typeof(IScheduleExecutionFailureStore))]
    [InlineData(typeof(ScheduleAdmissionPolicySelection))]
    [InlineData(typeof(ScheduleExecutionCompletionDisposition))]
    [InlineData(typeof(ScheduleFailureDisposition))]
    [InlineData(typeof(ScheduleFailureExecutionAdmission))]
    [InlineData(typeof(ScheduleFailureReason))]
    [InlineData(typeof(ScheduleOccurrenceFailure))]
    [InlineData(typeof(ScheduleOccurrenceFailureSnapshot))]
    [InlineData(typeof(ScheduleQuarantineRecoveryReceipt))]
    [InlineData(typeof(ScheduleQuarantineToken))]
    [InlineData(typeof(IScheduleStore))]
    [InlineData(typeof(IScheduleExecutionStore))]
    [InlineData(typeof(IScheduleExecutionLeaseRenewal))]
    [InlineData(typeof(IScheduledMessage))]
    [InlineData(typeof(IScheduledMessageIdentity))]
    [InlineData(typeof(IScheduleSpec))]
    [InlineData(typeof(CronRecurrenceProfile))]
    [InlineData(typeof(IScheduledMessageMetadata))]
    [InlineData(typeof(MissedExecutionBehavior))]
    [InlineData(typeof(ScheduledMessage))]
    [InlineData(typeof(ScheduleExecutionLease))]
    [InlineData(typeof(ScheduleExecutionSnapshot))]
    [InlineData(typeof(ScheduleExecutionResult))]
    public void OwnThePublicSchedulingContractInAbstractions(Type contract)
    {
        contract.Assembly.ShouldBe(typeof(IDispatcher).Assembly,
            $"{contract.FullName} must be consumable by persistence providers without exposing Dispatch core.");
    }

    [Fact]
    public void OwnTheSqlServerStoresInheritedSchedulingInterfacesInAbstractions()
    {
        var schedulingInterfaces = typeof(SqlServerScheduleStore).GetInterfaces()
            .Where(contract => contract.Namespace == typeof(IScheduleStore).Namespace)
            .ToArray();

        schedulingInterfaces.ShouldContain(typeof(IScheduleStore));
        schedulingInterfaces.ShouldContain(typeof(IScheduleExecutionStore));
        schedulingInterfaces.ShouldContain(typeof(IScheduleExecutionLeaseRenewal));
        foreach (var contract in schedulingInterfaces)
        {
            contract.Assembly.ShouldBe(typeof(IDispatcher).Assembly,
                $"The public SQL Server store exposes inherited contract {contract.FullName}.");
        }
    }
}
