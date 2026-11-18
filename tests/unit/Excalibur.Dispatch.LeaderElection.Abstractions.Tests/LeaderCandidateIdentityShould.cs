// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch.LeaderElection;

namespace Excalibur.Dispatch.LeaderElection.Abstractions.Tests;

/// <summary>Verifies candidate identity precedence and the complete generated fallback.</summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
[Trait("Pattern", "Regression")]
public sealed class LeaderCandidateIdentityShould : UnitTestBase
{
	[Fact]
	public void PreserveConfiguredIdentityWithoutReadingTheEnvironment()
	{
		LeaderCandidateIdentity.Resolve(" configured-instance ",
			_ => throw new InvalidOperationException("Configured identity must bypass environment lookup."))
			.ShouldBe(" configured-instance ");
	}

	[Fact]
	public void PreferHostnameWithoutReadingPodName()
	{
		LeaderCandidateIdentity.Resolve(null, name => name == "HOSTNAME"
			? " host-name "
			: throw new InvalidOperationException("HOSTNAME must take precedence."))
			.ShouldBe(" host-name ");
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData(" ")]
	public void UsePodNameWhenHostnameIsUnset(string? hostname)
	{
		LeaderCandidateIdentity.Resolve(" ", name => name == "HOSTNAME" ? hostname : " pod-name ")
			.ShouldBe(" pod-name ");
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData(" ")]
	public void PreserveTheEntireGeneratedGuidWhenEnvironmentIdentityIsUnset(string? environmentValue)
	{
		var first = LeaderCandidateIdentity.Resolve(null, _ => environmentValue);
		var second = LeaderCandidateIdentity.Resolve(null, _ => environmentValue);

		// Truncating machine-name + GUID to 24 characters loses nonce bits, potentially all of them.
		// A complete N-format GUID is independent of machine-name length and retains every nonce bit.
		first.Length.ShouldBe(32);
		Guid.TryParseExact(first, "N", out var parsed).ShouldBeTrue();
		parsed.ShouldNotBe(Guid.Empty);
		second.Length.ShouldBe(32);
		Guid.TryParseExact(second, "N", out _).ShouldBeTrue();
		second.ShouldNotBe(first);
	}
}
