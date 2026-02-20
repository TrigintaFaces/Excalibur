// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Diagnostics;

/// <summary>
/// Event IDs for streaming handler components (60000-60999).
/// </summary>
/// <remarks>
/// <para>Subcategory ranges:</para>
/// <list type="bullet">
/// <item>60000-60099: Streaming Handler Lifecycle</item>
/// <item>60100-60199: Document Processing</item>
/// <item>60200-60299: Chunk Production</item>
/// <item>60300-60399: Health Checks</item>
/// </list>
/// </remarks>
public static class StreamingHandlerEventId
{
	// ========================================
	// 60000-60099: Streaming Handler Lifecycle
	// ========================================

	/// <summary>Streaming handler started.</summary>
	public const int HandlerStarted = 60000;

	/// <summary>Streaming handler stopped.</summary>
	public const int HandlerStopped = 60001;

	/// <summary>Streaming handler registered.</summary>
	public const int HandlerRegistered = 60002;

	/// <summary>Streaming handler configuration loaded.</summary>
	public const int HandlerConfigurationLoaded = 60003;

	// ========================================
	// 60100-60199: Document Processing
	// ========================================

	/// <summary>Document processing started.</summary>
	public const int DocumentProcessingStarted = 60100;

	/// <summary>Document processing completed.</summary>
	public const int DocumentProcessingCompleted = 60101;

	/// <summary>Document processing failed.</summary>
	public const int DocumentProcessingFailed = 60102;

	/// <summary>Document processing retried.</summary>
	public const int DocumentProcessingRetried = 60103;

	/// <summary>Document skipped due to filter.</summary>
	public const int DocumentSkipped = 60104;

	// ========================================
	// 60200-60299: Chunk Production
	// ========================================

	/// <summary>Chunk produced from document.</summary>
	public const int ChunkProduced = 60200;

	/// <summary>Chunk production failed.</summary>
	public const int ChunkProductionFailed = 60201;

	/// <summary>All chunks produced for document.</summary>
	public const int AllChunksProduced = 60202;

	// ========================================
	// 60300-60399: Health Checks
	// ========================================

	/// <summary>Streaming handler health check passed.</summary>
	public const int HealthCheckPassed = 60300;

	/// <summary>Streaming handler health check degraded.</summary>
	public const int HealthCheckDegraded = 60301;

	/// <summary>Streaming handler health check failed.</summary>
	public const int HealthCheckFailed = 60302;
}
