// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Diagnostics;

using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Tests.Diagnostics;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class DispatchTelemetryOptionsValidatorShould
{
	[Fact]
	public void Validate_WithValidOptions_ReturnSuccess()
	{
		// Arrange
		var validator = CreateValidator();
		var options = new DispatchTelemetryOptions();

		// Act
		var result = validator.Validate(null, options);

		// Assert
		result.Succeeded.ShouldBeTrue();
	}

	[Fact]
	public void Validate_WithEmptyServiceName_ReturnFailure()
	{
		// Arrange
		var validator = CreateValidator();
		var options = new DispatchTelemetryOptions { ServiceName = "" };

		// Act
		var result = validator.Validate(null, options);

		// Assert
		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldNotBeNullOrWhiteSpace();
	}

	[Fact]
	public void Validate_WithNullServiceVersion_ReturnFailure()
	{
		// Arrange
		var validator = CreateValidator();
		var options = new DispatchTelemetryOptions { ServiceVersion = null! };

		// Act
		var result = validator.Validate(null, options);

		// Assert
		result.Failed.ShouldBeTrue();
	}

	[Fact]
	public void Validate_WithNegativeSamplingRatio_ReturnFailure()
	{
		// Arrange
		var validator = CreateValidator();
		var options = new DispatchTelemetryOptions { SamplingRatio = -1.0 };

		// Act
		var result = validator.Validate(null, options);

		// Assert
		result.Failed.ShouldBeTrue();
	}

	[Fact]
	public void Validate_WithZeroExportTimeout_ReturnFailure()
	{
		// Arrange
		var validator = CreateValidator();
		var options = new DispatchTelemetryOptions { ExportTimeout = TimeSpan.Zero };

		// Act
		var result = validator.Validate(null, options);

		// Assert
		result.Failed.ShouldBeTrue();
	}

	private static IValidateOptions<DispatchTelemetryOptions> CreateValidator()
	{
		// DispatchTelemetryOptionsValidator is internal - create via DI registration
		var services = new ServiceCollection();
		services.AddSingleton<IValidateOptions<DispatchTelemetryOptions>, DispatchTelemetryOptionsValidator>();
		using var provider = services.BuildServiceProvider();
		return provider.GetRequiredService<IValidateOptions<DispatchTelemetryOptions>>();
	}
}
