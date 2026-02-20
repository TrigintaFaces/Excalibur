namespace Examples.CloudNative.Serverless.GoogleCloudFunctions.CloudStorageTriggers;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Excalibur.Dispatch.CloudNative.Serverless.Google;
using Excalibur.Dispatch.CloudNative.Serverless.Google.Triggers;
using Google.Cloud.BigQuery.V2;
using Google.Cloud.Functions.Framework;
using Microsoft.Extensions.Logging;

/// <summary>
/// Example function that analyzes uploaded log files for errors, patterns, and metrics.
/// Demonstrates log parsing and analysis with BigQuery integration.
/// </summary>
[FunctionsStartup(typeof(LogAnalysisStartup))]
public class LogAnalysisFunction : CloudStorageFunction
{
 private readonly ILogger<LogAnalysisFunction> _logger;
 private readonly BigQueryClient _bigQueryClient;
 private readonly LogAnalysisOptions _options;

 // Regular expressions for log parsing
 private static readonly Regex ErrorPattern = new(@"(?i)\b(error|exception|failed)\b", RegexOptions.Compiled);
 private static readonly Regex IpAddressPattern = new(@"\b(?:[0-9]{1,3}\.){3}[0-9]{1,3}\b", RegexOptions.Compiled);
 private static readonly Regex TimestampPattern = new(@"\d{4}-\d{2}-\d{2}[T\s]\d{2}:\d{2}:\d{2}", RegexOptions.Compiled);
 private static readonly Regex HttpStatusPattern = new(@"\b([1-5]\d{2})\b", RegexOptions.Compiled);

 /// <summary>
 /// Initializes a new instance of the <see cref="LogAnalysisFunction"/> class.
 /// </summary>
 public LogAnalysisFunction(ILogger<LogAnalysisFunction> logger,
 IOptions<LogAnalysisOptions> options)
 {
 _logger = logger;
 _options = options.Value;
 _bigQueryClient = BigQueryClient.Create(_options.ProjectId);
 }

 /// <summary>
 /// Processes the Cloud Storage event for log files.
 /// </summary>
 protected override async Task ProcessEventAsync(
 CloudStorageEvent storageEvent,
 GoogleCloudFunctionExecutionContext context,
 CancellationToken cancellationToken)
 {
 // Only process finalized log files
 if (storageEvent.EventType != CloudStorageEventType.ObjectFinalized)
 {
 _logger.LogDebug("Skipping non-finalized event: {EventType}", storageEvent.EventType);
 return;
 }

 // Check if it's a log file
 if (!IsLogFile(storageEvent.Name))
 {
 _logger.LogDebug("Skipping non-log file: {FileName}", storageEvent.Name);
 return;
 }

 _logger.LogInformation(
 "Analyzing log file: {Bucket}/{Object} (Size: {Size} bytes)",
 storageEvent.Bucket,
 storageEvent.Name,
 storageEvent.Size);

 try
 {
 // Download and analyze the log file
 var logContent = await DownloadObjectTextAsync(
 storageEvent.Bucket,
 storageEvent.Name,
 cancellationToken);

 if (string.IsNullOrEmpty(logContent))
 {
 _logger.LogWarning("Log file is empty: {Bucket}/{Object}",
 storageEvent.Bucket, storageEvent.Name);
 return;
 }

 // Analyze the log content
 var analysis = await AnalyzeLogContentAsync(logContent, storageEvent, context, cancellationToken);

 // Store results in BigQuery
 await StoreAnalysisResultsAsync(analysis, storageEvent, context, cancellationToken);

 // Generate alerts if needed
 await GenerateAlertsAsync(analysis, storageEvent, cancellationToken);

 _logger.LogInformation(
 "Successfully analyzed log file: {Bucket}/{Object} - " +
 "Lines: {TotalLines}, Errors: {ErrorCount}, Unique IPs: {UniqueIps}",
 storageEvent.Bucket,
 storageEvent.Name,
 analysis.TotalLines,
 analysis.ErrorCount,
 analysis.UniqueIpAddresses.Count);
 }
 catch (Exception ex)
 {
 _logger.LogError(ex,
 "Failed to analyze log file: {Bucket}/{Object}",
 storageEvent.Bucket,
 storageEvent.Name);
 throw;
 }
 }

 /// <summary>
 /// Analyzes the log content for patterns and metrics.
 /// </summary>
 private async Task<LogAnalysisResult> AnalyzeLogContentAsync(
 string logContent,
 CloudStorageEvent storageEvent,
 GoogleCloudFunctionExecutionContext context,
 CancellationToken cancellationToken)
 {
 var result = new LogAnalysisResult
 {
 FileName = storageEvent.Name,
 Bucket = storageEvent.Bucket,
 FileSize = storageEvent.Size,
 AnalysisTimestamp = DateTime.UtcNow
 };

 // Process lines in parallel for better performance
 var lines = logContent.Split('\n', StringSplitOptions.RemoveEmptyEntries);
 result.TotalLines = lines.Length;

 // Use parallel processing for large files
 if (lines.Length > 1000)
 {
 await Task.Run(() =>
 {
 Parallel.ForEach(lines, line =>
 {
 AnalyzeLine(line, result);
 });
 }, cancellationToken);
 }
 else
 {
 foreach (var line in lines)
 {
 AnalyzeLine(line, result);
 }
 }

 // Calculate additional metrics
 result.ErrorRate = result.TotalLines > 0
 ? (double)result.ErrorCount / result.TotalLines
 : 0;

 // Extract time range
 if (result.Timestamps.Any())
 {
 result.StartTime = result.Timestamps.Min();
 result.EndTime = result.Timestamps.Max();
 }

 // HTTP status code distribution
 result.HttpStatusDistribution = result.HttpStatusCodes
 .GroupBy(s => s / 100)
 .ToDictionary(
 g => $"{g.Key}xx",
 g => g.Count()
 );

 return result;
 }

 /// <summary>
 /// Analyzes a single log line.
 /// </summary>
 private void AnalyzeLine(string line, LogAnalysisResult result)
 {
 // Check for errors
 if (ErrorPattern.IsMatch(line))
 {
 lock (result.ErrorLines)
 {
 result.ErrorCount++;
 result.ErrorLines.Add(line);
 }
 }

 // Extract IP addresses
 var ipMatches = IpAddressPattern.Matches(line);
 foreach (Match match in ipMatches)
 {
 lock (result.UniqueIpAddresses)
 {
 result.UniqueIpAddresses.Add(match.Value);
 }
 }

 // Extract timestamps
 var timestampMatch = TimestampPattern.Match(line);
 if (timestampMatch.Success && DateTime.TryParse(timestampMatch.Value, out var timestamp))
 {
 lock (result.Timestamps)
 {
 result.Timestamps.Add(timestamp);
 }
 }

 // Extract HTTP status codes
 var statusMatch = HttpStatusPattern.Match(line);
 if (statusMatch.Success && int.TryParse(statusMatch.Value, out var statusCode))
 {
 lock (result.HttpStatusCodes)
 {
 result.HttpStatusCodes.Add(statusCode);
 }
 }
 }

 /// <summary>
 /// Stores analysis results in BigQuery.
 /// </summary>
 private async Task StoreAnalysisResultsAsync(
 LogAnalysisResult analysis,
 CloudStorageEvent storageEvent,
 GoogleCloudFunctionExecutionContext context,
 CancellationToken cancellationToken)
 {
 var table = _bigQueryClient.GetTable(_options.DatasetId, _options.TableId);

 var rows = new[]
 {
 new BigQueryInsertRow
 {
 ["file_name"] = analysis.FileName,
 ["bucket"] = analysis.Bucket,
 ["file_size"] = analysis.FileSize,
 ["total_lines"] = analysis.TotalLines,
 ["error_count"] = analysis.ErrorCount,
 ["error_rate"] = analysis.ErrorRate,
 ["unique_ip_count"] = analysis.UniqueIpAddresses.Count,
 ["start_time"] = analysis.StartTime,
 ["end_time"] = analysis.EndTime,
 ["analysis_timestamp"] = analysis.AnalysisTimestamp,
 ["processing_duration_ms"] = context.Metrics.HandlerDuration.TotalMilliseconds,
 ["http_status_distribution"] = analysis.HttpStatusDistribution,
 ["top_error_samples"] = analysis.ErrorLines.Take(10).ToArray()
 }
 };

 await table.InsertRowsAsync(rows, cancellationToken: cancellationToken);

 _logger.LogInformation("Stored analysis results in BigQuery for {FileName}", analysis.FileName);
 }

 /// <summary>
 /// Generates alerts based on analysis results.
 /// </summary>
 private async Task GenerateAlertsAsync(
 LogAnalysisResult analysis,
 CloudStorageEvent storageEvent,
 CancellationToken cancellationToken)
 {
 var alerts = new List<string>();

 // High error rate alert
 if (analysis.ErrorRate > _options.ErrorRateThreshold)
 {
 alerts.Add($"High error rate detected: {analysis.ErrorRate:P2}");
 }

 // Suspicious IP activity
 if (analysis.UniqueIpAddresses.Count > _options.UniqueIpThreshold)
 {
 alerts.Add($"Unusual number of unique IPs: {analysis.UniqueIpAddresses.Count}");
 }

 // High 5xx error rate
 var serverErrorRate = analysis.HttpStatusDistribution.GetValueOrDefault("5xx", 0) / (double)analysis.TotalLines;
 if (serverErrorRate > _options.ServerErrorThreshold)
 {
 alerts.Add($"High server error rate: {serverErrorRate:P2}");
 }

 if (alerts.Any())
 {
 _logger.LogWarning(
 "Alerts generated for {FileName}: {Alerts}",
 analysis.FileName,
 string.Join(", ", alerts));

 // Here you would send alerts via email, Slack, PagerDuty, etc.
 // For example purposes, we just log them
 }
 }

 /// <summary>
 /// Checks if the file is a log file based on name and extension.
 /// </summary>
 private static bool IsLogFile(string fileName)
 {
 var extension = Path.GetExtension(fileName)?.ToLowerInvariant();
 return extension switch
 {
 ".log" or ".txt" or ".json" => true,
 _ => fileName.Contains("log", StringComparison.OrdinalIgnoreCase)
 };
 }

 /// <summary>
 /// Configures the trigger options for log analysis.
 /// </summary>
 protected override CloudStorageTriggerOptions ConfigureTriggerOptions()
 {
 return new CloudStorageTriggerOptions
 {
 // Enable content download for analysis
 DownloadContent = true,

 // Process logs from specific buckets
 BucketPatterns = new[] { "application-logs-*", "system-logs-*", "audit-logs-*" },

 // File patterns for logs
 FilePatterns = new[] { "*.log", "*.txt", "*access*", "*error*" },

 // Only process finalized objects
 EventTypes = new[] { CloudStorageEventType.ObjectFinalized },

 // Size limits (max 100MB)
 MaxFileSizeBytes = 100 * 1024 * 1024,

 // Validation
 ValidateEvent = true,

 // Metrics
 TrackMetrics = true
 };
 }
}

/// <summary>
/// Result of log analysis.
/// </summary>
public class LogAnalysisResult {
 public string FileName { get; set; } = "";
 public string Bucket { get; set; } = "";
 public long FileSize { get; set; }
 public int TotalLines { get; set; }
 public int ErrorCount { get; set; }
 public double ErrorRate { get; set; }
 public HashSet<string> UniqueIpAddresses { get; } = new();
 public List<string> ErrorLines { get; } = new();
 public List<DateTime> Timestamps { get; } = new();
 public List<int> HttpStatusCodes { get; } = new();
 public Dictionary<string, int> HttpStatusDistribution { get; set; } = new();
 public DateTime? StartTime { get; set; }
 public DateTime? EndTime { get; set; }
 public DateTime AnalysisTimestamp { get; set; }
}

/// <summary>
/// Configuration options for log analysis.
/// </summary>
public class LogAnalysisOptions {
 public string ProjectId { get; set; } = Environment.GetEnvironmentVariable("GOOGLE_CLOUD_PROJECT") ?? "";
 public string DatasetId { get; set; } = "log_analysis";
 public string TableId { get; set; } = "analysis_results";
 public double ErrorRateThreshold { get; set; } = 0.05; // 5%
 public int UniqueIpThreshold { get; set; } = 1000;
 public double ServerErrorThreshold { get; set; } = 0.01; // 1%
}

/// <summary>
/// Startup configuration for the log analysis function.
/// </summary>
public class LogAnalysisStartup : FunctionsStartup
{
 public override void ConfigureServices(IServiceCollection services)
 {
 // Add Cloud Storage trigger support
 services.AddGoogleCloudStorageTriggers();

 // Configure log analysis options
 services.Configure<LogAnalysisOptions>(Configuration.GetSection("LogAnalysis"));

 // Add BigQuery client
 services.AddSingleton(provider => BigQueryClient.Create());

 // Add monitoring
 services.AddSingleton<IMetricReporter, BigQueryMetricReporter>();
 }
}

/// <summary>
/// Reports metrics to BigQuery.
/// </summary>
public class BigQueryMetricReporter : IMetricReporter
{
 private readonly BigQueryClient _client;
 private readonly ILogger<BigQueryMetricReporter> _logger;

 public BigQueryMetricReporter(BigQueryClient client, ILogger<BigQueryMetricReporter> logger)
 {
 _client = client;
 _logger = logger;
 }

 public async Task ReportAsync(string metricName, double value, Dictionary<string, string> tags)
 {
 try
 {
 // Implementation would report metrics to BigQuery
 _logger.LogDebug("Reported metric {MetricName}: {Value}", metricName, value);
 }
 catch (Exception ex)
 {
 _logger.LogError(ex, "Failed to report metric {MetricName}", metricName);
 }
 }
}

/// <summary>
/// Metric reporter interface.
/// </summary>
public interface IMetricReporter {
 Task ReportAsync(string metricName, double value, Dictionary<string, string> tags);
}
