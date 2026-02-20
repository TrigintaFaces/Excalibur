// Copyright (c) Nexus Dynamics. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Threading;
using System.Threading.Tasks;
using CloudNative.CloudEvents;
using Google.Cloud.Functions.Framework;
using Google.Cloud.Functions.Hosting;
using Google.Events.Protobuf.Cloud.PubSub.V1;
using Microsoft.Extensions.Logging;

namespace examples.Excalibur.Dispatch.Examples.Serverless.Google
 /// <summary>
 /// Example of an optimized CloudEvent function with cold start mitigation.
 /// </summary>
 [FunctionsStartup(typeof(OptimizedCloudEventFunctionStartup))]
 public class OptimizedPubSubFunction : ICloudEventFunction<MessagePublishedData>
 {
 private readonly ILogger<OptimizedPubSubFunction> _logger;
 private readonly ColdStartMitigation _coldStartMitigation;
 private readonly MessageProcessor _processor;

 public OptimizedPubSubFunction(ILogger<OptimizedPubSubFunction> logger,
 ColdStartMitigation coldStartMitigation,
 MessageProcessor processor)
 {
 _logger = logger ?? throw new ArgumentNullException(nameof(logger));
 _coldStartMitigation = coldStartMitigation ?? throw new ArgumentNullException(nameof(coldStartMitigation));
 _processor = processor ?? throw new ArgumentNullException(nameof(processor));
 }

 public async Task HandleAsync(CloudEvent cloudEvent, MessagePublishedData data, CancellationToken cancellationToken)
 {
 var context = new GoogleCloudFunctionContext
 {
 FunctionName = nameof(OptimizedPubSubFunction),
 RequestId = cloudEvent.Id ?? Guid.NewGuid().ToString(),
 Region = cloudEvent.Source?.ToString() ?? "unknown"
 };

 // Apply cold start mitigation
 await _coldStartMitigation.ApplyMitigationAsync(context, cancellationToken).ConfigureAwait(false);

 // Process the message
 var message = data.Message;
 _logger.LogInformation(
 "Processing message {MessageId} with {ByteCount} bytes",
 message.MessageId,
 message.Data?.Length ?? 0);

 var result = await _processor.ProcessMessageAsync(message, cancellationToken).ConfigureAwait(false);

 _logger.LogInformation(
 "Message {MessageId} processed successfully. Result: {Result}",
 message.MessageId,
 result);
 }
 }

 /// <summary>
 /// Startup configuration for the optimized CloudEvent function.
 /// </summary>
 public class OptimizedCloudEventFunctionStartup : FunctionsStartup
 {
 public override void ConfigureServices(WebHostBuilderContext context, IServiceCollection services)
 {
 // Apply all optimizations
 services.OptimizeStartup();
 services.AddGoogleCloudFunctionsPerformance(context.Configuration);

 // Configure cold start options
 services.Configure<ColdStartOptions>(options =>
 {
 options.EnableKeepWarm = true;
 options.KeepWarmInterval = TimeSpan.FromMinutes(5);
 options.ColdThreshold = TimeSpan.FromMinutes(10);
 options.ForceGarbageCollection = true;
 });

 // Add message processor
 services.AddSingleton<MessageProcessor>();
 }
 }

 /// <summary>
 /// Example message processor that benefits from prewarming.
 /// </summary>
 public class MessageProcessor : IPrewarmable
 {
 private readonly ILogger<MessageProcessor> _logger;
 private volatile bool _isInitialized;
 private System.Text.Json.JsonSerializerOptions? _serializerOptions;

 public MessageProcessor(ILogger<MessageProcessor> logger)
 {
 _logger = logger ?? throw new ArgumentNullException(nameof(logger));
 }

 public async Task PrewarmAsync(CancellationToken cancellationToken = default)
 {
 if (_isInitialized)
 return;

 _logger.LogInformation("Prewarming message processor");

 // Initialize JSON serializer options
 _serializerOptions = new System.Text.Json.JsonSerializerOptions
 {
 PropertyNameCaseInsensitive = true,
 WriteIndented = false,
 DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
 };

 // Warm up serialization
 var testObject = new { test = "warmup" };
 _ = System.Text.Json.JsonSerializer.Serialize(testObject, _serializerOptions);

 await Task.Delay(10, cancellationToken).ConfigureAwait(false); // Simulate other initialization

 _isInitialized = true;
 _logger.LogInformation("Message processor prewarming completed");
 }

 public async Task<string> ProcessMessageAsync(PubsubMessage message, CancellationToken cancellationToken)
 {
 // Ensure initialized
 if (!_isInitialized)
 {
 await PrewarmAsync(cancellationToken).ConfigureAwait(false);
 }

 // Process the message
 if (message.Data == null || message.Data.IsEmpty)
 {
 return "Empty message";
 }

 var dataString = message.Data.ToStringUtf8();

 // Simulate processing
 await Task.Delay(50, cancellationToken).ConfigureAwait(false);

 return $"Processed {dataString.Length} characters";
 }
 }
}
