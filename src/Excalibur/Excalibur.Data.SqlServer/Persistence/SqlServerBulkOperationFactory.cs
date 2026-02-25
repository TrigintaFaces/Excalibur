// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Data;

using Microsoft.Data.SqlClient;

namespace Excalibur.Data.SqlServer.Persistence;

internal sealed class SqlServerBulkOperationFactory
{
	private readonly SqlServerPersistenceOptions _options;

	internal SqlServerBulkOperationFactory(SqlServerPersistenceOptions options) =>
		_options = options ?? throw new ArgumentNullException(nameof(options));

	internal static SqlParameter CreateTableValuedParameter(string typeName, DataTable data) =>
		new()
		{
			ParameterName = "@TableParam",
			SqlDbType = SqlDbType.Structured,
			TypeName = typeName,
			Value = data,
		};

	internal SqlBulkCopy CreateBulkCopy(SqlConnection connection, SqlTransaction? transaction = null)
	{
		var bulkCopyOptions = SqlBulkCopyOptions.Default;

		if (transaction != null)
		{
			bulkCopyOptions |= SqlBulkCopyOptions.CheckConstraints;
		}

		var bulkCopy = transaction != null
			? new SqlBulkCopy(connection, bulkCopyOptions, transaction)
			: new SqlBulkCopy(connection, bulkCopyOptions, externalTransaction: null);

		bulkCopy.BulkCopyTimeout = _options.CommandTimeout;
		bulkCopy.BatchSize = 1000;

		return bulkCopy;
	}
}
