// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

namespace Excalibur.EventSourcing;

/// <summary>Which apply path owns a projection destination, declared where the store is built.</summary>
/// <remarks>
/// <para>
/// <b>A destination belongs to exactly one apply path, and the two have mutually exclusive
/// preconditions.</b> A transactional destination's rows are written inside the event store's own
/// transaction and carry the unnumbered sentinel plus the writing source's identity; a positioned
/// destination's rows carry a real stream position that a compare-and-set advances. A row cannot
/// satisfy both, so neither can a caller.
/// </para>
/// <para>
/// <b>Why the store has to be told, rather than working it out.</b> The only physical discriminator is
/// the destination's shape, and on two providers the transactional columns exist only on a transactional
/// table while on the other two one table shape serves both. A store handed a table name cannot
/// distinguish them without probing the schema, and a store that probes would be deciding a
/// configuration question at write time, per write, against a database that may have changed. Declaring
/// the kind at construction makes the wrong call inexpressible instead of detected.
/// </para>
/// <para>
/// <b>What this replaced.</b> A single store type advertised both contracts and handed out whichever a
/// caller asked for, discriminating on nothing but a type test. The guard against addressing a
/// transactional destination through the positioned contract was a CHECK constraint in shipped DDL — a
/// deployment artifact the store cannot read, reporting through a provider exception that the positioned
/// contract does not declare. That constraint remains as a backstop against a hand-applied migration or
/// a provider defect; in correct operation it never fires, because the capability is no longer offered.
/// </para>
/// </remarks>
public enum ProjectionDestinationKind
{
    /// <summary>
    /// The destination is advanced by position-conditional writes. The default, because it is the
    /// narrower claim: it offers no transaction-enlisted write, so a store that should have been
    /// transactional fails at the seam that needs the capability rather than writing through it.
    /// </summary>
    Positioned,

    /// <summary>
    /// The destination is written inside the event store's append transaction, keyed to the writing
    /// source and aggregate type, and holds no stream position.
    /// </summary>
    Transactional,
}
