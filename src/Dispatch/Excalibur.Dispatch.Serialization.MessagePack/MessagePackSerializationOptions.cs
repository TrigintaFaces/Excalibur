// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using MessagePack;

namespace Excalibur.Dispatch.Serialization.MessagePack;

/// <summary>
/// Configuration options for MessagePack serialization.
/// </summary>
public sealed class MessagePackSerializationOptions
{
	private volatile CachedSnapshot? _cached;

	/// <summary>
	/// Gets or sets a value indicating whether to use LZ4 compression.
	/// </summary>
	/// <value> <see langword="true" /> to enable LZ4 compression; otherwise, <see langword="false" />. </value>
	public bool UseLz4Compression { get; set; }

	/// <summary>
	/// Gets the MessagePack serializer options configured from the properties.
	/// </summary>
	/// <value> The computed MessagePack serializer options. </value>
	public MessagePackSerializerOptions MessagePackSerializerOptions
	{
		get
		{
			var snapshot = _cached;
			if (snapshot is not null && snapshot.UseLz4 == UseLz4Compression)
			{
				return snapshot.Options;
			}

			var useLz4 = UseLz4Compression;
			var options = MessagePackSerializerOptions.Standard;

			if (useLz4)
			{
				options = options.WithCompression(MessagePackCompression.Lz4Block);
			}

			_cached = new CachedSnapshot(options, useLz4);
			return options;
		}
	}

	private sealed class CachedSnapshot(MessagePackSerializerOptions options, bool useLz4)
	{
		public MessagePackSerializerOptions Options { get; } = options;
		public bool UseLz4 { get; } = useLz4;
	}
}
