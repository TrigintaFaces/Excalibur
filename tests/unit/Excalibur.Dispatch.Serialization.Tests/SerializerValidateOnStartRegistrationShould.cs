// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Serialization.Tests;

/// <summary>
/// Regression lock: EVERY <c>AddXSerializer()</c> entry point wires
/// <c>AddOptions&lt;PluggableSerializationOptions&gt;().ValidateOnStart()</c> so serializer
/// misconfiguration fails fast at startup.
/// </summary>
/// <remarks>
/// Pre-fix drift: Protobuf and Avro called <c>ValidateOnStart()</c> while MessagePack and MemoryPack did
/// NOT — an accidental divergence, not a decision. Centralizing the registration ritual in the shared
/// <c>SetCurrentSerializer</c> helper makes the four entry points identical. <c>.ValidateOnStart()</c>
/// registers an <see cref="IStartupValidator"/>; on the pre-fix MessagePack/MemoryPack paths that
/// registration was absent, so these assertions are non-vacuous (RED on HEAD for MessagePack/MemoryPack).
/// </remarks>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Serialization)]
public sealed class SerializerValidateOnStartRegistrationShould
{
	[Fact]
	public void WireValidateOnStart_ForProtobuf()
	{
		AssertStartupValidatorRegistered(services => services.AddProtobufSerializer());
	}

	[Fact]
	public void WireValidateOnStart_ForAvro()
	{
		AssertStartupValidatorRegistered(services => services.AddAvroSerializer());
	}

	[Fact]
	public void WireValidateOnStart_ForMessagePack()
	{
		AssertStartupValidatorRegistered(services => services.AddMessagePackSerializer());
	}

	[Fact]
	public void WireValidateOnStart_ForMemoryPack()
	{
		AssertStartupValidatorRegistered(services => services.AddMemoryPackSerializer());
	}

	private static void AssertStartupValidatorRegistered(Action<IServiceCollection> register)
	{
		var services = new ServiceCollection();
		register(services);

		using var provider = services.BuildServiceProvider();

		// .ValidateOnStart() registers an IStartupValidator that fails fast at startup on invalid options.
		// Its presence is the observable proof that the entry point wired ValidateOnStart.
		var startupValidator = provider.GetService<IStartupValidator>();

		startupValidator.ShouldNotBeNull(
			"Every AddXSerializer() must wire AddOptions<PluggableSerializationOptions>().ValidateOnStart() " +
			"so serializer misconfiguration fails fast at startup (pre-fix MessagePack/MemoryPack did not).");
	}
}
