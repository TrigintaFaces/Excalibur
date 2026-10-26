// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

namespace Excalibur.Data.IdentityMap;

/// <summary>
/// Represents the result of a conditional identity bind operation.
/// </summary>
/// <param name="AggregateId">The aggregate ID (existing or newly bound).</param>
/// <param name="MappingWasCreated">
/// <see langword="true"/> if a new MAPPING was created; <see langword="false"/> if an existing mapping was
/// returned. This describes the mapping and nothing else — see the remarks before branching on it.
/// </param>
/// <remarks>
/// <para>
/// <b>A binding is a RESERVATION of an identity, never an assertion that the aggregate exists.</b> The
/// mapping and the aggregate's event stream are two writes to two stores, and nothing makes them atomic.
/// So <see cref="MappingWasCreated"/> answers "did I win the race to reserve this identity", which is a question
/// about this call. It does not answer "does a stream exist for this aggregate", which is a question about
/// a different store at a different time.
/// </para>
/// <para>
/// <b>Do not use it as the insert-versus-update discriminator.</b> That reading is wrong in one direction
/// and it is permanent. If anything interrupts between the bind and the append — cancellation at a
/// processing deadline, a crash, an eviction, a transport fault — the mapping is committed and the stream
/// never arrives. Every later call then returns <c><see cref="MappingWasCreated"/> = false</c> for that external
/// id, an update path that loads the aggregate finds nothing, and the record fails forever. It cannot
/// self-heal, because the only branch that would have created the stream is the one the flag no longer
/// selects.
/// </para>
/// <para>
/// <b>Branch on whether the STREAM exists.</b> Load the aggregate and treat "bound but absent" as the
/// insert case — that makes the sequence correct at every interruption point and needs no compensating
/// delete, which could not be relied upon anyway: an unbind in a failure path only runs if the process
/// survives to run it, and it races a concurrent writer that has already resolved the same mapping.
/// <see cref="MappingWasCreated"/> remains useful for what it actually reports — telling a first sighting from a
/// repeat, emitting a created-versus-updated metric, logging.
/// </para>
/// <example>
/// <code>
/// var bind = await identityMap.TryBindAsync(system, externalId, "Client", NewId(), cancellationToken);
/// var id = Guid.Parse(bind.AggregateId);
///
/// // The stream, not the flag, decides. A reservation whose append never landed lands here and recovers.
/// var client = await repository.GetByIdAsync(id, cancellationToken);
/// if (client is null)
/// {
///     await InsertAsync(id, data, cancellationToken);
/// }
/// else
/// {
///     await UpdateAsync(client, data, cancellationToken);
/// }
/// </code>
/// </example>
/// </remarks>
public sealed record IdentityBindResult(string AggregateId, bool MappingWasCreated);
