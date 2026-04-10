// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text;
using System.Text.Json;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Inbox.ElasticSearch;
using Excalibur.Outbox.ElasticSearch;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

// ============================================================================
// ElasticSearch Inbox/Outbox Sample
// ============================================================================
//
// Demonstrates:
//   1. Inbox pattern  -- Idempotent at-least-once message processing
//   2. Outbox pattern -- Reliable exactly-once message publishing
//
// Both patterns use Elasticsearch as the backing store.
//
// Prerequisites:
//   - Elasticsearch running on http://localhost:9200
//   - docker run -d --name es -p 9200:9200 -e "discovery.type=single-node" \
//       -e "xpack.security.enabled=false" elasticsearch:8.15.0
//
// ============================================================================

var builder = Host.CreateApplicationBuilder(args);

// ── Register Elasticsearch services ─────────────────────────────────────────
builder.Services.AddElasticsearchServices(builder.Configuration, registry: null);

// ── Register Inbox with Elasticsearch provider ──────────────────────────────
builder.Services.AddExcaliburInbox(inbox =>
{
    inbox.UseElasticSearch(options =>
    {
        options.IndexName = "sample-inbox";
        options.RefreshPolicy = "wait_for";
        options.RetentionDays = 7;
    });
});

// ── Register Outbox with Elasticsearch provider ─────────────────────────────
builder.Services.AddExcaliburOutbox(outbox =>
{
    outbox.UseElasticSearch(options =>
    {
        options.IndexName = "sample-outbox";
        options.DefaultBatchSize = 100;
        options.RefreshPolicy = "wait_for";
        options.SentMessageRetentionDays = 7;
    });
});

var app = builder.Build();

// ============================================================================
// INBOX DEMO: Idempotent Message Processing
// ============================================================================

Console.WriteLine("=== Inbox Demo: Idempotent Message Processing ===");
Console.WriteLine();

// Resolve the Elasticsearch inbox store (implements both IInboxStore and IInboxStoreAdmin)
var inboxStore = app.Services.GetRequiredService<ElasticsearchInboxStore>();

var messageId = Guid.NewGuid().ToString();
const string handlerType = "OrderCreatedHandler";
const string messageType = "OrderCreated";
var payload = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { OrderId = 42, Amount = 99.99 }));
var metadata = new Dictionary<string, object>(StringComparer.Ordinal)
{
    ["correlationId"] = Guid.NewGuid().ToString(),
    ["source"] = "order-service",
};

// Step 1: Create an inbox entry for the incoming message
Console.WriteLine($"1. Creating inbox entry for message '{messageId}'...");
var entry = await inboxStore.CreateEntryAsync(
    messageId, handlerType, messageType, payload, metadata, CancellationToken.None).ConfigureAwait(false);
Console.WriteLine($"   Created: Status={entry.Status}, ReceivedAt={entry.ReceivedAt:O}");

// Step 2: Check if the message has been processed
Console.WriteLine($"2. Checking if message is processed...");
var isProcessed = await inboxStore.IsProcessedAsync(messageId, handlerType, CancellationToken.None).ConfigureAwait(false);
Console.WriteLine($"   IsProcessed={isProcessed} (expected: false)");

// Step 3: Mark the message as processed after handling
Console.WriteLine($"3. Marking message as processed...");
await inboxStore.MarkProcessedAsync(messageId, handlerType, CancellationToken.None).ConfigureAwait(false);
Console.WriteLine("   Marked as processed.");

// Step 4: Verify it is now processed
isProcessed = await inboxStore.IsProcessedAsync(messageId, handlerType, CancellationToken.None).ConfigureAwait(false);
Console.WriteLine($"4. IsProcessed={isProcessed} (expected: true)");

// Step 5: Demonstrate idempotency with TryMarkAsProcessed on a new message
var duplicateMessageId = Guid.NewGuid().ToString();
Console.WriteLine($"5. Demonstrating idempotent TryMarkAsProcessed...");

var firstAttempt = await inboxStore.TryMarkAsProcessedAsync(
    duplicateMessageId, handlerType, CancellationToken.None).ConfigureAwait(false);
Console.WriteLine($"   First attempt:  result={firstAttempt} (expected: true -- first time processing)");

var secondAttempt = await inboxStore.TryMarkAsProcessedAsync(
    duplicateMessageId, handlerType, CancellationToken.None).ConfigureAwait(false);
Console.WriteLine($"   Second attempt: result={secondAttempt} (expected: false -- duplicate detected)");

// Step 6: Get inbox statistics
Console.WriteLine("6. Retrieving inbox statistics...");
var inboxStats = await inboxStore.GetStatisticsAsync(CancellationToken.None).ConfigureAwait(false);
Console.WriteLine($"   Total={inboxStats.TotalEntries}, Processed={inboxStats.ProcessedEntries}, " +
                  $"Failed={inboxStats.FailedEntries}, Pending={inboxStats.PendingEntries}");

Console.WriteLine();

// ============================================================================
// OUTBOX DEMO: Reliable Message Publishing
// ============================================================================

Console.WriteLine("=== Outbox Demo: Reliable Message Publishing ===");
Console.WriteLine();

// Resolve the Elasticsearch outbox store (implements both IOutboxStore and IOutboxStoreAdmin)
var outboxStore = app.Services.GetRequiredService<ElasticsearchOutboxStore>();

// Step 1: Stage a message in the outbox
Console.WriteLine("1. Staging a message in the outbox...");
var outboundMessage = new OutboundMessage(
    messageType: "OrderShipped",
    payload: Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { OrderId = 42, TrackingNumber = "TRACK-123" })),
    destination: "shipping-notifications");
await outboxStore.StageMessageAsync(outboundMessage, CancellationToken.None).ConfigureAwait(false);
Console.WriteLine($"   Staged message '{outboundMessage.Id}' -> destination: {outboundMessage.Destination}");

// Step 2: Stage a second message
Console.WriteLine("2. Staging a second message...");
var secondMessage = new OutboundMessage(
    messageType: "InvoiceGenerated",
    payload: Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { OrderId = 42, InvoiceNumber = "INV-001" })),
    destination: "billing-events");
await outboxStore.StageMessageAsync(secondMessage, CancellationToken.None).ConfigureAwait(false);
Console.WriteLine($"   Staged message '{secondMessage.Id}' -> destination: {secondMessage.Destination}");

// Step 3: Retrieve unsent messages (simulating the background processor)
Console.WriteLine("3. Retrieving unsent messages (batch size=10)...");
var unsentMessages = await outboxStore.GetUnsentMessagesAsync(10, CancellationToken.None).ConfigureAwait(false);
var unsentList = unsentMessages.ToList();
Console.WriteLine($"   Found {unsentList.Count} unsent message(s).");
foreach (var msg in unsentList)
{
    Console.WriteLine($"   - {msg.Id}: {msg.MessageType} -> {msg.Destination} (Status={msg.Status})");
}

// Step 4: Mark the first message as sent (simulating successful publish)
Console.WriteLine($"4. Marking message '{outboundMessage.Id}' as sent...");
await outboxStore.MarkSentAsync(outboundMessage.Id, CancellationToken.None).ConfigureAwait(false);
Console.WriteLine("   Marked as sent.");

// Step 5: Get outbox statistics
Console.WriteLine("5. Retrieving outbox statistics...");
var outboxStats = await outboxStore.GetStatisticsAsync(CancellationToken.None).ConfigureAwait(false);
Console.WriteLine($"   {outboxStats}");

// Step 6: Clean up old sent messages
Console.WriteLine("6. Cleaning up sent messages older than 1 hour...");
var cleaned = await outboxStore.CleanupSentMessagesAsync(
    DateTimeOffset.UtcNow.AddHours(-1), batchSize: 100, CancellationToken.None).ConfigureAwait(false);
Console.WriteLine($"   Cleaned up {cleaned} message(s).");

Console.WriteLine();
Console.WriteLine("=== Sample Complete ===");
