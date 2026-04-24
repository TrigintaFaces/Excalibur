// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Cdc;
using Excalibur.Cdc.Processing;
using Microsoft.Extensions.Configuration;

namespace Excalibur.Cdc.Tests.Builders;

/// <summary>
/// Unit tests for <see cref="ICdcBuilder.BindProcessingConfiguration"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class CdcBuilderBindProcessingConfigurationShould : UnitTestBase
{
	private static CdcBuilder CreateBuilder()
	{
		return new CdcBuilder(new ServiceCollection(), new CdcOptions());
	}

	[Fact]
	public void ThrowArgumentNullException_WhenSectionPathIsNull()
	{
		// Arrange
		var builder = CreateBuilder();

		// Act & Assert
		Should.Throw<ArgumentNullException>(() => builder.BindProcessingConfiguration(null!));
	}

	[Fact]
	public void ThrowArgumentException_WhenSectionPathIsEmpty()
	{
		// Arrange
		var builder = CreateBuilder();

		// Act & Assert
		Should.Throw<ArgumentException>(() => builder.BindProcessingConfiguration(""));
	}

	[Fact]
	public void ThrowArgumentException_WhenSectionPathIsWhitespace()
	{
		// Arrange
		var builder = CreateBuilder();

		// Act & Assert
		Should.Throw<ArgumentException>(() => builder.BindProcessingConfiguration("   "));
	}

	[Fact]
	public void StoreProcessingConfigSectionPath()
	{
		// Arrange
		var builder = CreateBuilder();

		// Act
		builder.BindProcessingConfiguration("Cdc:Processing");

		// Assert
		builder.ProcessingConfigSectionPath.ShouldBe("Cdc:Processing");
	}

	[Fact]
	public void ReturnSameBuilder_ForFluentChaining()
	{
		// Arrange
		var builder = CreateBuilder();

		// Act
		var result = builder.BindProcessingConfiguration("Cdc:Processing");

		// Assert
		result.ShouldBeSameAs(builder);
	}

	[Fact]
	public void OverwritePreviousPath_WhenCalledMultipleTimes()
	{
		// Arrange
		var builder = CreateBuilder();

		// Act
		builder.BindProcessingConfiguration("Cdc:Processing");
		builder.BindProcessingConfiguration("Cdc:OtherProcessing");

		// Assert
		builder.ProcessingConfigSectionPath.ShouldBe("Cdc:OtherProcessing");
	}

	[Fact]
	public void HaveNullPath_ByDefault()
	{
		// Arrange & Act
		var builder = CreateBuilder();

		// Assert
		builder.ProcessingConfigSectionPath.ShouldBeNull();
	}

	[Fact]
	public void BindConfigurationToOptions_WhenCalledWithBackgroundProcessing()
	{
		// Arrange
		var services = new ServiceCollection();
		var config = new Microsoft.Extensions.Configuration.ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["Cdc:Processing:BatchSize"] = "500",
			})
			.Build();
		services.AddSingleton<Microsoft.Extensions.Configuration.IConfiguration>(config);

		// Act
		services.AddCdcProcessor(cdc => cdc
			.EnableBackgroundProcessing()
			.BindProcessingConfiguration("Cdc:Processing"));

		// Assert — CdcProcessingOptions should be registered
		var provider = services.BuildServiceProvider();
		var options = provider.GetService<IOptions<CdcProcessingOptions>>();
		options.ShouldNotBeNull();
	}

	[Fact]
	public void NotBindConfiguration_WhenNotCalled()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddSingleton<Microsoft.Extensions.Configuration.IConfiguration>(
			new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build());

		// Act
		services.AddCdcProcessor(cdc => cdc.EnableBackgroundProcessing());

		// Assert — CdcProcessingOptions should still be registered but without config binding
		var provider = services.BuildServiceProvider();
		var options = provider.GetService<IOptions<CdcProcessingOptions>>();
		options.ShouldNotBeNull();
	}

	[Fact]
	public void NotRegisterProcessingOptions_WhenBackgroundProcessingDisabled()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddSingleton<Microsoft.Extensions.Configuration.IConfiguration>(
			new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build());

		// Act — bind processing config but do NOT enable background processing
		services.AddCdcProcessor(cdc => cdc
			.BindProcessingConfiguration("Cdc:Processing"));

		// Assert — CdcProcessingOptions should NOT be registered since background processing is off
		var processingOptionsDescriptors = services
			.Where(sd => sd.ServiceType == typeof(IConfigureOptions<CdcProcessingOptions>))
			.ToList();
		processingOptionsDescriptors.ShouldBeEmpty();
	}
}
