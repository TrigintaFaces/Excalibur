// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Serialization;
using Excalibur.Dispatch.Abstractions.Validation;

namespace Excalibur.Dispatch.Abstractions.Tests.Serialization;

/// <summary>
/// Depth coverage tests for <see cref="SerializableValidationResult"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class SerializableValidationResultDepthShould
{
	[Fact]
	public void Success_ReturnsValidResult()
	{
		var result = SerializableValidationResult.Success();
		result.IsValid.ShouldBeTrue();
		result.Errors.ShouldBeEmpty();
	}

	[Fact]
	public void Failed_ReturnsInvalidResult()
	{
		var result = SerializableValidationResult.Failed("err1", "err2");
		result.IsValid.ShouldBeFalse();
		result.Errors.Count.ShouldBe(2);
	}

	[Fact]
	public void Errors_SetNull_DefaultsToEmpty()
	{
		var result = new SerializableValidationResult { Errors = null! };
		result.Errors.ShouldBeEmpty();
	}

	[Fact]
	public void IsValid_CanBeSetDirectly()
	{
		var result = new SerializableValidationResult { IsValid = true };
		result.IsValid.ShouldBeTrue();
	}

	[Fact]
	public void InterfaceStaticMethods_DelegateToConcreteImplementation()
	{
		// Verify the explicit interface implementations work via the interface
		IValidationResult success = SerializableValidationResult.Success();
		success.IsValid.ShouldBeTrue();

		IValidationResult failed = SerializableValidationResult.Failed("error");
		failed.IsValid.ShouldBeFalse();
	}
}
