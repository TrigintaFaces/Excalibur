// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Application.Requests.Validation;

namespace Excalibur.Tests.Application.Requests.Validation;

[Trait("Category", "Unit")]
[Trait("Component", "Application")]
public sealed class MultiTenantValidatorShould
{
	private sealed class TestMultiTenantRequest : IAmMultiTenant
	{
		public string? TenantId { get; set; }
	}

	private readonly MultiTenantValidator<TestMultiTenantRequest> _validator = new();

	[Fact]
	public void PassValidation_ForValidTenantId()
	{
		var request = new TestMultiTenantRequest { TenantId = "valid-tenant-id" };
		var result = _validator.Validate(request);
		result.IsValid.ShouldBeTrue();
	}

	[Fact]
	public void FailValidation_ForNullTenantId()
	{
		var request = new TestMultiTenantRequest { TenantId = null };
		var result = _validator.Validate(request);
		result.IsValid.ShouldBeFalse();
	}

	[Fact]
	public void FailValidation_ForEmptyTenantId()
	{
		var request = new TestMultiTenantRequest { TenantId = "" };
		var result = _validator.Validate(request);
		result.IsValid.ShouldBeFalse();
	}

	[Fact]
	public void FailValidation_ForWhitespaceTenantId()
	{
		var request = new TestMultiTenantRequest { TenantId = "   " };
		var result = _validator.Validate(request);
		result.IsValid.ShouldBeFalse();
	}
}
