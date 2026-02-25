// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Abstractions;

/// <summary>
/// Marker interface for all messages that can be dispatched through the messaging pipeline.
/// </summary>
/// <remarks>
/// This is a marker interface used for type identification in the Excalibur framework.
/// Message properties such as MessageId, MessageType, Headers, and Kind are managed through the framework infrastructure:
/// <list type="bullet">
/// <item> <see cref="IMessageContext" /> - Contains message properties during pipeline processing </item>
/// <item> <see cref="IMessageMetadata" /> - Contains metadata for serialization and transport </item>
/// <item> MessageEnvelope - Wraps message with metadata and context for persistence (Inbox/Outbox) </item>
/// </list>
/// Common implementations include:
/// <list type="bullet">
/// <item> <see cref="IDispatchEvent" /> - For domain and integration events </item>
/// <item> <see cref="IDispatchAction" /> - For commands and queries </item>
/// <item> <see cref="IDispatchDocument" /> - For document-style messages </item>
/// </list>
/// </remarks>
public interface IDispatchMessage
{
}
