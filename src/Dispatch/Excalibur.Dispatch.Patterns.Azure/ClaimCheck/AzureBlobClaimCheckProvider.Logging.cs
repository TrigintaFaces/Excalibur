// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Patterns.Diagnostics;

using Microsoft.Extensions.Logging;

namespace Excalibur.Dispatch.Patterns.ClaimCheck;

public partial class AzureBlobClaimCheckProvider
{
	// Source-generated logging methods (Sprint 373 - LoggerMessage.Define migration)

	[LoggerMessage(PatternsEventId.AzureBlobPayloadStored, LogLevel.Information,
		"Stored payload with claim check {ClaimCheckId}, size: {Size} bytes (original: {OriginalSize} bytes)")]
	private partial void LogStoredPayload(string claimCheckId, int size, int originalSize);

	[LoggerMessage(PatternsEventId.AzureBlobPayloadRetrieved, LogLevel.Information,
		"Retrieved payload with claim check {ClaimCheckId}, size: {Size} bytes")]
	private partial void LogRetrievedPayload(string claimCheckId, int size);

	[LoggerMessage(PatternsEventId.AzureBlobClaimCheckNotFound, LogLevel.Warning,
		"Claim check {ClaimCheckId} not found")]
	private partial void LogClaimCheckNotFound(string claimCheckId);

	[LoggerMessage(PatternsEventId.AzureBlobClaimCheckDeleted, LogLevel.Information,
		"Deleted claim check {ClaimCheckId}")]
	private partial void LogDeletedClaimCheck(string claimCheckId);

	[LoggerMessage(PatternsEventId.AzureBlobClaimCheckDeleteError, LogLevel.Error,
		"Error deleting claim check {ClaimCheckId}")]
	private partial void LogDeleteClaimCheckError(string claimCheckId, Exception? exception);
}
