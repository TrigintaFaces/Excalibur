// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0
using System.Buffers;
using System.Text.Json;
using Excalibur.EventSourcing;

namespace Excalibur.Data.ElasticSearch.IndexManagement;

/// <summary>Conditionally repoints an alias while preserving its reader configuration.</summary>
public sealed class AliasGenerationRepoint : IProjectionGenerationRepoint
{
    private const string Prefix = "elasticsearch/";
    private readonly IIndexAliasManager _aliases;
    private readonly string _aliasName;

    /// <summary>Creates a repointer for one reader alias.</summary>
    /// <param name="aliases">The manager addressing the projection cluster.</param>
    /// <param name="aliasName">The exact reader alias.</param>
    public AliasGenerationRepoint(IIndexAliasManager aliases, string aliasName)
    {
        ArgumentNullException.ThrowIfNull(aliases);
        ArgumentException.ThrowIfNullOrWhiteSpace(aliasName);
        _aliases = aliases;
        _aliasName = aliasName;
    }

    /// <inheritdoc />
    public async Task RepointAsync(string expectedDestination, string destination, CancellationToken cancellationToken)
    {
        var expected = IndexName(expectedDestination);
        var next = IndexName(destination);
        if (string.Equals(expected, next, StringComparison.Ordinal))
        {
            throw new ArgumentException("The expected and new destinations must differ.", nameof(destination));
        }
        var bound = await ReadSingleAliasAsync(cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("A generation cutover requires an existing alias with exactly one target.");
        var current = bound.Indices.Single();
        if (string.Equals(current, next, StringComparison.Ordinal)) { return; }
        if (!string.Equals(current, expected, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The alias no longer addresses the expected generation.");
        }
        AliasOperation[] operations =
        [
            new() { OperationType = AliasOperationType.Remove, AliasName = _aliasName, IndexName = expected, MustExist = true },
            new() { OperationType = AliasOperationType.Add, AliasName = _aliasName, IndexName = next, ConfigurationJson = Configuration(bound) },
        ];
        if (!await _aliases.UpdateAliasesAsync(operations, cancellationToken).ConfigureAwait(false))
        {
            throw new InvalidOperationException("The conditional alias update was not acknowledged. Read authoritative state before retrying.");
        }
    }

    /// <inheritdoc />
    public async Task<string?> GetActiveDestinationAsync(CancellationToken cancellationToken)
    {
        var bound = await ReadSingleAliasAsync(cancellationToken).ConfigureAwait(false);
        return bound is null ? null : Prefix + bound.Indices.Single();
    }

    private async Task<AliasDefinition?> ReadSingleAliasAsync(CancellationToken cancellationToken)
    {
        var definitions = await _aliases.GetAliasesAsync(_aliasName, cancellationToken).ConfigureAwait(false);
        var exact = definitions.Where(d => string.Equals(d.AliasName, _aliasName, StringComparison.Ordinal)).ToArray();
        return exact.Length == 1 && exact[0].Indices.Count() == 1 ? exact[0] : null;
    }

    private static string IndexName(string identity)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(identity);
        if (!identity.StartsWith(Prefix, StringComparison.Ordinal) || identity.Length == Prefix.Length)
        {
            throw new ArgumentException("The destination identity belongs to another provider or contains no index.", nameof(identity));
        }
        var index = identity[Prefix.Length..];
        if (index.IndexOfAny(['*', '?', ',', '/', ' ']) >= 0)
        {
            throw new ArgumentException("A generation destination must identify one concrete index.", nameof(identity));
        }
        return index;
    }

    private static JsonElement Configuration(AliasDefinition definition)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            if (definition.FilterJson is { } filter) { writer.WritePropertyName("filter"); filter.WriteTo(writer); }
            if (definition.IndexRouting is { } indexRouting) { writer.WriteString("index_routing", indexRouting); }
            if (definition.SearchRouting is { } searchRouting) { writer.WriteString("search_routing", searchRouting); }
            if (definition.IsWriteIndex is { } writeIndex) { writer.WriteBoolean("is_write_index", writeIndex); }
            if (definition.IsHidden is { } hidden) { writer.WriteBoolean("is_hidden", hidden); }
            writer.WriteEndObject();
        }
        using var document = JsonDocument.Parse(buffer.WrittenMemory);
        return document.RootElement.Clone();
    }
}
