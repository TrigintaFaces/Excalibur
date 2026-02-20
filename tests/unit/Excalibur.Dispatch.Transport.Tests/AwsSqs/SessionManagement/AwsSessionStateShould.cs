// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Aws;

namespace Excalibur.Dispatch.Transport.Tests.AwsSqs.SessionManagement;

[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
public sealed class AwsSessionStateShould
{
	[Theory]
	[InlineData(AwsSessionState.Idle, SessionStatus.Idle)]
	[InlineData(AwsSessionState.Active, SessionStatus.Active)]
	[InlineData(AwsSessionState.Locked, SessionStatus.Locked)]
	[InlineData(AwsSessionState.Suspended, SessionStatus.Suspended)]
	[InlineData(AwsSessionState.Closing, SessionStatus.Closing)]
	[InlineData(AwsSessionState.Closed, SessionStatus.Closed)]
	[InlineData(AwsSessionState.Expired, SessionStatus.Expired)]
	public void MapToCorrectSessionStatus(AwsSessionState state, SessionStatus expectedStatus)
	{
		((int)state).ShouldBe((int)expectedStatus);
	}

	[Fact]
	public void HaveAllExpectedValues()
	{
		var values = Enum.GetValues<AwsSessionState>();
		values.Length.ShouldBe(7);
	}

	[Fact]
	public void SupportCastingToSessionStatus()
	{
		// Arrange
		var awsState = AwsSessionState.Active;

		// Act
		var sessionStatus = (SessionStatus)(int)awsState;

		// Assert
		sessionStatus.ShouldBe(SessionStatus.Active);
	}
}
