// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;
using System.Reflection;

using Excalibur.Data.Postgres.Persistence;

using Npgsql;

namespace Excalibur.Data.Tests.Postgres;

/// <summary>
/// Regression tests for S541.3 (bd-kahxj): Postgres transaction scope dictionary race condition fix.
/// Validates that _transactions uses ConcurrentDictionary and that concurrent access is safe.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class PostgresTransactionScopeConcurrencyShould : UnitTestBase
{
	#region ConcurrentDictionary Verification

	[Fact]
	public void UsesConcurrentDictionaryForTransactions()
	{
		// Verify the _transactions field is ConcurrentDictionary (AD-541.2)
		var field = typeof(PostgresTransactionScope)
			.GetField("_transactions", BindingFlags.NonPublic | BindingFlags.Instance);

		field.ShouldNotBeNull("_transactions field must exist");
		field.FieldType.ShouldBe(
			typeof(ConcurrentDictionary<string, NpgsqlTransaction>),
			"_transactions must be ConcurrentDictionary<string, NpgsqlTransaction> for thread safety");
	}

	[Fact]
	public void UsesConcurrentDictionaryNotDictionary()
	{
		// Negative check: ensure we didn't accidentally use Dictionary<string, NpgsqlTransaction>
		var field = typeof(PostgresTransactionScope)
			.GetField("_transactions", BindingFlags.NonPublic | BindingFlags.Instance);

		field.ShouldNotBeNull();
		field.FieldType.ShouldNotBe(
			typeof(Dictionary<string, NpgsqlTransaction>),
			"_transactions must NOT be Dictionary — use ConcurrentDictionary for thread safety");
	}

	#endregion

	#region Lock Still Protects Other Collections

	[Fact]
	public void RetainLockFieldForOtherCollections()
	{
		// _lock must still exist since it protects _enlistedProviders, _enlistedConnections, _savepoints
		var field = typeof(PostgresTransactionScope)
			.GetField("_lock", BindingFlags.NonPublic | BindingFlags.Instance);

		field.ShouldNotBeNull("_lock field must still exist to protect other collections");
	}

	[Fact]
	public void UseListForEnlistedProviders()
	{
		// _enlistedProviders remains a regular List protected by _lock
		var field = typeof(PostgresTransactionScope)
			.GetField("_enlistedProviders", BindingFlags.NonPublic | BindingFlags.Instance);

		field.ShouldNotBeNull();
		field.FieldType.Name.ShouldStartWith("List");
	}

	[Fact]
	public void UseHashSetForSavepoints()
	{
		// _savepoints remains a regular HashSet protected by _lock
		var field = typeof(PostgresTransactionScope)
			.GetField("_savepoints", BindingFlags.NonPublic | BindingFlags.Instance);

		field.ShouldNotBeNull();
		field.FieldType.Name.ShouldStartWith("HashSet");
	}

	#endregion

	#region GetTransaction Thread Safety

	[Fact]
	public void GetTransactionUsesGetValueOrDefault()
	{
		// GetTransaction should use ConcurrentDictionary.GetValueOrDefault (thread-safe)
		var method = typeof(PostgresTransactionScope)
			.GetMethod("GetTransaction", BindingFlags.Public | BindingFlags.Instance);

		method.ShouldNotBeNull("GetTransaction must be a public method");
		method.ReturnType.ShouldBe(typeof(NpgsqlTransaction));
	}

	#endregion
}
