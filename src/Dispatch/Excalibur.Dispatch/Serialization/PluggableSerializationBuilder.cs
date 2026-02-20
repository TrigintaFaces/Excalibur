// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Abstractions.Serialization;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Dispatch.Serialization;

/// <summary>
/// Implementation of <see cref="IPluggableSerializationBuilder"/> that collects
/// registration actions for deferred execution.
/// </summary>
/// <remarks>
/// <para>
/// This builder collects serializer registration actions during configuration.
/// The actions are stored in <see cref="PluggableSerializationOptions"/> and executed
/// when the <see cref="ISerializerRegistry"/> singleton is created.
/// </para>
/// <para>
/// This approach avoids the anti-pattern of calling <c>BuildServiceProvider()</c>
/// during configuration, following the <c>IConfigureOptions</c> pattern instead.
/// </para>
/// </remarks>
internal sealed class PluggableSerializationBuilder : IPluggableSerializationBuilder
{
	private readonly PluggableSerializationOptions _options;

	/// <summary>
	/// Initializes a new instance of the <see cref="PluggableSerializationBuilder"/> class.
	/// </summary>
	/// <param name="options">The options to configure.</param>
	public PluggableSerializationBuilder(PluggableSerializationOptions options)
	{
		_options = options ?? throw new ArgumentNullException(nameof(options));
	}

	/// <inheritdoc />
	public IPluggableSerializationBuilder RegisterMemoryPack()
	{
		_options.AddRegistration(registry =>
		{
			if (!registry.IsRegistered(SerializerIds.MemoryPack))
			{
				// MemoryPack package is a dependency, so we can always register it
				registry.Register(
					SerializerIds.MemoryPack,
					MemoryPackSerializationServiceCollectionExtensions.GetPluggableSerializer());
			}
		});
		return this;
	}

	/// <inheritdoc />
	public IPluggableSerializationBuilder RegisterSystemTextJson()
	{
		_options.AddRegistration(registry =>
		{
			if (!registry.IsRegistered(SerializerIds.SystemTextJson))
			{
				// System.Text.Json is built into .NET, so we can always register it
				registry.Register(SerializerIds.SystemTextJson, new SystemTextJsonPluggableSerializer());
			}
		});
		return this;
	}

	/// <inheritdoc />
	public IPluggableSerializationBuilder RegisterMessagePack()
	{
		_options.AddRegistration(registry =>
		{
			if (!registry.IsRegistered(SerializerIds.MessagePack))
			{
				throw new InvalidOperationException(
					"MessagePack serializer not available. " +
					"Ensure the Excalibur.Dispatch.Serialization.MessagePack package is referenced " +
					"and call services.AddMessagePackSerialization() before ConfigurePluggableSerialization().");
			}
		});
		return this;
	}

	/// <inheritdoc />
	public IPluggableSerializationBuilder RegisterProtobuf()
	{
		_options.AddRegistration(registry =>
		{
			if (!registry.IsRegistered(SerializerIds.Protobuf))
			{
				throw new InvalidOperationException(
					"Protobuf serializer not available. " +
					"Ensure the Excalibur.Dispatch.Serialization.Protobuf package is referenced.");
			}
		});
		return this;
	}

	/// <inheritdoc />
	public IPluggableSerializationBuilder RegisterCustom(IPluggableSerializer serializer, byte id)
	{
		ArgumentNullException.ThrowIfNull(serializer);

		// Validate custom range
		if (!SerializerIds.IsCustomId(id))
		{
			throw new ArgumentException(
				$"Custom serializer IDs must be in range {SerializerIds.CustomRangeStart}-{SerializerIds.CustomRangeEnd}. " +
				$"Got: {id}. Framework IDs (1-199) are reserved.",
				nameof(id));
		}

		_options.AddRegistration(registry => registry.Register(id, serializer));
		return this;
	}

	/// <inheritdoc />
	public IPluggableSerializationBuilder UseCurrent(string serializerName)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(serializerName);
		_options.CurrentSerializerName = serializerName;
		return this;
	}

	/// <inheritdoc />
	public IPluggableSerializationBuilder UseMemoryPack()
		=> UseCurrent("MemoryPack");

	/// <inheritdoc />
	public IPluggableSerializationBuilder UseSystemTextJson()
		=> UseCurrent("System.Text.Json");

	/// <inheritdoc />
	public IPluggableSerializationBuilder UseMessagePack()
		=> UseCurrent("MessagePack");

	/// <inheritdoc />
	public IPluggableSerializationBuilder UseProtobuf()
		=> UseCurrent("Protobuf");

	/// <inheritdoc />
	public IPluggableSerializationBuilder DisableMemoryPackAutoRegistration()
	{
		_options.AutoRegisterMemoryPack = false;
		return this;
	}
}
