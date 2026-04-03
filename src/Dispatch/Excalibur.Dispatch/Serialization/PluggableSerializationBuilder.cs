// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Abstractions.Serialization;

namespace Excalibur.Dispatch.Serialization;

/// <summary>
/// Implementation of <see cref="ISerializationBuilder"/> that collects
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
internal sealed class PluggableSerializationBuilder : ISerializationBuilder
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
	public ISerializationBuilder Register(ISerializer serializer, byte id)
	{
		ArgumentNullException.ThrowIfNull(serializer);
		_options.AddRegistration(registry => registry.Register(id, serializer));
		return this;
	}

	/// <inheritdoc />
	public ISerializationBuilder UseCurrent(string serializerName)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(serializerName);
		_options.CurrentSerializerName = serializerName;
		return this;
	}

}
