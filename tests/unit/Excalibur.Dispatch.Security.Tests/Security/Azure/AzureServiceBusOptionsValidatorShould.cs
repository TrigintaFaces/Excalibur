// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Security.Azure;

using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Security.Tests.Azure;

/// <summary>
/// Unit tests for <see cref="AzureServiceBusOptionsValidator"/>.
/// Verifies Sprint 390 implementation: Azure Service Bus options validator moved to dedicated package.
/// </summary>
[Trait("Category", "Unit")]
public sealed class AzureServiceBusOptionsValidatorShould : UnitTestBase
{
	[Fact]
	public void Validate_ReturnsSuccess_WhenOptionsAreValid()
	{
		// Arrange
		var validator = new AzureServiceBusOptionsValidator();
		var options = new AzureServiceBusOptions();

		// Act
		var result = validator.Validate(null, options);

		// Assert
		result.Succeeded.ShouldBeTrue();
	}

	[Fact]
	public void Validate_ReturnsSuccess_WhenOptionsNameIsProvided()
	{
		// Arrange
		var validator = new AzureServiceBusOptionsValidator();
		var options = new AzureServiceBusOptions();

		// Act
		var result = validator.Validate("TestOptions", options);

		// Assert
		result.Succeeded.ShouldBeTrue();
	}

	[Fact]
	public void ImplementsIValidateOptions()
	{
		// Arrange
		var validator = new AzureServiceBusOptionsValidator();

		// Assert
		_ = validator.ShouldBeAssignableTo<IValidateOptions<AzureServiceBusOptions>>();
	}
}
