// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Data;
using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Text;

using Excalibur.Dispatch;

using Microsoft.Data.SqlClient;

namespace Excalibur.EventSourcing.SqlServer;

public sealed partial class SqlServerOperationExecutor
{
	/// <summary>Binds a configured source to its database/capture generation, refusing silent history replacement.</summary>
	/// <param name="source">Stable configured source name; do not include its changing generation.</param>
	/// <param name="sourceIdentity">SHA-256 of database identity, recovery fork and capture creation identity.</param>
	/// <param name="cancellationToken">Cancels the check.</param>
	/// <remarks>
	/// Store this binding outside databases refreshed from source backups. Check before each delivery and
	/// on startup. A mismatch requires operator-coordinated reconciliation or rebuild before explicit rebind.
	/// This check is not a fence against an administrator forcing a restore during an in-flight handler;
	/// maintenance must stop and drain all consumers before replacing the source.
	/// </remarks>
	[SuppressMessage("Security", "CA2100", Justification = "Only constructor-validated SQL identifiers are interpolated; source and identity are typed parameters.")]
	public async Task BindSourceAsync(string source, ReadOnlyMemory<byte> sourceIdentity, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(source);
		if (source.Contains('\0', StringComparison.Ordinal) || sourceIdentity.Length != 32)
		{
			throw new ArgumentException("Source must not contain NUL; identity must be SHA-256.");
		}
		var tenant = TenantScope.FromContext(_tenantContext).TenantId;
		if (tenant.Contains('\0', StringComparison.Ordinal))
		{
			throw new ArgumentException("Tenant identifiers must not contain NUL.");
		}
		var key = SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('\0', _schema, _eventsTable, tenant, source)));
		await using var connection = _connectionFactory();
		await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
		await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
		await using var command = connection.CreateCommand();
		command.Transaction = transaction;
		command.CommandText = $"SELECT SourceIdentity FROM [{_schema}].[{_eventsTable}Sources] WITH (UPDLOCK,HOLDLOCK) WHERE SourceId=@id;";
		_ = command.Parameters.Add("@id", SqlDbType.Binary, 32).Value = key;
		var existing = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
		if (existing is byte[] prior && !CryptographicOperations.FixedTimeEquals(prior, sourceIdentity.Span))
		{
			throw new SqlServerSourceChangedException();
		}
		if (existing is null)
		{
			command.CommandText = $"INSERT INTO [{_schema}].[{_eventsTable}Sources] (SourceId,SourceIdentity) VALUES (@id,@identity);";
			_ = command.Parameters.Add("@identity", SqlDbType.Binary, 32).Value = sourceIdentity.ToArray();
			await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
		}
		await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
	}
}

/// <summary>The bound source database, recovery fork or capture generation changed.</summary>
public sealed class SqlServerSourceChangedException : InvalidOperationException
{
	/// <summary>Initializes a source replacement exception with the default explanation.</summary>
	public SqlServerSourceChangedException() : base("The CDC source history changed. Stop consumers and reconcile or rebuild target state before explicitly rebinding the source.") { }

	/// <summary>Initializes a source replacement exception with a caller-supplied explanation.</summary>
	/// <param name="message">The explanation. Supply one when you can name which source identity changed.</param>
	public SqlServerSourceChangedException(string message) : base(message) { }

	/// <summary>Initializes a source replacement exception that preserves the failure beneath it.</summary>
	/// <param name="message">The explanation.</param>
	/// <param name="innerException">The underlying failure, kept so a consumer wrapping this does not lose it.</param>
	public SqlServerSourceChangedException(string message, Exception innerException) : base(message, innerException) { }
}
