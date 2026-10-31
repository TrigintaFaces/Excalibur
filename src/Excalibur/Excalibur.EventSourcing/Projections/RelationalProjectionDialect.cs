// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Data.Common;
using System.Globalization;

namespace Excalibur.EventSourcing.Projections;

/// <summary>How one relational engine spells and executes the projection destination's statements.</summary>
/// <remarks>
/// <para>
/// <b>This replaced a <c>bool oracle</c> constructor flag, and the flag was not merely untidy.</b> It read
/// <c>oracle: false</c> at the SQLite call site, where the honest meaning is "SQLite" and the literal
/// meaning is "every engine that is not Oracle" -- so a third provider had no value to pass and would have
/// had to pick the branch that was least wrong. The divergences were also scattered across nine
/// conditionals in two files, which is where a new provider silently inherits whichever branch nobody
/// revisited. Naming the engine collects them in one place and makes the absent third provider a missing
/// subclass, which the compiler can see, rather than a missing <c>else</c>, which it cannot.
/// </para>
/// <para>
/// <b>Member count is over the five-member guideline, and that is a deliberate trade rather than an
/// oversight.</b> This is an internal type with a single consumer, and every member is the same question
/// asked of one engine: how does it spell this. Splitting it to satisfy the number would scatter one
/// concern across two types and re-open the gap this exists to close. Recorded so a reviewer reading the
/// guideline does not have to guess whether it was considered.
/// </para>
/// </remarks>
internal abstract class RelationalProjectionDialect
{
    private readonly Func<DbException, bool> _isDuplicateKey;

    private protected RelationalProjectionDialect(Func<DbException, bool> isDuplicateKey)
    {
        ArgumentNullException.ThrowIfNull(isDuplicateKey);
        _isDuplicateKey = isDuplicateKey;
    }

    /// <summary>Whether this engine reports a numeric JSON scalar only when the query asks for one.</summary>
    /// <remarks>
    /// Gates reflection over the projection's serialized metadata, which is why it is a flag rather than
    /// an argument the caller always computes.
    /// </remarks>
    internal abstract bool TypedJsonNumbers { get; }

    /// <summary>Oracle, whose duplicate-key signal is supplied by the provider package.</summary>
    /// <param name="isDuplicateKey">Recognises this engine's duplicate-key violation.</param>
    /// <returns>The Oracle dialect.</returns>
    /// <remarks>
    /// The predicate is injected because <c>Excalibur.EventSourcing</c> does not reference a provider's
    /// client library, and it travels WITH the dialect because recognising a conflict and spelling the
    /// statement that raises it are the same engine's business.
    /// </remarks>
    internal static RelationalProjectionDialect ForOracle(Func<DbException, bool> isDuplicateKey) =>
        new OracleProjectionDialect(isDuplicateKey);

    /// <summary>SQLite.</summary>
    /// <param name="isDuplicateKey">Recognises this engine's constraint violation.</param>
    /// <returns>The SQLite dialect.</returns>
    internal static RelationalProjectionDialect ForSqlite(Func<DbException, bool> isDuplicateKey) =>
        new SqliteProjectionDialect(isDuplicateKey);

    /// <summary>Whether this exception is the engine reporting that the row's key already exists.</summary>
    /// <param name="error">The provider exception.</param>
    /// <returns><see langword="true"/> when the engine is reporting a duplicate key.</returns>
    internal bool IsDuplicateKey(DbException error) => _isDuplicateKey(error);

    /// <summary>Renders a bound parameter reference.</summary>
    /// <param name="name">The parameter name, without a prefix.</param>
    /// <returns>The engine's parameter token.</returns>
    internal abstract string Parameter(string name);

    /// <summary>Forces byte-exact comparison of a character column where the engine's default is not.</summary>
    /// <param name="column">The column or qualified column reference.</param>
    /// <returns>The column, collated if the engine needs it.</returns>
    internal abstract string Collate(string column);

    /// <summary>Extracts a scalar from the stored JSON document.</summary>
    /// <param name="property">An already-validated simple JSON property name.</param>
    /// <param name="numeric">Whether the caller needs the scalar typed as a number.</param>
    /// <returns>The extraction expression.</returns>
    internal abstract string JsonValue(string property, bool numeric);

    /// <summary>Renders the row-limiting clause.</summary>
    /// <param name="skip">Rows to skip; never negative.</param>
    /// <param name="take">Rows to return, or <see langword="null"/> for all remaining.</param>
    /// <returns>The clause, including its leading space.</returns>
    internal abstract string Page(int skip, int? take);

    /// <summary>
    /// Renders an upsert that decides match-or-insert ATOMICALLY, or <see langword="null"/> when the
    /// engine cannot.
    /// </summary>
    /// <param name="table">The qualified destination table.</param>
    /// <param name="columns">The insert column list.</param>
    /// <param name="values">The insert value list.</param>
    /// <param name="assignments">The update assignment list.</param>
    /// <param name="guard">The predicate deciding which existing row may be overwritten.</param>
    /// <returns>One statement, or <see langword="null"/> when the caller must sequence it itself.</returns>
    /// <remarks>
    /// <b>The null is the point, and it is why this is not a boolean.</b> "This engine has no atomic
    /// match-or-insert" is a property of the engine that the caller must branch on, and returning no
    /// statement says it in a way the caller cannot forget to ask about -- where a capability flag can be
    /// read, ignored, and still compile.
    /// </remarks>
    internal abstract string? AtomicUpsert(string table, string columns, string values, string assignments, string guard);

    /// <summary>Renders an insert that writes nothing when the row already exists.</summary>
    /// <param name="table">The qualified destination table.</param>
    /// <param name="columns">The insert column list.</param>
    /// <param name="values">The insert value list.</param>
    /// <returns>The statement.</returns>
    /// <remarks>
    /// On an engine without an atomic match-or-insert this is best-effort: it may still raise a
    /// duplicate-key violation, which the caller reads through <see cref="IsDuplicateKey"/> as the
    /// zero-rows-affected it means.
    /// </remarks>
    internal abstract string InsertIfAbsent(string table, string columns, string values);

    /// <summary>Renders a bound parameter for a filter comparison against an extracted JSON scalar.</summary>
    /// <param name="name">The parameter name, without a prefix.</param>
    /// <param name="value">The value being bound.</param>
    /// <returns>The parameter token, coerced if the engine's binding needs it.</returns>
    internal virtual string FilterParameter(string name, object? value) => Parameter(name);

    private sealed class OracleProjectionDialect(Func<DbException, bool> isDuplicateKey)
        : RelationalProjectionDialect(isDuplicateKey)
    {
        internal override bool TypedJsonNumbers => true;

        internal override string Parameter(string name) => ":" + name;

        // Oracle's default comparison follows the session's NLS settings; the stores address rows by
        // exact bytes, so the comparison is pinned rather than inherited.
        internal override string Collate(string column) => column + " COLLATE BINARY";

        internal override string JsonValue(string property, bool numeric) =>
            $"JSON_VALUE(Data, '$.{property}'{(numeric ? " RETURNING NUMBER" : string.Empty)})";

        internal override string Page(int skip, int? take)
        {
            var clause = $" OFFSET {skip.ToString(CultureInfo.InvariantCulture)} ROWS";
            return take is { } limit
                ? clause + $" FETCH NEXT {limit.ToString(CultureInfo.InvariantCulture)} ROWS ONLY"
                : clause;
        }

        /// <inheritdoc />
        /// <remarks>
        /// NONE, and this is the finding that produced this type. A MERGE evaluates its NOT MATCHED arm
        /// against the state its own session can see, so two sessions racing a key that does not exist
        /// yet both take the insert arm; SQL Server buys the atomic decision with UPDLOCK/HOLDLOCK on the
        /// probe and Oracle offers no equivalent hint. Its only atomic insert-if-absent primitive is the
        /// unique index, which answers by raising, so the caller sequences the decision itself.
        /// </remarks>
        internal override string? AtomicUpsert(string table, string columns, string values, string assignments, string guard) => null;

        internal override string InsertIfAbsent(string table, string columns, string values) =>
            $"MERGE INTO {table} target USING (SELECT {Parameter("Tenant")} TenantId, {Parameter("Id")} Id FROM DUAL) incoming "
            + $"ON ({Collate("target.TenantId")} = incoming.TenantId AND {Collate("target.Id")} = incoming.Id) "
            + $"WHEN NOT MATCHED THEN INSERT ({columns}) VALUES ({values})";
    }

    private sealed class SqliteProjectionDialect(Func<DbException, bool> isDuplicateKey)
        : RelationalProjectionDialect(isDuplicateKey)
    {
        // SQLite extracts native JSON scalars, so an explicit numeric return type is never needed.
        internal override bool TypedJsonNumbers => false;

        internal override string Parameter(string name) => "@" + name;

        internal override string Collate(string column) => column;

        internal override string JsonValue(string property, bool numeric) => $"json_extract(Data, '$.{property}')";

        internal override string Page(int skip, int? take) =>
            $" LIMIT {(take ?? -1).ToString(CultureInfo.InvariantCulture)} OFFSET {skip.ToString(CultureInfo.InvariantCulture)}";

        /// <inheritdoc />
        /// <remarks>
        /// ON CONFLICT decides match-or-insert inside one index operation, so a concurrent writer
        /// serialises against it and the guard still governs the row it finds.
        /// </remarks>
        internal override string AtomicUpsert(string table, string columns, string values, string assignments, string guard) =>
            $"INSERT INTO {table} AS target ({columns}) VALUES ({values}) ON CONFLICT (TenantId, Id) DO UPDATE SET {assignments} WHERE {guard}";

        internal override string InsertIfAbsent(string table, string columns, string values) =>
            $"INSERT INTO {table} ({columns}) VALUES ({values}) ON CONFLICT (TenantId, Id) DO NOTHING";

        // Microsoft.Data.Sqlite binds decimal as TEXT; JSON extraction returns INTEGER/REAL.
        // Match SQLite's existing JSON numeric semantics without coercing string-valued filters.
        internal override string FilterParameter(string name, object? value) =>
            value is decimal ? $"CAST({Parameter(name)} AS NUMERIC)" : Parameter(name);
    }
}
