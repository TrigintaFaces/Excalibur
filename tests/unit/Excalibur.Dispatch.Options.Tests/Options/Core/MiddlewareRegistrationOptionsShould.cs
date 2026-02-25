// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Configuration;
using Excalibur.Dispatch.Options.Core;

namespace Excalibur.Dispatch.Tests.Options.Core;

/// <summary>
/// Unit tests for <see cref="MiddlewareRegistrationOptions"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Options")]
[Trait("Priority", "0")]
public sealed class MiddlewareRegistrationOptionsShould
{
	// Test middleware type for registrations
	private sealed class TestMiddleware;

	#region Default Value Tests

	[Fact]
	public void Default_Registrations_IsNotNull()
	{
		// Arrange & Act
		var options = new MiddlewareRegistrationOptions();

		// Assert
		_ = options.Registrations.ShouldNotBeNull();
	}

	[Fact]
	public void Default_Registrations_IsEmpty()
	{
		// Arrange & Act
		var options = new MiddlewareRegistrationOptions();

		// Assert
		options.Registrations.ShouldBeEmpty();
	}

	#endregion

	#region Collection Manipulation Tests

	[Fact]
	public void Registrations_CanAddRegistration()
	{
		// Arrange
		var options = new MiddlewareRegistrationOptions();
		var registration = new MiddlewareRegistration(typeof(TestMiddleware), DispatchMiddlewareStage.PreProcessing, 100);

		// Act
		options.Registrations.Add(registration);

		// Assert
		options.Registrations.Count.ShouldBe(1);
		options.Registrations.First().ShouldBeSameAs(registration);
	}

	[Fact]
	public void Registrations_CanAddMultipleRegistrations()
	{
		// Arrange
		var options = new MiddlewareRegistrationOptions();
		var registration1 = new MiddlewareRegistration(typeof(TestMiddleware), DispatchMiddlewareStage.PreProcessing, 100);
		var registration2 = new MiddlewareRegistration(typeof(TestMiddleware), DispatchMiddlewareStage.Validation, 200);
		var registration3 = new MiddlewareRegistration(typeof(TestMiddleware), DispatchMiddlewareStage.PostProcessing, 100);

		// Act
		options.Registrations.Add(registration1);
		options.Registrations.Add(registration2);
		options.Registrations.Add(registration3);

		// Assert
		options.Registrations.Count.ShouldBe(3);
	}

	[Fact]
	public void Registrations_CanClear()
	{
		// Arrange
		var options = new MiddlewareRegistrationOptions();
		options.Registrations.Add(new MiddlewareRegistration(typeof(TestMiddleware), DispatchMiddlewareStage.PreProcessing, 100));

		// Act
		options.Registrations.Clear();

		// Assert
		options.Registrations.ShouldBeEmpty();
	}

	[Fact]
	public void Registrations_CanRemoveRegistration()
	{
		// Arrange
		var options = new MiddlewareRegistrationOptions();
		var registration = new MiddlewareRegistration(typeof(TestMiddleware), DispatchMiddlewareStage.Processing, 100);
		options.Registrations.Add(registration);

		// Act
		var removed = options.Registrations.Remove(registration);

		// Assert
		removed.ShouldBeTrue();
		options.Registrations.ShouldBeEmpty();
	}

	#endregion

	#region Real-World Scenario Tests

	[Fact]
	public void Options_ForPipelineConfiguration_HasOrderedRegistrations()
	{
		// Arrange
		var options = new MiddlewareRegistrationOptions();

		// Act
		options.Registrations.Add(new MiddlewareRegistration(typeof(TestMiddleware), DispatchMiddlewareStage.PreProcessing, 100));
		options.Registrations.Add(new MiddlewareRegistration(typeof(TestMiddleware), DispatchMiddlewareStage.Validation, 200));
		options.Registrations.Add(new MiddlewareRegistration(typeof(TestMiddleware), DispatchMiddlewareStage.Authorization, 300));

		// Assert
		options.Registrations.Count.ShouldBe(3);
		var orderedRegistrations = options.Registrations.OrderBy(r => r.Order).ToList();
		orderedRegistrations[0].Order.ShouldBe(100);
		orderedRegistrations[1].Order.ShouldBe(200);
		orderedRegistrations[2].Order.ShouldBe(300);
	}

	[Fact]
	public void Options_ForEmptyPipeline_HasNoRegistrations()
	{
		// Act
		var options = new MiddlewareRegistrationOptions();

		// Assert
		options.Registrations.ShouldBeEmpty();
	}

	#endregion
}
