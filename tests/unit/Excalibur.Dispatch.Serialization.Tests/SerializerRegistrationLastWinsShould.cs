// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Serialization.Tests;

/// <summary>
/// Regression lock for <c>bd-fbd23t</c> (S856): the <c>ISerializer</c> registered in the DI container
/// MUST always agree with <c>PluggableSerializationOptions.CurrentSerializerName</c>.
/// When multiple <c>AddXSerializer()</c> calls are made, the <b>last</b> one wins on both paths — any
/// previous registration is replaced (not silently left as-is via <c>TryAdd</c>).
/// </summary>
/// <remarks>
/// <para>
/// <b>Pre-fix behaviour (RED on HEAD):</b> <c>AddXSerializer</c> used <c>TryAddSingleton</c>, so only the
/// <em>first</em> registration was kept. The <c>PluggableSerializationOptions.CurrentSerializerName</c>
/// path (set via <c>PostConfigure</c>) reflects the last-called extension, while the direct
/// <c>ISerializer</c> resolve still returns the first. The two paths silently diverge → wrong serializer
/// is used at runtime despite <c>CurrentSerializerName</c> claiming otherwise.
/// </para>
/// <para>
/// <b>Post-fix behaviour (GREEN):</b> each <c>AddXSerializer</c> calls <c>services.RemoveAll&lt;ISerializer&gt;()</c>
/// then <c>services.AddSingleton&lt;ISerializer&gt;(instance)</c>, so the last call wins on BOTH the
/// direct <c>ISerializer</c> path and the <c>CurrentSerializerName</c> option path.
/// </para>
/// </remarks>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Serialization)]
public sealed class SerializerRegistrationLastWinsShould
{
	[Fact]
	public void ResolveAvroSerializer_WhenAvroIsRegisteredAfterMessagePack()
	{
		// Arrange — MessagePack first, Avro second → Avro must win on BOTH paths.
		var services = new ServiceCollection();
		services.AddMessagePackSerializer();
		services.AddAvroSerializer();

		using var provider = services.BuildServiceProvider();

		// Act
		var serializer = provider.GetRequiredService<ISerializer>();
		var options = provider.GetRequiredService<IOptions<PluggableSerializationOptions>>().Value;

		// Assert — direct ISerializer path and CurrentSerializerName option path must agree.
		// Non-vacuous: pre-fix HEAD uses TryAdd → ISerializer is still MessagePack → .Name fails → RED.
		serializer.Name.ShouldBe("Avro",
			"Direct ISerializer must be the LAST-registered serializer (Avro), not the first (MessagePack). " +
			"Pre-fix TryAdd left the MessagePack registration intact.");
		options.CurrentSerializerName.ShouldBe("Avro",
			"CurrentSerializerName must agree with the directly-resolved ISerializer.");
	}

	[Fact]
	public void ResolveMessagePackSerializer_WhenMessagePackIsRegisteredAfterAvro()
	{
		// Arrange — Avro first, MessagePack second → MessagePack must win (reverse-order control).
		var services = new ServiceCollection();
		services.AddAvroSerializer();
		services.AddMessagePackSerializer();

		using var provider = services.BuildServiceProvider();

		// Act
		var serializer = provider.GetRequiredService<ISerializer>();
		var options = provider.GetRequiredService<IOptions<PluggableSerializationOptions>>().Value;

		// Assert — symmetric contract: last-wins regardless of registration order.
		// Non-vacuous: pre-fix HEAD → ISerializer is Avro (TryAdd first-wins) → .Name fails → RED.
		serializer.Name.ShouldBe("MessagePack",
			"Direct ISerializer must be the LAST-registered serializer (MessagePack), not the first (Avro). " +
			"Pre-fix TryAdd left the Avro registration intact.");
		options.CurrentSerializerName.ShouldBe("MessagePack",
			"CurrentSerializerName must agree with the directly-resolved ISerializer.");
	}

	[Fact]
	public void ResolveExactlyOneISerializerRegistration_WhenTwoSerializersAreRegistered()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddAvroSerializer();
		services.AddMessagePackSerializer();

		// Assert — exactly one ISerializer descriptor must remain; TryAdd leaves two, which means
		// GetRequiredService would non-deterministically pick one. Single registration is the contract.
		// Non-vacuous: pre-fix TryAdd → two descriptors → count == 2 → assertion fails → RED.
		var serializerDescriptors = services
			.Count(sd => sd.ServiceType == typeof(ISerializer));

		serializerDescriptors.ShouldBe(1,
			"AddXSerializer must replace any prior ISerializer registration (RemoveAll + AddSingleton). " +
			"Pre-fix TryAdd leaves multiple descriptors, making resolution non-deterministic.");
	}
}
