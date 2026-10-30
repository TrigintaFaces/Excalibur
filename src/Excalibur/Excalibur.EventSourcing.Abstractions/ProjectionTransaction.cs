// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Data;

namespace Excalibur.EventSourcing;

/// <summary>Authoritative stream history and transaction owned by a transactional append.</summary>
/// <param name="Transaction">The transaction. Participants must not commit or dispose it.</param>
/// <param name="History">Complete ordered history before this append, protected until commit.</param>
/// <param name="TenantId">The source tenant partition.</param>
/// <param name="SourceIdentity">The source table identity within the transaction's database.</param>
/// <remarks>Valid only during the append callback. No external effects or independently committed writes are permitted.</remarks>
public sealed record ProjectionTransaction(
    IDbTransaction Transaction,
    IReadOnlyList<StoredEvent> History,
    string TenantId,
    string SourceIdentity);
