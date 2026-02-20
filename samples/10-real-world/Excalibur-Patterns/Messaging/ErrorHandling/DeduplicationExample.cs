// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
//
// Licensed under multiple licenses:
// - Excalibur License 1.0 (see LICENSE-EXCALIBUR.txt)
// - GNU Affero General Public License v3.0 or later (AGPL-3.0) (see LICENSE-AGPL-3.0.txt)
// - Server Side Public License v1.0 (SSPL-1.0) (see LICENSE-SSPL-1.0.txt)
// - Apache License 2.0 (see LICENSE-APACHE-2.0.txt)
//
// You may not use this file except in compliance with the License terms above. You may obtain copies of the licenses in the project root or online.
//
// Unless required by applicable law or agreed to in writing, software distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace examples.Excalibur.Core.Messaging.ErrorHandling;

/// <summary>
///     Example demonstrating various deduplication configurations.
/// </summary>
public static class DeduplicationExample
{
	/// <summary>
	///     Example 1: Basic in-memory deduplication with message ID strategy.
	/// </summary>
	public static IHostBuilder ConfigureBasicDeduplication(this IHostBuilder hostBuilder) =>
		hostBuilder; // TODO: Implement when AddDispatch is available

	/*
 return hostBuilder.ConfigureServices((context, services) =>
 {
 services.AddDispatch(builder =>
 {
 // Enable deduplication with default settings
 builder.AddDeduplication(options =>
 {
 options.Enabled = true;
 options.TimeWindow = TimeSpan.FromHours(24);
 options.Strategy = DeduplicationStrategyType.MessageId;
 });

 // Inbox is configured separately at the service level
 });

 // Configure inbox with deduplication
 services.AddInbox<InMemoryInboxStore>(inboxOptions =>
 {
 inboxOptions.MaxAttempts = 3;
 inboxOptions.Deduplication.Enabled = true;
 });
 });
 */

	/// <summary>
	///     Example 2: Redis-based deduplication with content hash strategy.
	/// </summary>
	public static IHostBuilder ConfigureRedisDeduplication(this IHostBuilder hostBuilder) =>
		hostBuilder; // TODO: Implement when AddDispatch and AddStackExchangeRedisCache are available

	/*
 return hostBuilder.ConfigureServices((context, services) =>
 {
 // Add Redis cache
 services.AddStackExchangeRedisCache(options =>
 {
 options.Configuration = context.Configuration["Redis:ConnectionString"];
 });

 services.AddDispatch(builder =>
 {
 // Configure deduplication with Redis store
 builder.AddDeduplication(options =>
 {
 options.Enabled = true;
 options.TimeWindow = TimeSpan.FromHours(48);
 options.Strategy = DeduplicationStrategyType.ContentHash;
 options.UseBatchOperations = true;
 })
 .UseDistributedCacheDeduplicationStore();
 });
 });
 */

	/// <summary>
	///     Example 3: SQL Server deduplication with composite strategy.
	/// </summary>
	public static IHostBuilder ConfigureSqlServerDeduplication(this IHostBuilder hostBuilder) =>
		hostBuilder; // TODO: Implement when AddDispatch is available

	/*
 return hostBuilder.ConfigureServices((context, services) =>
 {
 var connectionString = context.Configuration.GetConnectionString("SqlServer");

 services.AddDispatch(builder =>
 {
 // Configure deduplication with SQL Server store
 builder.AddDeduplication(options =>
 {
 options.Enabled = true;
 options.TimeWindow = TimeSpan.FromDays(7);
 options.Strategy = DeduplicationStrategyType.Composite;
 options.CleanupInterval = TimeSpan.FromHours(6);
 })
 .UseSqlDeduplicationStore(
 () => new SqlConnection(connectionString),
 "MessageDeduplication");
 });
 });
 */

	/// <summary>
	///     Example 4: High-throughput configuration with batch operations.
	/// </summary>
	public static IHostBuilder ConfigureHighThroughputDeduplication(this IHostBuilder hostBuilder) =>
		hostBuilder; // TODO: Implement when AddDispatch is available

	/*
 return hostBuilder.ConfigureServices((context, services) =>
 {
 services.AddDispatch(builder =>
 {
 builder.AddDeduplication(options =>
 {
 options.Enabled = true;
 options.TimeWindow = TimeSpan.FromHours(12);
 options.Strategy = DeduplicationStrategyType.MessageId;
 options.UseBatchOperations = true;
 options.MaxInMemoryEntries = 1_000_000;
 options.CleanupInterval = TimeSpan.FromMinutes(30);
 });
 });

 // Configure inbox for high throughput
 services.AddInbox<InMemoryInboxStore>(inboxOptions =>
 {
 inboxOptions.ProducerBatchSize = 100;
 inboxOptions.ConsumerBatchSize = 50;
 inboxOptions.QueueCapacity = 1000;
 });
 });
 */

	/// <summary>
	///     Example 6: Using custom deduplication strategy.
	/// </summary>
	public static IHostBuilder ConfigureCustomDeduplication(this IHostBuilder hostBuilder) =>
		hostBuilder; // TODO: Implement when AddDispatch is available

	/*
 return hostBuilder.ConfigureServices((context, services) =>
 {
 services.AddDispatch(builder =>
 {
 builder.AddDeduplication(options =>
 {
 options.Enabled = true;
 options.TimeWindow = TimeSpan.FromHours(24);
 })
 .UseDeduplicationStrategy<TenantAwareDeduplicationStrategy>();
 });
 });
 */

	/// <summary>
	///     Example 7: Monitoring deduplication metrics.
	/// </summary>
	public static void ConfigureDeduplicationMetrics(IServiceProvider services)
	{
		// Access deduplication metrics
		var metrics = services.GetRequiredService<DeduplicationMetrics>();

		// Configure OpenTelemetry or other metrics collection The metrics are automatically exposed through the Meter API
	}

	/// <summary>
	///     Example 5: Custom deduplication strategy implementation.
	/// </summary>
	public class TenantAwareDeduplicationStrategy : IDeduplicationStrategy
	{
		private readonly ILogger<TenantAwareDeduplicationStrategy> _logger;

		public TenantAwareDeduplicationStrategy(ILogger<TenantAwareDeduplicationStrategy> logger) => _logger = logger;

		public string GenerateKey(IInboxMessage message)
		{
			// Extract tenant ID from message metadata
			var metadata = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(message.MessageMetadata);
			var tenantId = metadata?.GetValueOrDefault("TenantId", "default");

			// Create a composite key with tenant and message ID
			return $"tenant:{tenantId}:msg:{message.ExternalMessageId}";
		}

		public async Task<bool> IsDuplicateAsync(string key, IDeduplicationStore store, CancellationToken cancellationToken = default) =>
			await store.ContainsAsync(key, cancellationToken);

		public async Task RecordProcessedAsync(string key, IDeduplicationStore store, DeduplicationOptions options,
			CancellationToken cancellationToken = default)
		{
			var expiresAt = DateTimeOffset.UtcNow.Add(options.TimeWindow);
			await store.AddAsync(key, expiresAt, cancellationToken);
			_logger.LogDebug("Recorded tenant-aware deduplication key: {Key}", key);
		}
	}
}
