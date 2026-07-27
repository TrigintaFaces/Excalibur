// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.EventSourcing;

/// <summary>
/// Wire-time capability marker registered by an event-store provider whose concrete store implements
/// <see cref="ITransactionalEventStore"/>.
/// </summary>
/// <remarks>
/// <para>
/// Its presence in the service collection attests that the wired event store can atomically append events
/// and stage the outbox, <em>without</em> resolving (or constructing) the store. A startup validator can
/// therefore probe <see cref="Microsoft.Extensions.DependencyInjection.IServiceProviderIsService"/> for
/// this marker instead of resolving <see cref="IEventStore"/> from the root provider — which throws for a
/// scoped store under scope validation and cannot see the capability through a telemetry/decorator wrapper.
/// </para>
/// <para>
/// Only providers whose store implements <see cref="ITransactionalEventStore"/> register it; the absence of
/// the marker is the honest signal that no transactional event store is wired.
/// </para>
/// </remarks>
public sealed class TransactionalEventStoreMarker;
