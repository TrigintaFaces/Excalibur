// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Data;

using Excalibur.Data.Abstractions;

namespace Excalibur.Data.DataProcessing;

/// <summary>
/// Implements <see cref="IDataToProcessDb" /> by delegating functionality to an underlying <see cref="IDb" /> instance.
/// </summary>
/// <remarks>
/// This class acts as an adapter, ensuring that the functionality of the provided <see cref="IDb" /> implementation is exposed through
/// the <see cref="IDataToProcessDb" /> interface for use in data processing operations.
/// </remarks>
public sealed class DataToProcessDb : IDataToProcessDb
{
	private readonly IDb _db;

	/// <summary>
	/// Initializes a new instance of the <see cref="DataToProcessDb" /> class with the specified database instance.
	/// </summary>
	/// <param name="db"> The underlying database instance to use for operations. </param>
	/// <exception cref="ArgumentNullException"> Thrown if <paramref name="db" /> is <c> null </c>. </exception>
	public DataToProcessDb(IDb db)
	{
		ArgumentNullException.ThrowIfNull(db);
		_db = db;
	}

	/// <inheritdoc />
	public IDbConnection Connection => _db.Connection;

	/// <inheritdoc />
	public void Close() => _db.Close();

	/// <inheritdoc />
	public void Open() => _db.Open();
}
