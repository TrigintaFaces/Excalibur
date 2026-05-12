// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Domain.Model;

using Tests.Shared.Categories;

namespace Excalibur.Domain.Tests.Model;

/// <summary>
/// Tests for <see cref="Snapshot.Data"/> as <see cref="ReadOnlyMemory{T}"/> (Sprint 820 change).
/// Validates the byte[]→ReadOnlyMemory&lt;byte&gt; migration across construction,
/// edge cases, and interop patterns.
/// </summary>
[Trait("Category", "Unit")]
[Trait(TraitNames.Component, TestComponents.Core)]
[Trait(TraitNames.Feature, "Domain")]
public sealed class SnapshotReadOnlyMemoryShould
{
    private static Snapshot CreateSnapshot(byte[] data) => Snapshot.Create(
        aggregateId: Guid.NewGuid().ToString(),
        version: 1,
        data: data,
        aggregateType: "TestAggregate");

    private static Snapshot CreateSnapshotFromRom(ReadOnlyMemory<byte> rom) => new()
    {
        SnapshotId = Guid.NewGuid().ToString(),
        AggregateId = Guid.NewGuid().ToString(),
        Version = 1,
        CreatedAt = DateTimeOffset.UtcNow,
        Data = rom,
        AggregateType = "TestAggregate"
    };

    // ========================================
    // Happy Path — ReadOnlyMemory<byte> Construction
    // ========================================

    [Fact]
    public void AcceptByteArray_ViaImplicitConversion()
    {
        // Arrange
        var bytes = new byte[] { 0x01, 0x02, 0x03, 0x04 };

        // Act
        var snapshot = CreateSnapshot(bytes);

        // Assert
        snapshot.Data.Length.ShouldBe(4);
        snapshot.Data.Span[0].ShouldBe((byte)0x01);
        snapshot.Data.Span[3].ShouldBe((byte)0x04);
    }

    [Fact]
    public void AcceptReadOnlyMemory_Directly()
    {
        // Arrange
        var bytes = new byte[] { 0xAA, 0xBB, 0xCC };
        ReadOnlyMemory<byte> rom = bytes.AsMemory();

        // Act
        var snapshot = CreateSnapshotFromRom(rom);

        // Assert
        snapshot.Data.Length.ShouldBe(3);
        snapshot.Data.ToArray().ShouldBe(bytes);
    }

    [Fact]
    public void RoundTrip_DataContent()
    {
        // Arrange
        var original = new byte[] { 0x10, 0x20, 0x30, 0x40, 0x50 };

        // Act
        var snapshot = CreateSnapshot(original);
        var retrieved = snapshot.Data.ToArray();

        // Assert
        retrieved.ShouldBe(original);
    }

    // ========================================
    // Edge Cases
    // ========================================

    [Fact]
    public void HandleEmptyByteArray()
    {
        // Arrange & Act
        var snapshot = CreateSnapshot(Array.Empty<byte>());

        // Assert
        snapshot.Data.Length.ShouldBe(0);
        snapshot.Data.IsEmpty.ShouldBeTrue();
        snapshot.Data.ToArray().ShouldBeEmpty();
    }

    [Fact]
    public void HandleDefaultReadOnlyMemory()
    {
        // Arrange — default(ReadOnlyMemory<byte>) is the struct default
        ReadOnlyMemory<byte> defaultRom = default;

        // Act
        var snapshot = CreateSnapshotFromRom(defaultRom);

        // Assert
        snapshot.Data.Length.ShouldBe(0);
        snapshot.Data.IsEmpty.ShouldBeTrue();
    }

    [Fact]
    public void HandleSlicedMemory()
    {
        // Arrange — ReadOnlyMemory<byte> from a larger buffer
        var largeBuffer = new byte[] { 0x00, 0x11, 0x22, 0x33, 0x44, 0x55 };
        ReadOnlyMemory<byte> slice = largeBuffer.AsMemory(2, 3); // [0x22, 0x33, 0x44]

        // Act
        var snapshot = CreateSnapshotFromRom(slice);

        // Assert
        snapshot.Data.Length.ShouldBe(3);
        snapshot.Data.ToArray().ShouldBe(new byte[] { 0x22, 0x33, 0x44 });
    }

    [Fact]
    public void HandleLargeData()
    {
        // Arrange — simulate a realistic snapshot payload
        var largeData = new byte[64 * 1024]; // 64KB
        Random.Shared.NextBytes(largeData);

        // Act
        var snapshot = CreateSnapshot(largeData);

        // Assert
        snapshot.Data.Length.ShouldBe(64 * 1024);
        snapshot.Data.ToArray().ShouldBe(largeData);
    }

    // ========================================
    // Interop — Span Access Patterns
    // ========================================

    [Fact]
    public void SupportSpanAccess()
    {
        // Arrange
        var bytes = new byte[] { 0xDE, 0xAD, 0xBE, 0xEF };
        var snapshot = CreateSnapshot(bytes);

        // Act
        var span = snapshot.Data.Span;

        // Assert
        span.Length.ShouldBe(4);
        span[0].ShouldBe((byte)0xDE);
        span[3].ShouldBe((byte)0xEF);
    }

    [Fact]
    public void SupportToArrayConversion()
    {
        // Arrange
        var bytes = new byte[] { 1, 2, 3 };
        var snapshot = CreateSnapshot(bytes);

        // Act
        var array = snapshot.Data.ToArray();

        // Assert — ToArray creates a copy
        array.ShouldBe(bytes);
        array.ShouldNotBeSameAs(bytes); // distinct array instance
    }

    // ========================================
    // Property Type Verification
    // ========================================

    [Fact]
    public void ExposeDataAsReadOnlyMemory_NotByteArray()
    {
        // Assert — the property type is ReadOnlyMemory<byte>, not byte[]
        var dataProperty = typeof(ISnapshot).GetProperty(nameof(ISnapshot.Data));
        dataProperty.ShouldNotBeNull();
        dataProperty!.PropertyType.ShouldBe(typeof(ReadOnlyMemory<byte>));
    }
}
