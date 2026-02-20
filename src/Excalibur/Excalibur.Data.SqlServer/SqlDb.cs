// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Data;

using Excalibur.Data.Abstractions;

namespace Excalibur.Data.SqlServer;

/// <summary>
/// Represents a specific implementation of <see cref="Db" /> for SQL databases.
/// </summary>
/// <remarks> Initializes a new instance of the <see cref="SqlDb" /> class. </remarks>
/// <param name="connection"> The SQL database connection to manage. </param>
public sealed class SqlDb(IDbConnection connection) : Db(connection)
{
}
