// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Security.Cryptography;
using System.Text;

using Excalibur.Dispatch;

using Microsoft.Data.SqlClient;

namespace Excalibur.EventSourcing.SqlServer;

/// <summary>Deduplicates a single SQL event append using a receipt committed in its append transaction.</summary>
/// <remarks>
/// Apply Scripts/006_CreateOperationReceipts.sql first (adapt names for a custom events table).
/// The callback may perform one nonempty append through SqlServerEventStore to this exact destination
/// and tenant. It must not perform independently committed business writes or external effects.
/// No-event callbacks are not recorded. Receipts are retained indefinitely. Concurrent callbacks may run;
/// the unique receipt in the append transaction permits only one committed append.
/// </remarks>
public sealed partial class SqlServerOperationExecutor : ISqlServerOperationExecutor
{
	private readonly Func<SqlConnection> _connectionFactory;
	private readonly ITenantContext _tenantContext;
	private readonly string _schema;
	private readonly string _eventsTable;

	// The canonical names are PRIVATE consts read by the two-argument overload, deliberately not public
	// and deliberately not default parameter values. A default argument is compiled into the CALLER's
	// assembly, so changing one later splits behaviour between consumers who recompiled and consumers who
	// did not -- silently, with no diagnostic. A public const has the same property. An overload resolves
	// the names here, inside this assembly, so a later change reaches every consumer on upgrade.
	private const string CanonicalSchema = "dbo";
	private const string CanonicalEventsTable = "EventStoreEvents";

	/// <summary>Initializes an executor using the event store's canonical schema and table names.</summary>
	/// <param name="connectionFactory">Creates fresh owned primary SQL connections.</param>
	/// <param name="tenantContext">The event store's tenant context.</param>
	public SqlServerOperationExecutor(Func<SqlConnection> connectionFactory, ITenantContext tenantContext)
		: this(connectionFactory, tenantContext, CanonicalSchema, CanonicalEventsTable)
	{
	}

	/// <summary>Initializes an executor for the same primary database and names as the event store.</summary>
	/// <param name="connectionFactory">Creates fresh owned primary SQL connections.</param>
	/// <param name="tenantContext">The event store's tenant context.</param>
	/// <param name="schema">The event store schema.</param>
	/// <param name="eventsTable">The event table; receipts use this name followed by Operations.</param>
	public SqlServerOperationExecutor(Func<SqlConnection> connectionFactory, ITenantContext tenantContext,
		string schema, string eventsTable)
	{
		ArgumentNullException.ThrowIfNull(connectionFactory);
		ArgumentNullException.ThrowIfNull(tenantContext);
		ValidateIdentifier(schema);
		ValidateIdentifier(eventsTable);
		ValidateIdentifier(eventsTable + "Operations");
		_connectionFactory = connectionFactory;
		_tenantContext = tenantContext;
		_schema = schema;
		_eventsTable = eventsTable;
	}

	/// <summary>Runs a single-append operation or returns its original durable receipt without running it.</summary>
	/// <param name="scope">A stable authenticated caller and operation namespace, or CDC source incarnation.</param>
	/// <param name="operationKey">The caller's stable retry key. Compared ordinally.</param>
	/// <param name="fingerprint">SHA-256 of immutable input, including target and original preconditions.</param>
	/// <param name="action">The handler. At most one nonempty event append is supported.</param>
	/// <param name="cancellationToken">Cancels this attempt; cancellation does not prove rollback.</param>
	/// <returns>The original receipt on replay, or the outcome of this attempt.</returns>
	public async Task<SqlServerOperationResult> ExecuteAsync(string scope, string operationKey,
		ReadOnlyMemory<byte> fingerprint, Func<CancellationToken, Task> action, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(scope);
		ArgumentException.ThrowIfNullOrWhiteSpace(operationKey);
		ArgumentNullException.ThrowIfNull(action);
		if (scope.Contains('\0', StringComparison.Ordinal) || operationKey.Contains('\0', StringComparison.Ordinal)
			|| fingerprint.Length != 32)
		{
			throw new ArgumentException("Scope/key must not contain NUL; fingerprint must be SHA-256.");
		}
		if (SqlServerOperationContext.Current is not null)
		{
			throw new InvalidOperationException("Nested idempotent operations are not supported.");
		}
		var tenant = TenantScope.FromContext(_tenantContext).TenantId;
		if (tenant.Contains('\0', StringComparison.Ordinal))
		{
			throw new ArgumentException("Tenant identifiers must not contain NUL.");
		}
		var identity = SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('\0', _schema, _eventsTable, tenant, scope, operationKey)));
		SqlServerOperationContext context;
		// Never hold a coordination connection while the handler needs another pooled connection.
		// Receipt uniqueness in the append transaction is sufficient even for concurrent callbacks.
		await using (var lookup = _connectionFactory())
		{
			await lookup.OpenAsync(cancellationToken).ConfigureAwait(false);
			context = new SqlServerOperationContext(lookup.DataSource, lookup.Database, _schema,
				_eventsTable, tenant, identity, fingerprint.ToArray());
			var prior = await context.ReadReceiptAsync(lookup, cancellationToken).ConfigureAwait(false);
			if (prior is not null)
			{
				context.Close();
				return new SqlServerOperationResult(true, prior);
			}
		}
		try
		{
			SqlServerOperationContext.Current = context;
			try
			{
				await action(cancellationToken).ConfigureAwait(false);
			}
			catch (Exception)
			{
				// A commit error is not proof of rollback. Reconcile on a fresh primary connection,
				// independently of request cancellation, before classifying the fault.
				var committed = await ReadReceiptAsync(context).ConfigureAwait(false);
				if (committed is not null)
				{
					return new SqlServerOperationResult(true, committed);
				}
				throw;
			}
			var receipt = await ReadReceiptAsync(context).ConfigureAwait(false);
			if (context.AppendAttempted && receipt is null)
			{
				throw new InvalidOperationException("An append was attempted without an atomic operation receipt. The operation did not complete.");
			}
			return new SqlServerOperationResult(receipt is not null && !context.ReceiptBelongsToThisAttempt, receipt);
		}
		finally
		{
			context.Close();
			SqlServerOperationContext.Current = null;
		}
	}

	private async Task<SqlServerOperationReceipt?> ReadReceiptAsync(SqlServerOperationContext context)
	{
		await using var connection = _connectionFactory();
		await connection.OpenAsync(CancellationToken.None).ConfigureAwait(false);
		return await context.ReadReceiptAsync(connection, CancellationToken.None).ConfigureAwait(false);
	}
	private static void ValidateIdentifier(string value)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(value);
		if (value.Length > 128 || !value.All(c => char.IsAsciiLetterOrDigit(c) || c == '_'))
		{
			throw new ArgumentException("SQL identifiers must contain only ASCII letters, digits or underscores.", nameof(value));
		}
	}
}


