// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Compliance.KeyManagement;

namespace Excalibur.Compliance.Tests.KeyManagement;

[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class KeyEscrowBackupOptionsValidatorShould
{
	private readonly KeyEscrowBackupOptionsValidator _validator = new();

	private static KeyEscrowBackupOptions CreateValidOptions() => new()
	{
		EscrowProvider = "InMemory",
		SplitThreshold = 3,
		TotalShares = 5,
	};

	[Fact]
	public void SucceedForValidOptions()
	{
		var result = _validator.Validate(null, CreateValidOptions());

		result.Succeeded.ShouldBeTrue();
	}

	[Fact]
	public void ThrowArgumentNullException_WhenOptionsIsNull()
	{
		Should.Throw<ArgumentNullException>(() => _validator.Validate(null, null!));
	}

	// SAFETY: a 1-of-N quorum is security-nonsense (any single custodian reconstructs the secret)
	// and the underlying ShamirSecretSharing.Split requires a threshold of at least 2 — so a
	// SplitThreshold below 2 MUST fail fast at startup, never surface as a runtime surprise.
	[Theory]
	[InlineData(1)]
	[InlineData(0)]
	[InlineData(-1)]
	public void FailWhenSplitThresholdIsBelowTwo(int threshold)
	{
		var options = CreateValidOptions();
		options.SplitThreshold = threshold;
		options.TotalShares = 5;

		var result = _validator.Validate(null, options);

		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain(nameof(KeyEscrowBackupOptions.SplitThreshold));
	}

	// LIVENESS: the smallest legitimate quorum (2-of-N) MUST be accepted — the validator must not
	// over-reject and block a valid M-of-N scheme.
	[Theory]
	[InlineData(2, 2)]
	[InlineData(2, 5)]
	[InlineData(3, 5)]
	public void SucceedForValidMOfNQuorum(int threshold, int totalShares)
	{
		var options = CreateValidOptions();
		options.SplitThreshold = threshold;
		options.TotalShares = totalShares;

		var result = _validator.Validate(null, options);

		result.Succeeded.ShouldBeTrue();
	}

	[Fact]
	public void FailWhenTotalSharesIsLessThanSplitThreshold()
	{
		var options = CreateValidOptions();
		options.SplitThreshold = 3;
		options.TotalShares = 2;

		var result = _validator.Validate(null, options);

		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain(nameof(KeyEscrowBackupOptions.TotalShares));
	}

	[Theory]
	[InlineData("")]
	[InlineData("   ")]
	public void FailWhenEscrowProviderIsEmpty(string provider)
	{
		var options = CreateValidOptions();
		options.EscrowProvider = provider;

		var result = _validator.Validate(null, options);

		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain(nameof(KeyEscrowBackupOptions.EscrowProvider));
	}
}
