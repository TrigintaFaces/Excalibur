// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch.Configuration;
using Excalibur.Dispatch.ErrorHandling;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace OutboxPattern;

/// <summary>Composes the sample host using the framework inbox and outbox infrastructure.</summary>
public static class OutboxSample
{
    /// <summary>Creates the sample builder so its real composition can also be exercised independently.</summary>
    public static HostApplicationBuilder CreateBuilder(string[]? args = null)
    {
        var builder = new HostApplicationBuilder(args);
        
        // Add appsettings.json configuration
        builder.Configuration.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);
        
        // Configure logging for visibility
        builder.Services.AddLogging(logging =>
        {
        	_ = logging.AddConsole();
        	_ = logging.SetMinimumLevel(LogLevel.Information);
        });
        
        // ============================================================
        // Configure Dispatch with Outbox + Inbox middleware pipeline
        // ============================================================
        // The middleware pipeline approach is the recommended way to configure
        // outbox and inbox. Middleware is registered in the dispatch pipeline
        // and automatically intercepts messages.
        builder.Services.AddDispatch(dispatch =>
        {
        	// Add inbox middleware first -- deduplicates before processing
        	dispatch.UseInbox();
        
        	// Add outbox middleware -- stages integration events for reliable delivery
        	dispatch.UseOutbox();
        
        	// Register handlers from this assembly
        	dispatch.AddHandlersFromAssembly(typeof(OutboxSample).Assembly);
        });
        
        // ============================================================
        // Configure Outbox infrastructure (store + background processing)
        // ============================================================
        // The outbox builder configures the storage provider and processing behavior.
        // The UseOutbox() middleware above handles pipeline integration;
        // IExcaliburBuilder.AddOutbox(...) configures the backend infrastructure
        // as the canonical public path per (the legacy
        // AddExcaliburOutbox aggregator is internal to Excalibur.Outbox).
        builder.Services.AddExcalibur(excalibur => excalibur.AddOutbox(outbox =>
        {
        	outbox.UseInMemory() // In-memory for demo; use UseSqlServer() in production
        		.WithProcessing(processing =>
        		{
        			processing.BatchSize(50)              // Process 50 messages per batch
        				.PollingInterval(TimeSpan.FromSeconds(2))  // Check for messages every 2 seconds
        				.MaxRetryCount(3)                  // Retry failed messages up to 3 times
        				.RetryDelay(TimeSpan.FromSeconds(10));      // Requested delay; provider failure floor can be longer
        		})
        		.EnableBackgroundProcessing(); // Start the background processor hosted service
        }));
        
        // The background processor requires an EXPLICIT dead-letter decision. There is no default, so
        // that no deployment can silently discard a message it failed to deliver. Note WHERE this one
        // surfaces: the drain loop resolves its processor per iteration, so without this line the host
        // starts cleanly and throws on the FIRST DRAIN rather than at startup.
        //
        // This sample discards, because UseInMemory() above gives it nowhere durable to dead-letter to.
        // A real outbox deployment should NOT copy that choice -- it is the one place the pattern most
        // needs a durable queue, since MaxRetryCount(3) above is exactly what decides when a message
        // becomes someone's problem to inspect:
        //   services.AddSqlServerDeadLetterQueue(o => o.ConnectionString = ...);
        builder.Services.AddSingleton<IDeadLetterQueue>(NullDeadLetterQueue.Instance);

        // ============================================================
        // Configure Inbox infrastructure (store)
        // ============================================================
        builder.Services.AddExcaliburInbox(inbox =>
        {
        	inbox.UseInMemory(); // In-memory for demo; use UseSqlServer() in production
        });
        
        // ============================================================
        // Build and start the host
        // ============================================================
        
        return builder;
    }
}
