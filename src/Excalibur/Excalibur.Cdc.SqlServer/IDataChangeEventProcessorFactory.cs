// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Data;
using Microsoft.Data.SqlClient;

namespace Excalibur.Cdc.SqlServer;

/// <summary>
/// Factory interface for creating instances of <see cref="IDataChangeEventProcessor" /> with specified configuration and database connections.
/// </summary>
public interface IDataChangeEventProcessorFactory
{
	/// <summary>
	/// Creates a new instance of <see cref="IDataChangeEventProcessor" /> using the specified database configuration and connections.
	/// </summary>
	/// <param name="dbConfig"> The database configuration containing details for the CDC process. </param>
	/// <param name="cdcRepository"> The CDC repository for querying change data. </param>
	/// <param name="stateStoreConnectionFactory"> Supplies a connection per CDC-state operation. </param>
	/// <returns> An instance of <see cref="IDataChangeEventProcessor" /> configured with the provided inputs. </returns>
	/// <exception cref="ArgumentNullException"> Thrown if any of the parameters are <c> null </c>. </exception>
	IDataChangeEventProcessor Create(IDatabaseOptions dbConfig, CdcRepository cdcRepository, Func<IDbConnection> stateStoreConnectionFactory);
}
