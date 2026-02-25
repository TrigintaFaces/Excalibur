// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Cdc;
using Excalibur.Cdc.Processing;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Excalibur.Tests.Cdc.DependencyInjection;

/// <summary>
/// Unit tests for <see cref="CdcServiceCollectionExtensions"/>.
/// Tests DI registration, configuration, and service discovery.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Cdc")]
[Trait("Priority", "0")]
public sealed class CdcServiceCollectionExtensionsShould : UnitTestBase
{
	#region AddCdcProcessor with Configure Action Tests

	[Fact]
	public void ThrowArgumentNullException_WhenServicesIsNull_WithConfigureAction()
	{
		// Arrange
		IServiceCollection services = null!;
		Action<ICdcBuilder> configure = _ => { };

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			services.AddCdcProcessor(configure));
	}

	[Fact]
	public void ThrowArgumentNullException_WhenConfigureIsNull()
	{
		// Arrange
		var services = new ServiceCollection();
		Action<ICdcBuilder>? configure = null;

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			services.AddCdcProcessor(configure));
	}

	[Fact]
	public void ReturnSameServiceCollection_WithConfigureAction()
	{
		// Arrange
		var services = new ServiceCollection();
		Action<ICdcBuilder> configure = _ => { };

		// Act
		var result = services.AddCdcProcessor(configure);

		// Assert
		result.ShouldBeSameAs(services);
	}

	[Fact]
	public void RegisterCdcOptionsConfiguration()
	{
		// Arrange
		var services = new ServiceCollection();
		Action<ICdcBuilder> configure = _ => { };

		// Act
		services.AddCdcProcessor(configure);
		var provider = services.BuildServiceProvider();

		// Assert
		var options = provider.GetService<IOptions<CdcOptions>>();
		options.ShouldNotBeNull();
		options.Value.ShouldNotBeNull();
	}

	[Fact]
	public void ApplyConfigurationFromBuilder()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddCdcProcessor(cdc =>
		{
			cdc.WithRecovery(r =>
			{
				r.Strategy(StalePositionRecoveryStrategy.FallbackToLatest);
				r.MaxAttempts(10);
			});
		});
		var provider = services.BuildServiceProvider();

		// Assert
		var options = provider.GetRequiredService<IOptions<CdcOptions>>().Value;
		options.RecoveryStrategy.ShouldBe(StalePositionRecoveryStrategy.FallbackToLatest);
		options.MaxRecoveryAttempts.ShouldBe(10);
	}

	[Fact]
	public void RegisterHostedService_WhenBackgroundProcessingEnabled()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddCdcProcessor(cdc =>
		{
			cdc.EnableBackgroundProcessing();
		});

		// Assert
		var hostedServiceDescriptor = services.FirstOrDefault(
			s => s.ServiceType == typeof(IHostedService) &&
			     s.ImplementationType == typeof(CdcProcessingHostedService));
		hostedServiceDescriptor.ShouldNotBeNull();
	}

	[Fact]
	public void NotRegisterHostedService_WhenBackgroundProcessingNotEnabled()
	{
		// Arrange
		var services = new ServiceCollection();
		Action<ICdcBuilder> configure = _ => { };

		// Act
		services.AddCdcProcessor(configure);

		// Assert
		var hostedServiceDescriptor = services.FirstOrDefault(
			s => s.ServiceType == typeof(IHostedService) &&
			     s.ImplementationType == typeof(CdcProcessingHostedService));
		hostedServiceDescriptor.ShouldBeNull();
	}

	[Fact]
	public void RegisterCdcProcessingOptions_WhenBackgroundProcessingEnabled()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddCdcProcessor(cdc =>
		{
			cdc.EnableBackgroundProcessing();
		});
		var provider = services.BuildServiceProvider();

		// Assert
		var options = provider.GetService<IOptions<CdcProcessingOptions>>();
		options.ShouldNotBeNull();
	}

	#endregion

	#region AddCdcProcessor Parameterless Tests

	[Fact]
	public void ThrowArgumentNullException_WhenServicesIsNull_Parameterless()
	{
		// Arrange
		IServiceCollection services = null!;

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			services.AddCdcProcessor());
	}

	[Fact]
	public void ReturnSameServiceCollection_Parameterless()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		var result = services.AddCdcProcessor();

		// Assert
		result.ShouldBeSameAs(services);
	}

	[Fact]
	public void RegisterCoreServices_Parameterless()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddCdcProcessor();
		var provider = services.BuildServiceProvider();

		// Assert
		var options = provider.GetService<IOptions<CdcOptions>>();
		options.ShouldNotBeNull();
	}

	#endregion

	#region HasCdcProcessor Tests

	[Fact]
	public void ThrowArgumentNullException_WhenServicesIsNull_InHasCdcProcessor()
	{
		// Arrange
		IServiceCollection services = null!;

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			services.HasCdcProcessor());
	}

	[Fact]
	public void ReturnFalse_WhenCdcNotRegistered()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		var result = services.HasCdcProcessor();

		// Assert
		result.ShouldBeFalse();
	}

	[Fact]
	public void ReturnTrue_WhenCdcRegisteredWithConfigureAction()
	{
		// Arrange
		var services = new ServiceCollection();
		Action<ICdcBuilder> configure = _ => { };
		services.AddCdcProcessor(configure);

		// Act
		var result = services.HasCdcProcessor();

		// Assert
		result.ShouldBeTrue();
	}

	[Fact]
	public void ReturnTrue_WhenCdcRegisteredParameterless()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddCdcProcessor();

		// Act
		var result = services.HasCdcProcessor();

		// Assert
		result.ShouldBeTrue();
	}

	#endregion

	#region Idempotency Tests

	[Fact]
	public void AllowMultipleRegistrations_Parameterless()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act - should not throw
		services.AddCdcProcessor();
		services.AddCdcProcessor();

		// Assert
		services.HasCdcProcessor().ShouldBeTrue();
	}

	[Fact]
	public void AllowMultipleRegistrations_WithConfigureAction()
	{
		// Arrange
		var services = new ServiceCollection();
		Action<ICdcBuilder> configure = _ => { };

		// Act - should not throw
		services.AddCdcProcessor(configure);
		services.AddCdcProcessor(configure);

		// Assert
		services.HasCdcProcessor().ShouldBeTrue();
	}

	#endregion

	#region Configuration Validation Tests

	[Fact]
	public void ThrowInvalidOperationException_WhenInvalidConfiguration()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert - InvokeCallback strategy without handler should fail validation
		Should.Throw<InvalidOperationException>(() =>
			services.AddCdcProcessor(cdc =>
			{
				cdc.WithRecovery(r =>
				{
					r.Strategy(StalePositionRecoveryStrategy.InvokeCallback);
					// No callback set - should fail validation
				});
			}));
	}

	[Fact]
	public void AcceptValidConfiguration_WithInvokeCallbackAndHandler()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act - should not throw
		services.AddCdcProcessor(cdc =>
		{
			cdc.WithRecovery(r =>
			{
				r.Strategy(StalePositionRecoveryStrategy.InvokeCallback);
				r.OnPositionReset((_, _) => Task.CompletedTask);
			});
		});

		// Assert
		services.HasCdcProcessor().ShouldBeTrue();
	}

	#endregion

	#region Table Tracking Configuration Tests

	[Fact]
	public void ConfigureTrackedTables()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddCdcProcessor(cdc =>
		{
			cdc.TrackTable("dbo.Orders", table =>
			{
				table.MapInsert<OrderCreatedEvent>();
				table.MapUpdate<OrderUpdatedEvent>();
				table.MapDelete<OrderDeletedEvent>();
			});
		});
		var provider = services.BuildServiceProvider();

		// Assert
		var options = provider.GetRequiredService<IOptions<CdcOptions>>().Value;
		options.TrackedTables.Count.ShouldBe(1);
		options.TrackedTables[0].TableName.ShouldBe("dbo.Orders");
	}

	[Fact]
	public void ConfigureMultipleTrackedTables()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddCdcProcessor(cdc =>
		{
			cdc.TrackTable("dbo.Orders", table =>
				{
					table.MapAll<OrderChangedEvent>();
				})
				.TrackTable("dbo.Customers", table =>
				{
					table.MapAll<CustomerChangedEvent>();
				});
		});
		var provider = services.BuildServiceProvider();

		// Assert
		var options = provider.GetRequiredService<IOptions<CdcOptions>>().Value;
		options.TrackedTables.Count.ShouldBe(2);
	}

	#endregion

	#region Test Event Types

	private sealed class OrderCreatedEvent { }
	private sealed class OrderUpdatedEvent { }
	private sealed class OrderDeletedEvent { }
	private sealed class OrderChangedEvent { }
	private sealed class CustomerChangedEvent { }

	#endregion
}
