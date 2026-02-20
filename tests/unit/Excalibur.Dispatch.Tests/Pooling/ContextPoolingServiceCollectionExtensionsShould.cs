// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Messaging;
using Excalibur.Dispatch.Pooling;

namespace Excalibur.Dispatch.Tests.Pooling;

/// <summary>
/// Tests for <see cref="ContextPoolingServiceCollectionExtensions"/>.
/// Covers null guards, registration with and without configure action,
/// singleton replacement, and method chaining.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class ContextPoolingServiceCollectionExtensionsShould
{
	[Fact]
	public void AddContextPoolingWithAction_ThrowsWhenServicesIsNull()
	{
		Should.Throw<ArgumentNullException>(() =>
			ContextPoolingServiceCollectionExtensions.AddContextPooling(null!, _ => { }));
	}

	[Fact]
	public void AddContextPoolingWithAction_ThrowsWhenConfigureIsNull()
	{
		var services = new ServiceCollection();

		Should.Throw<ArgumentNullException>(() =>
			services.AddContextPooling(null!));
	}

	[Fact]
	public void AddContextPoolingWithAction_RegistersMessageContextPoolAdapter()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddContextPooling(opts =>
		{
			opts.Enabled = true;
			opts.MaxPoolSize = 32;
		});

		// Assert
		services.ShouldContain(sd =>
			sd.ServiceType == typeof(IMessageContextPool) &&
			sd.ImplementationType == typeof(MessageContextPoolAdapter));
	}

	[Fact]
	public void AddContextPoolingWithAction_ReturnsSameServiceCollection()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		var result = services.AddContextPooling(opts => opts.Enabled = true);

		// Assert
		result.ShouldBeSameAs(services);
	}

	[Fact]
	public void AddContextPoolingWithoutAction_ThrowsWhenServicesIsNull()
	{
		Should.Throw<ArgumentNullException>(() =>
			ContextPoolingServiceCollectionExtensions.AddContextPooling(null!));
	}

	[Fact]
	public void AddContextPoolingWithoutAction_RegistersMessageContextPoolAdapter()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddContextPooling();

		// Assert
		services.ShouldContain(sd =>
			sd.ServiceType == typeof(IMessageContextPool) &&
			sd.ImplementationType == typeof(MessageContextPoolAdapter));
	}

	[Fact]
	public void AddContextPoolingWithoutAction_ReturnsSameServiceCollection()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		var result = services.AddContextPooling();

		// Assert
		result.ShouldBeSameAs(services);
	}

	[Fact]
	public void AddContextPooling_ReplacesExistingRegistration()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddSingleton<IMessageContextPool>(A.Fake<IMessageContextPool>());

		// Act
		services.AddContextPooling(opts => opts.Enabled = true);

		// Assert — should have replaced the fake with the adapter
		var descriptors = services.Where(sd => sd.ServiceType == typeof(IMessageContextPool)).ToList();
		descriptors.Count.ShouldBe(1);
		descriptors[0].ImplementationType.ShouldBe(typeof(MessageContextPoolAdapter));
	}

	[Fact]
	public void AddContextPooling_RegistersSingleton()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddContextPooling(opts => opts.Enabled = true);

		// Assert
		var descriptor = services.Single(sd => sd.ServiceType == typeof(IMessageContextPool));
		descriptor.Lifetime.ShouldBe(ServiceLifetime.Singleton);
	}
}
