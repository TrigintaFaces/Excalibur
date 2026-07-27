// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.EventSourcing.Oracle;

using Oracle.ManagedDataAccess.Client;

using Xunit;

namespace Excalibur.EventSourcing.Tests.Oracle;

/// <summary>
/// Unit lock for <c>OracleBlobParameter</c> — the fix for the size-dependent Oracle snapshot/erase
/// write failure (a7zf4r). Dapper infers a <c>byte[]</c> parameter as <c>DbType.Binary</c>, which
/// ODP.NET maps to <c>RAW</c> (capped at 2000 bytes), so a real aggregate's snapshot above the limit is
/// rejected (ORA-01460 / ORA-12899) even though the column is <c>BLOB</c>. <c>OracleBlobParameter</c>
/// binds an explicit <see cref="OracleDbType.Blob"/> so the full payload streams regardless of length.
/// </summary>
/// <remarks>
/// This is the server-free arm: it asserts the emitted parameter's type, which is the exact property
/// that regressed (Binary/RAW). It goes RED the instant the binding reverts to any non-BLOB type. The
/// end-to-end proof that a &gt;2000-byte payload round-trips is a real-Oracle integration test
/// (Docker-gated, not in CI) and is tracked separately.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
[Trait("Database", "Oracle")]
public sealed class OracleBlobParameterShould
{
	[Fact]
	public void BindAByteArrayAsBlob_NotBinaryOrRaw()
	{
		// Arrange — a payload above the 2000-byte RAW limit: the exact case that fails under Binary/RAW.
		var payload = new byte[3000];
		for (var i = 0; i < payload.Length; i++)
		{
			payload[i] = (byte)(i % 256);
		}

		var parameter = BindThroughCommand(payload);

		// Assert — BLOB, so the full payload streams regardless of length. RED if it reverts to RAW/Binary.
		parameter.OracleDbType.ShouldBe(OracleDbType.Blob);
		parameter.Value.ShouldBe(payload);
	}

	[Fact]
	public void BindNullAsBlobDbNull()
	{
		var parameter = BindThroughCommand(value: null);

		parameter.OracleDbType.ShouldBe(OracleDbType.Blob, "a null payload still binds the BLOB type");
		parameter.Value.ShouldBe(DBNull.Value, "a null payload binds SQL NULL, not a CLR null");
	}

	[Fact]
	public void BindASmallPayloadAsBlobToo()
	{
		// Liveness: the fix binds BLOB unconditionally — a small payload (which RAW would have accepted)
		// is still bound as BLOB, so there is one binding path, not a size-branch that could diverge.
		var parameter = BindThroughCommand([1, 2, 3]);

		parameter.OracleDbType.ShouldBe(OracleDbType.Blob);
	}

	// Adds the parameter through a real (connectionless) OracleCommand — no database is contacted;
	// constructing a command and populating its parameter collection is a pure client-side operation.
	private static OracleParameter BindThroughCommand(byte[]? value)
	{
		using var command = new OracleCommand();
		var blobParameter = new OracleBlobParameter(value);

		// AddParameter is a public method on the (InternalsVisibleTo-visible) type — call it directly.
		blobParameter.AddParameter(command, ":payload");

		command.Parameters.Count.ShouldBe(1);
		return command.Parameters[0].ShouldBeOfType<OracleParameter>();
	}
}
