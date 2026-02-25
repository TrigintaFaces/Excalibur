// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Abstractions;

/// <summary>
/// Provides additional context for timeout decisions and adaptive timeout calculations. R7.4: Context-aware timeout handling.
/// </summary>
public sealed class TimeoutContext
{
	/// <summary>
	/// Gets or sets the complexity level of the operation, affecting timeout calculations.
	/// </summary>
	/// <value> The assessed complexity for the current operation. </value>
	public OperationComplexity Complexity { get; set; } = OperationComplexity.Normal;

	/// <summary>
	/// Gets or sets the message type for message-specific timeout overrides.
	/// </summary>
	/// <value> The message type associated with the operation. </value>
	public Type? MessageType { get; set; }

	/// <summary>
	/// Gets or sets the handler type for handler-specific timeout overrides.
	/// </summary>
	/// <value> The handler type for the execution context. </value>
	public Type? HandlerType { get; set; }

	/// <summary>
	/// Gets additional properties for custom timeout logic.
	/// </summary>
	/// <value> A dictionary of customizable timeout properties. </value>
	public IDictionary<string, object> Properties { get; init; } = new Dictionary<string, object>(StringComparer.Ordinal);

	/// <summary>
	/// Gets or sets the expected message size, which may influence timeout calculations.
	/// </summary>
	/// <value> The anticipated message payload size in bytes. </value>
	public long? ExpectedMessageSizeBytes { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether this is a retry operation, which may affect timeout behavior.
	/// </summary>
	/// <value> <see langword="true" /> when the context represents a retry attempt; otherwise, <see langword="false" />. </value>
	public bool IsRetry { get; set; }

	/// <summary>
	/// Gets or sets the number of retries attempted, for progressive timeout strategies.
	/// </summary>
	/// <value> The number of retry attempts already performed. </value>
	public int RetryCount { get; set; }

	/// <summary>
	/// Gets additional tags for categorizing the operation.
	/// </summary>
	/// <value> A set of tags describing the operation. </value>
	public ISet<string> Tags { get; init; } = new HashSet<string>(StringComparer.Ordinal);

	/// <summary>
	/// Creates a new timeout context with the specified message type.
	/// </summary>
	/// <param name="messageType"> The message type. </param>
	/// <returns> A new timeout context. </returns>
	public static TimeoutContext ForMessage(Type messageType) => new() { MessageType = messageType };

	/// <summary>
	/// Creates a new timeout context with the specified handler type.
	/// </summary>
	/// <param name="handlerType"> The handler type. </param>
	/// <returns> A new timeout context. </returns>
	public static TimeoutContext ForHandler(Type handlerType) => new() { HandlerType = handlerType };

	/// <summary>
	/// Creates a new timeout context with the specified complexity.
	/// </summary>
	/// <param name="complexity"> The operation complexity. </param>
	/// <returns> A new timeout context. </returns>
	public static TimeoutContext WithComplexity(OperationComplexity complexity) => new() { Complexity = complexity };

	/// <summary>
	/// Creates a new timeout context for a retry operation.
	/// </summary>
	/// <param name="retryCount"> The number of retries attempted. </param>
	/// <returns> A new timeout context. </returns>
	public static TimeoutContext ForRetry(int retryCount) => new() { IsRetry = true, RetryCount = retryCount };
}
