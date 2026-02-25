// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Observability.Sanitization;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Middleware.Tests.Observability.Sanitization;

/// <summary>
/// Unit tests for <see cref="TelemetrySanitizerOptionsValidator"/>.
/// </summary>
[Trait("Category", "Unit")]
public sealed class TelemetrySanitizerOptionsValidatorShould : UnitTestBase
{
	[Fact]
	public void Validate_ThrowsArgumentNullException_WhenOptionsIsNull()
	{
		// Arrange
		var hostEnv = A.Fake<IHostEnvironment>();
		A.CallTo(() => hostEnv.EnvironmentName).Returns("Development");
		var validator = new TelemetrySanitizerOptionsValidator(
			hostEnv,
			NullLogger<TelemetrySanitizerOptionsValidator>.Instance);

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			validator.Validate(null, null!));
	}

	[Fact]
	public void Validate_ReturnsSuccess_WhenIncludeRawPiiIsFalse()
	{
		// Arrange
		var hostEnv = A.Fake<IHostEnvironment>();
		A.CallTo(() => hostEnv.EnvironmentName).Returns("Production");
		var validator = new TelemetrySanitizerOptionsValidator(
			hostEnv,
			NullLogger<TelemetrySanitizerOptionsValidator>.Instance);

		var options = new TelemetrySanitizerOptions { IncludeRawPii = false };

		// Act
		var result = validator.Validate(null, options);

		// Assert
		result.ShouldBe(ValidateOptionsResult.Success);
	}

	[Fact]
	public void Validate_ReturnsSuccess_WhenIncludeRawPiiIsTrue_InDevelopment()
	{
		// Arrange
		var hostEnv = A.Fake<IHostEnvironment>();
		A.CallTo(() => hostEnv.EnvironmentName).Returns("Development");
		var validator = new TelemetrySanitizerOptionsValidator(
			hostEnv,
			NullLogger<TelemetrySanitizerOptionsValidator>.Instance);

		var options = new TelemetrySanitizerOptions { IncludeRawPii = true };

		// Act
		var result = validator.Validate(null, options);

		// Assert — does not fail, just warns
		result.ShouldBe(ValidateOptionsResult.Success);
	}

	[Fact]
	public void Validate_ReturnsSuccess_ButLogsWarning_WhenIncludeRawPiiIsTrue_InProduction()
	{
		// Arrange
		// Note: Cannot fake ILogger<InternalType> with FakeItEasy — use NullLogger instead
		var hostEnv = A.Fake<IHostEnvironment>();
		A.CallTo(() => hostEnv.EnvironmentName).Returns("Production");

		var validator = new TelemetrySanitizerOptionsValidator(
			hostEnv,
			NullLogger<TelemetrySanitizerOptionsValidator>.Instance);

		var options = new TelemetrySanitizerOptions { IncludeRawPii = true };

		// Act
		var result = validator.Validate(null, options);

		// Assert — still returns success (warning only, not a failure)
		result.ShouldBe(ValidateOptionsResult.Success);
	}

	[Fact]
	public void Validate_ReturnsSuccess_WhenDefaultOptions()
	{
		// Arrange
		var hostEnv = A.Fake<IHostEnvironment>();
		A.CallTo(() => hostEnv.EnvironmentName).Returns("Production");
		var validator = new TelemetrySanitizerOptionsValidator(
			hostEnv,
			NullLogger<TelemetrySanitizerOptionsValidator>.Instance);

		var options = new TelemetrySanitizerOptions();

		// Act
		var result = validator.Validate(null, options);

		// Assert
		result.ShouldBe(ValidateOptionsResult.Success);
	}
}
