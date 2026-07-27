// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch;

/// <summary>
/// Marker interface for all messages that can be dispatched through the messaging pipeline.
/// </summary>
/// <remarks>
/// This is a marker interface used for type identification in the Excalibur framework.
/// Message properties such as MessageId, MessageType and Headers are managed through the framework infrastructure:
/// <list type="bullet">
/// <item> <see cref="IMessageContext" /> - Contains message properties during pipeline processing </item>
/// <item> <see cref="IMessageMetadata" /> - Contains metadata for serialization and transport </item>
/// <item> MessageEnvelope - Wraps message with metadata and context for persistence (Inbox/Outbox) </item>
/// </list>
/// <para>
/// A message's <b>kind</b> is not carried by any of those. It is determined by which of the following
/// interfaces the type implements, and that choice decides which middleware applies to it:
/// </para>
/// <list type="bullet">
/// <item> <see cref="IDispatchEvent" /> - For domain and integration events </item>
/// <item> <see cref="IDispatchAction" /> - For commands and queries </item>
/// <item> <see cref="IDispatchDocument" /> - For document-style messages </item>
/// </list>
/// <para>
/// Implement whichever matches your message's intent. Implementing only this marker leaves the type
/// with no kind, and the middleware that applies to it cannot be determined from the type alone. Such
/// a message is treated as <see cref="MessageKinds.All" />, so <i>every</i> middleware applies to it —
/// the least-understood type receives the most protection, never the least. Dispatch is not
/// refused and no exception is thrown.
/// </para>
/// <para>
/// Because that fallback is silent by design, it is reported on the current activity as an event named
/// <c>dispatch.message.unclassified</c>, tagged with the message type and the interfaces it may
/// declare. Without it the omission would surface downstream as an authorization failure, which names
/// the wrong problem: a missing interface, seen as a rejected message.
/// </para>
/// </remarks>
public interface IDispatchMessage
{
}
