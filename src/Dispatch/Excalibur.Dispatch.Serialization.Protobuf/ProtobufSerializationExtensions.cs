// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Abstractions.Serialization;


using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Excalibur.Dispatch.Serialization.Protobuf;

/// <summary>
/// Extension methods for registering Protobuf serialization support.
/// </summary>
/// <remarks>
/// Per R0.14, R9.46, R9.47: This is an opt-in serialization package.
/// - Protobuf is NOT included in Excalibur.Dispatch core (violates R0.14).
/// - This package provides opt-in support for GCP/AWS/external Protobuf interoperability.
/// - AOT/trim-safe via source-generated Protobuf types.
/// - Does NOT change Excalibur.Dispatch defaults (MemoryPack remains internal wire format per R9.44).
/// </remarks>
public static class ProtobufSerializationExtensions
{
	/// <summary>
	/// Adds Protobuf serialization support to the Dispatch pipeline.
	/// This is an opt-in package for Google Cloud Platform and AWS interoperability.
	/// </summary>
	/// <param name="services">The service collection.</param>
	/// <param name="configure">Optional configuration delegate for Protobuf serialization options.</param>
	/// <returns>The service collection for chaining.</returns>
	/// <remarks>
	/// Usage:
	/// <code>
	/// services.AddDispatch()
	///     .AddProtobufSerialization(options =>
	///     {
	///         options.WireFormat = ProtobufWireFormat.Binary; // Default
	///         options.IgnoreMissingFields = true; // Default
	///     });
	/// </code>
	/// </remarks>
	public static IServiceCollection AddProtobufSerialization(
		this IServiceCollection services,
		Action<ProtobufSerializationOptions>? configure = null)
	{
		ArgumentNullException.ThrowIfNull(services);

		// Always register options (required by ProtobufMessageSerializer)
		// Apply custom configuration if provided, otherwise use defaults
		var optionsBuilder = services.AddOptions<ProtobufSerializationOptions>()
			.Configure(options =>
			{
				configure?.Invoke(options);
			});
		optionsBuilder.ValidateDataAnnotations().ValidateOnStart();

		// Register Protobuf serializer as IMessageSerializer (opt-in)
		services.TryAddSingleton<IMessageSerializer, ProtobufMessageSerializer>();

		return services;
	}
}
