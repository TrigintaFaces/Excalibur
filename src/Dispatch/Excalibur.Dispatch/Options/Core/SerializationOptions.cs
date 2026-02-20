// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.ComponentModel.DataAnnotations;

namespace Excalibur.Dispatch.Options.Core;

/// <summary>
/// General serialization configuration options.
/// </summary>
public sealed class SerializationOptions
{
	/// <summary>
	/// Gets or sets a value indicating whether to embed message type information in serialized data.
	/// </summary>
	/// <value> Default is true. </value>
	public bool EmbedMessageType { get; set; } = true;

	/// <summary>
	/// Gets or sets a value indicating whether to include assembly information in type names.
	/// </summary>
	/// <value> Default is false. </value>
	public bool IncludeAssemblyInfo { get; set; }

	/// <summary>
	/// Gets or sets the default buffer size for serialization operations.
	/// </summary>
	/// <value> Default is 4096 bytes. </value>
	[Range(1, int.MaxValue)]
	public int DefaultBufferSize { get; set; } = 4096;

	/// <summary>
	/// Gets or sets a value indicating whether to use memory pooling for serialization buffers.
	/// </summary>
	/// <value> Default is true. </value>
	public bool UseBufferPooling { get; set; } = true;
}
