// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.IO.Compression;

using Excalibur.Dispatch.Abstractions.Serialization;

namespace Excalibur.Dispatch.Transport.Google;

/// <summary>
/// Configuration options for Pub/Sub message serialization.
/// </summary>
public sealed class PubSubSerializationOptions
{
	/// <summary>
	/// Gets or sets the serialization format to use.
	/// Default: Json.
	/// </summary>
	/// <value>
	/// The serialization format to use.
	/// Default: Json.
	/// </value>
	public SerializationFormat Format { get; set; } = SerializationFormat.Json;

	/// <summary>
	/// Gets or sets a value indicating whether gets or sets whether to enable compression for large messages.
	/// Default: true.
	/// </summary>
	/// <value>
	/// A value indicating whether gets or sets whether to enable compression for large messages.
	/// Default: true.
	/// </value>
	public bool EnableCompression { get; set; } = true;

	/// <summary>
	/// Gets or sets the compression level to use.
	/// Default: Optimal.
	/// </summary>
	/// <value>
	/// The compression level to use.
	/// Default: Optimal.
	/// </value>
	public CompressionLevel CompressionLevel { get; set; } = CompressionLevel.Optimal;

	/// <summary>
	/// Gets or sets the minimum message size in bytes before compression is applied.
	/// Default: 1024 bytes (1KB).
	/// </summary>
	/// <value>
	/// The minimum message size in bytes before compression is applied.
	/// Default: 1024 bytes (1KB).
	/// </value>
	public int CompressionThreshold { get; set; } = 1024;

	/// <summary>
	/// Gets or sets a value indicating whether gets or sets whether to use full type names in message attributes.
	/// Default: false.
	/// </summary>
	/// <value>
	/// A value indicating whether gets or sets whether to use full type names in message attributes.
	/// Default: false.
	/// </value>
	public bool UseFullTypeNames { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether gets or sets whether to enable source generation for serialization.
	/// Default: true.
	/// </summary>
	/// <value>
	/// A value indicating whether gets or sets whether to enable source generation for serialization.
	/// Default: true.
	/// </value>
	public bool EnableSourceGeneration { get; set; } = true;

	/// <summary>
	/// Gets or sets a value indicating whether gets or sets whether to skip validation during serialization.
	/// Default: false.
	/// </summary>
	/// <value>
	/// A value indicating whether gets or sets whether to skip validation during serialization.
	/// Default: false.
	/// </value>
	public bool SkipValidation { get; set; }

	/// <summary>
	/// Gets or sets the initial buffer size for serialization.
	/// Default: 4096 bytes.
	/// </summary>
	/// <value>
	/// The initial buffer size for serialization.
	/// Default: 4096 bytes.
	/// </value>
	public int InitialBufferSize { get; set; } = 4096;

	/// <summary>
	/// Gets or sets the maximum buffer size for array pool.
	/// Default: 1MB.
	/// </summary>
	/// <value>
	/// The maximum buffer size for array pool.
	/// Default: 1MB.
	/// </value>
	public int MaxBufferSize { get; set; } = 1024 * 1024;

	/// <summary>
	/// Gets or sets the maximum number of buffers per bucket in array pool.
	/// Default: 50.
	/// </summary>
	/// <value>
	/// The maximum number of buffers per bucket in array pool.
	/// Default: 50.
	/// </value>
	public int MaxBuffersPerBucket { get; set; } = 50;

	/// <summary>
	/// Gets or sets a value indicating whether gets or sets whether to enable schema validation.
	/// Default: false.
	/// </summary>
	/// <value>
	/// A value indicating whether gets or sets whether to enable schema validation.
	/// Default: false.
	/// </value>
	public bool EnableSchemaValidation { get; set; }

	/// <summary>
	/// Gets or sets the schema registry URL if schema validation is enabled.
	/// </summary>
	/// <value>
	/// The schema registry URL if schema validation is enabled.
	/// </value>
	public Uri? SchemaRegistryUrl { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether gets or sets whether to cache serialized messages.
	/// Default: false.
	/// </summary>
	/// <value>
	/// A value indicating whether gets or sets whether to cache serialized messages.
	/// Default: false.
	/// </value>
	public bool EnableMessageCaching { get; set; }

	/// <summary>
	/// Gets or sets the cache duration for serialized messages.
	/// Default: 5 minutes.
	/// </summary>
	/// <value>
	/// The cache duration for serialized messages.
	/// Default: 5 minutes.
	/// </value>
	public TimeSpan CacheDuration { get; set; } = TimeSpan.FromMinutes(5);

	/// <summary>
	/// Validates the configuration settings.
	/// </summary>
	/// <exception cref="InvalidOperationException"> Thrown when configuration is invalid. </exception>
	public void Validate()
	{
		if (CompressionThreshold < 0)
		{
			throw new InvalidOperationException("CompressionThreshold must be non-negative.");
		}

		if (InitialBufferSize <= 0)
		{
			throw new InvalidOperationException("InitialBufferSize must be greater than zero.");
		}

		if (MaxBufferSize < InitialBufferSize)
		{
			throw new InvalidOperationException("MaxBufferSize must be greater than or equal to InitialBufferSize.");
		}

		if (MaxBuffersPerBucket <= 0)
		{
			throw new InvalidOperationException("MaxBuffersPerBucket must be greater than zero.");
		}

		if (EnableSchemaValidation && SchemaRegistryUrl is null)
		{
			throw new InvalidOperationException("SchemaRegistryUrl is required when EnableSchemaValidation is true.");
		}

		if (CacheDuration < TimeSpan.Zero)
		{
			throw new InvalidOperationException("CacheDuration must be non-negative.");
		}
	}
}
