// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.SqlServer.Cdc;

/// <summary>
/// Provides default configuration values for database change data capture (CDC) operations.
/// </summary>
public static class DatabaseConfigDefaults
{
	/// <summary>
	/// Default batch time interval for CDC operations in milliseconds.
	/// </summary>
	public const int CdcDefaultBatchTimeInterval = 5000;

	/// <summary>
	/// Default queue size for CDC operations.
	/// </summary>
	public const int CdcDefaultQueueSize = 1000;

	/// <summary>
	/// Default batch size for CDC producer operations.
	/// </summary>
	public const int CdcDefaultProducerBatchSize = 100;

	/// <summary>
	/// Default batch size for CDC consumer operations.
	/// </summary>
	public const int CdcDefaultConsumerBatchSize = 10;

	/// <summary>
	/// Default setting for whether to stop CDC processing when a table handler is missing.
	/// </summary>
	public const bool CdcDefaultStopOnMissingTableHandler = true;

	/// <summary>
	/// Default capture instances for CDC operations.
	/// </summary>
	public static readonly string[] CdcDefaultCaptureInstances = [];
}
