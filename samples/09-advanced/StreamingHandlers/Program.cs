// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Runtime.CompilerServices;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Delivery;

using Microsoft.Extensions.DependencyInjection;

Console.WriteLine("=== Streaming Handlers Sample ===");
Console.WriteLine();

// Build the service provider with all handlers
var services = new ServiceCollection();

// Register handlers (in a real app, use AddHandlersFromAssembly for auto-discovery)
services.AddSingleton<CsvStreamingHandler>();
services.AddSingleton<BatchImportHandler>();
services.AddSingleton<RecordEnricher>();
services.AddSingleton<ReportExportHandler>();

var provider = services.BuildServiceProvider();

// Run all demos
await Demo1_OutputStreaming(provider);
await Demo2_InputStreaming(provider);
await Demo3_StreamTransform(provider);
await Demo4_ProgressReporting(provider);

Console.WriteLine();
Console.WriteLine("=== All demos completed ===");

// ============================================================================
// Demo 1: Output Streaming (IStreamingDocumentHandler)
// ============================================================================
static async Task Demo1_OutputStreaming(IServiceProvider provider)
{
	Console.WriteLine("--- Demo 1: Output Streaming (Document -> Stream) ---");
	Console.WriteLine("Parsing CSV document into individual rows...");

	var handler = provider.GetRequiredService<CsvStreamingHandler>();
	var document = new CsvDocument("""
	                               Id,Name,Email
	                               1,Alice,alice@example.com
	                               2,Bob,bob@example.com
	                               3,Charlie,charlie@example.com
	                               4,Diana,diana@example.com
	                               5,Eve,eve@example.com
	                               """);

	var ct = CancellationToken.None;
	await foreach (var row in handler.HandleAsync(document, ct))
	{
		Console.WriteLine($"  Row: Id={row.Id}, Name={row.Name}, Email={row.Email}");
	}

	Console.WriteLine("Output streaming complete!");
	Console.WriteLine();
}

// ============================================================================
// Demo 2: Input Streaming (IStreamConsumerHandler)
// ============================================================================
static async Task Demo2_InputStreaming(IServiceProvider provider)
{
	Console.WriteLine("--- Demo 2: Input Streaming (Stream -> Sink) ---");
	Console.WriteLine("Consuming stream with batch processing...");

	var handler = provider.GetRequiredService<BatchImportHandler>();

	// Create an async stream
	async IAsyncEnumerable<DataRow> GenerateRows([EnumeratorCancellation] CancellationToken ct)
	{
		for (int i = 1; i <= 15; i++)
		{
			ct.ThrowIfCancellationRequested();
			await Task.Delay(50, ct); // Simulate work
			yield return new DataRow(i, $"User{i}", $"user{i}@example.com");
		}
	}

	await handler.HandleAsync(GenerateRows(CancellationToken.None), CancellationToken.None);

	Console.WriteLine("Input streaming complete!");
	Console.WriteLine();
}

// ============================================================================
// Demo 3: Stream Transform (IStreamTransformHandler)
// ============================================================================
static async Task Demo3_StreamTransform(IServiceProvider provider)
{
	Console.WriteLine("--- Demo 3: Stream Transform (Stream -> Stream) ---");
	Console.WriteLine("Enriching data records with additional information...");

	var handler = provider.GetRequiredService<RecordEnricher>();

	// Input stream
	async IAsyncEnumerable<DataRow> GenerateRows([EnumeratorCancellation] CancellationToken ct)
	{
		for (int i = 1; i <= 5; i++)
		{
			ct.ThrowIfCancellationRequested();
			yield return new DataRow(i, $"Customer{i}", $"customer{i}@example.com");
		}
	}

	var ct = CancellationToken.None;
	await foreach (var enriched in handler.HandleAsync(GenerateRows(ct), ct))
	{
		Console.WriteLine($"  Enriched: {enriched.Original.Name} -> Score: {enriched.CreditScore}, Tier: {enriched.Tier}");
	}

	Console.WriteLine("Stream transform complete!");
	Console.WriteLine();
}

// ============================================================================
// Demo 4: Progress Reporting (IProgressDocumentHandler)
// ============================================================================
static async Task Demo4_ProgressReporting(IServiceProvider provider)
{
	Console.WriteLine("--- Demo 4: Progress Reporting ---");
	Console.WriteLine("Exporting report with progress updates...");

	var handler = provider.GetRequiredService<ReportExportHandler>();
	var document = new ReportDocument(10);

	// Create a progress reporter
	var progress = new Progress<DocumentProgress>(p =>
	{
		if (p.PercentComplete >= 0)
		{
			var bar = new string('=', (int)(p.PercentComplete / 5)) + new string(' ', 20 - (int)(p.PercentComplete / 5));
			Console.WriteLine($"  [{bar}] {p.PercentComplete,5:F1}% - {p.CurrentPhase}");
		}
		else
		{
			Console.WriteLine($"  [....working....] - {p.CurrentPhase} ({p.ItemsProcessed} items)");
		}
	});

	await handler.HandleAsync(document, progress, CancellationToken.None);

	Console.WriteLine("Progress reporting complete!");
	Console.WriteLine();
}

// ============================================================================
// Documents
// ============================================================================

public record CsvDocument(string Content) : IDispatchDocument;

public record ReportDocument(int PageCount) : IDispatchDocument;

// ============================================================================
// Data Types
// ============================================================================

public record DataRow(int Id, string Name, string Email) : IDispatchDocument;

public record EnrichedRow(DataRow Original, int CreditScore, string Tier);

// ============================================================================
// Handler Implementations
// ============================================================================

/// <summary>
/// IStreamingDocumentHandler: Parses a CSV document into individual rows.
/// </summary>
public class CsvStreamingHandler : IStreamingDocumentHandler<CsvDocument, DataRow>
{
	public async IAsyncEnumerable<DataRow> HandleAsync(
		CsvDocument document,
		[EnumeratorCancellation] CancellationToken cancellationToken)
	{
		var lines = document.Content.Split('\n', StringSplitOptions.RemoveEmptyEntries);
		var isHeader = true;

		foreach (var line in lines)
		{
			cancellationToken.ThrowIfCancellationRequested();

			if (isHeader)
			{
				isHeader = false;
				continue;
			}

			await Task.Delay(100, cancellationToken); // Simulate I/O
			var fields = line.Split(',');
			yield return new DataRow(
				int.Parse(fields[0].Trim()),
				fields[1].Trim(),
				fields[2].Trim());
		}
	}
}

/// <summary>
/// IStreamConsumerHandler: Consumes a stream of rows with batching.
/// </summary>
public class BatchImportHandler : IStreamConsumerHandler<DataRow>
{
	private const int BatchSize = 5;

	public async Task HandleAsync(
		IAsyncEnumerable<DataRow> documents,
		CancellationToken cancellationToken)
	{
		var batch = new List<DataRow>(BatchSize);

		await foreach (var row in documents.WithCancellation(cancellationToken))
		{
			batch.Add(row);

			if (batch.Count >= BatchSize)
			{
				Console.WriteLine($"  Flushing batch of {batch.Count} items: [{string.Join(", ", batch.Select(r => r.Name))}]");
				await Task.Delay(50, cancellationToken); // Simulate bulk insert
				batch.Clear();
			}
		}

		// Flush remaining
		if (batch.Count > 0)
		{
			Console.WriteLine($"  Flushing final batch of {batch.Count} items: [{string.Join(", ", batch.Select(r => r.Name))}]");
		}
	}
}

/// <summary>
/// IStreamTransformHandler: Enriches data records with additional information.
/// </summary>
public class RecordEnricher : IStreamTransformHandler<DataRow, EnrichedRow>
{
	public async IAsyncEnumerable<EnrichedRow> HandleAsync(
		IAsyncEnumerable<DataRow> input,
		[EnumeratorCancellation] CancellationToken cancellationToken)
	{
		await foreach (var row in input.WithCancellation(cancellationToken))
		{
			// Simulate external service call
			await Task.Delay(100, cancellationToken);

			var score = Random.Shared.Next(300, 850);
			var tier = score switch
			{
				>= 750 => "Premium",
				>= 650 => "Standard",
				_ => "Basic"
			};

			yield return new EnrichedRow(row, score, tier);
		}
	}
}

/// <summary>
/// IProgressDocumentHandler: Exports a report with progress reporting.
/// </summary>
public class ReportExportHandler : IProgressDocumentHandler<ReportDocument>
{
	public async Task HandleAsync(
		ReportDocument document,
		IProgress<DocumentProgress> progress,
		CancellationToken cancellationToken)
	{
		var totalPages = document.PageCount;

		// Phase 1: Initialization (0-10%)
		progress.Report(new DocumentProgress(0, 0, totalPages, "Initializing export"));
		await Task.Delay(200, cancellationToken);
		progress.Report(new DocumentProgress(10, 0, totalPages, "Export initialized"));

		// Phase 2: Processing pages (10-90%)
		for (int i = 0; i < totalPages; i++)
		{
			cancellationToken.ThrowIfCancellationRequested();

			await Task.Delay(150, cancellationToken); // Simulate page rendering

			var percent = 10 + (80.0 * (i + 1) / totalPages);
			progress.Report(DocumentProgress.FromItems(
				itemsProcessed: i + 1,
				totalItems: totalPages,
				currentPhase: $"Rendering page {i + 1} of {totalPages}"));
		}

		// Phase 3: Finalization (90-100%)
		progress.Report(new DocumentProgress(90, totalPages, totalPages, "Finalizing export"));
		await Task.Delay(200, cancellationToken);

		progress.Report(DocumentProgress.Completed(totalPages, "Export complete"));
	}
}
