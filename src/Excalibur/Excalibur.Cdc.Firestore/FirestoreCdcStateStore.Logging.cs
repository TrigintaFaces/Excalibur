// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Data.Firestore.Diagnostics;

using Microsoft.Extensions.Logging;

namespace Excalibur.Cdc.Firestore;

public sealed partial class FirestoreCdcStateStore
{
	// Source-generated logging methods

	[LoggerMessage(DataFirestoreEventId.CdcSavingPosition, LogLevel.Debug,
		"Saving CDC position for processor '{ProcessorName}'")]
	private partial void LogSavingPosition(string processorName);

	[LoggerMessage(DataFirestoreEventId.CdcGettingPosition, LogLevel.Debug,
		"Getting CDC position for processor '{ProcessorName}'")]
	private partial void LogGettingPosition(string processorName);

	[LoggerMessage(DataFirestoreEventId.CdcDeletingPosition, LogLevel.Debug,
		"Deleting CDC position for processor '{ProcessorName}'")]
	private partial void LogDeletingPosition(string processorName);

	[LoggerMessage(DataFirestoreEventId.CdcPositionNotFound, LogLevel.Debug,
		"No CDC position found for processor '{ProcessorName}'")]
	private partial void LogPositionNotFound(string processorName);

	[LoggerMessage(DataFirestoreEventId.CdcSavePositionContended, LogLevel.Warning,
		"CDC position for processor '{ProcessorName}' was not saved: the transaction could not complete because of concurrent activity ({Detail}). The next checkpoint supersedes it.")]
	private partial void LogSavePositionContended(string processorName, string detail);
}
