// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


// Using local Data namespace now that DataAccess is merged
using Excalibur.Data.Abstractions;

namespace Excalibur.Data;

/// <summary>
/// Represents a domain-specific database abstraction that extends the generic database interface <see cref="IDb" />.
/// </summary>
public interface IDomainDb : IDb;
