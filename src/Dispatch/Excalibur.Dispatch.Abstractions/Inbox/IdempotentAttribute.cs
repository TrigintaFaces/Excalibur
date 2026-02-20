// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Abstractions;

/// <summary>
/// Marks a handler as idempotent, enabling automatic message deduplication.
/// </summary>
/// <remarks>
/// <para>
/// When applied to a handler, the IdempotentHandlerMiddleware will automatically track
/// processed message IDs and skip duplicate messages. This is useful for ensuring
/// at-least-once delivery semantics without duplicate processing.
/// </para>
/// <para>
/// Example usage:
/// <code>
/// [Idempotent(RetentionMinutes = 60, UseInMemory = true)]
/// public class PaymentHandler : IEventHandler&lt;PaymentEvent&gt;
/// {
///     // Duplicate messages automatically skipped
/// }
/// </code>
/// </para>
/// <para>
/// The attribute supports two storage modes:
/// <list type="bullet">
/// <item>
/// <description>
/// <see cref="UseInMemory"/> = <see langword="true"/>: Uses in-memory storage via
/// <see cref="IInMemoryDeduplicator"/>. Best for serverless or short-lived processes.
/// </description>
/// </item>
/// <item>
/// <description>
/// <see cref="UseInMemory"/> = <see langword="false"/>: Uses persistent storage via
/// IInboxStore. Best for multi-instance deployments requiring distributed deduplication.
/// </description>
/// </item>
/// </list>
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
public sealed class IdempotentAttribute : Attribute
{
	/// <summary>
	/// Gets or sets the retention period for processed message IDs in minutes.
	/// </summary>
	/// <value>
	/// Default is 1440 (24 hours). Messages processed before this period will no longer
	/// be tracked and may be reprocessed if received again.
	/// </value>
	public int RetentionMinutes { get; set; } = 1440;

	/// <summary>
	/// Gets or sets whether to use in-memory storage instead of IInboxStore.
	/// </summary>
	/// <value>
	/// Default is <see langword="false"/> (use configured IInboxStore).
	/// Set to <see langword="true"/> for serverless or testing scenarios.
	/// </value>
	/// <remarks>
	/// In-memory storage is faster but not shared across instances.
	/// For distributed scenarios, use the default persistent storage.
	/// </remarks>
	public bool UseInMemory { get; set; }

	/// <summary>
	/// Gets or sets the strategy for extracting the message ID for deduplication.
	/// </summary>
	/// <value>
	/// Default is <see cref="MessageIdStrategy.FromHeader"/>.
	/// </value>
	public MessageIdStrategy Strategy { get; set; } = MessageIdStrategy.FromHeader;

	/// <summary>
	/// Gets or sets the header name to use when <see cref="Strategy"/> is
	/// <see cref="MessageIdStrategy.FromHeader"/>.
	/// </summary>
	/// <value>
	/// Default is "MessageId".
	/// </value>
	public string HeaderName { get; set; } = "MessageId";
}
