// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Cdc;

namespace Excalibur.Cdc.Tests.Builders;

/// <summary>
/// Unit tests for <see cref="ICdcBuilder.TrackTablesFromHandlers"/> (T.9 / j5oj1).
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class CdcBuilderTrackTablesFromHandlersShould : UnitTestBase
{
	private static CdcBuilder CreateBuilder()
	{
		return new CdcBuilder(new ServiceCollection(), new CdcOptions());
	}

	[Fact]
	public void SetAutoDiscoverFromHandlersFlag()
	{
		// Arrange
		var builder = CreateBuilder();

		// Act
		builder.TrackTablesFromHandlers();

		// Assert
		builder.AutoDiscoverFromHandlers.ShouldBeTrue();
	}

	[Fact]
	public void ReturnSameBuilder_ForFluentChaining()
	{
		// Arrange
		var builder = CreateBuilder();

		// Act
		var result = builder.TrackTablesFromHandlers();

		// Assert
		result.ShouldBeSameAs(builder);
	}

	[Fact]
	public void DefaultToFalse_WhenNotCalled()
	{
		// Arrange & Act
		var builder = CreateBuilder();

		// Assert
		builder.AutoDiscoverFromHandlers.ShouldBeFalse();
	}

	[Fact]
	public void RegisterPostConfigureOptions_WhenCalledInAddCdcProcessor()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddCdcProcessor(cdc => cdc.TrackTablesFromHandlers());

		// Assert
		services.ShouldContain(sd =>
			sd.ServiceType == typeof(IPostConfigureOptions<CdcOptions>));
	}

	[Fact]
	public void CombineWithBindTrackedTables_InFluentChain()
	{
		// Arrange
		var builder = CreateBuilder();

		// Act
		builder
			.BindTrackedTables("Cdc:Tables")
			.TrackTablesFromHandlers();

		// Assert
		builder.TrackedTablesConfigSectionPath.ShouldBe("Cdc:Tables");
		builder.AutoDiscoverFromHandlers.ShouldBeTrue();
	}
}
