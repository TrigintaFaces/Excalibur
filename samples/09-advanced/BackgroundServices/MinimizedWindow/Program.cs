// MinimizedWindow Example
// Demonstrates per-message completion to minimize the duplicate failure window.

using Excalibur.Dispatch.Options.Delivery;

using Excalibur.Outbox.SqlServer;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

// Configure services with MinimizedWindow guarantee
var builder = Host.CreateApplicationBuilder(args);

builder.Services.Configure<SqlServerOutboxOptions>(options =>
{
	options.ConnectionString = builder.Configuration["ConnectionStrings:Outbox"]
		?? "Server=localhost;Database=OutboxDemo;Integrated Security=true;TrustServerCertificate=true";
	options.SchemaName = "dbo";
	options.OutboxTableName = "OutboxMessages";
});

builder.Services.Configure<OutboxOptions>(options =>
{
	// MinimizedWindow: Each message is marked sent immediately after publishing
	// Lower throughput but smaller failure window (single message vs entire batch)
	options.DeliveryGuarantee = OutboxDeliveryGuarantee.MinimizedWindow;
	options.ConsumerBatchSize = 50;
});

var app = builder.Build();

Console.WriteLine("MinimizedWindow Delivery Guarantee Example");
Console.WriteLine("==========================================");
Console.WriteLine();
Console.WriteLine("Configuration:");
Console.WriteLine("  - DeliveryGuarantee: MinimizedWindow");
Console.WriteLine("  - Behavior: Per-message completion (mark sent immediately after publish)");
Console.WriteLine("  - Throughput: ~50% lower than AtLeastOnce (N DB round-trips per batch)");
Console.WriteLine("  - Failure window: Single message (not entire batch)");
Console.WriteLine();
Console.WriteLine("Trade-off:");
Console.WriteLine("  - More DB operations = lower throughput");
Console.WriteLine("  - Smaller failure window = fewer duplicates on crash");
Console.WriteLine();
Console.WriteLine("Best for:");
Console.WriteLine("  - Financial transactions");
Console.WriteLine("  - Audit logging");
Console.WriteLine("  - Scenarios where duplicate impact is significant");
Console.WriteLine();

await app.RunAsync();
