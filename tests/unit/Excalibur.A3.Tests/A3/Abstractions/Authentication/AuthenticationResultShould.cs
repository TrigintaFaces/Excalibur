// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.A3.Abstractions.Authentication;

namespace Excalibur.Tests.A3.Abstractions.Authentication;

[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class AuthenticationResultShould
{
	[Fact]
	public void CreateSucceededResult()
	{
		var principal = new AuthenticatedPrincipal("user-1", "tenant-1", null);
		var result = new AuthenticationResult(true, principal);

		result.Succeeded.ShouldBeTrue();
		result.Principal.ShouldBe(principal);
		result.FailureReason.ShouldBeNull();
	}

	[Fact]
	public void CreateFailedResult()
	{
		var result = new AuthenticationResult(false, null, "Token expired");

		result.Succeeded.ShouldBeFalse();
		result.Principal.ShouldBeNull();
		result.FailureReason.ShouldBe("Token expired");
	}

	[Fact]
	public void DefaultFailureReasonToNull()
	{
		var result = new AuthenticationResult(true, null);

		result.FailureReason.ShouldBeNull();
	}

	[Fact]
	public void SupportRecordEquality()
	{
		var principal = new AuthenticatedPrincipal("user-1", "tenant-1", null);
		var result1 = new AuthenticationResult(true, principal);
		var result2 = new AuthenticationResult(true, principal);

		result1.ShouldBe(result2);
	}

	[Fact]
	public void SupportRecordInequality()
	{
		var result1 = new AuthenticationResult(true, null);
		var result2 = new AuthenticationResult(false, null, "Failed");

		result1.ShouldNotBe(result2);
	}
}
