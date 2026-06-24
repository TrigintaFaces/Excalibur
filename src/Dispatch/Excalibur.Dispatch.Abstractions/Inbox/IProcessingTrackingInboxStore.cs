// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch;

/// <summary>
/// An optional capability for <see cref="IInboxStore"/> implementations that durably persist the
/// in-flight <see cref="InboxStatus.Processing"/> status of a message before its handler executes.
/// </summary>
/// <remarks>
/// <para>
/// This is a segregated capability interface (composition, NOT inheritance of <see cref="IInboxStore"/>)
/// so that <see cref="IInboxStore"/> stays within the Interface Segregation threshold. Inbox stores that
/// support durable Processing tracking implement this interface in addition to <see cref="IInboxStore"/>.
/// </para>
/// <para>
/// Persisting <see cref="InboxStatus.Processing"/> durably is what makes the inbox middleware's
/// at-most-once concurrency guard and stuck-processing timeout functional: without it, the
/// <see cref="InboxStatus.Processing"/> state is only mutated in memory and never observable by a
/// second concurrent consumer, so the guard is dead code.
/// </para>
/// </remarks>
public interface IProcessingTrackingInboxStore
{
	/// <summary>
	/// Durably marks the inbox entry for the specified message and handler as
	/// <see cref="InboxStatus.Processing"/> before its handler executes.
	/// </summary>
	/// <remarks>
	/// Implementations persist the transition (and the attempt timestamp used by the stuck-processing
	/// timeout) so that a concurrent delivery of the same <paramref name="messageId"/>/<paramref name="handlerType"/>
	/// observes the durable Processing state and is skipped by the at-most-once guard.
	/// </remarks>
	/// <param name="messageId">The unique identifier of the message being processed.</param>
	/// <param name="handlerType">The fully qualified type name of the handler processing the message.</param>
	/// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
	/// <returns>A task that represents the asynchronous mark-processing operation.</returns>
	/// <exception cref="ArgumentException">Thrown when <paramref name="messageId"/> or <paramref name="handlerType"/> is null or empty.</exception>
	ValueTask MarkProcessingAsync(string messageId, string handlerType, CancellationToken cancellationToken);
}
