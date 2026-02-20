// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch.Abstractions.Configuration;
using Excalibur.Dispatch.Abstractions.Serialization;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Dispatch.Serialization;

/// <summary>
/// Extension methods for configuring message serialization in the dispatch system. Provides methods to register serializers and configure
/// serialization behavior.
/// </summary>
public static class SerializationDispatchBuilderExtensions
{
	/// <summary>
	/// Adds the default dispatch message serializer to the builder. Registers the default JSON-based message serialization services.
	/// </summary>
	/// <param name="builder"> The dispatch builder to configure. </param>
	/// <returns> The dispatch builder for method chaining. </returns>
	/// <exception cref="ArgumentNullException"> Thrown when <paramref name="builder" /> is null. </exception>
	public static IDispatchBuilder AddDispatchSerializer(this IDispatchBuilder builder)
	{
		ArgumentNullException.ThrowIfNull(builder);

		_ = builder.Services.AddDispatchSerializer();

		return builder;
	}

	/// <summary>
	/// Adds a custom message serializer implementation to the builder with a specific version. Enables registration of alternative
	/// serialization strategies with versioning support.
	/// </summary>
	/// <typeparam name="TSerializer"> The type of serializer to register. Must implement IMessageSerializer. </typeparam>
	/// <param name="builder"> The dispatch builder to configure. </param>
	/// <param name="version"> The version number for the serializer implementation. </param>
	/// <returns> The dispatch builder for method chaining. </returns>
	/// <exception cref="ArgumentNullException"> Thrown when <paramref name="builder" /> is null. </exception>
	public static IDispatchBuilder AddDispatchSerializer<
	[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
	TSerializer>(
		this IDispatchBuilder builder,
		int version)
		where TSerializer : class, IMessageSerializer
	{
		ArgumentNullException.ThrowIfNull(builder);

		_ = builder.Services.AddDispatchSerializer<TSerializer>(version);

		return builder;
	}
}
