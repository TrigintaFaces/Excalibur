// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Validation;
using Excalibur.Dispatch.Validation;

namespace Excalibur.Dispatch.Tests.Validation;

/// <summary>
///     Tests for the <see cref="StrictProfileValidationRules" /> class.
/// </summary>
[Trait("Category", "Unit")]
public sealed class StrictProfileValidationRulesShould
{
	private readonly StrictProfileValidationRules _sut = new();

	[Fact]
	public void HaveStrictProfileName()
	{
		_sut.ProfileName.ShouldBe("strict");
	}

	[Fact]
	public void HaveStrictValidationLevel()
	{
		_sut.ValidationLevel.ShouldBe(ValidationLevel.Strict);
	}

	[Fact]
	public void HaveMaxMessageSizeOf500KB()
	{
		_sut.MaxMessageSize.ShouldBe(512_000);
	}

	[Fact]
	public void RequireMessageIdField()
	{
		_sut.RequiredFields.ShouldContain("MessageId");
	}

	[Fact]
	public void RequireCorrelationIdField()
	{
		_sut.RequiredFields.ShouldContain("CorrelationId");
	}

	[Fact]
	public void RequireTimestampField()
	{
		_sut.RequiredFields.ShouldContain("Timestamp");
	}

	[Fact]
	public void RequireSourceField()
	{
		_sut.RequiredFields.ShouldContain("Source");
	}

	[Fact]
	public void HaveCustomValidators()
	{
		_sut.CustomValidators.Count.ShouldBeGreaterThan(0);
	}

	[Fact]
	public void HaveFieldConstraints()
	{
		_sut.FieldConstraints.Count.ShouldBeGreaterThan(0);
	}

	[Fact]
	public void ImplementIProfileValidationRules()
	{
		_sut.ShouldBeAssignableTo<IProfileValidationRules>();
	}
}
