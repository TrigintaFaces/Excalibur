// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Application.Requests.Validation;

namespace Excalibur.Tests.Application.Requests.Validation;

[Trait("Category", "Unit")]
[Trait("Component", "Application")]
public sealed class RequestValidatorShould
{
	// IActivity inherits IAmCorrelatable + IAmMultiTenant, so all test requests need both
	private sealed class SimpleActivity : IActivity
	{
		public ActivityType ActivityType => ActivityType.Command;
		public string ActivityName => "TestActivity";
		public string ActivityDisplayName => "Test Activity";
		public string ActivityDescription => "A test activity";
		public Guid CorrelationId { get; set; } = Guid.NewGuid();
		public string? TenantId { get; set; } = "default";
	}

	private sealed class SimpleActivityValidator : RequestValidator<SimpleActivity>;

	[Fact]
	public void CreateValidator_Successfully()
	{
		var validator = new SimpleActivityValidator();
		validator.ShouldNotBeNull();
	}

	[Fact]
	public void IncludeMultiTenantAndCorrelationAndActivityRules()
	{
		// IActivity inherits IAmCorrelatable and IAmMultiTenant, so all three included
		var validator = new SimpleActivityValidator();
		var result = validator.Validate(new SimpleActivity
		{
			TenantId = "tenant-1",
			CorrelationId = Guid.NewGuid(),
		});
		result.IsValid.ShouldBeTrue();
	}

	[Fact]
	public void FailValidation_WhenTenantIdMissing()
	{
		var validator = new SimpleActivityValidator();
		var result = validator.Validate(new SimpleActivity
		{
			TenantId = null,
			CorrelationId = Guid.NewGuid(),
		});
		result.IsValid.ShouldBeFalse();
		result.Errors.ShouldContain(e => e.PropertyName == "TenantId");
	}

	[Fact]
	public void FailValidation_WhenCorrelationIdEmpty()
	{
		var validator = new SimpleActivityValidator();
		var result = validator.Validate(new SimpleActivity
		{
			TenantId = "tenant-1",
			CorrelationId = Guid.Empty,
		});
		result.IsValid.ShouldBeFalse();
	}

	[Fact]
	public void PassValidation_WhenAllFieldsValid()
	{
		var validator = new SimpleActivityValidator();
		var result = validator.Validate(new SimpleActivity
		{
			TenantId = "valid-tenant",
			CorrelationId = Guid.NewGuid(),
		});
		result.IsValid.ShouldBeTrue();
		result.Errors.ShouldBeEmpty();
	}
}
