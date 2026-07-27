// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Saga.Handlers;

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
