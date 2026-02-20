// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Serialization;
using Excalibur.Dispatch.Patterns;
using Excalibur.Dispatch.Patterns.ClaimCheck;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Patterns.Tests.Hosting.Json;

/// <summary>
/// Depth coverage tests for <see cref="DispatchPatternsJsonServiceCollectionExtensions"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class DispatchPatternsJsonServiceCollectionExtensionsDepthShould
{
	[Fact]
	public void AddJsonSerialization_ThrowsArgumentNullException_WhenServicesIsNull()
	{
		IServiceCollection services = null!;
		Should.Throw<ArgumentNullException>(() => services.AddJsonSerialization());
	}

	[Fact]
	public void AddJsonSerialization_RegistersIJsonSerializer()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddJsonSerialization();
		using var sp = services.BuildServiceProvider();

		// Assert
		sp.GetService<IJsonSerializer>().ShouldNotBeNull();
	}

	[Fact]
	public void AddJsonSerialization_WithConfigure_AppliesOptions()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddJsonSerialization(o => o.SerializerOptions.WriteIndented = true);
		using var sp = services.BuildServiceProvider();

		// Assert
		var options = sp.GetRequiredService<IOptions<DispatchPatternsJsonOptions>>().Value;
		options.SerializerOptions.WriteIndented.ShouldBeTrue();
	}

	[Fact]
	public void AddJsonSerialization_WithoutConfigure_UsesDefaults()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddJsonSerialization();
		using var sp = services.BuildServiceProvider();

		// Assert
		var options = sp.GetRequiredService<IOptions<DispatchPatternsJsonOptions>>().Value;
		options.SerializerOptions.WriteIndented.ShouldBeFalse();
	}

	[Fact]
	public void AddJsonSerialization_IsSingleton()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddJsonSerialization();
		using var sp = services.BuildServiceProvider();

		// Act
		var first = sp.GetRequiredService<IJsonSerializer>();
		var second = sp.GetRequiredService<IJsonSerializer>();

		// Assert
		first.ShouldBeSameAs(second);
	}

	[Fact]
	public void AddJsonSerialization_DoesNotOverrideExistingRegistration()
	{
		// Arrange
		var services = new ServiceCollection();
		var existingSerializer = A.Fake<IJsonSerializer>();
		services.AddSingleton(existingSerializer);

		// Act
		services.AddJsonSerialization();
		using var sp = services.BuildServiceProvider();

		// Assert - The first registration wins (TryAddSingleton)
		sp.GetRequiredService<IJsonSerializer>().ShouldBeSameAs(existingSerializer);
	}

	[Fact]
	public void AddDispatchPatternsClaimCheckJson_ThrowsArgumentNullException_WhenServicesIsNull()
	{
		IServiceCollection services = null!;
		Should.Throw<ArgumentNullException>(() => services.AddDispatchPatternsClaimCheckJson());
	}

	[Fact]
	public void AddDispatchPatternsClaimCheckJson_RegistersIBinaryMessageSerializer()
	{
		// Arrange
		var services = new ServiceCollection();
		var fakeProvider = A.Fake<IClaimCheckProvider>();
		services.AddSingleton(fakeProvider);

		// Act
		services.AddDispatchPatternsClaimCheckJson();
		using var sp = services.BuildServiceProvider();

		// Assert
		sp.GetService<IBinaryMessageSerializer>().ShouldNotBeNull();
	}

	[Fact]
	public void AddDispatchPatternsClaimCheckJson_ReturnsSameServices()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		var result = services.AddDispatchPatternsClaimCheckJson();

		// Assert
		result.ShouldBeSameAs(services);
	}

	[Fact]
	public void AddJsonSerialization_ReturnsSameServices()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		var result = services.AddJsonSerialization();

		// Assert
		result.ShouldBeSameAs(services);
	}
}
