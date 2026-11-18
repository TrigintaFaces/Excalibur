// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Globalization;
using System.Text.Json;

using Excalibur.Dispatch.Aot.Sample.EventSourcing;
using Excalibur.Dispatch.Aot.Sample.Handlers;
using Excalibur.Dispatch.Aot.Sample.Messages;
using Excalibur.Dispatch.Options.Serialization;
using Excalibur.Dispatch.Transport;
using Excalibur.Dispatch.Aot.Sample.Serialization;
using Excalibur.EventSourcing;

namespace Excalibur.Dispatch.Aot.Sample;

internal static class DemoOutcomes
{
    internal static void Verify(int demo, bool succeeded)
    {
        var id = demo.ToString("D2", CultureInfo.InvariantCulture);
        if (!succeeded)
        {
            Console.WriteLine($"AOT_DEMO_FAIL:{id}");
            throw new InvalidOperationException($"AOT demo {id} did not satisfy its observable outcome.");
        }

        Console.WriteLine($"AOT_DEMO_PASS:{id}");
    }

    internal static void VerifyOrderHandlers(Guid orderId, CreateOrderCommand command)
    {
        var first = OrderCreatedHandler.LastReceived;
        var second = OrderAnalyticsHandler.LastReceived;
        Verify(2, first is not null && second is not null && first == second
            && first.OrderId == orderId && second.OrderId == orderId
            && first.CustomerId == command.CustomerId && second.CustomerId == command.CustomerId
            && first.TotalAmount == command.Items.Sum(item => item.Quantity * item.UnitPrice)
            && second.TotalAmount == first.TotalAmount);
    }

    internal static void VerifyQuery(bool succeeded, OrderDto? order, Guid orderId, CreateOrderCommand command)
    {
        Verify(3, succeeded && order is not null && order.Id == orderId
            && order.CustomerId == command.CustomerId && order.Status == "Created"
            && order.TotalAmount == command.Items.Sum(item => item.Quantity * item.UnitPrice)
            && order.Items.SequenceEqual(command.Items));
    }

    internal static void VerifyRoundTrip(CreateOrderCommand original, CreateOrderCommand? actual)
    {
        Verify(5, actual is not null && actual.CustomerId == original.CustomerId
            && actual.Items.SequenceEqual(original.Items) && DispatchJsonSerializerOptions.Web.WriteIndented);
    }

    internal static void VerifyTransportRegistration(InMemoryTransportAdapter transport)
    {
        Verify(6, transport.Name == "demo" && transport.TransportType == "inmemory" && !transport.IsRunning);
    }

    internal static void VerifyAccount(BankAccountAggregate account)
    {
        Verify(7, account.Id == "ACC-001" && account.HolderName == "Alice Johnson"
            && account.Balance == 1300m && account.IsOpen && account.GetUncommittedEvents().Count == 3);
    }

    internal static void VerifyReconstruction(BankAccountAggregate original, BankAccountAggregate reconstructed)
    {
        Verify(9, reconstructed.Id == original.Id && reconstructed.HolderName == original.HolderName
            && reconstructed.Balance == original.Balance && reconstructed.IsOpen
            && reconstructed.GetUncommittedEvents().Count == 0);
    }

    internal static void VerifyInventory(InventoryUpdatedEvent expected, InventoryUpdatedEvent? roundTripped,
        InMemoryTransportAdapter transport, TransportHealthCheckResult health)
    {
        Verify(10, roundTripped == expected && InventoryUpdatedHandler.LastReceived == expected
            && transport.SentMessages.Values.Any(item => item is InventoryUpdatedEvent value && value == expected)
            && health.Status == TransportHealthStatus.Healthy && !transport.IsRunning);
    }

    internal static void VerifyStoredEvents(bool appended, IReadOnlyList<StoredEvent> stored, string accountId)
    {
        if (!appended || stored.Count != 3 || stored.Any(static item => item.EventData is null))
        {
            Verify(8, false);
        }

        var opened = JsonSerializer.Deserialize(stored[0].EventData!, AppJsonSerializerContext.Default.AccountOpenedEvent);
        var deposited = JsonSerializer.Deserialize(stored[1].EventData!, AppJsonSerializerContext.Default.FundsDepositedEvent);
        var withdrawn = JsonSerializer.Deserialize(stored[2].EventData!, AppJsonSerializerContext.Default.FundsWithdrawnEvent);
        Verify(8, opened is not null && deposited is not null && withdrawn is not null
            && stored.All(item => item.AggregateId == accountId)
            && stored[0].EventType == "Contoso.Banking.AccountOpenedEvent"
            && stored[1].EventType == "Contoso.Banking.FundsDepositedEvent"
            && stored[2].EventType == "Contoso.Banking.FundsWithdrawnEvent"
            && opened.AccountId == accountId && opened.HolderName == "Alice Johnson" && opened.InitialDeposit == 1000m
            && deposited.AccountId == accountId && deposited.Amount == 500m
            && withdrawn.AccountId == accountId && withdrawn.Amount == 200m);
    }

    internal static void VerifyReport(byte[] data, string contentType, Guid reportId, string title)
    {
        if (data.Length == 0 || contentType != "application/json")
        {
            Verify(12, false);
        }

        using var json = JsonDocument.Parse(data);
        Verify(12, json.RootElement.GetProperty("reportId").GetGuid() == reportId
            && json.RootElement.GetProperty("title").GetString() == title);
    }
}
