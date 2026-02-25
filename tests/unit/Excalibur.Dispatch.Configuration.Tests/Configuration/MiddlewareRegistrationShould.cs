// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Configuration;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Dispatch.Tests.Configuration;

/// <summary>
/// Unit tests for <see cref="MiddlewareRegistration"/>.
/// </summary>
/// <remarks>
/// Tests middleware registration construction and property behavior.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Configuration")]
[Trait("Priority", "0")]
public sealed class MiddlewareRegistrationShould
{
	#region Constructor Tests

	[Fact]
	public void Constructor_WithNullMiddlewareType_ThrowsArgumentNullException()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			new MiddlewareRegistration(null!, DispatchMiddlewareStage.PreProcessing));
	}

	[Fact]
	public void Constructor_WithValidParameters_CreatesRegistration()
	{
		// Act
		var registration = new MiddlewareRegistration(
			typeof(TestMiddleware),
			DispatchMiddlewareStage.PreProcessing);

		// Assert
		_ = registration.ShouldNotBeNull();
		registration.MiddlewareType.ShouldBe(typeof(TestMiddleware));
		registration.Stage.ShouldBe(DispatchMiddlewareStage.PreProcessing);
	}

	[Fact]
	public void Constructor_WithDefaultOrder_SetsOrderTo100()
	{
		// Act
		var registration = new MiddlewareRegistration(
			typeof(TestMiddleware),
			DispatchMiddlewareStage.PreProcessing);

		// Assert
		registration.Order.ShouldBe(100);
	}

	[Fact]
	public void Constructor_WithCustomOrder_SetsOrderToCustomValue()
	{
		// Act
		var registration = new MiddlewareRegistration(
			typeof(TestMiddleware),
			DispatchMiddlewareStage.PreProcessing,
			order: 50);

		// Assert
		registration.Order.ShouldBe(50);
	}

	[Fact]
	public void Constructor_WithNullConfigureOptions_SetsConfigureOptionsToNull()
	{
		// Act
		var registration = new MiddlewareRegistration(
			typeof(TestMiddleware),
			DispatchMiddlewareStage.PreProcessing);

		// Assert
		registration.ConfigureOptions.ShouldBeNull();
	}

	[Fact]
	public void Constructor_WithConfigureOptions_SetsConfigureOptions()
	{
		// Arrange
		Action<IServiceCollection> configure = services => { };

		// Act
		var registration = new MiddlewareRegistration(
			typeof(TestMiddleware),
			DispatchMiddlewareStage.PreProcessing,
			configureOptions: configure);

		// Assert
		registration.ConfigureOptions.ShouldBe(configure);
	}

	[Fact]
	public void Constructor_SetsIsEnabledToTrueByDefault()
	{
		// Act
		var registration = new MiddlewareRegistration(
			typeof(TestMiddleware),
			DispatchMiddlewareStage.PreProcessing);

		// Assert
		registration.IsEnabled.ShouldBeTrue();
	}

	#endregion

	#region Stage Tests

	[Theory]
	[InlineData(DispatchMiddlewareStage.PreProcessing)]
	[InlineData(DispatchMiddlewareStage.Processing)]
	[InlineData(DispatchMiddlewareStage.PostProcessing)]
	[InlineData(DispatchMiddlewareStage.ErrorHandling)]
	public void Constructor_WithVariousStages_StoresCorrectStage(DispatchMiddlewareStage stage)
	{
		// Act
		var registration = new MiddlewareRegistration(
			typeof(TestMiddleware),
			stage);

		// Assert
		registration.Stage.ShouldBe(stage);
	}

	#endregion

	#region Property Modification Tests

	[Fact]
	public void Order_CanBeModified()
	{
		// Arrange
		var registration = new MiddlewareRegistration(
			typeof(TestMiddleware),
			DispatchMiddlewareStage.PreProcessing,
			order: 100);

		// Act
		registration.Order = 25;

		// Assert
		registration.Order.ShouldBe(25);
	}

	[Fact]
	public void IsEnabled_CanBeSetToFalse()
	{
		// Arrange
		var registration = new MiddlewareRegistration(
			typeof(TestMiddleware),
			DispatchMiddlewareStage.PreProcessing);

		// Act
		registration.IsEnabled = false;

		// Assert
		registration.IsEnabled.ShouldBeFalse();
	}

	[Fact]
	public void IsEnabled_CanBeToggledMultipleTimes()
	{
		// Arrange
		var registration = new MiddlewareRegistration(
			typeof(TestMiddleware),
			DispatchMiddlewareStage.PreProcessing);

		// Act & Assert
		registration.IsEnabled.ShouldBeTrue();
		registration.IsEnabled = false;
		registration.IsEnabled.ShouldBeFalse();
		registration.IsEnabled = true;
		registration.IsEnabled.ShouldBeTrue();
	}

	#endregion

	#region Order Boundary Tests

	[Theory]
	[InlineData(int.MinValue)]
	[InlineData(-1)]
	[InlineData(0)]
	[InlineData(1)]
	[InlineData(int.MaxValue)]
	public void Order_AcceptsAnyIntegerValue(int order)
	{
		// Act
		var registration = new MiddlewareRegistration(
			typeof(TestMiddleware),
			DispatchMiddlewareStage.PreProcessing,
			order: order);

		// Assert
		registration.Order.ShouldBe(order);
	}

	#endregion

	#region ConfigureOptions Invocation Tests

	[Fact]
	public void ConfigureOptions_CanBeInvoked()
	{
		// Arrange
		var invoked = false;
		Action<IServiceCollection> configure = services => { invoked = true; };
		var registration = new MiddlewareRegistration(
			typeof(TestMiddleware),
			DispatchMiddlewareStage.PreProcessing,
			configureOptions: configure);

		// Act
		registration.ConfigureOptions?.Invoke(new ServiceCollection());

		// Assert
		invoked.ShouldBeTrue();
	}

	[Fact]
	public void ConfigureOptions_ReceivesServiceCollection()
	{
		// Arrange
		IServiceCollection? receivedServices = null;
		Action<IServiceCollection> configure = services => { receivedServices = services; };
		var registration = new MiddlewareRegistration(
			typeof(TestMiddleware),
			DispatchMiddlewareStage.PreProcessing,
			configureOptions: configure);
		var services = new ServiceCollection();

		// Act
		registration.ConfigureOptions?.Invoke(services);

		// Assert
		receivedServices.ShouldBe(services);
	}

	#endregion

	#region Test Types

	/// <summary>
	/// Simple stub class used as middleware type for registration tests.
	/// MiddlewareRegistration stores the type but doesn't validate it implements IDispatchMiddleware.
	/// </summary>
	private sealed class TestMiddleware;

	#endregion
}
