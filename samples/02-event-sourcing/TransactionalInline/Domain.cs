// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch;
using Excalibur.Domain.Model;

namespace Excalibur.Examples.TransactionalInline;

[MessageName("Sample.TransactionalInline.AmountAdded")]
public sealed record AmountAdded(string OrderId, decimal Amount) : DomainEvent;

public sealed class Order : AggregateRoot
{
    public decimal Total { get; private set; }

    public void Add(string id, decimal amount)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        if (!string.IsNullOrEmpty(Id) && !string.Equals(Id, id, StringComparison.Ordinal))
        { throw new InvalidOperationException("An existing order cannot change its identifier."); }
        RaiseEvent(new AmountAdded(id, amount) { Metadata = new Dictionary<string, object>() });
    }

    protected override bool ApplyEventInternal(IDomainEvent @event)
    {
        if (@event is not AmountAdded added) { return false; }
        Id = added.OrderId;
        Total += added.Amount;
        return true;
    }
}

public sealed class OrderSummary
{
    public decimal Total { get; set; }
}
