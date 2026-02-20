// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using FluentValidation;

using Microsoft.Extensions.Options;

using AppOptionsBuilderExtensions = Excalibur.Application.OptionsBuilderExtensions;

namespace Excalibur.Tests.Application;

/// <summary>
/// Unit tests for <see cref="Excalibur.Application.OptionsBuilderExtensions"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Application")]
public sealed class OptionsBuilderExtensionsShould
{
	[Fact]
	public void Validate_ValidOptions_DoesNotThrow()
	{
		// Arrange
		var services = new ServiceCollection();
		var builder = services.AddOptions<TestOptions>();
		AppOptionsBuilderExtensions.Validate<TestOptions, TestOptionsValidator>(builder);
		services.Configure<TestOptions>(o => o.Name = "Valid");

		using var provider = services.BuildServiceProvider();

		// Act & Assert — accessing the options should not throw
		var options = provider.GetRequiredService<IOptions<TestOptions>>();
		options.Value.Name.ShouldBe("Valid");
	}

	[Fact]
	public void Validate_InvalidOptions_ThrowsValidationException()
	{
		// Arrange
		var services = new ServiceCollection();
		var builder = services.AddOptions<TestOptions>();
		AppOptionsBuilderExtensions.Validate<TestOptions, TestOptionsValidator>(builder);
		services.Configure<TestOptions>(o => o.Name = ""); // Invalid: empty

		using var provider = services.BuildServiceProvider();

		// Act & Assert — FluentValidation.ValidationException is thrown from the validate callback
		var options = provider.GetRequiredService<IOptions<TestOptions>>();
		var ex = Should.Throw<ValidationException>(() => _ = options.Value);
		ex.Message.ShouldContain("Validation failed");
		ex.Errors.ShouldNotBeEmpty();
	}

	[Fact]
	public void Validate_NullBuilder_ThrowsArgumentNullException()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			AppOptionsBuilderExtensions.Validate<TestOptions, TestOptionsValidator>(null!));
	}

	[Fact]
	public void Validate_WithRuleSetDiscriminator_AppliesRuleSets()
	{
		// Arrange
		var services = new ServiceCollection();
		var builder = services.AddOptions<TestRuleSetOptions>();
		AppOptionsBuilderExtensions.Validate<TestRuleSetOptions, TestRuleSetOptionsValidator>(builder);
		services.Configure<TestRuleSetOptions>(o =>
		{
			o.Name = "Valid";
			o.UseAdvanced = true;
			o.AdvancedSetting = ""; // Invalid under "Advanced" rule set
		});

		using var provider = services.BuildServiceProvider();

		// Act & Assert — the Advanced rule set should trigger the AdvancedSetting validation
		var options = provider.GetRequiredService<IOptions<TestRuleSetOptions>>();
		var ex = Should.Throw<ValidationException>(() => _ = options.Value);
		ex.Message.ShouldContain("Validation failed");
		ex.Errors.ShouldContain(e => e.PropertyName == "AdvancedSetting");
	}

	#region Test Types

	private sealed class TestOptions
	{
		public string Name { get; set; } = string.Empty;
	}

	private sealed class TestOptionsValidator : AbstractValidator<TestOptions>
	{
		public TestOptionsValidator()
		{
			RuleFor(x => x.Name).NotEmpty();
		}
	}

	private sealed class TestRuleSetOptions
	{
		public string Name { get; set; } = string.Empty;
		public bool UseAdvanced { get; set; }
		public string AdvancedSetting { get; set; } = string.Empty;
	}

	private sealed class TestRuleSetOptionsValidator :
		AbstractValidator<TestRuleSetOptions>,
		Excalibur.Application.IDetermineValidationRuleSetByConfig<TestRuleSetOptions>
	{
		public TestRuleSetOptionsValidator()
		{
			RuleFor(x => x.Name).NotEmpty();

			RuleSet("Advanced", () =>
			{
				RuleFor(x => x.AdvancedSetting).NotEmpty();
			});
		}

		public string[] WhichRuleSets(TestRuleSetOptions config) =>
			config.UseAdvanced ? ["Advanced"] : [];
	}

	#endregion
}
