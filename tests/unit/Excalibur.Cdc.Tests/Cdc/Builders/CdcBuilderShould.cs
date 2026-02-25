// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Cdc;

namespace Excalibur.Tests.Cdc.Builders;

/// <summary>
/// Unit tests for <see cref="ICdcBuilder"/> fluent API.
/// </summary>
/// <remarks>
/// These tests validate the ADR-098 Microsoft-style fluent builder pattern implementation
/// for the CDC (Change Data Capture) subsystem.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class CdcBuilderShould : UnitTestBase
{
	[Fact]
	public void AddCdcProcessor_WithFluentBuilder_ThrowsOnNullServices()
	{
		// Arrange
		IServiceCollection services = null!;
		Action<ICdcBuilder> configure = _ => { };

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => services.AddCdcProcessor(configure));
	}

	[Fact]
	public void AddCdcProcessor_WithFluentBuilder_ThrowsOnNullConfigure()
	{
		// Arrange
		var services = new ServiceCollection();
		Action<ICdcBuilder> configure = null!;

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => services.AddCdcProcessor(configure));
	}

	[Fact]
	public void AddCdcProcessor_WithFluentBuilder_ReturnsServiceCollection()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		var result = services.AddCdcProcessor((ICdcBuilder _) => { });

		// Assert
		result.ShouldBeSameAs(services);
	}

	[Fact]
	public void AddCdcProcessor_WithFluentBuilder_RegistersDefaultOptions()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddCdcProcessor((ICdcBuilder _) => { });
		var provider = services.BuildServiceProvider();

		// Assert
		var options = provider.GetService<IOptions<CdcOptions>>();
		_ = options.ShouldNotBeNull();
		_ = options.Value.ShouldNotBeNull();
		options.Value.RecoveryStrategy.ShouldBe(StalePositionRecoveryStrategy.FallbackToEarliest);
		options.Value.MaxRecoveryAttempts.ShouldBe(3);
		options.Value.RecoveryAttemptDelay.ShouldBe(TimeSpan.FromSeconds(1));
		options.Value.EnableStructuredLogging.ShouldBeTrue();
		options.Value.EnableBackgroundProcessing.ShouldBeFalse();
	}

	[Fact]
	public void AddCdcProcessor_WithFluentBuilder_ProvidesBuilderWithServices()
	{
		// Arrange
		var services = new ServiceCollection();
		IServiceCollection? capturedServices = null;

		// Act
		_ = services.AddCdcProcessor(builder =>
		{
			capturedServices = builder.Services;
		});

		// Assert
		_ = capturedServices.ShouldNotBeNull();
		capturedServices.ShouldBeSameAs(services);
	}

	[Fact]
	public void AddCdcProcessor_WithNoArgs_RegistersCoreServices()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddCdcProcessor();
		var provider = services.BuildServiceProvider();

		// Assert
		var options = provider.GetService<IOptions<CdcOptions>>();
		_ = options.ShouldNotBeNull();
	}

	[Fact]
	public void HasCdcProcessor_ReturnsFalse_WhenNotRegistered()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		var result = services.HasCdcProcessor();

		// Assert
		result.ShouldBeFalse();
	}

	[Fact]
	public void HasCdcProcessor_ReturnsTrue_WhenRegistered()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddCdcProcessor((ICdcBuilder _) => { });

		// Act
		var result = services.HasCdcProcessor();

		// Assert
		result.ShouldBeTrue();
	}

	[Fact]
	public void TrackTable_ThrowsOnNullTableName()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			services.AddCdcProcessor(builder =>
			{
				_ = builder.TrackTable(null!, _ => { });
			}));
	}

	[Fact]
	public void TrackTable_ThrowsOnEmptyTableName()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() =>
			services.AddCdcProcessor(builder =>
			{
				_ = builder.TrackTable("", _ => { });
			}));
	}

	[Fact]
	public void TrackTable_ThrowsOnWhitespaceTableName()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() =>
			services.AddCdcProcessor(builder =>
			{
				_ = builder.TrackTable("   ", _ => { });
			}));
	}

	[Fact]
	public void TrackTable_ThrowsOnNullConfigure()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			services.AddCdcProcessor(builder =>
			{
				_ = builder.TrackTable("dbo.Orders", null!);
			}));
	}

	[Fact]
	public void TrackTable_RegistersTableTracking()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddCdcProcessor(builder =>
		{
			_ = builder.TrackTable("dbo.Orders", _ => { });
		});
		var provider = services.BuildServiceProvider();

		// Assert
		var options = provider.GetRequiredService<IOptions<CdcOptions>>();
		options.Value.TrackedTables.ShouldNotBeEmpty();
		options.Value.TrackedTables.ShouldContain(t => t.TableName == "dbo.Orders");
	}

	[Fact]
	public void TrackTable_SupportsMultipleTables()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddCdcProcessor(builder =>
		{
			_ = builder.TrackTable("dbo.Orders", _ => { })
				   .TrackTable("dbo.Customers", _ => { })
				   .TrackTable("dbo.Products", _ => { });
		});
		var provider = services.BuildServiceProvider();

		// Assert
		var options = provider.GetRequiredService<IOptions<CdcOptions>>();
		options.Value.TrackedTables.Count.ShouldBe(3);
		options.Value.TrackedTables.ShouldContain(t => t.TableName == "dbo.Orders");
		options.Value.TrackedTables.ShouldContain(t => t.TableName == "dbo.Customers");
		options.Value.TrackedTables.ShouldContain(t => t.TableName == "dbo.Products");
	}

	[Fact]
	public void TrackTable_GenericOverload_InfersTableName()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddCdcProcessor(builder =>
		{
			_ = builder.TrackTable<Order>(_ => { });
		});
		var provider = services.BuildServiceProvider();

		// Assert
		var options = provider.GetRequiredService<IOptions<CdcOptions>>();
		options.Value.TrackedTables.ShouldContain(t => t.TableName == "dbo.Orders");
	}

	[Fact]
	public void TrackTable_GenericOverload_WithoutConfigure()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddCdcProcessor(builder =>
		{
			_ = builder.TrackTable<Customer>();
		});
		var provider = services.BuildServiceProvider();

		// Assert
		var options = provider.GetRequiredService<IOptions<CdcOptions>>();
		options.Value.TrackedTables.ShouldContain(t => t.TableName == "dbo.Customers");
	}

	[Fact]
	public void WithRecovery_ThrowsOnNullConfigure()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			services.AddCdcProcessor(builder =>
			{
				_ = builder.WithRecovery(null!);
			}));
	}

	[Fact]
	public void WithRecovery_ConfiguresRecoveryStrategy()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddCdcProcessor(builder =>
		{
			_ = builder.WithRecovery(recovery =>
			{
				_ = recovery.Strategy(StalePositionRecoveryStrategy.FallbackToLatest);
			});
		});
		var provider = services.BuildServiceProvider();

		// Assert
		var options = provider.GetRequiredService<IOptions<CdcOptions>>();
		options.Value.RecoveryStrategy.ShouldBe(StalePositionRecoveryStrategy.FallbackToLatest);
	}

	[Fact]
	public void WithRecovery_ConfiguresMaxAttempts()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddCdcProcessor(builder =>
		{
			_ = builder.WithRecovery(recovery =>
			{
				_ = recovery.MaxAttempts(10);
			});
		});
		var provider = services.BuildServiceProvider();

		// Assert
		var options = provider.GetRequiredService<IOptions<CdcOptions>>();
		options.Value.MaxRecoveryAttempts.ShouldBe(10);
	}

	[Fact]
	public void WithRecovery_ConfiguresAttemptDelay()
	{
		// Arrange
		var services = new ServiceCollection();
		var expectedDelay = TimeSpan.FromSeconds(30);

		// Act
		_ = services.AddCdcProcessor(builder =>
		{
			_ = builder.WithRecovery(recovery =>
			{
				_ = recovery.AttemptDelay(expectedDelay);
			});
		});
		var provider = services.BuildServiceProvider();

		// Assert
		var options = provider.GetRequiredService<IOptions<CdcOptions>>();
		options.Value.RecoveryAttemptDelay.ShouldBe(expectedDelay);
	}

	[Fact]
	public void WithRecovery_ConfiguresStructuredLogging()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddCdcProcessor(builder =>
		{
			_ = builder.WithRecovery(recovery =>
			{
				_ = recovery.EnableStructuredLogging(false);
			});
		});
		var provider = services.BuildServiceProvider();

		// Assert
		var options = provider.GetRequiredService<IOptions<CdcOptions>>();
		options.Value.EnableStructuredLogging.ShouldBeFalse();
	}

	[Fact]
	public void WithRecovery_SupportsFluentChaining()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddCdcProcessor(builder =>
		{
			_ = builder.WithRecovery(recovery =>
			{
				_ = recovery
					.Strategy(StalePositionRecoveryStrategy.FallbackToEarliest)
					.MaxAttempts(5)
					.AttemptDelay(TimeSpan.FromSeconds(15))
					.EnableStructuredLogging(true);
			});
		});
		var provider = services.BuildServiceProvider();

		// Assert
		var options = provider.GetRequiredService<IOptions<CdcOptions>>();
		options.Value.RecoveryStrategy.ShouldBe(StalePositionRecoveryStrategy.FallbackToEarliest);
		options.Value.MaxRecoveryAttempts.ShouldBe(5);
		options.Value.RecoveryAttemptDelay.ShouldBe(TimeSpan.FromSeconds(15));
		options.Value.EnableStructuredLogging.ShouldBeTrue();
	}

	[Fact]
	public void EnableBackgroundProcessing_SetsOptionFlag()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddCdcProcessor(builder =>
		{
			_ = builder.EnableBackgroundProcessing();
		});
		var provider = services.BuildServiceProvider();

		// Assert
		var options = provider.GetRequiredService<IOptions<CdcOptions>>();
		options.Value.EnableBackgroundProcessing.ShouldBeTrue();
	}

	[Fact]
	public void EnableBackgroundProcessing_DisablesWhenFalse()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddCdcProcessor(builder =>
		{
			_ = builder.EnableBackgroundProcessing(false);
		});
		var provider = services.BuildServiceProvider();

		// Assert
		var options = provider.GetRequiredService<IOptions<CdcOptions>>();
		options.Value.EnableBackgroundProcessing.ShouldBeFalse();
	}

	[Fact]
	public void Builder_SupportsFullFluentChaining()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddCdcProcessor(builder =>
		{
			_ = builder
				.TrackTable("dbo.Orders", t => t.MapAll<OrderChangedEvent>())
				.TrackTable("dbo.Customers", t => t.MapInsert<CustomerCreatedEvent>())
				.WithRecovery(r => r.MaxAttempts(5).AttemptDelay(TimeSpan.FromSeconds(10)))
				.EnableBackgroundProcessing();
		});
		var provider = services.BuildServiceProvider();

		// Assert
		var options = provider.GetRequiredService<IOptions<CdcOptions>>();
		options.Value.TrackedTables.Count.ShouldBe(2);
		options.Value.MaxRecoveryAttempts.ShouldBe(5);
		options.Value.RecoveryAttemptDelay.ShouldBe(TimeSpan.FromSeconds(10));
		options.Value.EnableBackgroundProcessing.ShouldBeTrue();
	}

	// Test entity types for TrackTable<T>
	private sealed class Order { }
	private sealed class Customer { }
	private sealed class OrderChangedEvent { }
	private sealed class CustomerCreatedEvent { }
}
