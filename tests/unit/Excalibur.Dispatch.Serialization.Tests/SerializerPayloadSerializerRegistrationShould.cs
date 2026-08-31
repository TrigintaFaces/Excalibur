// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Serialization.Tests;

/// <summary>
/// Regression lock: EVERY <c>AddXSerializer()</c> entry point seats the serialization services the
/// selected serializer needs, so a consumer who opts into a format actually gets that format.
/// </summary>
/// <remarks>
/// Pre-fix defect: the four entry points only post-configured <see cref="PluggableSerializationOptions"/>;
/// none of them registered <see cref="IPayloadSerializer"/>, whose sole registration is
/// <c>AddPluggableSerialization()</c>. A consumer who called only <c>AddMessagePackSerializer()</c> — the
/// sequence the package READMEs documented — got no payload serializer at all, and so silently stayed on
/// the JSON path instead of the format they selected: no error, no log. These assertions are non-vacuous:
/// on the pre-fix code <c>GetService&lt;IPayloadSerializer&gt;()</c> returned <see langword="null"/> for all
/// four entry points, so each fact is RED by construction before the shared registration seam seats it.
/// </remarks>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Serialization)]
public sealed class SerializerPayloadSerializerRegistrationShould
{
	[Fact]
	public void SeatSelectedSerializer_ForProtobuf()
	{
		AssertSelectedSerializerIsCurrent(services => services.AddProtobufSerializer(), "Protobuf");
	}

	[Fact]
	public void SeatSelectedSerializer_ForAvro()
	{
		AssertSelectedSerializerIsCurrent(services => services.AddAvroSerializer(), "Avro");
	}

	[Fact]
	public void SeatSelectedSerializer_ForMessagePack()
	{
		AssertSelectedSerializerIsCurrent(services => services.AddMessagePackSerializer(), "MessagePack");
	}

	[Fact]
	public void SeatSelectedSerializer_ForMemoryPack()
	{
		AssertSelectedSerializerIsCurrent(services => services.AddMemoryPackSerializer(), "MemoryPack");
	}

	private static void AssertSelectedSerializerIsCurrent(
		Action<IServiceCollection> register,
		string expectedSerializerName)
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();
		register(services);

		using var provider = services.BuildServiceProvider();

		// The reported symptom: resolving the inbox/transport payload seam threw because nothing
		// registered it. Selecting a serializer must seat it without a second opt-in call.
		var payloadSerializer = provider.GetService<IPayloadSerializer>();

		payloadSerializer.ShouldNotBeNull(
			$"Add{expectedSerializerName}Serializer() must register IPayloadSerializer itself; requiring a " +
			"separate AddPluggableSerialization() call left the consumer with no payload serializer at all.");

		// The consumer-visible defect behind the symptom: the format the consumer selected must be the
		// format actually in use, not a silent fallback.
		var current = provider.GetRequiredService<ISerializerRegistry>().GetCurrent();

		current.Serializer.Name.ShouldBe(
			expectedSerializerName,
			$"Add{expectedSerializerName}Serializer() must make {expectedSerializerName} the current " +
			"serializer, otherwise the consumer silently stays on the default format they did not choose.");
	}
}
