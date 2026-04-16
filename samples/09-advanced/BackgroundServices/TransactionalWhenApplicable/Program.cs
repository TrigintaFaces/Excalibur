// TransactionalWhenApplicable Example
// Demonstrates exactly-once delivery when outbox and inbox share the same database.

using Excalibur.Dispatch.Options.Delivery;
using Excalibur.Outbox.SqlServer;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

// Configure services with TransactionalWhenApplicable guarantee
var builder = Host.CreateApplicationBuilder(args);

// IMPORTANT: Same connection string for both outbox and inbox
// This enables atomic transactional completion
var connectionString = builder.Configuration["ConnectionStrings:Database"]
	?? "Server=localhost;Database=OutboxDemo;Integrated Security=true;TrustServerCertificate=true";

builder.Services.AddExcaliburOutbox(outbox =>
{
	outbox.UseSqlServer(sql =>
		sql.ConnectionString(connectionString)
		   .SchemaName("dbo")
		   .TableName("OutboxMessages"));
});

builder.Services.AddExcaliburInbox(inbox =>
{
	inbox.UseSqlServer(sql =>
		sql.ConnectionString(connectionString) // MUST match outbox connection string
		   .SchemaName("dbo")
		   .TableName("InboxMessages"));
});

builder.Services.Configure<OutboxDeliveryOptions>(options =>
{
	// TransactionalWhenApplicable: Uses atomic transaction when same database detected
	// Falls back to MinimizedWindow when not applicable
	options.DeliveryGuarantee = OutboxDeliveryGuarantee.TransactionalWhenApplicable;
	options.ConsumerBatchSize = 50;
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
Console.WriteLine();
Console.WriteLine("Fallback:");
Console.WriteLine("  - Falls back to MinimizedWindow when:");
Console.WriteLine("    * Different databases (connection strings don't match)");
Console.WriteLine("    * Inbox options not configured");
Console.WriteLine("    * Non-SQL Server stores");
Console.WriteLine();

await app.RunAsync();
