// TransactionalWhenApplicable Example
// Demonstrates exactly-once delivery when outbox and inbox share the same database.

using Excalibur.Data.SqlServer.Inbox;
using Excalibur.Dispatch.Options.Delivery;
using Excalibur.Outbox.SqlServer;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

// Configure services with TransactionalWhenApplicable guarantee
var builder = Host.CreateApplicationBuilder(args);

// IMPORTANT: Same connection string for both outbox and inbox
// This enables atomic transactional completion
var connectionString = builder.Configuration["ConnectionStrings:Database"]
	?? "Server=localhost;Database=OutboxDemo;Integrated Security=true;TrustServerCertificate=true";

builder.Services.Configure<SqlServerOutboxOptions>(options =>
{
	options.ConnectionString = connectionString;
	options.SchemaName = "dbo";
	options.OutboxTableName = "OutboxMessages";
});

builder.Services.Configure<SqlServerInboxOptions>(options =>
{
	options.ConnectionString = connectionString; // MUST match outbox connection string
	options.SchemaName = "dbo";
	options.TableName = "InboxMessages";
});

builder.Services.Configure<OutboxOptions>(options =>
{
	// TransactionalWhenApplicable: Uses atomic transaction when same database detected
	// Falls back to MinimizedWindow when not applicable
	options.DeliveryGuarantee = OutboxDeliveryGuarantee.TransactionalWhenApplicable;
	options.ConsumerBatchSize = 50;
});

// Register SqlServerOutboxStore with inbox options for transactional completion
builder.Services.AddSingleton(sp =>
{
	var outboxOptions = sp.GetRequiredService<IOptions<SqlServerOutboxOptions>>();
	var inboxOptions = sp.GetRequiredService<IOptions<SqlServerInboxOptions>>();
	var logger = sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<SqlServerOutboxStore>>();
	return new SqlServerOutboxStore(outboxOptions, inboxOptions, logger);
});

var app = builder.Build();

Console.WriteLine("TransactionalWhenApplicable Delivery Guarantee Example");
Console.WriteLine("======================================================");
Console.WriteLine();
Console.WriteLine("Configuration:");
Console.WriteLine("  - DeliveryGuarantee: TransactionalWhenApplicable");
Console.WriteLine("  - Behavior: Atomic outbox+inbox completion in single transaction");
Console.WriteLine("  - Result: Exactly-once delivery (zero duplicates)");
Console.WriteLine();
Console.WriteLine("Requirements:");
Console.WriteLine("  - SQL Server outbox and inbox stores");
Console.WriteLine("  - Same connection string for both (same database)");
Console.WriteLine("  - SqlServerInboxOptions provided to SqlServerOutboxStore constructor");
Console.WriteLine();
Console.WriteLine("Fallback:");
Console.WriteLine("  - Falls back to MinimizedWindow when:");
Console.WriteLine("    * Different databases (connection strings don't match)");
Console.WriteLine("    * Inbox options not configured");
Console.WriteLine("    * Non-SQL Server stores");
Console.WriteLine();

await app.RunAsync();
