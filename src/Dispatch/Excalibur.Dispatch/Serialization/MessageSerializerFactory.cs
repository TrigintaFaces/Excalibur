// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Abstractions.Serialization;
using Excalibur.Dispatch.Options.Serialization;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Serialization;

/// <summary>
/// Factory for creating message serializers based on version mappings and dependency injection. Provides centralized access to registered
/// serializers with version-based routing.
/// </summary>
/// <param name="provider"> The service provider for resolving serializer instances. </param>
/// <param name="options"> Configuration options containing serializer version mappings. </param>
public sealed class MessageSerializerFactory(
	IServiceProvider provider,
	IOptions<MessageSerializerOptions> options)
{
	private readonly MessageSerializerOptions _options = options.Value;

	/// <summary>
	/// Gets a message serializer instance for the specified version.
	/// </summary>
	/// <param name="serializerVersion"> The version of the serializer to retrieve. Defaults to 0 for the default serializer. </param>
	/// <returns> The message serializer instance for the specified version. </returns>
	/// <exception cref="NotSupportedException"> Thrown when the specified serializer version is not registered. </exception>
	public IMessageSerializer GetSerializer(int serializerVersion = 0)
	{
		if (_options.SerializerMap.TryGetValue(serializerVersion, out var serializerType))
		{
			return (IMessageSerializer)provider.GetRequiredService(serializerType);
		}

		throw new NotSupportedException($"Serializer version {serializerVersion} is unsupported or not registered.");
	}
}
