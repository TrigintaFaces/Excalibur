// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Serialization;
using Excalibur.Dispatch.Serialization;

namespace Excalibur.Dispatch.Tests.Serialization;

/// <summary>
/// Unit tests for <see cref="IPluggableSerializationBuilder"/> and
/// <see cref="PluggableSerializationServiceCollectionExtensions"/> validating
/// DI configuration and auto-registration behavior.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Dispatch.Core")]
public sealed class PluggableSerializationBuilderShould
{
	#region AutoRegisterMemoryPack Tests

	[Fact]
	public void NotAutoRegisterMemoryPack_ByDefault()
	{
		// ADR-295: MemoryPack is opt-in, not auto-registered
		var services = new ServiceCollection();
		_ = services.AddPluggableSerialization();

		using var provider = services.BuildServiceProvider();
		var registry = provider.GetRequiredService<ISerializerRegistry>();

		registry.IsRegistered(SerializerIds.MemoryPack).ShouldBeFalse();
	}

	[Fact]
	public void AutoRegisterMemoryPack_WhenOptedIn()
	{
		var services = new ServiceCollection();
		_ = services.AddPluggableSerialization();
		_ = services.AddPluggableSerializer(
			SerializerIds.MemoryPack,
			MemoryPackSerializationServiceCollectionExtensions.GetPluggableSerializer(),
			setAsCurrent: true);

		using var provider = services.BuildServiceProvider();
		var registry = provider.GetRequiredService<ISerializerRegistry>();

		registry.IsRegistered(SerializerIds.MemoryPack).ShouldBeTrue();
		var (id, serializer) = registry.GetCurrent();
		serializer.Name.ShouldBe("MemoryPack");
		id.ShouldBe(SerializerIds.MemoryPack);
	}

	[Fact]
	public void AllowOnlySystemTextJson_AsDefault()
	{
		var services = new ServiceCollection();
		_ = services.AddPluggableSerialization();

		_ = services.AddPluggableSerializer(
			SerializerIds.SystemTextJson,
			new SystemTextJsonSerializer(),
			setAsCurrent: true);

		using var provider = services.BuildServiceProvider();
		var registry = provider.GetRequiredService<ISerializerRegistry>();

		registry.IsRegistered(SerializerIds.MemoryPack).ShouldBeFalse();
		registry.IsRegistered(SerializerIds.SystemTextJson).ShouldBeTrue();

		var (id, serializer) = registry.GetCurrent();
		serializer.Name.ShouldBe("System.Text.Json");
	}

	#endregion AutoRegisterMemoryPack Tests

	#region Serializer Registration Tests

	[Fact]
	public void RegisterMultipleSerializers()
	{
		var services = new ServiceCollection();
		_ = services.AddPluggableSerialization();
		_ = services.AddPluggableSerializer(
			SerializerIds.MemoryPack,
			MemoryPackSerializationServiceCollectionExtensions.GetPluggableSerializer());
		_ = services.AddPluggableSerializer(SerializerIds.SystemTextJson, new SystemTextJsonSerializer());

		using var provider = services.BuildServiceProvider();
		var registry = provider.GetRequiredService<ISerializerRegistry>();

		registry.IsRegistered(SerializerIds.MemoryPack).ShouldBeTrue();
		registry.IsRegistered(SerializerIds.SystemTextJson).ShouldBeTrue();
	}

	[Fact]
	public void SwitchCurrentSerializer_ViaAddPluggableSerializer()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddPluggableSerialization();
		_ = services.AddPluggableSerializer(
			SerializerIds.SystemTextJson,
			new SystemTextJsonSerializer(),
			setAsCurrent: true);

		// Act
		using var provider = services.BuildServiceProvider();
		var registry = provider.GetRequiredService<ISerializerRegistry>();

		// Assert
		var (id, serializer) = registry.GetCurrent();
		serializer.Name.ShouldBe("System.Text.Json");
		id.ShouldBe(SerializerIds.SystemTextJson);
	}

	#endregion Serializer Registration Tests

	// IHttpSerializer registration test removed: HttpJsonSerializer was deleted in Sprint 586
	// serialization consolidation. AddPluggableSerialization() no longer registers IHttpSerializer.

	#region Options Tests

	[Fact]
	public void Options_CurrentSerializerName_DefaultsToNull()
	{
		// Arrange & Act
		var options = new PluggableSerializationOptions();

		// Assert
		options.CurrentSerializerName.ShouldBeNull();
	}

	#endregion Options Tests

	#region Default Serializer Tests (Sprint 739 A.6 / ADR-295)

	[Fact]
	public void AddEventSerializer_RegistersJsonEventSerializerAsDefault()
	{
		// ADR-295: JSON is the default event serializer, not SpanEventSerializer
		var services = new ServiceCollection();
		_ = services.AddEventSerializer();

		using var provider = services.BuildServiceProvider();
		var serializer = provider.GetRequiredService<IEventSerializer>();

		serializer.ShouldBeOfType<JsonEventSerializer>();
	}

	[Fact]
	public void AddEventSerializer_DoesNotRegisterSpanEventSerializer()
	{
		// ADR-295: SpanEventSerializer is no longer the default
		var services = new ServiceCollection();
		_ = services.AddEventSerializer();

		using var provider = services.BuildServiceProvider();
		var serializer = provider.GetRequiredService<IEventSerializer>();

		serializer.ShouldNotBeOfType<SpanEventSerializer>();
	}

	[Fact]
	public void AddEventSerializer_ThrowsOnNullServices()
	{
		IServiceCollection services = null!;
		Should.Throw<ArgumentNullException>(() => services.AddEventSerializer());
	}

	#endregion Default Serializer Tests (Sprint 739 A.6 / ADR-295)
}
