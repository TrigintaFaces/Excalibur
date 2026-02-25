// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.Firestore.Diagnostics;

using Microsoft.Extensions.Logging;

namespace Excalibur.Data.Firestore.Cdc;

public sealed partial class FirestoreCdcProcessor
{
	// Source-generated logging methods

	[LoggerMessage(DataFirestoreEventId.CdcStarting, LogLevel.Information,
		"Starting Firestore CDC processor '{ProcessorName}' for collection '{CollectionPath}'")]
	private partial void LogStarting(string processorName, string collectionPath);

	[LoggerMessage(DataFirestoreEventId.CdcStopping, LogLevel.Information,
		"Stopping Firestore CDC processor '{ProcessorName}'")]
	private partial void LogStopping(string processorName);

	[LoggerMessage(DataFirestoreEventId.CdcReceivedChanges, LogLevel.Debug,
		"CDC processor '{ProcessorName}' received {Count} document changes")]
	private partial void LogReceivedChanges(string processorName, int count);

	[LoggerMessage(DataFirestoreEventId.CdcProcessingChange, LogLevel.Debug,
		"Processing {ChangeType} event for document '{DocumentId}'")]
	private partial void LogProcessingChange(string changeType, string documentId);

	[LoggerMessage(DataFirestoreEventId.CdcConfirmingPosition, LogLevel.Debug,
		"Confirming position for processor '{ProcessorName}'")]
	private partial void LogConfirmingPosition(string processorName);

	[LoggerMessage(DataFirestoreEventId.CdcProcessingError, LogLevel.Error,
		"Error processing change for processor '{ProcessorName}' document '{DocumentId}'")]
	private partial void LogProcessingError(string processorName, string documentId, Exception ex);

	[LoggerMessage(DataFirestoreEventId.CdcResumingFromPosition, LogLevel.Information,
		"Resuming CDC processor '{ProcessorName}' from saved position")]
	private partial void LogResumingFromPosition(string processorName);

	[LoggerMessage(DataFirestoreEventId.CdcStartingFromBeginning, LogLevel.Information,
		"Starting CDC processor '{ProcessorName}' from beginning (no saved position)")]
	private partial void LogStartingFromBeginning(string processorName);

	[LoggerMessage(DataFirestoreEventId.CdcEventDropped, LogLevel.Warning,
		"CDC processor '{ProcessorName}' dropped event for document '{DocumentId}' — channel full")]
	private partial void LogEventDropped(string processorName, string documentId);
}
