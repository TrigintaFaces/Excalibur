// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Configuration;

namespace Excalibur.Dispatch.Tests.Configuration;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class MiddlewareRegistrationShould
{
	[Fact]
	public void Constructor_SetPropertiesCorrectly()
	{
		// Act
		var registration = new MiddlewareRegistration(
			typeof(string),
			DispatchMiddlewareStage.PreProcessing);

		// Assert
		registration.MiddlewareType.ShouldBe(typeof(string));
		registration.Stage.ShouldBe(DispatchMiddlewareStage.PreProcessing);
		registration.Order.ShouldBe(100); // default
		registration.IsEnabled.ShouldBeTrue(); // default
		registration.ConfigureOptions.ShouldBeNull(); // default
	}

	[Fact]
	public void Constructor_WithAllParameters_SetProperties()
	{
		// Arrange
		Action<IServiceCollection> configure = _ => { };

		// Act
		var registration = new MiddlewareRegistration(
			typeof(int),
			DispatchMiddlewareStage.PostProcessing,
			order: 50,
			configureOptions: configure);

		// Assert
		registration.MiddlewareType.ShouldBe(typeof(int));
		registration.Stage.ShouldBe(DispatchMiddlewareStage.PostProcessing);
		registration.Order.ShouldBe(50);
		registration.ConfigureOptions.ShouldBeSameAs(configure);
	}

	[Fact]
	public void Constructor_ThrowOnNullType()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(
			() => new MiddlewareRegistration(null!, DispatchMiddlewareStage.PreProcessing));
	}

	[Fact]
	public void Order_CanBeUpdated()
	{
		// Arrange
		var registration = new MiddlewareRegistration(typeof(string), DispatchMiddlewareStage.PreProcessing);

		// Act
		registration.Order = 200;

		// Assert
		registration.Order.ShouldBe(200);
	}

	[Fact]
	public void IsEnabled_CanBeDisabled()
	{
		// Arrange
		var registration = new MiddlewareRegistration(typeof(string), DispatchMiddlewareStage.PreProcessing);

		// Act
		registration.IsEnabled = false;

		// Assert
		registration.IsEnabled.ShouldBeFalse();
	}
}
