// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Cdc;

namespace Excalibur.Cdc.Tests.Builders;

/// <summary>
/// Unit tests for <see cref="ICdcBuilder.BindTrackedTables"/> (T.8 / fhrb1).
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class CdcBuilderBindTrackedTablesShould : UnitTestBase
{
	private static CdcBuilder CreateBuilder()
	{
		return new CdcBuilder(new ServiceCollection(), new CdcOptions());
	}

	[Fact]
	public void ThrowArgumentNullException_WhenConfigSectionPathIsNull()
	{
		// Arrange
		var builder = CreateBuilder();

		// Act & Assert
		Should.Throw<ArgumentNullException>(() => builder.BindTrackedTables(null!));
	}

	[Fact]
	public void ThrowArgumentException_WhenConfigSectionPathIsEmpty()
	{
		// Arrange
		var builder = CreateBuilder();

		// Act & Assert
		Should.Throw<ArgumentException>(() => builder.BindTrackedTables(""));
	}

	[Fact]
	public void ThrowArgumentException_WhenConfigSectionPathIsWhitespace()
	{
		// Arrange
		var builder = CreateBuilder();

		// Act & Assert
		Should.Throw<ArgumentException>(() => builder.BindTrackedTables("   "));
	}

	[Fact]
	public void StoreConfigSectionPath()
	{
		// Arrange
		var builder = CreateBuilder();

		// Act
		builder.BindTrackedTables("Cdc:Tables");

		// Assert
		builder.TrackedTablesConfigSectionPath.ShouldBe("Cdc:Tables");
	}

	[Fact]
	public void ReturnSameBuilder_ForFluentChaining()
	{
		// Arrange
		var builder = CreateBuilder();

		// Act
		var result = builder.BindTrackedTables("Cdc:Tables");

		// Assert
		result.ShouldBeSameAs(builder);
	}

	[Fact]
	public void OverwritePreviousPath_WhenCalledMultipleTimes()
	{
		// Arrange
		var builder = CreateBuilder();

		// Act
		builder.BindTrackedTables("Cdc:Tables");
		builder.BindTrackedTables("Cdc:OtherTables");

		// Assert
		builder.TrackedTablesConfigSectionPath.ShouldBe("Cdc:OtherTables");
	}

	[Fact]
	public void RegisterPostConfigureOptions_WhenCalledInAddCdcProcessor()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddSingleton<Microsoft.Extensions.Configuration.IConfiguration>(
			new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build());

		// Act
		services.AddCdcProcessor(cdc => cdc.BindTrackedTables("Cdc:Tables"));

		// Assert
		services.ShouldContain(sd =>
			sd.ServiceType == typeof(IPostConfigureOptions<CdcOptions>));
	}

	[Fact]
	public void NotRegisterPostConfigureOptions_WhenNotCalled()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddCdcProcessor(_ => { });

		// Assert — no IPostConfigureOptions with factory should be registered
		var postConfigureDescriptors = services
			.Where(sd => sd.ServiceType == typeof(IPostConfigureOptions<CdcOptions>))
			.Where(sd => sd.ImplementationFactory != null)
			.ToList();
		postConfigureDescriptors.ShouldBeEmpty();
	}
}
