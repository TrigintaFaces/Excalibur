// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

namespace Excalibur.A3.Authorization;

/// <summary>Committed outcome of one atomic revocation.</summary>
/// <param name="OperationId">Stable operation identifier.</param>
/// <param name="Revision">Committed user revision.</param>
/// <param name="RevokedCount">Number of removed grants, including expired grants and every grant type.</param>
public sealed record GrantRevocationResult(Guid OperationId, long Revision, int RevokedCount);
