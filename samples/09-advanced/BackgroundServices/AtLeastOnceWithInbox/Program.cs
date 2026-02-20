// AtLeastOnceWithInbox Example
// Demonstrates outbox with inbox deduplication for at-least-once delivery.

using Excalibur.Data.SqlServer.Inbox;
using Excalibur.Dispatch.Options.Delivery;
using Excalibur.Outbox.SqlServer;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

// Configure services with AtLeastOnce guarantee (default)
var builder = Host.CreateApplicationBuilder(args);

builder.Services.Configure<SqlServerOutboxOptions>(options =>
{
	options.ConnectionString = builder.Configuration["ConnectionStrings:Outbox"]
		?? "Server=localhost;Database=OutboxDemo;Integrated Security=true;TrustServerCertificate=true";
	options.SchemaName = "dbo";
	options.OutboxTableName = "OutboxMessages";
});

builder.Services.Configure<SqlServerInboxOptions>(options =>
{
	options.ConnectionString = builder.Configuration["ConnectionStrings:Inbox"]
		?? "Server=localhost;Database=OutboxDemo;Integrated Security=true;TrustServerCertificate=true";
	options.SchemaName = "dbo";
	options.TableName = "InboxMessages";
});

builder.Services.Configure<OutboxOptions>(options =>
{
	// AtLeastOnce is the default - highest throughput, batch completion
	// Messages are marked sent after the entire batch is published
	options.DeliveryGuarantee = OutboxDeliveryGuarantee.AtLeastOnce;
	options.ConsumerBatchSize = 100;
});

// Register inbox store for deduplication
builder.Services.AddSingleton(sp =>
{
	var options = sp.GetRequiredService<IOptions<SqlServerInboxOptions>>();
	var logger = sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<SqlServerInboxStore>>();
	return new SqlServerInboxStore(options, logger);
});

var app = builder.Build();

Console.WriteLine("AtLeastOnce Delivery Guarantee Example");
Console.WriteLine("======================================");
Console.WriteLine();
Console.WriteLine("Configuration:");
Console.WriteLine("  - DeliveryGuarantee: AtLeastOnce (default)");
Console.WriteLine("  - Behavior: Batch completion after all messages published");
Console.WriteLine("  - Throughput: Highest (1 DB round-trip per batch)");
Console.WriteLine("  - Failure window: Entire batch on crash");
Console.WriteLine();
Console.WriteLine("Use inbox deduplication to handle potential duplicates:");
Console.WriteLine("  1. Check inbox before processing: IsProcessedAsync(messageId, handlerType)");
Console.WriteLine("  2. Mark as processed after success: MarkProcessedAsync(messageId, handlerType)");
Console.WriteLine();

await app.RunAsync();
