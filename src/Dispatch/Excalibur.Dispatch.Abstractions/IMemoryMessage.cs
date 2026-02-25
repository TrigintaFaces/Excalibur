// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Abstractions;

/// <summary>
/// Represents a message that can provide its body as Memory&lt;byte&gt; for zero-copy operations.
/// </summary>
public interface IMemoryMessage : IDispatchMessage
{
	/// <summary>
	/// Gets the message body as a Memory&lt;byte&gt;.
	/// </summary>
	/// <remarks>
	/// This property enables zero-copy message processing by allowing messages to reference memory directly without copying data. The
	/// memory should remain valid for the lifetime of the message processing.
	/// </remarks>
	/// <value> The memory-backed payload for the message. </value>
	Memory<byte> Body { get; }

	/// <summary>
	/// Gets the content type of the message body.
	/// </summary>
	/// <remarks>
	/// Common values include "application/json", "application/octet-stream", "text/plain", etc. This helps consumers understand how to
	/// interpret the Body data.
	/// </remarks>
	/// <value> The MIME content type of the payload. </value>
	string ContentType { get; }

	/// <summary>
	/// Gets a value indicating whether the body memory is owned by this message.
	/// </summary>
	/// <remarks>
	/// When true, the message is responsible for managing the lifetime of the memory. When false, the memory is borrowed and should not be
	/// disposed by the message.
	/// </remarks>
	/// <value> <see langword="true" /> if the message owns the underlying memory; otherwise, <see langword="false" />. </value>
	bool OwnsMemory { get; }
}

/// <summary>
/// Represents a message that can provide both typed and memory-based access to its content.
/// </summary>
/// <typeparam name="T"> The type of the message content. </typeparam>
public interface IMemoryMessage<out T> : IMemoryMessage
	where T : class
{
	/// <summary>
	/// Gets the deserialized message content.
	/// </summary>
	/// <remarks> This property may lazily deserialize the content from the Body on first access. </remarks>
	/// <value> The deserialized representation of the payload. </value>
	T Content { get; }

	/// <summary>
	/// Gets a value indicating whether the content has been deserialized.
	/// </summary>
	/// <value> <see langword="true" /> when the payload has been materialized; otherwise, <see langword="false" />. </value>
	bool IsDeserialized { get; }
}
