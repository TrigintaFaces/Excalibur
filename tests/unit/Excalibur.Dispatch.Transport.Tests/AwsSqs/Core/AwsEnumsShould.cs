// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Aws;

using AwsPollingStatus = Excalibur.Dispatch.Transport.Aws.PollingStatus;

namespace Excalibur.Dispatch.Transport.Tests.AwsSqs.Core;

[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
public sealed class AwsEnumsShould
{
	[Fact]
	public void HaveAllMetricUnitMembers()
	{
		Enum.GetValues<MetricUnit>().Length.ShouldBe(27);
	}

	[Theory]
	[InlineData(MetricUnit.None, 0)]
	[InlineData(MetricUnit.Count, 1)]
	[InlineData(MetricUnit.Bytes, 2)]
	[InlineData(MetricUnit.Percent, 12)]
	[InlineData(MetricUnit.Seconds, 13)]
	[InlineData(MetricUnit.Milliseconds, 15)]
	[InlineData(MetricUnit.CountPerSecond, 26)]
	public void HaveCorrectMetricUnitValues(MetricUnit unit, int expected)
	{
		((int)unit).ShouldBe(expected);
	}

	[Theory]
	[InlineData(DlqAction.None, 0)]
	[InlineData(DlqAction.Redriven, 1)]
	[InlineData(DlqAction.RetryFailed, 2)]
	[InlineData(DlqAction.Archived, 3)]
	[InlineData(DlqAction.Deleted, 4)]
	[InlineData(DlqAction.Skipped, 5)]
	public void HaveCorrectDlqActionValues(DlqAction action, int expected)
	{
		((int)action).ShouldBe(expected);
	}

	[Fact]
	public void HaveAllDlqActionMembers()
	{
		Enum.GetValues<DlqAction>().Length.ShouldBe(6);
	}

	[Theory]
	[InlineData(AwsPollingStatus.Inactive, 0)]
	[InlineData(AwsPollingStatus.Active, 1)]
	[InlineData(AwsPollingStatus.Stopping, 2)]
	[InlineData(AwsPollingStatus.Error, 3)]
	public void HaveCorrectPollingStatusValues(AwsPollingStatus status, int expected)
	{
		((int)status).ShouldBe(expected);
	}

	[Fact]
	public void HaveAllPollingStatusMembers()
	{
		Enum.GetValues<AwsPollingStatus>().Length.ShouldBe(4);
	}

	[Theory]
	[InlineData(SessionStatus.Idle, 0)]
	[InlineData(SessionStatus.Active, 1)]
	[InlineData(SessionStatus.Locked, 2)]
	[InlineData(SessionStatus.Closed, 3)]
	[InlineData(SessionStatus.Expired, 4)]
	[InlineData(SessionStatus.Suspended, 5)]
	[InlineData(SessionStatus.Closing, 6)]
	public void HaveCorrectSessionStatusValues(SessionStatus status, int expected)
	{
		((int)status).ShouldBe(expected);
	}

	[Fact]
	public void HaveAllSessionStatusMembers()
	{
		Enum.GetValues<SessionStatus>().Length.ShouldBe(7);
	}

	[Fact]
	public void HaveAwsSessionStateMatchingSessionStatus()
	{
		// AwsSessionState values should match SessionStatus values
		((int)AwsSessionState.Idle).ShouldBe((int)SessionStatus.Idle);
		((int)AwsSessionState.Active).ShouldBe((int)SessionStatus.Active);
		((int)AwsSessionState.Locked).ShouldBe((int)SessionStatus.Locked);
		((int)AwsSessionState.Closed).ShouldBe((int)SessionStatus.Closed);
		((int)AwsSessionState.Expired).ShouldBe((int)SessionStatus.Expired);
		((int)AwsSessionState.Suspended).ShouldBe((int)SessionStatus.Suspended);
		((int)AwsSessionState.Closing).ShouldBe((int)SessionStatus.Closing);
	}
}
