// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Security;

using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Security.Tests.Security.Options;

/// <summary>
/// Unit tests for <see cref="RabbitMqOptionsValidator"/> internal class.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Security")]
[Trait("Feature", "Options")]
public sealed class RabbitMqOptionsValidatorShould
{
	private readonly RabbitMqOptionsValidator _sut;

	public RabbitMqOptionsValidatorShould()
	{
		_sut = new RabbitMqOptionsValidator();
	}

	[Fact]
	public void ImplementIValidateOptions()
	{
		// Assert
		_sut.ShouldBeAssignableTo<IValidateOptions<RabbitMqOptions>>();
	}

	[Fact]
	public void BeInternalAndSealed()
	{
		// Assert
		typeof(RabbitMqOptionsValidator).IsNotPublic.ShouldBeTrue();
		typeof(RabbitMqOptionsValidator).IsSealed.ShouldBeTrue();
	}

	[Fact]
	public void ReturnSuccess()
	{
		// Arrange
		var options = new RabbitMqOptions();

		// Act
		var result = _sut.Validate(null, options);

		// Assert
		result.Succeeded.ShouldBeTrue();
	}

	[Fact]
	public void ReturnSuccessWithNamedOptions()
	{
		// Arrange
		var options = new RabbitMqOptions();

		// Act
		var result = _sut.Validate("Production", options);

		// Assert
		result.Succeeded.ShouldBeTrue();
	}
}
