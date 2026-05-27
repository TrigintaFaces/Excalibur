// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Saga.Correlation;
using Excalibur.Saga.Handlers;
using Excalibur.Saga.Reminders;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Excalibur.Saga.Tests.DependencyInjection;

/// <summary>
/// Tests for <see cref="SagaEnhancementsServiceCollectionExtensions"/> — DI consolidation (bd-uflk26, ADR-333).
/// Validates the new enhancement entry points that replaced the deleted AddExcaliburAdvancedSagas.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Saga.DependencyInjection")]
public sealed class SagaEnhancementsServiceCollectionExtensionsShould
{
	#region AddSagaCorrelation

	[Fact]
	public void AddSagaCorrelation_RegistersConventionBasedCorrelator()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddSagaCorrelation();
		var sp = services.BuildServiceProvider();

		// Assert
		sp.GetService<ConventionBasedCorrelator>().ShouldNotBeNull();
	}

	[Fact]
	public void AddSagaCorrelation_ReturnsSameServiceCollectionForChaining()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		var result = services.AddSagaCorrelation();

		// Assert
		result.ShouldBeSameAs(services);
	}

	[Fact]
	public void AddSagaCorrelation_ThrowsWhenServicesIsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			SagaEnhancementsServiceCollectionExtensions.AddSagaCorrelation(null!));
	}

	[Fact]
	public void AddSagaCorrelation_UseTryAddSoConsumerCanOverride()
	{
		// Arrange — register twice, should not throw or duplicate
		var services = new ServiceCollection();

		// Act
		services.AddSagaCorrelation();
		services.AddSagaCorrelation();
		var sp = services.BuildServiceProvider();

		// Assert — only one registration
		sp.GetServices<ConventionBasedCorrelator>().Count().ShouldBe(1);
	}

	#endregion

	#region AddSagaNotFoundHandler

	[Fact]
	public void AddSagaNotFoundHandler_RegistersDefaultLoggingHandler()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddLogging();

		// Act
		services.AddSagaNotFoundHandler<TestSagaType>();
		var sp = services.BuildServiceProvider();

		// Assert
		sp.GetService<ISagaNotFoundHandler<TestSagaType>>().ShouldNotBeNull();
		sp.GetService<ISagaNotFoundHandler<TestSagaType>>().ShouldBeOfType<LoggingNotFoundHandler<TestSagaType>>();
	}

	[Fact]
	public void AddSagaNotFoundHandler_ThrowsWhenServicesIsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			SagaEnhancementsServiceCollectionExtensions.AddSagaNotFoundHandler<TestSagaType>(null!));
	}

	[Fact]
	public void AddSagaNotFoundHandler_ReturnsSameServiceCollectionForChaining()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddLogging();

		// Act
		var result = services.AddSagaNotFoundHandler<TestSagaType>();

		// Assert
		result.ShouldBeSameAs(services);
	}

	#endregion

	#region AddSagaReminders

	[Fact]
	public void AddSagaReminders_DefaultOverload_RegistersOptions()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddSagaReminders();
		var sp = services.BuildServiceProvider();

		// Assert
		sp.GetService<IOptions<SagaReminderOptions>>().ShouldNotBeNull();
	}

	[Fact]
	public void AddSagaReminders_ConfigureOverload_AppliesConfiguration()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddSagaReminders(opts => opts.DefaultDelay = TimeSpan.FromSeconds(120));
		var sp = services.BuildServiceProvider();

		// Assert
		var options = sp.GetRequiredService<IOptions<SagaReminderOptions>>().Value;
		options.DefaultDelay.ShouldBe(TimeSpan.FromSeconds(120));
	}

	[Fact]
	public void AddSagaReminders_ConfigureOverload_ThrowsWhenServicesIsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			SagaEnhancementsServiceCollectionExtensions.AddSagaReminders(null!, _ => { }));
	}

	[Fact]
	public void AddSagaReminders_ConfigureOverload_ThrowsWhenConfigureIsNull()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			services.AddSagaReminders((Action<SagaReminderOptions>)null!));
	}

	[Fact]
	public void AddSagaReminders_ReturnsSameServiceCollectionForChaining()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		var result = services.AddSagaReminders();

		// Assert
		result.ShouldBeSameAs(services);
	}

	#endregion

	#region Model B Deletion Verification (ADR-333)

	[Fact]
	public void AddExcaliburAdvancedSagas_ShouldNotExist()
	{
		// Verify ADR-333 decision: Model B entry point was deleted
		var methods = typeof(SagaEnhancementsServiceCollectionExtensions)
			.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);

		methods.Select(m => m.Name).ShouldNotContain("AddExcaliburAdvancedSagas",
			"AddExcaliburAdvancedSagas was deleted per ADR-333 (Model B removal)");
	}

	#endregion

	#region Test Doubles

	private sealed class TestSagaType;

	#endregion
}
