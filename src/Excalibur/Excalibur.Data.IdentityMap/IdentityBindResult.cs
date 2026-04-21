// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Data.IdentityMap;

/// <summary>
/// Represents the result of a conditional identity bind operation.
/// </summary>
/// <param name="AggregateId">The aggregate ID (existing or newly bound).</param>
/// <param name="WasCreated"><see langword="true"/> if a new mapping was created; <see langword="false"/> if an existing mapping was returned.</param>
public sealed record IdentityBindResult(string AggregateId, bool WasCreated);
