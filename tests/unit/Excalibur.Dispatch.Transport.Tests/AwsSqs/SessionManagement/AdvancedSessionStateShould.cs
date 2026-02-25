// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Aws;

namespace Excalibur.Dispatch.Transport.Tests.AwsSqs.SessionManagement;

[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
public sealed class AdvancedSessionStateShould
{
	[Fact]
	public void HaveCorrectDefaults()
	{
		// Arrange & Act
		var state = new AdvancedSessionState();

		// Assert
		state.SessionId.ShouldBe(string.Empty);
		state.Status.ShouldBe(SessionStatus.Idle);
		state.LastActivityUtc.ShouldBe(default);
		state.CreatedUtc.ShouldBe(default);
		state.ExpiresUtc.ShouldBeNull();
		state.MessageCount.ShouldBe(0);
		state.LockToken.ShouldBeNull();
		state.LockOwner.ShouldBeNull();
		state.Metadata.ShouldNotBeNull();
		state.Metadata.ShouldBeEmpty();
	}

	[Fact]
	public void AllowSettingAllProperties()
	{
		// Arrange
		var now = DateTime.UtcNow;

		// Act
		var state = new AdvancedSessionState
		{
			SessionId = "advanced-session-1",
			Status = SessionStatus.Locked,
			LastActivityUtc = now,
			CreatedUtc = now.AddHours(-2),
			ExpiresUtc = now.AddHours(1),
			MessageCount = 15000,
			LockToken = "lock-token-abc",
			LockOwner = "worker-1",
		};
		state.Metadata["region"] = "us-east-1";

		// Assert
		state.SessionId.ShouldBe("advanced-session-1");
		state.Status.ShouldBe(SessionStatus.Locked);
		state.LastActivityUtc.ShouldBe(now);
		state.CreatedUtc.ShouldBe(now.AddHours(-2));
		state.ExpiresUtc.ShouldBe(now.AddHours(1));
		state.MessageCount.ShouldBe(15000);
		state.LockToken.ShouldBe("lock-token-abc");
		state.LockOwner.ShouldBe("worker-1");
		state.Metadata["region"].ShouldBe("us-east-1");
	}
}
