// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Security.Tests.Compliance.Abstractions.Encryption;

/// <summary>
/// Unit tests for <see cref="EscrowStatus"/> record and <see cref="EscrowState"/> enum.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
[Trait("Feature", "Encryption")]
public sealed class EscrowStatusShould : UnitTestBase
{
	[Fact]
	public void CreateValidEscrowStatusWithRequiredProperties()
	{
		// Arrange
		var escrowedAt = DateTimeOffset.UtcNow;

		// Act
		var status = new EscrowStatus
		{
			KeyId = "key-001",
			EscrowId = "escrow-001",
			State = EscrowState.Active,
			EscrowedAt = escrowedAt
		};

		// Assert
		status.KeyId.ShouldBe("key-001");
		status.EscrowId.ShouldBe("escrow-001");
		status.State.ShouldBe(EscrowState.Active);
		status.EscrowedAt.ShouldBe(escrowedAt);
	}

	[Fact]
	public void CreateFullyPopulatedEscrowStatus()
	{
		// Arrange
		var escrowedAt = DateTimeOffset.UtcNow.AddMonths(-1);
		var expiresAt = DateTimeOffset.UtcNow.AddMonths(11);
		var lastRecoveryAttempt = DateTimeOffset.UtcNow.AddDays(-7);

		// Act
		var status = new EscrowStatus
		{
			KeyId = "key-002",
			EscrowId = "escrow-002",
			State = EscrowState.Active,
			EscrowedAt = escrowedAt,
			ExpiresAt = expiresAt,
			ActiveTokenCount = 3,
			RecoveryAttempts = 1,
			LastRecoveryAttempt = lastRecoveryAttempt,
			TenantId = "tenant-abc",
			Purpose = "disaster-recovery"
		};

		// Assert
		status.KeyId.ShouldBe("key-002");
		status.ExpiresAt.ShouldBe(expiresAt);
		status.ActiveTokenCount.ShouldBe(3);
		status.RecoveryAttempts.ShouldBe(1);
		status.LastRecoveryAttempt.ShouldBe(lastRecoveryAttempt);
		status.TenantId.ShouldBe("tenant-abc");
		status.Purpose.ShouldBe("disaster-recovery");
	}

	[Fact]
	public void BeRecoverableWhenActiveAndNotExpired()
	{
		// Act
		var status = new EscrowStatus
		{
			KeyId = "key-recoverable",
			EscrowId = "escrow-recoverable",
			State = EscrowState.Active,
			EscrowedAt = DateTimeOffset.UtcNow.AddMonths(-1),
			ExpiresAt = DateTimeOffset.UtcNow.AddMonths(11)
		};

		// Assert
		status.IsRecoverable.ShouldBeTrue();
	}

	[Fact]
	public void BeRecoverableWhenActiveAndNoExpiry()
	{
		// Act
		var status = new EscrowStatus
		{
			KeyId = "key-no-expiry",
			EscrowId = "escrow-no-expiry",
			State = EscrowState.Active,
			EscrowedAt = DateTimeOffset.UtcNow,
			ExpiresAt = null
		};

		// Assert
		status.IsRecoverable.ShouldBeTrue();
	}

	[Fact]
	public void NotBeRecoverableWhenExpired()
	{
		// Act
		var status = new EscrowStatus
		{
			KeyId = "key-expired",
			EscrowId = "escrow-expired",
			State = EscrowState.Active,
			EscrowedAt = DateTimeOffset.UtcNow.AddYears(-2),
			ExpiresAt = DateTimeOffset.UtcNow.AddYears(-1) // Expired
		};

		// Assert
		status.IsRecoverable.ShouldBeFalse();
	}

	[Theory]
	[InlineData(EscrowState.Recovered)]
	[InlineData(EscrowState.Expired)]
	[InlineData(EscrowState.Revoked)]
	public void NotBeRecoverableWhenNotActive(EscrowState state)
	{
		// Act
		var status = new EscrowStatus
		{
			KeyId = "key-inactive",
			EscrowId = "escrow-inactive",
			State = state,
			EscrowedAt = DateTimeOffset.UtcNow,
			ExpiresAt = DateTimeOffset.UtcNow.AddYears(1)
		};

		// Assert
		status.IsRecoverable.ShouldBeFalse();
	}

	[Theory]
	[InlineData(EscrowState.Active)]
	[InlineData(EscrowState.Recovered)]
	[InlineData(EscrowState.Expired)]
	[InlineData(EscrowState.Revoked)]
	public void SupportAllEscrowStates(EscrowState state)
	{
		// Act
		var status = new EscrowStatus
		{
			KeyId = "key-state",
			EscrowId = "escrow-state",
			State = state,
			EscrowedAt = DateTimeOffset.UtcNow
		};

		// Assert
		status.State.ShouldBe(state);
	}

	[Fact]
	public void HaveActiveAsDefaultState()
	{
		// Arrange
		EscrowState defaultValue = default;

		// Assert
		defaultValue.ShouldBe(EscrowState.Active);
	}

	[Theory]
	[InlineData(EscrowState.Active, 0)]
	[InlineData(EscrowState.Recovered, 1)]
	[InlineData(EscrowState.Expired, 2)]
	[InlineData(EscrowState.Revoked, 3)]
	public void HaveCorrectStateUnderlyingValues(EscrowState state, int expectedValue)
	{
		// Assert
		((int)state).ShouldBe(expectedValue);
	}

	[Fact]
	public void SupportRecordEquality()
	{
		// Arrange
		var escrowedAt = DateTimeOffset.UtcNow;

		var status1 = new EscrowStatus
		{
			KeyId = "key-eq",
			EscrowId = "escrow-eq",
			State = EscrowState.Active,
			EscrowedAt = escrowedAt
		};

		var status2 = new EscrowStatus
		{
			KeyId = "key-eq",
			EscrowId = "escrow-eq",
			State = EscrowState.Active,
			EscrowedAt = escrowedAt
		};

		// Assert
		status1.ShouldBe(status2);
	}

	[Fact]
	public void HaveDefaultValuesForOptionalCounters()
	{
		// Act
		var status = new EscrowStatus
		{
			KeyId = "key-defaults",
			EscrowId = "escrow-defaults",
			State = EscrowState.Active,
			EscrowedAt = DateTimeOffset.UtcNow
		};

		// Assert
		status.ActiveTokenCount.ShouldBe(0);
		status.RecoveryAttempts.ShouldBe(0);
		status.LastRecoveryAttempt.ShouldBeNull();
		status.TenantId.ShouldBeNull();
		status.Purpose.ShouldBeNull();
	}
}
