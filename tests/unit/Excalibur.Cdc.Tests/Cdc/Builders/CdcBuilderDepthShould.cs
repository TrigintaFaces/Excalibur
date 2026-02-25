// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Cdc;

namespace Excalibur.Cdc.Tests.Builders;

/// <summary>
/// Depth coverage tests for CdcBuilder.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class CdcBuilderDepthShould
{
	[Fact]
	public void CreateBuilderWithValidArguments()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		ICdcBuilder? capturedBuilder = null;
		services.AddCdcProcessor(builder => capturedBuilder = builder);

		// Assert
		capturedBuilder.ShouldNotBeNull();
		capturedBuilder.Services.ShouldNotBeNull();
	}

	[Fact]
	public void TrackTableWithStringName()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		ICdcBuilder? capturedBuilder = null;
		services.AddCdcProcessor(builder =>
		{
			capturedBuilder = builder;
			builder.TrackTable("Orders", _ => { });
		});

		// Assert
		capturedBuilder.ShouldNotBeNull();
	}

	[Fact]
	public void TrackTableThrowWhenTableNameIsNull()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		services.AddCdcProcessor(builder =>
		{
			Should.Throw<ArgumentNullException>(() =>
				builder.TrackTable(null!, _ => { }));
		});
	}

	[Fact]
	public void TrackTableThrowWhenTableNameIsWhitespace()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		services.AddCdcProcessor(builder =>
		{
			Should.Throw<ArgumentException>(() =>
				builder.TrackTable("   ", _ => { }));
		});
	}

	[Fact]
	public void TrackTableThrowWhenConfigureIsNull()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		services.AddCdcProcessor(builder =>
		{
			Should.Throw<ArgumentNullException>(() =>
				builder.TrackTable("Orders", null!));
		});
	}

	[Fact]
	public void TrackGenericTableWithoutConfigure()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddCdcProcessor(builder =>
		{
			builder.TrackTable<TestEntity>();
		});

		// Assert - should not throw
	}

	[Fact]
	public void TrackGenericTableWithConfigure()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddCdcProcessor(builder =>
		{
			builder.TrackTable<TestEntity>(tb => { });
		});

		// Assert - should not throw
	}

	[Fact]
	public void EnableBackgroundProcessing()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddCdcProcessor(builder =>
		{
			builder.EnableBackgroundProcessing();
		});

		// Assert - should not throw
	}

	[Fact]
	public void DisableBackgroundProcessing()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddCdcProcessor(builder =>
		{
			builder.EnableBackgroundProcessing(false);
		});

		// Assert - should not throw
	}

	[Fact]
	public void WithRecoveryThrowWhenConfigureIsNull()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		services.AddCdcProcessor(builder =>
		{
			Should.Throw<ArgumentNullException>(() =>
				builder.WithRecovery(null!));
		});
	}

	[Fact]
	public void SupportFluentChaining()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddCdcProcessor(builder =>
		{
			var result = builder
				.TrackTable("Orders", _ => { })
				.TrackTable<TestEntity>()
				.EnableBackgroundProcessing()
				.WithRecovery(r => r.MaxAttempts(3));

			// Assert
			result.ShouldNotBeNull();
		});
	}

	private sealed class TestEntity;
}
