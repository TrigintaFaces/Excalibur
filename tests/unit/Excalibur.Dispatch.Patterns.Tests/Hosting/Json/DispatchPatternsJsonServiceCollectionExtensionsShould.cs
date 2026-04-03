// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Abstractions.Serialization;
using Excalibur.Dispatch.Patterns;

using Microsoft.Extensions.DependencyInjection;

using Shouldly;

using Tests.Shared;

using Xunit;

namespace Excalibur.Dispatch.Patterns.Tests.Hosting.Json;

/// <summary>
/// Unit tests for <see cref="DispatchPatternsJsonServiceCollectionExtensions"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Patterns")]
public sealed class DispatchPatternsJsonServiceCollectionExtensionsShould : UnitTestBase
{
	[Fact]
	public void AddJsonSerialization_RegistersDispatchJsonSerializer()
	{
		// Arrange & Act
		_ = Services.AddJsonSerialization();
		BuildServiceProvider();

		// Assert
		var serializer = GetService<DispatchJsonSerializer>();
		serializer.ShouldNotBeNull();
	}

	[Fact]
	public void AddJsonSerialization_RegistersAsSingleton()
	{
		// Arrange & Act
		_ = Services.AddJsonSerialization();
		BuildServiceProvider();

		// Assert — same instance returned each time
		var first = GetRequiredService<DispatchJsonSerializer>();
		var second = GetRequiredService<DispatchJsonSerializer>();
		first.ShouldBeSameAs(second);
	}

	[Fact]
	public void AddJsonSerialization_WithConfigure_AppliesOptions()
	{
		// Arrange & Act
		_ = Services.AddJsonSerialization(opt => opt.SerializerOptions.WriteIndented = true);
		BuildServiceProvider();

		// Assert
		var options = GetRequiredService<Microsoft.Extensions.Options.IOptions<DispatchPatternsJsonOptions>>().Value;
		options.SerializerOptions.WriteIndented.ShouldBeTrue();
	}

	[Fact]
	public void AddJsonSerialization_WithNullConfigure_DoesNotThrow()
	{
		// Act & Assert — null configure delegate should be fine
		_ = Services.AddJsonSerialization((Action<Excalibur.Dispatch.Patterns.DispatchPatternsJsonOptions>?)null);
		BuildServiceProvider();
		var serializer = GetService<DispatchJsonSerializer>();
		serializer.ShouldNotBeNull();
	}

	[Fact]
	public void AddJsonSerialization_ThrowsOnNullServices()
	{
		// Act & Assert
		IServiceCollection nullServices = null!;
		Should.Throw<ArgumentNullException>(() => nullServices.AddJsonSerialization());
	}

	[Fact]
	public void AddJsonSerialization_DoesNotReplaceExistingRegistration()
	{
		// Arrange — register a fake first
		var fake = new DispatchJsonSerializer();
		_ = Services.AddSingleton(fake);

		// Act
		_ = Services.AddJsonSerialization();
		BuildServiceProvider();

		// Assert — TryAdd should not replace
		var resolved = GetRequiredService<DispatchJsonSerializer>();
		resolved.ShouldBeSameAs(fake);
	}

	[Fact]
	public void AddJsonSerialization_RegistersOptionsDefaults()
	{
		// Arrange & Act
		_ = Services.AddJsonSerialization();
		BuildServiceProvider();

		// Assert
		var options = GetRequiredService<Microsoft.Extensions.Options.IOptions<DispatchPatternsJsonOptions>>().Value;
		options.SerializerOptions.ShouldNotBeNull();
		options.SerializerContext.ShouldBeNull();
		options.SerializerOptions.WriteIndented.ShouldBeFalse();
	}
}
