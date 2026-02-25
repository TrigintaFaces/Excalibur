// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Data;

using Excalibur.Data.Abstractions;

namespace Excalibur.Data;

/// <summary>
/// Represents a specialized database context for read-side projection persistence operations.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="ProjectionDb" /> class. This class extends the <see cref="Db" /> class to provide
/// a typed database abstraction for projection stores.
/// </remarks>
/// <param name="connection"> The database connection to be used for operations. </param>
/// <exception cref="ArgumentNullException"> Thrown if <paramref name="connection" /> is <c> null </c>. </exception>
public sealed class ProjectionDb(IDbConnection connection) : Db(connection), IProjectionDb
{
}
