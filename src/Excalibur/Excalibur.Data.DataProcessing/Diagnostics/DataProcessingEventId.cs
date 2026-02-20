// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Data.DataProcessing.Diagnostics;

/// <summary>
/// Event IDs for data processing operations (107000-107999).
/// </summary>
/// <remarks>
/// <para>Subcategory ranges:</para>
/// <list type="bullet">
/// <item>107000-107099: Data Orchestration Manager</item>
/// <item>107100-107199: Data Processor Core</item>
/// <item>107200-107299: Data Processor Consumer</item>
/// <item>107300-107399: Data Processor Producer</item>
/// </list>
/// </remarks>
public static class DataProcessingEventId
{
	// ========================================
	// 107000-107099: Data Orchestration Manager
	// ========================================

	/// <summary>Processor not found for record type.</summary>
	public const int ProcessorNotFound = 107000;

	/// <summary>Error processing data task.</summary>
	public const int ProcessingDataTaskError = 107001;

	/// <summary>UpdateCompletedCount did not match any rows.</summary>
	public const int UpdateCompletedCountMismatch = 107002;

	// ========================================
	// 107100-107199: Data Processor Core
	// ========================================

	/// <summary>Disposing DataProcessor asynchronously.</summary>
	public const int DisposeAsync = 107100;

	/// <summary>Consumer not completed during async disposal.</summary>
	public const int ConsumerNotCompletedAsync = 107101;

	/// <summary>Consumer timeout during async disposal.</summary>
	public const int ConsumerTimeoutAsync = 107102;

	/// <summary>Error during async disposal.</summary>
	public const int DisposeAsyncError = 107103;

	/// <summary>Disposing DataProcessor synchronously.</summary>
	public const int DisposeSync = 107104;

	/// <summary>Consumer not completed during sync disposal.</summary>
	public const int ConsumerNotCompletedSync = 107105;

	/// <summary>Consumer timeout during sync disposal.</summary>
	public const int ConsumerTimeoutSync = 107106;

	/// <summary>Dispose error on shutdown.</summary>
	public const int DisposeError = 107107;

	// ========================================
	// 107200-107299: Data Processor Consumer
	// ========================================

	/// <summary>Consumer disposal requested.</summary>
	public const int ConsumerDisposalRequested = 107200;

	/// <summary>No more records, consumer exiting.</summary>
	public const int NoMoreRecordsConsumerExit = 107201;

	/// <summary>Queue is empty, waiting.</summary>
	public const int QueueEmptyWaiting = 107202;

	/// <summary>Processing batch of records.</summary>
	public const int ProcessingBatch = 107203;

	/// <summary>Error processing record.</summary>
	public const int ProcessingRecordError = 107204;

	/// <summary>Completed batch processing.</summary>
	public const int CompletedBatch = 107205;

	/// <summary>Completed all processing.</summary>
	public const int CompletedProcessing = 107206;

	/// <summary>Consumer canceled.</summary>
	public const int ConsumerCanceled = 107207;

	/// <summary>Error in consumer loop.</summary>
	public const int ConsumerError = 107208;

	/// <summary>No handler found for record type.</summary>
	public const int NoHandlerFound = 107209;

	// ========================================
	// 107300-107399: Data Processor Producer
	// ========================================

	/// <summary>No more records, producer exiting.</summary>
	public const int NoMoreRecordsProducerExit = 107300;

	/// <summary>Enqueuing records.</summary>
	public const int EnqueuingRecords = 107301;

	/// <summary>Successfully enqueued records.</summary>
	public const int SuccessfullyEnqueued = 107302;

	/// <summary>Producer canceled.</summary>
	public const int ProducerCanceled = 107303;

	/// <summary>Error in producer loop.</summary>
	public const int ProducerError = 107304;

	/// <summary>Producer completed.</summary>
	public const int ProducerCompleted = 107305;

	/// <summary>Application stopping, cancelling producer.</summary>
	public const int ApplicationStopping = 107306;

	/// <summary>Producer cancellation requested.</summary>
	public const int ProducerCancellationRequested = 107307;

	/// <summary>Waiting for consumer to finish.</summary>
	public const int WaitingForConsumer = 107308;
}
