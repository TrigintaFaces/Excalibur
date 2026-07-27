// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Reflection;

using Excalibur.Dispatch;

namespace Excalibur.Outbox.Tests;

/// <summary>
/// Structural drift guard (bd-pihoca) pinning the staged-outbound-message write DTO
/// (<see cref="OutboundMessage"/>) against the read/query projection (<see cref="OutboxMessageRow"/>)
/// so a field added to one but not the other becomes a compile-visible test failure rather than silent
/// write/read drift.
/// </summary>
/// <remarks>
/// The concept was fragmented across multiple types (write DTO, read row, a parallel record, a mapper);
/// the write DTO and read row already diverged on <c>TransportDeliveries</c>. Rather than rely on review
/// vigilance, this reflection contract makes the drift <em>inexpressible</em>
/// (enforce-invariants-structurally): the only fields allowed to exist on the write DTO without a read-row
/// counterpart are the documented, intentionally-not-row-persisted ones on <see cref="WriteOnlyAllowlist"/>.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class OutboxWriteReadFieldParityShould
{
    /// <summary>
    /// Write-DTO properties intentionally NOT persisted 1:1 as a column on the read row.
    /// </summary>
    private static readonly HashSet<string> WriteOnlyAllowlist = new(StringComparer.Ordinal)
    {
        // Multi-transport fan-out is persisted in a SEPARATE per-transport delivery table/row, not a
        // column on the main outbox row — so the collection has no single OutboxMessageRow counterpart
        // (the divergence bd-pihoca documents). Adding it to the allowlist keeps the guard honest while
        // recording the deliberate exception.
        nameof(OutboundMessage.TransportDeliveries),
    };

    private static HashSet<string> PublicInstancePropertyNames(Type type) =>
        type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => p.Name)
            .ToHashSet(StringComparer.Ordinal);

    [Fact]
    public void CarryEveryWriteDtoFieldToTheReadRow_ExceptDocumentedWriteOnlyFields()
    {
        var writeFields = PublicInstancePropertyNames(typeof(OutboundMessage));
        var readFields = PublicInstancePropertyNames(typeof(OutboxMessageRow));

        var drift = writeFields.Except(readFields).Except(WriteOnlyAllowlist).OrderBy(n => n, StringComparer.Ordinal).ToList();

        drift.ShouldBeEmpty(
            $"OutboundMessage (write DTO) has field(s) with no OutboxMessageRow (read row) counterpart — " +
            $"write→read drift: [{string.Join(", ", drift)}]. Add the matching column to OutboxMessageRow " +
            $"(+ the INSERT/SELECT + mapper), or, if the field is deliberately not row-persisted, add it to " +
            $"WriteOnlyAllowlist with a justification.");
    }

    [Fact]
    public void NotHaveReadRowFieldsMissingFromTheWriteDto()
    {
        var writeFields = PublicInstancePropertyNames(typeof(OutboundMessage));
        var readFields = PublicInstancePropertyNames(typeof(OutboxMessageRow));

        var orphan = readFields.Except(writeFields).OrderBy(n => n, StringComparer.Ordinal).ToList();

        orphan.ShouldBeEmpty(
            $"OutboxMessageRow (read row) has field(s) with no OutboundMessage (write DTO) source — " +
            $"read→write drift (data read back that the write path never sets): [{string.Join(", ", orphan)}].");
    }

    [Fact]
    public void KeepTheWriteOnlyAllowlistHonest_EveryEntryStillExistsOnTheWriteDto()
    {
        var writeFields = PublicInstancePropertyNames(typeof(OutboundMessage));

        foreach (var allowlisted in WriteOnlyAllowlist)
        {
            writeFields.ShouldContain(
                allowlisted,
                $"WriteOnlyAllowlist names '{allowlisted}', which no longer exists on OutboundMessage — " +
                $"remove it from the allowlist so the guard cannot pass vacuously.");
        }
    }
}
