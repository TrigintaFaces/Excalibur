// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.A3.Abstractions.Authentication;

namespace Excalibur.Tests.A3.Abstractions.Authentication;

[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class AuthenticatedPrincipalShould
{
	[Fact]
	public void StoreSubjectId()
	{
		var principal = new AuthenticatedPrincipal("user-42", null, null);
		principal.SubjectId.ShouldBe("user-42");
	}

	[Fact]
	public void StoreTenantId()
	{
		var principal = new AuthenticatedPrincipal("user-1", "tenant-abc", null);
		principal.TenantId.ShouldBe("tenant-abc");
	}

	[Fact]
	public void AllowNullTenantId()
	{
		var principal = new AuthenticatedPrincipal("user-1", null, null);
		principal.TenantId.ShouldBeNull();
	}

	[Fact]
	public void StoreClaims()
	{
		var claims = new Dictionary<string, string> { ["role"] = "admin", ["email"] = "user@test.com" };
		var principal = new AuthenticatedPrincipal("user-1", "tenant-1", claims);

		principal.Claims.ShouldNotBeNull();
		principal.Claims.ShouldContainKeyAndValue("role", "admin");
		principal.Claims.ShouldContainKeyAndValue("email", "user@test.com");
	}

	[Fact]
	public void AllowNullClaims()
	{
		var principal = new AuthenticatedPrincipal("user-1", null, null);
		principal.Claims.ShouldBeNull();
	}

	[Fact]
	public void SupportRecordEquality()
	{
		var p1 = new AuthenticatedPrincipal("user-1", "tenant-1", null);
		var p2 = new AuthenticatedPrincipal("user-1", "tenant-1", null);

		p1.ShouldBe(p2);
	}
}
