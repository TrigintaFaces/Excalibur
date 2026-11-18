// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0
using Excalibur.Dispatch.Examples.ECommerceSample.Infrastructure;
using Excalibur.Outbox.SqlServer;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Excalibur.Dispatch.Examples.ECommerceSample;

// Explicit single-tenant, managed SQL order+intent profile. Inventory/scheduling are not certified by this mode.
internal static class SqlCommerceProfile
{
	public static async Task<int> RunAsync(string[] args)
	{
		var stageOnly = args.Contains("--stage-only", StringComparer.Ordinal);
		var drainOnly = args.Contains("--drain-only", StringComparer.Ordinal);
		if (stageOnly && drainOnly) { throw new ArgumentException("Select stage-only or drain-only, not both.", nameof(args)); }
		var connectionString = Environment.GetEnvironmentVariable("ECOMMERCE_SQL_CONNECTION");
		if (string.IsNullOrWhiteSpace(connectionString))
		{
			throw new InvalidOperationException("SQL orders require ECOMMERCE_SQL_CONNECTION and a provisioned shipping outbox schema.");
		}
		using var host = BuildHost(connectionString, stageOnly);
		var orders = host.Services.GetRequiredService<SqlOrderSubmissionService>(); // Capability refusal before execution.
		using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30));
		await orders.PrepareAsync(deadline.Token).ConfigureAwait(false);
		var order = DemoOrder();
		if (!drainOnly)
		{
			var result = await orders.ProcessAsync(order, deadline.Token).ConfigureAwait(false);
			if (result is not (OrderProcessingOutcome.Processed or OrderProcessingOutcome.Duplicate)) { return 1; }
			if (await orders.ProcessAsync(order with { Price = order.Price + 1 }, deadline.Token).ConfigureAwait(false)
				!= OrderProcessingOutcome.Conflict) { return 1; }
			if (await orders.ProcessAsync(order, deadline.Token).ConfigureAwait(false) != OrderProcessingOutcome.Duplicate) { return 1; }
		}
		var evidence = await orders.ReadAsync(order.OrderId, deadline.Token).ConfigureAwait(false);
		if (evidence is null || !evidence.IntentCommitted || evidence.Fingerprint != new OrderSubmissionPlan(order).Fingerprint) { return 1; }
		Console.WriteLine(drainOnly ? "Retained SQL order and committed intent evidence found; no submission was reissued."
			: "SQL order and intent committed; same-input duplicate and conflict checks passed.");
		if (stageOnly) { return 0; }
		var store = host.Services.GetRequiredService<IOutboxStore>();
		var admin = store.GetService(typeof(IOutboxStoreAdmin)) as IOutboxStoreAdmin
			?? throw new InvalidOperationException("The SQL sample needs operator outbox statistics.");
		var before = await admin.GetAllTenantsStatisticsAsync(deadline.Token).ConfigureAwait(false);
		await host.StartAsync(deadline.Token).ConfigureAwait(false);
		try
		{
			while (true)
			{
				var stats = await admin.GetAllTenantsStatisticsAsync(deadline.Token).ConfigureAwait(false);
				if (stats.StagedMessageCount + stats.SendingMessageCount + stats.FailedMessageCount + stats.ScheduledMessageCount == 0) { break; }
				await Task.Delay(TimeSpan.FromMilliseconds(100), deadline.Token).ConfigureAwait(false);
			}
			if (before.StagedMessageCount + before.SendingMessageCount + before.FailedMessageCount > 0
				&& host.Services.GetRequiredService<InMemoryEmailService>().GetSentEmailCount() == 0) { return 1; }
			Console.WriteLine("SQL outbox has no pending work. Intent commit is not proof of exactly-once external email delivery.");
			return 0;
		}
		finally
		{
			using var shutdown = new CancellationTokenSource(TimeSpan.FromSeconds(10));
			await host.StopAsync(shutdown.Token).ConfigureAwait(false);
		}
	}

	private static IHost BuildHost(string connectionString, bool stageOnly)
	{
		var builder = Host.CreateApplicationBuilder(Array.Empty<string>());
		Func<SqlConnection> connections = () => new SqlConnection(connectionString);
		builder.Services.AddSingleton<Func<SqlConnection>>(connections);
		builder.Services.AddExcaliburInbox(inbox => inbox.UseSqlServer(sql => sql.ConnectionString(connectionString)));
		builder.Services.AddExcalibur(excalibur => excalibur.AddOutbox(outbox =>
			outbox.UseSqlServer(sql => sql.ConnectionString(connectionString))));
		builder.Services.AddSingleton<SqlOrderSubmissionService>();
		builder.Services.AddSingleton<PerformanceMonitor>();
		builder.Services.AddSingleton<InMemoryEmailService>();
		builder.Services.AddSingleton<IEmailSender>(provider => provider.GetRequiredService<InMemoryEmailService>());
		if (!stageOnly) { builder.Services.AddHostedService<NotificationDrainService>(); }
		return builder.Build();
	}

	internal static OrderCreated DemoOrder() => new()
	{
		OrderId = "sql-commerce-demo", CustomerId = "customer@example.test", ProductId = "product",
		ProductName = "Product", Price = 10m, Quantity = 1,
		OrderDate = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
	};
}
