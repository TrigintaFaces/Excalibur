// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Patterns.ClaimCheck;

using Shouldly;

using Xunit;

namespace Excalibur.Dispatch.Patterns.Tests.ClaimCheck;

/// <summary>
/// Binds the retention contract every claim check provider resolves expiry through: a zero retention
/// period disables expiry, and an expired reference is treated as unretrievable rather than as an
/// invalid state.
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Patterns)]
public sealed class ClaimCheckExpiryContractShould
{
	private static readonly DateTimeOffset StoredAt = new(2026, 3, 14, 9, 26, 53, TimeSpan.Zero);

	[Fact]
	public void ResolveExpiresAt_ReturnsNull_WhenRetentionPeriodIsZero()
	{
		// A zero retention period means "never expires". Adding it to the store time would produce an
		// instant equal to the store time, marking the payload expired the moment it is written.
		var options = new ClaimCheckOptions { RetentionPeriod = TimeSpan.Zero };

		options.ResolveExpiresAt(StoredAt).ShouldBeNull();
	}

	[Fact]
	public void ResolveExpiresAt_ReturnsNull_WhenRetentionPeriodIsNegative()
	{
		var options = new ClaimCheckOptions { RetentionPeriod = TimeSpan.FromHours(-1) };

		options.ResolveExpiresAt(StoredAt).ShouldBeNull();
	}

	[Fact]
	public void ResolveExpiresAt_AddsRetentionPeriodToStoredAt_WhenRetentionPeriodIsPositive()
	{
		var options = new ClaimCheckOptions { RetentionPeriod = TimeSpan.FromHours(6) };

		options.ResolveExpiresAt(StoredAt).ShouldBe(StoredAt.AddHours(6));
	}

	[Fact]
	public void ResolveExpiresAt_TreatsDefaultTtlAndRetentionPeriodAsOneSetting()
	{
		var options = new ClaimCheckOptions { DefaultTtl = TimeSpan.Zero };

		options.RetentionPeriod.ShouldBe(TimeSpan.Zero);
		options.ResolveExpiresAt(StoredAt).ShouldBeNull();
	}

	[Fact]
	public void IsExpired_ReturnsFalse_WhenReferenceHasNoExpiry()
	{
		var reference = new ClaimCheckReference { Id = "cc-1", ExpiresAt = null };

		reference.IsExpired(StoredAt.AddYears(100)).ShouldBeFalse();
	}

	[Fact]
	public void IsExpired_ReturnsFalse_BeforeTheExpiryInstant()
	{
		var reference = new ClaimCheckReference { Id = "cc-1", ExpiresAt = StoredAt.AddMinutes(5) };

		reference.IsExpired(StoredAt.AddMinutes(4)).ShouldBeFalse();
	}

	[Fact]
	public void IsExpired_ReturnsTrue_AtTheExpiryInstant()
	{
		var reference = new ClaimCheckReference { Id = "cc-1", ExpiresAt = StoredAt.AddMinutes(5) };

		reference.IsExpired(StoredAt.AddMinutes(5)).ShouldBeTrue();
	}

	[Fact]
	public void IsExpired_ReturnsTrue_AfterTheExpiryInstant()
	{
		var reference = new ClaimCheckReference { Id = "cc-1", ExpiresAt = StoredAt.AddMinutes(5) };

		reference.IsExpired(StoredAt.AddMinutes(6)).ShouldBeTrue();
	}
}
