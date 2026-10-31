// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

namespace Excalibur.EventSourcing.Subscriptions;

/// <summary>How one relational engine spells the projection-generation routing statements.</summary>
/// <remarks>
/// <para>
/// <b>This replaced two independent boolean flags, and the pair was worse than either alone.</b> The
/// routing store took <c>snakeCase</c> and <c>oracle</c>, which between them encoded THREE dialects
/// across FOUR providers -- and the fourth combination, <c>snakeCase: true</c> with <c>oracle: true</c>,
/// was expressible, compiled, and meant nothing. Both also defaulted to <see langword="false"/>, so two
/// providers selected their dialect by saying nothing at all, which is the hardest kind of choice to
/// review: there is no argument at the call site to read.
/// </para>
/// <para>
/// Naming the engine makes the meaningless combination inexpressible rather than merely unused, and puts
/// each provider's choice in its own constructor call where a reviewer can see it.
/// </para>
/// <para>
/// SQL Server and SQLite share one dialect today and are still exposed under their own names, so a
/// future divergence is a one-line change at one call site rather than a new boolean threaded through
/// every statement.
/// </para>
/// </remarks>
internal abstract class ProjectionGenerationDialect
{
    /// <summary>Oracle: colon-prefixed binds, and comparisons pinned to exact bytes.</summary>
    internal static ProjectionGenerationDialect Oracle { get; } = new OracleDialect();

    /// <summary>PostgreSQL: snake-cased columns, because unquoted identifiers fold to lower case.</summary>
    internal static ProjectionGenerationDialect Postgres { get; } = new SnakeCaseDialect();

    /// <summary>SQL Server.</summary>
    internal static ProjectionGenerationDialect SqlServer { get; } = new PascalCaseDialect();

    /// <summary>SQLite.</summary>
    internal static ProjectionGenerationDialect Sqlite { get; } = new PascalCaseDialect();

    /// <summary>Renders a column name in the casing this engine's schema uses.</summary>
    /// <param name="name">The canonical PascalCase column name.</param>
    /// <returns>The column name as this engine's schema declares it.</returns>
    internal abstract string Column(string name);

    /// <summary>Renders a bound parameter reference.</summary>
    /// <param name="name">The parameter name, without a prefix.</param>
    /// <returns>The engine's parameter token.</returns>
    internal abstract string Parameter(string name);

    /// <summary>Forces byte-exact comparison where the engine's default collation is not.</summary>
    /// <param name="column">The column or qualified column reference.</param>
    /// <returns>The column, collated if the engine needs it.</returns>
    internal abstract string Collate(string column);

    private sealed class PascalCaseDialect : ProjectionGenerationDialect
    {
        internal override string Column(string name) => name;

        internal override string Parameter(string name) => "@" + name;

        internal override string Collate(string column) => column;
    }

    private sealed class SnakeCaseDialect : ProjectionGenerationDialect
    {
        // Unquoted in the store's SQL, so lower case: an unquoted identifier folds to lower case in
        // PostgreSQL, and a quoted mixed-case column would then be unreachable from those queries.
        internal override string Column(string name) => name switch
        {
            "SubscriptionName" => "subscription_name",
            "ProjectionKey" => "projection_key",
            "DefinitionHash" => "definition_hash",
            "BindingId" => "binding_id",
            "ReaderName" => "reader_name",
            "DestinationScope" => "destination_scope",
            "Destination" => "destination",
            "Position" => "position",
            _ => name,
        };

        internal override string Parameter(string name) => "@" + name;

        internal override string Collate(string column) => column;
    }

    private sealed class OracleDialect : ProjectionGenerationDialect
    {
        internal override string Column(string name) => name;

        internal override string Parameter(string name) => ":" + name;

        internal override string Collate(string column) => column + " COLLATE BINARY";
    }
}
