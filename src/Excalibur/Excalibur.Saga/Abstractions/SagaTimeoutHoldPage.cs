// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch;

namespace Excalibur.Saga.Abstractions;

/// <summary>A bounded page of detached held-source receipts.</summary>
public sealed class SagaTimeoutHoldPage
{
    private readonly SagaTimeoutHoldReceipt[] _items;
    /// <summary>Initializes a bounded detached held page.</summary>
    /// <param name="items">At most 256 actual held receipts.</param>
    /// <param name="nextCursor">The next exclusive provider cursor, or null for the end.</param>
    public SagaTimeoutHoldPage(IEnumerable<SagaTimeoutHoldReceipt> items, string? nextCursor)
    {
        ArgumentNullException.ThrowIfNull(items);
        _items = items.ToArray();
        if (_items.Length > 256 || _items.Any(static item => item is null || item.State != SagaTimeoutHoldState.Held))
        { throw new ArgumentException("A page contains at most 256 actual held receipts.", nameof(items)); }
        NextCursor = nextCursor;
    }
    /// <summary>Gets the read-only detached held receipts.</summary>
    public IReadOnlyList<SagaTimeoutHoldReceipt> Items => Array.AsReadOnly(_items);
    /// <summary>Gets the next exclusive cursor, or null for the end.</summary>
    public string? NextCursor { get; }
}
