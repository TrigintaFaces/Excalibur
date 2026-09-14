// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch;

/// <summary>
/// Marks an event as one that leaves this service: it crosses a bounded-context or system boundary and is
/// consumed by code you do not deploy.
/// </summary>
/// <remarks>
/// <para>
/// Applying this interface is a design decision, not a routing detail, and the decision is made before the
/// first property is added. Answer this: does the consumer need to be TOLD that something happened, or does
/// it need the DATA? The two answers produce different events and impose different obligations, and only one
/// of them is reversible cheaply.
/// </para>
/// <list type="bullet">
/// <item>
/// <term> Event notification </term>
/// <description>
/// Carry identity and nothing more: the key of the thing that changed, and enough to say what changed about
/// it. A consumer that needs detail calls back to this service to read it. The event stays small, you keep
/// control of what is exposed, and no consumer can come to depend on a field you never meant as a contract.
/// You pay for that with a callback per consumer, and with consumers that cannot proceed while this service
/// is unavailable.
/// </description>
/// </item>
/// <item>
/// <term> Event-carried state transfer </term>
/// <description>
/// Carry the data the consumer needs so that it never calls back. The consumer keeps working when this
/// service is down, and in exchange you take on three obligations. Every field you put on the event is a
/// published contract you can no longer change freely. The copy the consumer holds is stale from the moment
/// you send it, so it must not be treated as authoritative for a decision that requires current state. And
/// events can arrive out of order, so the consumer needs to reject an update older than one it has already
/// applied; give it a version or a timestamp on the event, or it has no way to tell.
/// </description>
/// </item>
/// </list>
/// <para>
/// Two further patterns are usually named alongside those two and are not alternatives to them. Event
/// sourcing makes events the system of record for an aggregate, and CQRS serves reads from a model built
/// separately from the write model; both are internal to one service, and neither is expressed by this
/// interface. An event that is the source of truth for an aggregate is a domain event
/// (<see cref="IDomainEvent"/>) and stays inside the boundary: publishing one directly turns your storage
/// format into a public contract, and every later change to the aggregate becomes a breaking change for
/// somebody else.
/// </para>
/// <para>
/// The shape you get by not choosing is a notification that grew one field at a time until it was carrying
/// state, with none of the state-transfer obligations met. Choose at declaration time.
/// </para>
/// </remarks>
/// <seealso href="https://docs.excalibur-dispatch.dev/docs/core-concepts/event-patterns">
/// Which of the four event patterns?
/// </seealso>
public interface IIntegrationEvent : IDispatchEvent
{
}
