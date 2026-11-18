// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.ComponentModel.DataAnnotations;

using Excalibur.Data.Validation;

namespace Excalibur.Compliance.Postgres.Erasure;

/// <summary>
/// Configuration options for the Postgres erasure store.
/// </summary>
public sealed class PostgresErasureStoreOptions
{
	/// <summary>
	/// Gets or sets the Postgres connection string.
	/// </summary>
	[Required]
	public string ConnectionString { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the schema name for erasure tables.
	/// </summary>
	[Required]
	public string SchemaName { get; set; } = "compliance";

	/// <summary>
	/// Gets or sets the erasure requests table name.
	/// </summary>
	[Required]
	public string RequestsTableName { get; set; } = "erasure_requests";

	/// <summary>
	/// Gets or sets the erasure certificates table name.
	/// </summary>
	[Required]
	public string CertificatesTableName { get; set; } = "erasure_certificates";

	/// <summary>
	/// Gets or sets the destruction LEDGER table: the key generations this framework has irreversibly destroyed.
	/// </summary>
	/// <remarks>
	/// A table rather than a column on the request, because the value is a SET and the writes are appends. Its
	/// primary key is the GENERATION alone — a generation is minted once and never reused, so a row's existence
	/// IS the statement that it was destroyed, and a second row for one generation is a contradiction the
	/// database refuses. The request and the handle ride along as audit attributes and are never part of the key:
	/// keying on the handle instead silently drops a second destruction at the same handle, which is a
	/// destruction that happened and is not on file.
	/// </remarks>
	public string DestroyedKeysTableName { get; set; } = "erasure_destroyed_keys";

	/// <summary>
	/// Gets or sets the staging table holding generations a request is ABOUT TO destroy.
	/// </summary>
	/// <remarks>
	/// Separate from the ledger, and the separation is the design rather than a tidiness choice. A staged row is
	/// written BEFORE the destruction — it has to be, because the destruction destroys the generation identifier
	/// itself and a crash afterwards would leave the record unwritable forever. So between the stage and the
	/// destruction the row names LIVE material, and nothing that resolves a destruction predicate may be able to
	/// reach it. Holding both states in one table behind a nullable timestamp would make that a discipline; two
	/// tables make it inexpressible.
	/// </remarks>
	public string DestructionIntentsTableName { get; set; } = "erasure_destruction_intents";

	/// <summary>
	/// Gets or sets the MINT ledger table: the key handles this framework has intended to mint for a data
	/// subject, written ahead of each mint.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Its primary key is the HANDLE ALONE, and the asymmetry with the destruction ledger above is the
	/// design rather than an inconsistency. Existence is a per-HANDLE fact here, where a destruction is a
	/// per-GENERATION one: a handle minted, destroyed and minted again has one row throughout, and that row
	/// stays true. A generation column would have to be updated after a re-mint, which makes the row mutable
	/// and rebuilds a create-or-rotate race one layer down.
	/// </para>
	/// <para>
	/// <b>The POLARITY is inverted from the destruction ledger, which is why this table may not be
	/// volatile.</b> The destruction ledger needs row ⇒ destroyed, so it is written AFTER the act and a lost
	/// write under-reports coverage. This one needs minted ⇒ row, so it is written BEFORE the act and a lost
	/// write produces a key no erasure will ever enumerate — a subject reported erased whose personal data
	/// is still readable, with nothing downstream able to learn otherwise.
	/// </para>
	/// </remarks>
	public string MintedKeysTableName { get; set; } = "erasure_minted_keys";

	/// <summary>
	/// Gets or sets the table recording which data-subject token function produced this database's
	/// pseudonymisation tokens.
	/// </summary>
	/// <remarks>
	/// <para>
	/// It holds at most one row, for the whole database, and the row is write-once. Changing the hashing
	/// key changes every token this store is keyed by, so without this record an erasure enumerates no
	/// handles and certifies over what it missed, a legal-hold lookup finds nothing and the erasure
	/// proceeds over data the law required kept, and already-encrypted data stops being decryptable —
	/// none of it raising and none of it logged.
	/// </para>
	/// <para>
	/// <b>Its table carries NO tenant column, deliberately.</b> The token function is a property of the
	/// process, not of a tenant; a per-tenant fingerprint would record a rotated key as correct for a
	/// newly onboarded tenant while every earlier tenant was already orphaned.
	/// </para>
	/// </remarks>
	public string TokenFingerprintTableName { get; set; } = "erasure_token_fingerprint";

	/// <summary>
	/// Gets or sets the table name for the deployment-wide record of declared retention declarations.
	/// </summary>
	/// <remarks>
	/// <para>
	/// One row per declared (tenant, aggregate type) pair, inserted if absent and never updated. A
	/// retention declaration lives in the consumer's code, so each instance holds only its own view; this
	/// table is what lets an instance discover that the deployment has recorded declarations it does not
	/// hold, and withhold a destruction it cannot perform correctly.
	/// </para>
	/// <para>
	/// <b>Its tenant lives in the PRIMARY KEY and nowhere else.</b> The pair is the identity of a row, so
	/// the key addresses it completely, and a tenant predicate added to a statement the key already
	/// addresses can only ever turn the correct row into zero rows.
	/// </para>
	/// </remarks>
	public string RetentionDeclarationsTableName { get; set; } = "erasure_retention_declarations";

	/// <summary>
	/// Gets or sets the command timeout in seconds.
	/// </summary>
	[Range(1, 3600)]
	public int CommandTimeoutSeconds { get; set; } = 30;

	/// <summary>
	/// Gets or sets whether to auto-create the schema and tables on startup. Defaults to
	/// <see langword="false"/>: an application connection holding schema-DDL privileges in production is a
	/// posture most regulated environments refuse, so provisioning is opt-in. When <see langword="false"/>,
	/// startup VERIFIES the required schema and tables exist and FAILS FAST if they do not — it never
	/// silently skips, and it never creates them.
	/// </summary>
	public bool AutoCreateSchema { get; set; }

	/// <summary>
	/// Gets the full requests table name including schema.
	/// </summary>
	public string FullRequestsTableName => $"\"{SchemaName}\".\"{RequestsTableName}\"";

	/// <summary>
	/// Gets the full certificates table name including schema.
	/// </summary>
	public string FullCertificatesTableName => $"\"{SchemaName}\".\"{CertificatesTableName}\"";

	/// <summary>
	/// Gets the full destroyed-keys table name including schema.
	/// </summary>
	public string FullDestroyedKeysTableName => $"\"{SchemaName}\".\"{DestroyedKeysTableName}\"";

	/// <summary>
	/// Gets the full destruction-intents table name including schema.
	/// </summary>
	public string FullDestructionIntentsTableName => $"\"{SchemaName}\".\"{DestructionIntentsTableName}\"";

	/// <summary>
	/// Gets the full minted-keys table name including schema.
	/// </summary>
	public string FullMintedKeysTableName => $"\"{SchemaName}\".\"{MintedKeysTableName}\"";

	/// <summary>
	/// Gets the full token-fingerprint table name including schema.
	/// </summary>
	public string FullTokenFingerprintTableName => $"\"{SchemaName}\".\"{TokenFingerprintTableName}\"";

	/// <summary>
	/// Gets the full retention-declarations table name including schema.
	/// </summary>
	public string FullRetentionDeclarationsTableName =>
		$"\"{SchemaName}\".\"{RetentionDeclarationsTableName}\"";

	/// <summary>
	/// Validates the options.
	/// </summary>
	/// <exception cref="InvalidOperationException">Thrown when options are invalid.</exception>
	public void Validate()
	{
		if (string.IsNullOrWhiteSpace(ConnectionString))
		{
			throw new InvalidOperationException("ConnectionString is required for PostgresErasureStore.");
		}

		if (string.IsNullOrWhiteSpace(SchemaName))
		{
			throw new InvalidOperationException("SchemaName cannot be empty.");
		}

		if (string.IsNullOrWhiteSpace(RequestsTableName))
		{
			throw new InvalidOperationException("RequestsTableName cannot be empty.");
		}

		if (string.IsNullOrWhiteSpace(CertificatesTableName))
		{
			throw new InvalidOperationException("CertificatesTableName cannot be empty.");
		}

		if (CommandTimeoutSeconds <= 0)
		{
			throw new InvalidOperationException("CommandTimeoutSeconds must be positive.");
		}

		if (!SqlIdentifierValidator.IsValid(SchemaName))
		{
			throw new InvalidOperationException(
				$"SQL identifier '{nameof(SchemaName)}' contains invalid characters. Only alphanumeric characters and underscores are allowed.");
		}

		if (!SqlIdentifierValidator.IsValid(RequestsTableName))
		{
			throw new InvalidOperationException(
				$"SQL identifier '{nameof(RequestsTableName)}' contains invalid characters. Only alphanumeric characters and underscores are allowed.");
		}

		if (!SqlIdentifierValidator.IsValid(CertificatesTableName))
		{
			throw new InvalidOperationException(
				$"SQL identifier '{nameof(CertificatesTableName)}' contains invalid characters. Only alphanumeric characters and underscores are allowed.");
		}
	}
}
