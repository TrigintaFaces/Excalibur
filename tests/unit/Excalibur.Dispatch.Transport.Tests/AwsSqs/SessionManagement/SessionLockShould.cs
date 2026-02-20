// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Aws;

namespace Excalibur.Dispatch.Transport.Tests.AwsSqs.SessionManagement;

[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
public sealed class SessionLockShould
{
	[Fact]
	public void SetPropertiesFromConstructor()
	{
		// Arrange
		var expiresAt = DateTimeOffset.UtcNow.AddMinutes(5);

		// Act
		var lockObj = new SessionLock("session-1", "lock-abc", expiresAt);

		// Assert
		lockObj.SessionId.ShouldBe("session-1");
		lockObj.LockId.ShouldBe("lock-abc");
		lockObj.ExpiresAt.ShouldBe(expiresAt);
		lockObj.AcquiredAt.ShouldNotBe(default);
	}

	[Fact]
	public void ReportNotExpiredWhenFutureExpiresAt()
	{
		// Arrange
		var expiresAt = DateTimeOffset.UtcNow.AddMinutes(5);
		var lockObj = new SessionLock("session-1", "lock-abc", expiresAt);

		// Act & Assert
		lockObj.IsExpired.ShouldBeFalse();
		lockObj.RemainingTime.ShouldBeGreaterThan(TimeSpan.Zero);
	}

	[Fact]
	public void ReportExpiredWhenPastExpiresAt()
	{
		// Arrange
		var expiresAt = DateTimeOffset.UtcNow.AddMinutes(-1);
		var lockObj = new SessionLock("session-1", "lock-abc", expiresAt);

		// Act & Assert
		lockObj.IsExpired.ShouldBeTrue();
		lockObj.RemainingTime.ShouldBe(TimeSpan.Zero);
	}

	[Fact]
	public void ThrowOnNullSessionId()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() => new SessionLock(null!, "lock-id", DateTimeOffset.UtcNow));
	}

	[Fact]
	public void ThrowOnNullLockId()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() => new SessionLock("session-id", null!, DateTimeOffset.UtcNow));
	}

	[Fact]
	public async Task DisposeAsyncCallsReleaseAction()
	{
		// Arrange
		var released = false;
		var lockObj = new SessionLock("session-1", "lock-abc", DateTimeOffset.UtcNow.AddMinutes(5),
			() => { released = true; return ValueTask.CompletedTask; });

		// Act
		await lockObj.DisposeAsync();

		// Assert
		released.ShouldBeTrue();
	}

	[Fact]
	public async Task DisposeAsyncOnlyCallsReleaseActionOnce()
	{
		// Arrange
		var callCount = 0;
		var lockObj = new SessionLock("session-1", "lock-abc", DateTimeOffset.UtcNow.AddMinutes(5),
			() => { callCount++; return ValueTask.CompletedTask; });

		// Act
		await lockObj.DisposeAsync();
		await lockObj.DisposeAsync();

		// Assert
		callCount.ShouldBe(1);
	}

	[Fact]
	public async Task DisposeAsyncWithoutReleaseActionDoesNotThrow()
	{
		// Arrange
		var lockObj = new SessionLock("session-1", "lock-abc", DateTimeOffset.UtcNow.AddMinutes(5));

		// Act & Assert — should not throw
		await lockObj.DisposeAsync();
	}
}
