// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Dapper;

using Excalibur.Dispatch;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Excalibur.Workflows.SqlServer;

/// <summary>
/// Durable SQL Server implementation of <see cref="IWorkflowSignalInbox"/>. Admitted signals and their
/// deduplication keys are persisted, so a signal admitted but not yet drained survives a process restart and
/// a producer's redelivery of the same <c>(instanceId, signalId)</c> is still deduplicated after a restart —
/// the property the in-process default cannot provide.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="TryEnqueueAsync"/> is a <em>conditional insert</em>: a unique constraint on
/// <c>(TenantId, InstanceId, SignalId)</c> makes a redelivery a no-op (the unique-violation is caught and
/// reported as "not newly admitted"), never an upsert — matching the contract's idempotence promise for the
/// lifetime of the durable store. <see cref="DrainAsync"/> orders by an <c>IDENTITY</c> arrival sequence,
/// never a wall-clock timestamp, so consumption order is deterministic and reproducible.
/// </para>
/// <para>
/// <b>The tenant is part of signal identity, not a filter over it.</b> The uniqueness constraint that decides
/// admission carries the tenant term, and this is the half that is easy to get wrong: keyed on
/// <c>(InstanceId, SignalId)</c> alone, two tenants presenting the same pair collided, and the second tenant's
/// signal was reported "not newly admitted" and <em>silently dropped</em>. Its workflow then waited forever
/// for a signal the system had received and discarded, with no error raised and no row left behind as
/// evidence. Widening the key does not weaken deduplication — a redelivery <em>within</em> one tenant still
/// collides and is still refused; it stops one tenant's signal from being mistaken for another's.
/// </para>
/// <para>
/// <see cref="DrainAsync"/> is confined for a different and weaker reason than a key-addressed statement, so
/// it carries its own term: <c>InstanceId</c> is not unique on this table — it is the leading half of a
/// composite, with many signals per instance — so the tenant predicate genuinely restricts which rows are
/// visible rather than merely re-filtering a row already addressed by its key.
/// </para>
/// </remarks>
public sealed class SqlServerWorkflowSignalInbox : IWorkflowSignalInbox
{
    private readonly Func<SqlConnection> _connectionFactory;
    private readonly SqlServerWorkflowSignalInboxOptions _options;
    private readonly ITenantContext _tenantContext;

    /// <summary>
    /// Gets the tenant term this inbox runs under, resolved in one place so admission and drain bind the
    /// same value. The context is a required dependency, so the term is decided identically on every path:
    /// the inbox cannot admit a signal into one partition and then fail to drain it from another.
    /// </summary>
    private KeyedTenantPartition CurrentTenantPartition =>
        KeyedTenantPartition.FromContext(_tenantContext);

    /// <summary>
    /// Initializes a new instance of the <see cref="SqlServerWorkflowSignalInbox"/> class.
    /// </summary>
    /// <param name="options">The configured SQL Server signal-inbox options.</param>
    /// <param name="tenantContext">
    /// The ambient tenant context. Required: this inbox partitions signals by tenant, and it resolves that
    /// partition from here, so there is no state in which the partition is undecided. A single-tenant host
    /// receives the framework default context and operates as the one canonical tenant.
    /// </param>
    // Marked so the tenant-aware registration seam has one unambiguous constructor to hand the resolved
    // ambient context to. The sibling below takes an explicit connection factory and exists for tests
    // against a real instance; without this marker the seam refuses to guess between them.
    [ActivatorUtilitiesConstructor]
    public SqlServerWorkflowSignalInbox(
        IOptions<SqlServerWorkflowSignalInboxOptions> options,
        ITenantContext tenantContext)
        : this(CreateConnectionFactory(options?.Value), options!.Value, tenantContext)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SqlServerWorkflowSignalInbox"/> class with an explicit
    /// connection factory (used by tests against a real SQL Server instance).
    /// </summary>
    /// <param name="connectionFactory">A factory that creates open-able <see cref="SqlConnection"/> instances.</param>
    /// <param name="options">The configured SQL Server signal-inbox options.</param>
    /// <param name="tenantContext">
    /// The ambient tenant context. Required: this inbox partitions signals by tenant, and it resolves that
    /// partition from here, so there is no state in which the partition is undecided. A single-tenant host
    /// receives the framework default context and operates as the one canonical tenant.
    /// </param>
    public SqlServerWorkflowSignalInbox(
        Func<SqlConnection> connectionFactory,
        SqlServerWorkflowSignalInboxOptions options,
        ITenantContext tenantContext)
    {
        ArgumentNullException.ThrowIfNull(connectionFactory);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(tenantContext);

        _connectionFactory = connectionFactory;
        _options = options;
        _tenantContext = tenantContext;
    }

    /// <inheritdoc />
    public async ValueTask<bool> TryEnqueueAsync(
        string instanceId,
        string signalId,
        string signalName,
        string? payloadJson,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(instanceId);
        ArgumentException.ThrowIfNullOrWhiteSpace(signalId);
        ArgumentException.ThrowIfNullOrWhiteSpace(signalName);

        // Conditional insert: the UNIQUE (TenantId, InstanceId, SignalId) constraint makes redelivery
        // idempotent WITHIN a tenant, while leaving two tenants' identically-named signals as the distinct
        // signals they are. The table name is the only interpolated fragment and comes from trusted options,
        // never caller input; every value is a @-parameter.
#pragma warning disable CA2100 // Table identifier is from trusted configuration, not user input.
        var sql = $"""
            INSERT INTO {_options.QualifiedTableName} (TenantId, InstanceId, SignalId, SignalName, PayloadJson)
            VALUES (@TenantId, @InstanceId, @SignalId, @SignalName, @PayloadJson)
            """;
#pragma warning restore CA2100

        await using var connection = _connectionFactory();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            var command = new CommandDefinition(
                sql,
                new
                {
                    TenantId = CurrentTenantPartition.TenantId,
                    InstanceId = instanceId,
                    SignalId = signalId,
                    SignalName = signalName,
                    PayloadJson = payloadJson
                },
                commandTimeout: _options.CommandTimeoutSeconds,
                cancellationToken: cancellationToken);

            _ = await connection.ExecuteAsync(command).ConfigureAwait(false);
            return true;
        }
        catch (SqlException ex) when (ex.Number is 2627 or 2601)
        {
            // Unique constraint / index violation: this tenant has already admitted a signal with the same
            // (InstanceId, SignalId). Redelivery is a no-op — report "not newly admitted", never an upsert.
            // Another tenant's identical pair does not reach here: it is a different row under the widened
            // key, so it is admitted rather than mistaken for a duplicate of this one.
            return false;
        }
    }

    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<WorkflowSignalEntry>> DrainAsync(
        string instanceId,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(instanceId);

#pragma warning disable CA2100 // Table identifier is from trusted configuration, not user input.
        var sql = $"""
            SELECT Sequence, SignalId, SignalName, PayloadJson
            FROM {_options.QualifiedTableName}
            WHERE TenantId = @TenantId AND InstanceId = @InstanceId
            ORDER BY Sequence ASC
            """;
#pragma warning restore CA2100

        await using var connection = _connectionFactory();
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        var command = new CommandDefinition(
            sql,
            new { TenantId = CurrentTenantPartition.TenantId, InstanceId = instanceId },
            commandTimeout: _options.CommandTimeoutSeconds,
            cancellationToken: cancellationToken);

        var rows = await connection.QueryAsync<WorkflowSignalEntry>(command).ConfigureAwait(false);
        return rows.ToArray();
    }

    private static Func<SqlConnection> CreateConnectionFactory(SqlServerWorkflowSignalInboxOptions? options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return () => new SqlConnection(options.ConnectionString);
    }
}
