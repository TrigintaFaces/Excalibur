using System;
using System.IO;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;
using Azure.Storage.Blobs;
using Excalibur.Dispatch.CloudNativePatterns.ClaimCheck;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace examples.ClaimCheck
 /// <summary>
 /// Example demonstrating processing of large files using Claim Check pattern with Azure Service Bus.
 /// </summary>
 public class LargeFileProcessingExample
{
	public static async Task Main(string[] args)
	{
		var host = Host.CreateDefaultBuilder(args)
		.ConfigureServices((context, services) =>
		{
			// Configure Azure services
			services.AddSingleton<BlobServiceClient>(sp =>
	 new BlobServiceClient(context.Configuration["AzureStorage:ConnectionString"]));

			services.AddSingleton<ServiceBusClient>(sp =>
	 new ServiceBusClient(context.Configuration["ServiceBus:ConnectionString"]));

			// Configure Claim Check with custom settings for large files
			services.AddClaimCheck(options =>
	 {
			options.ContainerName = "large-files-container";
			options.CompressionThreshold = 10 * 1024; // Compress files > 10KB
			options.PayloadThreshold = 100 * 1024 * 1024; // Support up to 100MB files
			options.DefaultTtl = TimeSpan.FromDays(7); // Keep claims for 7 days
			options.ChunkSize = 10 * 1024 * 1024; // 10MB chunks for streaming
		});

			// Add file processor service
			services.AddHostedService<FileProcessorService>();
		})
		.Build();

		await host.RunAsync();
	}
}

/// <summary>
/// Service that processes large files using claim check pattern.
/// </summary>
public class FileProcessorService : BackgroundService
{
	private readonly IClaimCheckProvider _claimCheck;
	private readonly ServiceBusClient _serviceBusClient;
	private readonly ILogger<FileProcessorService> _logger;
	private ServiceBusSender _sender;
	private ServiceBusProcessor _processor;

	/// <summary>
	/// Initializes a new instance of the <see cref="FileProcessorService"/> class.
	/// </summary>
	public FileProcessorService(
	IClaimCheckService claimCheck,
	ServiceBusClient serviceBusClient,
	ILogger<FileProcessorService> logger)
	{
		_claimCheck = claimCheck;
		_serviceBusClient = serviceBusClient;
		_logger = logger;
	}

	/// <summary>
	/// Starts the file processing service.
	/// </summary>
	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		// Create Service Bus sender and processor
		_sender = _serviceBusClient.CreateSender("file-processing-queue");
		_processor = _serviceBusClient.CreateProcessor("file-processing-queue");

		// Configure message handlers
		_processor.ProcessMessageAsync += HandleFileMessageAsync;
		_processor.ProcessErrorAsync += HandleErrorAsync;

		// Start processing
		await _processor.StartProcessingAsync(stoppingToken);

		// Example: Process a large file
		await ProcessLargeFile(@"C:\LargeFiles\report.pdf", stoppingToken);

		// Keep service running
		await Task.Delay(Timeout.Infinite, stoppingToken);
	}

	/// <summary>
	/// Processes a large file by storing it as a claim.
	/// </summary>
	private async Task ProcessLargeFile(string filePath, CancellationToken cancellationToken)
	{
		try
		{
			var fileInfo = new FileInfo(filePath);
			_logger.LogInformation("Processing large file: {FileName}, Size: {Size:N0} bytes",
			fileInfo.Name, fileInfo.Length);

			// Read file content
			using var fileStream = File.OpenRead(filePath);
			var fileContent = new FileContent
			{
				FileName = fileInfo.Name,
				ContentType = GetContentType(fileInfo.Extension),
				FileSize = fileInfo.Length,
				UploadedAt = DateTime.UtcNow,
				Stream = fileStream
			};

			// Store as claim with streaming
			var claimResult = await _claimCheck.StoreStreamAsync(
			fileContent.Stream,
			new ClaimMetadata
			{
				MessageType = "FileContent",
				CorrelationId = Guid.NewGuid().ToString(),
				Source = "FileProcessor",
				Properties = new Dictionary<string, string>
				{
					["FileName"] = fileContent.FileName,
					["ContentType"] = fileContent.ContentType,
					["FileSize"] = fileContent.FileSize.ToString()
				}
			},
			cancellationToken);

			if (claimResult.Success)
			{
				_logger.LogInformation(
				"File stored as claim {ClaimId}. Compression ratio: {CompressionRatio:P}",
				claimResult.ClaimId,
				1 - (double)claimResult.StoredSize / claimResult.OriginalSize);

				// Send claim reference through Service Bus
				var message = new ServiceBusMessage
				{
					Body = BinaryData.FromObjectAsJson(new FileClaimMessage
					{
						ClaimId = claimResult.ClaimId,
						FileName = fileContent.FileName,
						ContentType = fileContent.ContentType,
						OriginalSize = fileContent.FileSize
					}),
					ContentType = "application/json",
					Subject = "FileProcessing"
				};

				await _sender.SendMessageAsync(message, cancellationToken);
				_logger.LogInformation("Sent file claim message for {FileName}", fileContent.FileName);
			}
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error processing file {FilePath}", filePath);
		}
	}

	/// <summary>
	/// Handles incoming file messages from Service Bus.
	/// </summary>
	private async Task HandleFileMessageAsync(ProcessMessageEventArgs args)
	{
		try
		{
			// Deserialize the claim message
			var claimMessage = args.Message.Body.ToObjectFromJson<FileClaimMessage>();
			_logger.LogInformation("Received file claim: {ClaimId} for {FileName}",
			claimMessage.ClaimId, claimMessage.FileName);

			// Retrieve the file content using streaming
			await using var retrieveStream = await _claimCheck.RetrieveStreamAsync(
			claimMessage.ClaimId,
			args.CancellationToken);

			if (retrieveStream != null)
			{
				// Process the file stream
				await ProcessFileStream(
				retrieveStream,
				claimMessage.FileName,
				args.CancellationToken);

				// Clean up the claim after successful processing
				await _claimCheck.DeleteAsync(claimMessage.ClaimId, args.CancellationToken);

				// Complete the message
				await args.CompleteMessageAsync(args.Message, args.CancellationToken);

				_logger.LogInformation("Successfully processed file {FileName}", claimMessage.FileName);
			}
			else
			{
				_logger.LogError("Failed to retrieve claim {ClaimId}", claimMessage.ClaimId);
				await args.DeadLetterMessageAsync(args.Message, "ClaimNotFound",
				"Unable to retrieve claim from storage", args.CancellationToken);
			}
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error processing file message");
			await args.DeadLetterMessageAsync(args.Message, "ProcessingError",
			ex.Message, args.CancellationToken);
		}
	}

	/// <summary>
	/// Processes the retrieved file stream.
	/// </summary>
	private async Task ProcessFileStream(Stream fileStream, string fileName, CancellationToken cancellationToken)
	{
		// Example: Save to processed folder
		var outputPath = Path.Combine(@"C:\ProcessedFiles", $"processed_{fileName}");

		using var outputStream = File.Create(outputPath);
		await fileStream.CopyToAsync(outputStream, cancellationToken);

		_logger.LogInformation("File saved to {OutputPath}", outputPath);

		// Additional processing could be done here:
		// - Generate thumbnails for images
		// - Extract text from PDFs
		// - Scan for viruses
		// - Transform file format
		// etc.
	}

	/// <summary>
	/// Handles Service Bus processing errors.
	/// </summary>
	private Task HandleErrorAsync(ProcessErrorEventArgs args)
	{
		_logger.LogError(args.Exception,
		"Error processing message from {Source}", args.ErrorSource);
		return Task.CompletedTask;
	}

	/// <summary>
	/// Gets content type from file extension.
	/// </summary>
	private string GetContentType(string extension)
	{
		return extension.ToLowerInvariant() switch
		{
			".pdf" => "application/pdf",
			".jpg" or ".jpeg" => "image/jpeg",
			".png" => "image/png",
			".doc" => "application/msword",
			".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
			".xls" => "application/vnd.ms-excel",
			".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
			".zip" => "application/zip",
			_ => "application/octet-stream"
		};
	}

	/// <summary>
	/// Disposes resources.
	/// </summary>
	public override async Task StopAsync(CancellationToken cancellationToken)
	{
		await _processor.StopProcessingAsync(cancellationToken);
		await _processor.DisposeAsync();
		await _sender.DisposeAsync();
		await base.StopAsync(cancellationToken);
	}
}

/// <summary>
/// File content model.
/// </summary>
public class FileContent
{
	public string FileName { get; set; }
	public string ContentType { get; set; }
	public long FileSize { get; set; }
	public DateTime UploadedAt { get; set; }
	public Stream Stream { get; set; }
}

/// <summary>
/// Message sent through Service Bus with claim Excalibur.Dispatch.Transport.Kafka.
/// </summary>
public class FileClaimMessage
{
	public string ClaimId { get; set; }
	public string FileName { get; set; }
	public string ContentType { get; set; }
	public long OriginalSize { get; set; }
}
}