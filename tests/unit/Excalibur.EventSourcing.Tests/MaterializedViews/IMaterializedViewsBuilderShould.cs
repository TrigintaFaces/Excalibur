// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.EventSourcing.DependencyInjection;

namespace Excalibur.EventSourcing.Tests.MaterializedViews;

/// <summary>
/// Unit tests for <see cref="IMaterializedViewsBuilder"/> interface contract.
/// </summary>
/// <remarks>
/// Sprint 516: Materialized Views foundation tests.
/// Tests verify fluent builder interface structure and method signatures.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "MaterializedViews")]
[Trait("Feature", "DependencyInjection")]
public sealed class IMaterializedViewsBuilderShould
{
	#region Interface Structure Tests

	[Fact]
	public void BeAnInterface()
	{
		// Assert
		typeof(IMaterializedViewsBuilder).IsInterface.ShouldBeTrue();
	}

	[Fact]
	public void BePublic()
	{
		// Assert
		typeof(IMaterializedViewsBuilder).IsPublic.ShouldBeTrue();
	}

	[Fact]
	public void HaveServicesProperty()
	{
		// Arrange
		var property = typeof(IMaterializedViewsBuilder).GetProperty("Services");

		// Assert
		property.ShouldNotBeNull();
		property.PropertyType.ShouldBe(typeof(IServiceCollection));
		property.CanRead.ShouldBeTrue();
	}

	#endregion

	#region AddBuilder Method Tests

	[Fact]
	public void HaveGenericAddBuilderMethod()
	{
		// Arrange - AddBuilder<TView, TBuilder> on interface, AddBuilder<TView>(factory) on extension
		var interfaceMethod = typeof(IMaterializedViewsBuilder).GetMethods()
			.Where(m => m.Name == "AddBuilder" && m.IsGenericMethod)
			.ToList();
		var extensionMethod = typeof(MaterializedViewsBuilderExtensions).GetMethods()
			.Where(m => m.Name == "AddBuilder" && m.IsGenericMethod)
			.ToList();

		// Assert - one on interface, one on extension class
		interfaceMethod.Count.ShouldBe(1);
		extensionMethod.Count.ShouldBe(1);
	}

	[Fact]
	public void AddBuilderGeneric_ReturnIMaterializedViewsBuilder()
	{
		// Arrange
		var method = typeof(IMaterializedViewsBuilder).GetMethods()
			.First(m => m.Name == "AddBuilder" && m.GetGenericArguments().Length == 2);

		// Assert
		method.ReturnType.ShouldBe(typeof(IMaterializedViewsBuilder));
	}

	[Fact]
	public void AddBuilderWithFactory_ReturnIMaterializedViewsBuilder()
	{
		// Arrange - AddBuilder<TView>(factory) moved to extension method
		var method = typeof(MaterializedViewsBuilderExtensions).GetMethods()
			.First(m => m.Name == "AddBuilder" && m.GetGenericArguments().Length == 1);

		// Assert
		method.ReturnType.ShouldBe(typeof(IMaterializedViewsBuilder));
	}

	#endregion

	#region UseStore Method Tests

	[Fact]
	public void HaveUseStoreGenericMethod()
	{
		// Arrange
		var method = typeof(IMaterializedViewsBuilder).GetMethods()
			.FirstOrDefault(m => m.Name == "UseStore" && m.IsGenericMethod);

		// Assert
		method.ShouldNotBeNull();
		method.ReturnType.ShouldBe(typeof(IMaterializedViewsBuilder));
	}

	[Fact]
	public void HaveUseStoreFactoryMethod()
	{
		// Arrange
		var method = typeof(IMaterializedViewsBuilder).GetMethods()
			.FirstOrDefault(m => m.Name == "UseStore" && !m.IsGenericMethod);

		// Assert
		method.ShouldNotBeNull();
		method.ReturnType.ShouldBe(typeof(IMaterializedViewsBuilder));
	}

	#endregion

	#region UseProcessor Method Tests

	[Fact]
	public void HaveUseProcessorGenericMethod()
	{
		// Arrange
		var method = typeof(IMaterializedViewsBuilder).GetMethods()
			.FirstOrDefault(m => m.Name == "UseProcessor" && m.IsGenericMethod);

		// Assert
		method.ShouldNotBeNull();
		method.ReturnType.ShouldBe(typeof(IMaterializedViewsBuilder));
	}

	#endregion

	#region Configuration Method Tests

	[Fact]
	public void HaveEnableCatchUpOnStartupExtensionMethod()
	{
		// Arrange - moved to extension class
		var method = typeof(MaterializedViewsBuilderExtensions).GetMethod("EnableCatchUpOnStartup");

		// Assert
		method.ShouldNotBeNull();
		method.ReturnType.ShouldBe(typeof(IMaterializedViewsBuilder));
	}

	[Fact]
	public void HaveWithBatchSizeExtensionMethod()
	{
		// Arrange - moved to extension class
		var method = typeof(MaterializedViewsBuilderExtensions).GetMethod("WithBatchSize");

		// Assert
		method.ShouldNotBeNull();
		method.ReturnType.ShouldBe(typeof(IMaterializedViewsBuilder));
	}

	[Fact]
	public void WithBatchSize_TakeIntParameter()
	{
		// Arrange - moved to extension class
		var method = typeof(MaterializedViewsBuilderExtensions).GetMethod("WithBatchSize");
		var parameters = method!.GetParameters();

		// Assert - first param is 'this' (builder), second is batchSize
		parameters.Length.ShouldBe(2);
		parameters[1].Name.ShouldBe("batchSize");
		parameters[1].ParameterType.ShouldBe(typeof(int));
	}

	#endregion
}
