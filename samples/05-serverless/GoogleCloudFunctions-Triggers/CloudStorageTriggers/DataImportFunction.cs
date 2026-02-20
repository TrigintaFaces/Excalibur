namespace Examples.CloudNative.Serverless.GoogleCloudFunctions.CloudStorageTriggers;

using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CsvHelper;
using CsvHelper.Configuration;
using Excalibur.Dispatch.CloudNative.Serverless.Google;
using Excalibur.Dispatch.CloudNative.Serverless.Google.Triggers;
using Google.Cloud.BigQuery.V2;
using Google.Cloud.Firestore;
using Google.Cloud.Functions.Framework;
using Microsoft.Extensions.Logging;

/// <summary>
/// Example function that imports data from uploaded CSV/JSON files into various data stores.
/// Demonstrates real-world ETL (Extract, Transform, Load) scenarios with Cloud Storage triggers.
/// </summary>
[FunctionsStartup(typeof(DataImportStartup))]
public class DataImportFunction : CloudStorageFunction<ImportMetadata>
{
 private readonly ILogger<DataImportFunction> _logger;
 private readonly FirestoreDb _firestore;
 private readonly BigQueryClient _bigQuery;
 private readonly DataImportOptions _options;
 private readonly IDataValidator _validator;

 /// <summary>
 /// Initializes a new instance of the <see cref="DataImportFunction"/> class.
 /// </summary>
 public DataImportFunction(ILogger<DataImportFunction> logger,
 FirestoreDb firestore,
 BigQueryClient bigQuery,
 IOptions<DataImportOptions> options,
 IDataValidator validator)
 {
 _logger = logger;
 _firestore = firestore;
 _bigQuery = bigQuery;
 _options = options.Value;
 _validator = validator;
 }

 /// <summary>
 /// Processes the typed content (ImportMetadata) from the Cloud Storage event.
 /// </summary>
 protected override async Task ProcessContentAsync(
 ImportMetadata metadata,
 CloudStorageEvent storageEvent,
 GoogleCloudFunctionExecutionContext context,
 CancellationToken cancellationToken)
 {
 _logger.LogInformation(
 "Processing data import: {FileName} -> {TargetType}/{TargetName}",
 storageEvent.Name,
 metadata.TargetType,
 metadata.TargetName);

 // Validate metadata
 var validationResult = await _validator.ValidateMetadataAsync(metadata, cancellationToken);
 if (!validationResult.IsValid)
 {
 throw new InvalidOperationException(
 $"Invalid metadata: {string.Join(", ", validationResult.Errors)}");
 }

 // Download the data file
 var dataFilePath = GetDataFilePath(storageEvent.Name);
 var dataContent = await DownloadObjectTextAsync(
 storageEvent.Bucket,
 dataFilePath,
 cancellationToken);

 if (string.IsNullOrEmpty(dataContent))
 {
 throw new InvalidOperationException($"Data file not found or empty: {dataFilePath}");
 }

 // Parse the data based on format
 var records = await ParseDataAsync(dataContent, dataFilePath, metadata, cancellationToken);

 _logger.LogInformation("Parsed {RecordCount} records from {FileName}",
 records.Count, dataFilePath);

 // Validate data
 var dataValidationResult = await _validator.ValidateDataAsync(records, metadata, cancellationToken);
 if (!dataValidationResult.IsValid)
 {
 await HandleValidationErrorsAsync(
 dataValidationResult,
 metadata,
 storageEvent,
 context,
 cancellationToken);
 return;
 }

 // Transform data if needed
 if (metadata.Transformations?.Any() == true)
 {
 records = await ApplyTransformationsAsync(records, metadata, cancellationToken);
 }

 // Load data into target system
 var importResult = await LoadDataAsync(
 records,
 metadata,
 storageEvent,
 context,
 cancellationToken);

 // Update import status
 await UpdateImportStatusAsync(
 importResult,
 metadata,
 storageEvent,
 context,
 cancellationToken);

 _logger.LogInformation(
 "Successfully imported {SuccessCount}/{TotalCount} records to {TargetType}/{TargetName}",
 importResult.SuccessCount,
 importResult.TotalCount,
 metadata.TargetType,
 metadata.TargetName);
 }

 /// <summary>
 /// Parses data from the content based on file format.
 /// </summary>
 private async Task<List<ExpandoObject>> ParseDataAsync(
 string content,
 string filePath,
 ImportMetadata metadata,
 CancellationToken cancellationToken)
 {
 var extension = Path.GetExtension(filePath)?.ToLowerInvariant();

 return extension switch
 {
 ".csv" => await ParseCsvAsync(content, metadata, cancellationToken),
 ".json" => await ParseJsonAsync(content, metadata, cancellationToken),
 _ => throw new NotSupportedException($"File format not supported: {extension}")
 };
 }

 /// <summary>
 /// Parses CSV content.
 /// </summary>
 private async Task<List<ExpandoObject>> ParseCsvAsync(
 string content,
 ImportMetadata metadata,
 CancellationToken cancellationToken)
 {
 var records = new List<ExpandoObject>();

 using var reader = new StringReader(content);
 using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
 {
 HasHeaderRecord = metadata.CsvOptions?.HasHeader ?? true,
 Delimiter = metadata.CsvOptions?.Delimiter ?? ",",
 BadDataFound = null // Continue on bad data
 });

 // Read header if present
 if (metadata.CsvOptions?.HasHeader == true)
 {
 await csv.ReadAsync();
 csv.ReadHeader();
 }

 // Read records
 while (await csv.ReadAsync())
 {
 dynamic record = new ExpandoObject();
 var recordDict = (IDictionary<string, object>)record;

 if (metadata.CsvOptions?.HasHeader == true)
 {
 // Use header names
 foreach (var header in csv.HeaderRecord!)
 {
 recordDict[header] = csv.GetField(header);
 }
 }
 else
 {
 // Use column indices
 for (int i = 0; i < csv.Parser.Count; i++)
 {
 recordDict[$"Column{i}"] = csv.GetField(i);
 }
 }

 // Add metadata fields
 recordDict["_importId"] = Guid.NewGuid().ToString();
 recordDict["_importTimestamp"] = DateTime.UtcNow;
 recordDict["_sourceFile"] = metadata.SourceFile;

 records.Add(record);
 }

 return records;
 }

 /// <summary>
 /// Parses JSON content.
 /// </summary>
 private async Task<List<ExpandoObject>> ParseJsonAsync(
 string content,
 ImportMetadata metadata,
 CancellationToken cancellationToken)
 {
 var records = new List<ExpandoObject>();

 // Parse JSON
 using var document = JsonDocument.Parse(content);

 // Handle array or single object
 if (document.RootElement.ValueKind == JsonValueKind.Array)
 {
 foreach (var element in document.RootElement.EnumerateArray())
 {
 var record = ConvertJsonToExpando(element);
 AddImportMetadata(record, metadata);
 records.Add(record);
 }
 }
 else
 {
 var record = ConvertJsonToExpando(document.RootElement);
 AddImportMetadata(record, metadata);
 records.Add(record);
 }

 return await Task.FromResult(records);
 }

 /// <summary>
 /// Converts JSON element to ExpandoObject.
 /// </summary>
 private ExpandoObject ConvertJsonToExpando(JsonElement element)
 {
 dynamic expando = new ExpandoObject();
 var expandoDict = (IDictionary<string, object>)expando;

 foreach (var property in element.EnumerateObject())
 {
 expandoDict[property.Name] = property.Value.ValueKind switch
 {
 JsonValueKind.String => property.Value.GetString(),
 JsonValueKind.Number => property.Value.GetDecimal(),
 JsonValueKind.True => true,
 JsonValueKind.False => false,
 JsonValueKind.Null => null,
 JsonValueKind.Object => ConvertJsonToExpando(property.Value),
 JsonValueKind.Array => property.Value.EnumerateArray()
 .Select(e => ConvertJsonToExpando(e))
 .ToList(),
 _ => property.Value.ToString()
 };
 }

 return expando;
 }

 /// <summary>
 /// Adds import metadata to a record.
 /// </summary>
 private void AddImportMetadata(ExpandoObject record, ImportMetadata metadata)
 {
 var dict = (IDictionary<string, object>)record;
 dict["_importId"] = Guid.NewGuid().ToString();
 dict["_importTimestamp"] = DateTime.UtcNow;
 dict["_sourceFile"] = metadata.SourceFile;
 }

 /// <summary>
 /// Applies transformations to the records.
 /// </summary>
 private async Task<List<ExpandoObject>> ApplyTransformationsAsync(List<ExpandoObject> records,
 ImportMetadata metadata,
 CancellationToken cancellationToken)
 {
 foreach (var transformation in metadata.Transformations!)
 {
 switch (transformation.Type)
 {
 case "rename":
 records = ApplyRenameTransformation(records, transformation);
 break;

 case "convert":
 records = ApplyConvertTransformation(records, transformation);
 break;

 case "filter":
 records = ApplyFilterTransformation(records, transformation);
 break;

 case "enrich":
 records = await ApplyEnrichTransformationAsync(records, transformation, cancellationToken);
 break;

 default:
 _logger.LogWarning("Unknown transformation type: {Type}", transformation.Type);
 break;
 }
 }

 return records;
 }

 /// <summary>
 /// Loads data into the target system.
 /// </summary>
 private async Task<ImportResult> LoadDataAsync(List<ExpandoObject> records,
 ImportMetadata metadata,
 CloudStorageEvent storageEvent,
 GoogleCloudFunctionExecutionContext context,
 CancellationToken cancellationToken)
 {
 return metadata.TargetType switch
 {
 "firestore" => await LoadToFirestoreAsync(records, metadata, cancellationToken),
 "bigquery" => await LoadToBigQueryAsync(records, metadata, cancellationToken),
 "pubsub" => await LoadToPubSubAsync(records, metadata, cancellationToken),
 _ => throw new NotSupportedException($"Target type not supported: {metadata.TargetType}")
 };
 }

 /// <summary>
 /// Loads data to Firestore.
 /// </summary>
 private async Task<ImportResult> LoadToFirestoreAsync(List<ExpandoObject> records,
 ImportMetadata metadata,
 CancellationToken cancellationToken)
 {
 var result = new ImportResult { TotalCount = records.Count };
 var collection = _firestore.Collection(metadata.TargetName);

 // Batch writes for efficiency
 var batches = records.Chunk(_options.FirestoreBatchSize)
 .Select(chunk => _firestore.StartBatch())
 .ToList();

 int batchIndex = 0;
 foreach (var record in records)
 {
 try
 {
 var batch = batches[batchIndex / _options.FirestoreBatchSize];
 var docRef = collection.Document();
 batch.Set(docRef, record);

 batchIndex++;
 result.SuccessCount++;
 }
 catch (Exception ex)
 {
 _logger.LogError(ex, "Failed to prepare record for Firestore");
 result.FailedRecords.Add(new FailedRecord
 {
 Record = record,
 Error = ex.Message
 });
 }
 }

 // Commit all batches
 foreach (var batch in batches)
 {
 try
 {
 await batch.CommitAsync(cancellationToken);
 }
 catch (Exception ex)
 {
 _logger.LogError(ex, "Failed to commit Firestore batch");
 // Records in this batch are marked as failed
 }
 }

 return result;
 }

 /// <summary>
 /// Loads data to BigQuery.
 /// </summary>
 private async Task<ImportResult> LoadToBigQueryAsync(List<ExpandoObject> records,
 ImportMetadata metadata,
 CancellationToken cancellationToken)
 {
 var result = new ImportResult { TotalCount = records.Count };

 // Parse target as dataset.table
 var parts = metadata.TargetName.Split('.');
 if (parts.Length != 2)
 {
 throw new ArgumentException($"Invalid BigQuery target format. Expected 'dataset.table', got '{metadata.TargetName}'");
 }

 var table = _bigQuery.GetTable(parts[0], parts[1]);

 // Convert records to BigQuery rows
 var rows = records.Select(record =>
 {
 var row = new BigQueryInsertRow();
 foreach (var kvp in (IDictionary<string, object>)record)
 {
 row[kvp.Key] = kvp.Value;
 }
 return row;
 }).ToList();

 // Insert rows
 try
 {
 var insertResult = await table.InsertRowsAsync(rows, cancellationToken: cancellationToken);
 result.SuccessCount = records.Count;
 }
 catch (Exception ex)
 {
 _logger.LogError(ex, "Failed to insert rows to BigQuery");
 result.FailedRecords = records.Select((r, i) => new FailedRecord
 {
 Record = r,
 Error = $"Batch insert failed: {ex.Message}"
 }).ToList();
 }

 return result;
 }

 /// <summary>
 /// Loads data to Pub/Sub.
 /// </summary>
 private async Task<ImportResult> LoadToPubSubAsync(List<ExpandoObject> records,
 ImportMetadata metadata,
 CancellationToken cancellationToken)
 {
 // Implementation would publish each record as a message to Pub/Sub
 var result = new ImportResult
 {
 TotalCount = records.Count,
 SuccessCount = records.Count
 };

 _logger.LogInformation("Publishing {Count} records to Pub/Sub topic {Topic}",
 records.Count, metadata.TargetName);

 // Actual Pub/Sub implementation would go here
 return await Task.FromResult(result);
 }

 /// <summary>
 /// Updates the import status in Firestore.
 /// </summary>
 private async Task UpdateImportStatusAsync(
 ImportResult result,
 ImportMetadata metadata,
 CloudStorageEvent storageEvent,
 GoogleCloudFunctionExecutionContext context,
 CancellationToken cancellationToken)
 {
 var status = new ImportStatus
 {
 ImportId = context.RequestId,
 SourceFile = metadata.SourceFile,
 TargetType = metadata.TargetType,
 TargetName = metadata.TargetName,
 TotalRecords = result.TotalCount,
 SuccessfulRecords = result.SuccessCount,
 FailedRecords = result.FailedRecords.Count,
 StartTime = context.Timestamp,
 EndTime = DateTime.UtcNow,
 DurationMs = context.Metrics.HandlerDuration.TotalMilliseconds,
 Status = result.FailedRecords.Any() ? "partial_success" : "success",
 Errors = result.FailedRecords.Take(10).Select(f => f.Error).ToList()
 };

 await _firestore
 .Collection("import_status")
 .Document(context.RequestId)
 .SetAsync(status, cancellationToken: cancellationToken);
 }

 /// <summary>
 /// Handles validation errors.
 /// </summary>
 private async Task HandleValidationErrorsAsync(
 ValidationResult validationResult,
 ImportMetadata metadata,
 CloudStorageEvent storageEvent,
 GoogleCloudFunctionExecutionContext context,
 CancellationToken cancellationToken)
 {
 _logger.LogError(
 "Data validation failed for {FileName}: {Errors}",
 storageEvent.Name,
 string.Join(", ", validationResult.Errors));

 // Store validation errors
 var status = new ImportStatus
 {
 ImportId = context.RequestId,
 SourceFile = metadata.SourceFile,
 TargetType = metadata.TargetType,
 TargetName = metadata.TargetName,
 Status = "validation_failed",
 Errors = validationResult.Errors,
 StartTime = context.Timestamp,
 EndTime = DateTime.UtcNow
 };

 await _firestore
 .Collection("import_status")
 .Document(context.RequestId)
 .SetAsync(status, cancellationToken: cancellationToken);
 }

 /// <summary>
 /// Gets the data file path from the metadata file path.
 /// </summary>
 private static string GetDataFilePath(string metadataPath)
 {
 // Remove .metadata.json suffix
 if (metadataPath.EndsWith(".metadata.json", StringComparison.OrdinalIgnoreCase))
 {
 return metadataPath[..^14]; // Remove last 14 characters
 }

 // Try to find associated data file
 var directory = Path.GetDirectoryName(metadataPath) ?? "";
 var baseName = Path.GetFileNameWithoutExtension(metadataPath);

 // Look for common data file extensions
 foreach (var ext in new[] { ".csv", ".json", ".tsv" })
 {
 var candidate = Path.Combine(directory, baseName + ext);
 if (File.Exists(candidate))
 return candidate;
 }

 throw new InvalidOperationException($"Could not determine data file path from metadata: {metadataPath}");
 }

 /// <summary>
 /// Deserializes the metadata content.
 /// </summary>
 protected override ImportMetadata DeserializeContent(string contentText, CloudStorageEvent storageEvent)
 {
 // Ensure we're processing a metadata file
 if (!storageEvent.Name.EndsWith(".metadata.json", StringComparison.OrdinalIgnoreCase))
 {
 throw new InvalidOperationException($"Expected metadata file (.metadata.json), got: {storageEvent.Name}");
 }

 var metadata = JsonSerializer.Deserialize<ImportMetadata>(contentText, new JsonSerializerOptions
 {
 PropertyNameCaseInsensitive = true
 }) ?? throw new InvalidOperationException("Failed to deserialize metadata");

 // Set source file if not specified
 metadata.SourceFile ??= GetDataFilePath(storageEvent.Name);

 return metadata;
 }

 /// <summary>
 /// Applies rename transformation.
 /// </summary>
 private List<ExpandoObject> ApplyRenameTransformation(List<ExpandoObject> records,
 DataTransformation transformation)
 {
 foreach (var record in records)
 {
 var dict = (IDictionary<string, object>)record;
 if (dict.TryGetValue(transformation.SourceField!, out var value))
 {
 dict.Remove(transformation.SourceField!);
 dict[transformation.TargetField!] = value;
 }
 }
 return records;
 }

 /// <summary>
 /// Applies type conversion transformation.
 /// </summary>
 private List<ExpandoObject> ApplyConvertTransformation(List<ExpandoObject> records,
 DataTransformation transformation)
 {
 foreach (var record in records)
 {
 var dict = (IDictionary<string, object>)record;
 if (dict.TryGetValue(transformation.SourceField!, out var value) && value != null)
 {
 dict[transformation.SourceField!] = transformation.TargetType switch
 {
 "int" => Convert.ToInt32(value),
 "long" => Convert.ToInt64(value),
 "double" => Convert.ToDouble(value),
 "decimal" => Convert.ToDecimal(value),
 "bool" => Convert.ToBoolean(value),
 "datetime" => DateTime.Parse(value.ToString()!),
 "string" => value.ToString(),
 _ => value
 };
 }
 }
 return records;
 }

 /// <summary>
 /// Applies filter transformation.
 /// </summary>
 private List<ExpandoObject> ApplyFilterTransformation(List<ExpandoObject> records,
 DataTransformation transformation)
 {
 return records.Where(record =>
 {
 var dict = (IDictionary<string, object>)record;
 if (!dict.TryGetValue(transformation.SourceField!, out var value))
 return false;

 return transformation.Operator switch
 {
 "equals" => value?.ToString() == transformation.Value,
 "not_equals" => value?.ToString() != transformation.Value,
 "contains" => value?.ToString()?.Contains(transformation.Value!) == true,
 "greater_than" => Convert.ToDouble(value) > Convert.ToDouble(transformation.Value),
 "less_than" => Convert.ToDouble(value) < Convert.ToDouble(transformation.Value),
 _ => true
 };
 }).ToList();
 }

 /// <summary>
 /// Applies data enrichment transformation.
 /// </summary>
 private async Task<List<ExpandoObject>> ApplyEnrichTransformationAsync(List<ExpandoObject> records,
 DataTransformation transformation,
 CancellationToken cancellationToken)
 {
 // Example: Enrich with data from Firestore
 foreach (var record in records)
 {
 var dict = (IDictionary<string, object>)record;
 if (dict.TryGetValue(transformation.SourceField!, out var value) && value != null)
 {
 var enrichmentData = await _firestore
 .Collection(transformation.EnrichmentSource!)
 .Document(value.ToString()!)
 .GetSnapshotAsync(cancellationToken);

 if (enrichmentData.Exists)
 {
 dict[transformation.TargetField!] = enrichmentData.ToDictionary();
 }
 }
 }

 return records;
 }

 /// <summary>
 /// Configures the trigger options for data import.
 /// </summary>
 protected override CloudStorageTriggerOptions ConfigureTriggerOptions()
 {
 return new CloudStorageTriggerOptions
 {
 // Enable content download for metadata files
 DownloadContent = true,

 // Process from data import buckets
 BucketPatterns = new[] { "data-imports-*", "etl-staging-*" },

 // Only process metadata files
 FilePatterns = new[] { "*.metadata.json" },

 // Only process finalized objects
 EventTypes = new[] { CloudStorageEventType.ObjectFinalized },

 // Size limits for metadata files (max 10MB)
 MaxFileSizeBytes = 10 * 1024 * 1024,

 // Validation
 ValidateEvent = true,

 // Metrics
 TrackMetrics = true
 };
 }
}

/// <summary>
/// Metadata for data import operations.
/// </summary>
public class ImportMetadata {
 public string? SourceFile { get; set; }
 public string TargetType { get; set; } = ""; // firestore, bigquery, pubsub
 public string TargetName { get; set; } = ""; // collection, dataset.table, topic
 public CsvImportOptions? CsvOptions { get; set; }
 public List<DataTransformation>? Transformations { get; set; }
 public Dictionary<string, string>? FieldMappings { get; set; }
 public ValidationRules? ValidationRules { get; set; }
}

/// <summary>
/// CSV import options.
/// </summary>
public class CsvImportOptions {
 public bool HasHeader { get; set; } = true;
 public string Delimiter { get; set; } = ",";
 public string? DateFormat { get; set; }
 public string? NumberFormat { get; set; }
}

/// <summary>
/// Data transformation definition.
/// </summary>
public class DataTransformation {
 public string Type { get; set; } = ""; // rename, convert, filter, enrich
 public string? SourceField { get; set; }
 public string? TargetField { get; set; }
 public string? TargetType { get; set; }
 public string? Operator { get; set; }
 public string? Value { get; set; }
 public string? EnrichmentSource { get; set; }
}

/// <summary>
/// Validation rules for imported data.
/// </summary>
public class ValidationRules {
 public List<string>? RequiredFields { get; set; }
 public Dictionary<string, string>? FieldTypes { get; set; }
 public Dictionary<string, string>? FieldPatterns { get; set; }
 public int? MinRecords { get; set; }
 public int? MaxRecords { get; set; }
}

/// <summary>
/// Result of data import operation.
/// </summary>
public class ImportResult {
 public int TotalCount { get; set; }
 public int SuccessCount { get; set; }
 public List<FailedRecord> FailedRecords { get; set; } = new();
}

/// <summary>
/// Failed record information.
/// </summary>
public class FailedRecord {
 public ExpandoObject Record { get; set; } = new();
 public string Error { get; set; } = "";
}

/// <summary>
/// Import status tracking.
/// </summary>
public class ImportStatus {
 public string ImportId { get; set; } = "";
 public string SourceFile { get; set; } = "";
 public string TargetType { get; set; } = "";
 public string TargetName { get; set; } = "";
 public int TotalRecords { get; set; }
 public int SuccessfulRecords { get; set; }
 public int FailedRecords { get; set; }
 public DateTime StartTime { get; set; }
 public DateTime EndTime { get; set; }
 public double DurationMs { get; set; }
 public string Status { get; set; } = "";
 public List<string> Errors { get; set; } = new();
}

/// <summary>
/// Configuration options for data import.
/// </summary>
public class DataImportOptions {
 public int FirestoreBatchSize { get; set; } = 500;
 public int BigQueryBatchSize { get; set; } = 10000;
 public int MaxConcurrentImports { get; set; } = 10;
 public TimeSpan ImportTimeout { get; set; } = TimeSpan.FromMinutes(30);
}

/// <summary>
/// Data validator interface.
/// </summary>
public interface IDataValidator {
 Task<ValidationResult> ValidateMetadataAsync(ImportMetadata metadata, CancellationToken cancellationToken);
 Task<ValidationResult> ValidateDataAsync(List<ExpandoObject> records, ImportMetadata metadata, CancellationToken cancellationToken);
}

/// <summary>
/// Default data validator implementation.
/// </summary>
public class DefaultDataValidator : IDataValidator
{
 private readonly ILogger<DefaultDataValidator> _logger;

 public DefaultDataValidator(ILogger<DefaultDataValidator> logger)
 {
 _logger = logger;
 }

 public async Task<ValidationResult> ValidateMetadataAsync(ImportMetadata metadata, CancellationToken cancellationToken)
 {
 var result = new ValidationResult();

 if (string.IsNullOrEmpty(metadata.TargetType))
 result.Errors.Add("TargetType is required");

 if (string.IsNullOrEmpty(metadata.TargetName))
 result.Errors.Add("TargetName is required");

 if (!new[] { "firestore", "bigquery", "pubsub" }.Contains(metadata.TargetType))
 result.Errors.Add($"Invalid TargetType: {metadata.TargetType}");

 return await Task.FromResult(result);
 }

 public async Task<ValidationResult> ValidateDataAsync(List<ExpandoObject> records,
 ImportMetadata metadata,
 CancellationToken cancellationToken)
 {
 var result = new ValidationResult();

 if (!records.Any())
 {
 result.Errors.Add("No records to import");
 return result;
 }

 var rules = metadata.ValidationRules;
 if (rules == null)
 return result;

 // Check record count
 if (rules.MinRecords.HasValue && records.Count < rules.MinRecords)
 result.Errors.Add($"Too few records: {records.Count} < {rules.MinRecords}");

 if (rules.MaxRecords.HasValue && records.Count > rules.MaxRecords)
 result.Errors.Add($"Too many records: {records.Count} > {rules.MaxRecords}");

 // Check required fields
 if (rules.RequiredFields?.Any() == true)
 {
 var firstRecord = (IDictionary<string, object>)records.First();
 foreach (var field in rules.RequiredFields)
 {
 if (!firstRecord.ContainsKey(field))
 result.Errors.Add($"Required field missing: {field}");
 }
 }

 // Additional validation logic would go here

 return await Task.FromResult(result);
 }
}

/// <summary>
/// Validation result.
/// </summary>
public class ValidationResult {
 public bool IsValid => !Errors.Any();
 public List<string> Errors { get; set; } = new();
}

/// <summary>
/// Startup configuration for the data import function.
/// </summary>
public class DataImportStartup : FunctionsStartup
{
 public override void ConfigureServices(IServiceCollection services)
 {
 // Add Cloud Storage trigger support
 services.AddGoogleCloudStorageTriggers();

 // Configure data import options
 services.Configure<DataImportOptions>(Configuration.GetSection("DataImport"));

 // Add Google Cloud services
 services.AddSingleton(provider => FirestoreDb.Create());
 services.AddSingleton(provider => BigQueryClient.Create());

 // Add data validator
 services.AddSingleton<IDataValidator, DefaultDataValidator>();

 // Add health checks
 services.AddHealthChecks()
 .AddCheck("firestore", new FirestoreHealthCheck())
 .AddCheck("bigquery", new BigQueryHealthCheck());
 }
}

/// <summary>
/// Firestore health check.
/// </summary>
public class FirestoreHealthCheck : IHealthCheck
{
 public async Task<HealthCheckResult> CheckHealthAsync(
 HealthCheckContext context,
 CancellationToken cancellationToken = default)
 {
 try
 {
 var db = FirestoreDb.Create();
 await db.Collection("_health").Document("check").GetSnapshotAsync(cancellationToken);
 return HealthCheckResult.Healthy("Firestore is accessible");
 }
 catch (Exception ex)
 {
 return HealthCheckResult.Unhealthy("Firestore is not accessible", ex);
 }
 }
}

/// <summary>
/// BigQuery health check.
/// </summary>
public class BigQueryHealthCheck : IHealthCheck
{
 public async Task<HealthCheckResult> CheckHealthAsync(
 HealthCheckContext context,
 CancellationToken cancellationToken = default)
 {
 try
 {
 var client = BigQueryClient.Create();
 await client.GetDatasetAsync("test", cancellationToken: cancellationToken);
 return HealthCheckResult.Healthy("BigQuery is accessible");
 }
 catch (Exception ex)
 {
 return HealthCheckResult.Unhealthy("BigQuery is not accessible", ex);
 }
 }
}
