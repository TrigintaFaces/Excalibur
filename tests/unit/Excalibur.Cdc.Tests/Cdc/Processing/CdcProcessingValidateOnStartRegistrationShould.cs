// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Cdc;
using Excalibur.Cdc.Processing;

namespace Excalibur.Tests.Cdc.Processing;

/// <summary>
/// Verifies that <c>AddCdcProcessor()</c> properly registers
/// <see cref="CdcProcessingOptionsValidator"/> when background processing is enabled.
/// Sprint 561 S561.51: ValidateOnStart registration tests.
/// </summary>
[Trait("Category", "Unit")]
public sealed class CdcProcessingValidateOnStartRegistrationShould
{
	[Fact]
	public void RegisterCdcProcessingOptionsValidator_WhenBackgroundProcessingEnabled()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act - EnableBackgroundProcessing triggers the validator registration
		_ = services.AddCdcProcessor(cdc =>
		{
			cdc.EnableBackgroundProcessing();
		});

		// Assert
		using var provider = services.BuildServiceProvider();
		var validators = provider.GetServices<IValidateOptions<CdcProcessingOptions>>();
		validators.ShouldNotBeEmpty("EnableBackgroundProcessing should register IValidateOptions<CdcProcessingOptions>");
		validators.ShouldContain(v => v is CdcProcessingOptionsValidator);
	}

	[Fact]
	public void NotRegisterCdcProcessingOptionsValidator_WhenBackgroundProcessingDisabled()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act - without EnableBackgroundProcessing, the validator is not registered
		_ = services.AddCdcProcessor(cdc =>
		{
			// No background processing
		});

		// Assert - CdcProcessingOptionsValidator specifically should NOT be registered
		using var provider = services.BuildServiceProvider();
		var validators = provider.GetServices<IValidateOptions<CdcProcessingOptions>>();
		validators.ShouldNotContain(v => v is CdcProcessingOptionsValidator);
	}

	[Fact]
	public void ValidOptions_ResolveSuccessfully_WhenBackgroundProcessingEnabled()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddCdcProcessor(cdc =>
		{
			cdc.EnableBackgroundProcessing();
		});

		// Act
		using var provider = services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptions<CdcProcessingOptions>>();

		// Assert - default options are valid, accessing .Value triggers validation
		var value = options.Value;
		value.PollingInterval.ShouldBeGreaterThan(TimeSpan.Zero);
		value.DrainTimeoutSeconds.ShouldBeGreaterThan(0);
	}
}
