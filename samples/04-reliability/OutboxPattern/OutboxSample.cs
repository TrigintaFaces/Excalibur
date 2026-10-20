// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch.Configuration;
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
