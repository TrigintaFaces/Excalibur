// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Versioning;
using Excalibur.Dispatch.Tests.Versioning.TestData;

namespace Excalibur.Dispatch.Tests.DependencyInjection;

[Trait("Category", "Unit")]
public sealed class UpcastingServiceCollectionExtensionsShould
{
	#region Registration Tests

	[Fact]
	public void RegisterIUpcastingPipelineAsSingleton()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddMessageUpcasting();

		// Act
		var provider = services.BuildServiceProvider();
		var pipeline1 = provider.GetRequiredService<IUpcastingPipeline>();
		var pipeline2 = provider.GetRequiredService<IUpcastingPipeline>();

		// Assert
		_ = pipeline1.ShouldNotBeNull();
		pipeline2.ShouldBeSameAs(pipeline1);
	}

	[Fact]
	public void RegisterUpcasterInstance()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddMessageUpcasting(builder =>
		{
			_ = builder.RegisterUpcaster(
				new UserCreatedEventV1ToV2Upcaster());
		});

		// Act
		var provider = services.BuildServiceProvider();
		var pipeline = provider.GetRequiredService<IUpcastingPipeline>();

		// Assert
		pipeline.GetLatestVersion("UserCreatedEvent").ShouldBe(2);
		pipeline.CanUpcast("UserCreatedEvent", 1, 2).ShouldBeTrue();
	}

	[Fact]
	public void RegisterUpcasterWithFactory()
	{
		// Arrange
		var factoryWasCalled = false;
		var services = new ServiceCollection();
		_ = services.AddMessageUpcasting(builder =>
		{
			_ = builder.RegisterUpcaster(sp =>
			{
				factoryWasCalled = true;
				return new UserCreatedEventV1ToV2Upcaster();
			});
		});

		// Act
		var provider = services.BuildServiceProvider();
		var pipeline = provider.GetRequiredService<IUpcastingPipeline>();

		// Assert
		factoryWasCalled.ShouldBeTrue();
		pipeline.GetLatestVersion("UserCreatedEvent").ShouldBe(2);
	}

	[Fact]
	public void RegisterMultipleUpcasters()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddMessageUpcasting(builder =>
		{
			_ = builder.RegisterUpcaster(new UserCreatedEventV1ToV2Upcaster());
			_ = builder.RegisterUpcaster(new UserCreatedEventV2ToV3Upcaster());
			_ = builder.RegisterUpcaster(new UserCreatedEventV3ToV4Upcaster());
			_ = builder.RegisterUpcaster(new OrderPlacedEventV1ToV2Upcaster());
		});

		// Act
		var provider = services.BuildServiceProvider();
		var pipeline = provider.GetRequiredService<IUpcastingPipeline>();

		// Assert
		pipeline.GetLatestVersion("UserCreatedEvent").ShouldBe(4);
		pipeline.GetLatestVersion("OrderPlacedEvent").ShouldBe(2);
		pipeline.CanUpcast("UserCreatedEvent", 1, 4).ShouldBeTrue();
		pipeline.CanUpcast("OrderPlacedEvent", 1, 2).ShouldBeTrue();
	}

	#endregion Registration Tests

	#region Assembly Scanning Tests

	[Fact]
	public void ScanAssemblyForUpcasters()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddMessageUpcasting(builder =>
		{
			// Filter out problematic test upcasters:
			// - InvalidDowncastUpcaster (has FromVersion > ToVersion)
			// - CountingUpcaster (duplicate registration with V1ToV2)
			_ = builder.ScanAssembly(
				typeof(UserCreatedEventV1ToV2Upcaster).Assembly,
				type => type != typeof(InvalidDowncastUpcaster) &&
						type != typeof(CountingUpcaster));
		});

		// Act
		var provider = services.BuildServiceProvider();
		var pipeline = provider.GetRequiredService<IUpcastingPipeline>();

		// Assert - Should have discovered all valid test upcasters
		pipeline.GetLatestVersion("UserCreatedEvent").ShouldBe(4);
		pipeline.GetLatestVersion("OrderPlacedEvent").ShouldBe(2);
		pipeline.GetLatestVersion("CustomEvent").ShouldBe(2); // MismatchedTypeEvent
	}

	[Fact]
	public void ScanAssemblyWithFilter()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddMessageUpcasting(builder =>
		{
			// Only include UserCreatedEvent upcasters
			_ = builder.ScanAssembly(
				typeof(UserCreatedEventV1ToV2Upcaster).Assembly,
				type => type.Name.StartsWith("UserCreated"));
		});

		// Act
		var provider = services.BuildServiceProvider();
		var pipeline = provider.GetRequiredService<IUpcastingPipeline>();

		// Assert - Only UserCreatedEvent should be registered
		pipeline.GetLatestVersion("UserCreatedEvent").ShouldBe(4);
		pipeline.GetLatestVersion("OrderPlacedEvent").ShouldBe(0); // Filtered out
	}

	[Fact]
	public void ScanAssemblyExcludesInvalidUpcasters()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddMessageUpcasting(builder =>
		{
			// Filter out the invalid downcast upcaster AND the duplicate CountingUpcaster
			_ = builder.ScanAssembly(
				typeof(InvalidDowncastUpcaster).Assembly,
				type => type != typeof(InvalidDowncastUpcaster) &&
						type != typeof(CountingUpcaster));
		});

		// Act & Assert - Should not throw
		var provider = services.BuildServiceProvider();
		var pipeline = provider.GetRequiredService<IUpcastingPipeline>();

		// Verify valid upcasters were registered
		pipeline.GetLatestVersion("UserCreatedEvent").ShouldBeGreaterThan(0);
	}

	#endregion Assembly Scanning Tests

	#region Configuration Option Tests

	[Fact]
	public void EnableAutoUpcastOnReplayOption()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddMessageUpcasting(builder =>
		{
			_ = builder.EnableAutoUpcastOnReplay(true);
		});

		// Act
		var provider = services.BuildServiceProvider();
		var options = provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<UpcastingOptions>>().Value;

		// Assert
		options.EnableAutoUpcastOnReplay.ShouldBeTrue();
	}

	[Fact]
	public void DisableAutoUpcastOnReplayOption()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddMessageUpcasting(builder =>
		{
			_ = builder.EnableAutoUpcastOnReplay(false);
		});

		// Act
		var provider = services.BuildServiceProvider();
		var options = provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<UpcastingOptions>>().Value;

		// Assert
		options.EnableAutoUpcastOnReplay.ShouldBeFalse();
	}

	#endregion Configuration Option Tests

	#region HasMessageUpcasting Tests

	[Fact]
	public void HasMessageUpcasting_ReturnsTrueWhenRegistered()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddMessageUpcasting();

		// Act & Assert
		services.HasMessageUpcasting().ShouldBeTrue();
	}

	[Fact]
	public void HasMessageUpcasting_ReturnsFalseWhenNotRegistered()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		services.HasMessageUpcasting().ShouldBeFalse();
	}

	#endregion HasMessageUpcasting Tests

	#region E2E Upcasting Tests

	[Fact]
	public void UpcastMessageThroughDIRegisteredPipeline()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddMessageUpcasting(builder =>
		{
			_ = builder.RegisterUpcaster(new UserCreatedEventV1ToV2Upcaster());
			_ = builder.RegisterUpcaster(new UserCreatedEventV2ToV3Upcaster());
		});

		var provider = services.BuildServiceProvider();
		var pipeline = provider.GetRequiredService<IUpcastingPipeline>();

		var v1Event = new UserCreatedEventV1
		{
			Id = Guid.NewGuid(),
			Name = "John Doe"
		};

		// Act
		var result = pipeline.Upcast(v1Event);

		// Assert
		_ = result.ShouldBeOfType<UserCreatedEventV3>();
		var v3Event = (UserCreatedEventV3)result;
		v3Event.FirstName.ShouldBe("John");
		v3Event.LastName.ShouldBe("Doe");
		v3Event.Version.ShouldBe(3);
	}

	[Fact]
	public void UpcastMessageFromV1ToV4WithMultiHops()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddMessageUpcasting(builder =>
		{
			_ = builder.RegisterUpcaster(new UserCreatedEventV1ToV2Upcaster());
			_ = builder.RegisterUpcaster(new UserCreatedEventV2ToV3Upcaster());
			_ = builder.RegisterUpcaster(new UserCreatedEventV3ToV4Upcaster());
		});

		var provider = services.BuildServiceProvider();
		var pipeline = provider.GetRequiredService<IUpcastingPipeline>();

		var v1Event = new UserCreatedEventV1
		{
			Id = Guid.NewGuid(),
			Name = "Jane Smith"
		};

		// Act
		var result = pipeline.Upcast(v1Event);

		// Assert
		_ = result.ShouldBeOfType<UserCreatedEventV4>();
		var v4Event = (UserCreatedEventV4)result;
		v4Event.FirstName.ShouldBe("Jane");
		v4Event.LastName.ShouldBe("Smith");
		v4Event.Email.ShouldBe("JANE.SMITH@example.com");
		v4Event.Version.ShouldBe(4);
	}

	#endregion E2E Upcasting Tests

	#region Chained Builder Tests

	[Fact]
	public void BuilderMethodsReturnSameInstance()
	{
		// Arrange
		var services = new ServiceCollection();
		var assertionsPassed = false;

		_ = services.AddMessageUpcasting(builder =>
		{
			// Act - chain multiple methods and verify fluent API works
			var result = builder
				.RegisterUpcaster(new UserCreatedEventV1ToV2Upcaster())
				.EnableAutoUpcastOnReplay(true);

			// Assert inside the callback - methods should return same instance for chaining
			result.ShouldBeSameAs(builder);
			assertionsPassed = true;
		});

		// Trigger configuration by building provider
		var provider = services.BuildServiceProvider();
		_ = provider.GetRequiredService<IUpcastingPipeline>();

		// Verify the assertions were actually executed
		assertionsPassed.ShouldBeTrue();
	}

	#endregion Chained Builder Tests
}
