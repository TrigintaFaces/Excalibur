// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.DataProcessing;

/// <summary>
/// Provides default configuration values for the <see cref="DataProcessor{TRecord}" /> class.
/// </summary>
/// <remarks>
/// These constants are intended to be used as default parameters in data processing classes, ensuring consistent behavior across the
/// application when custom values are not provided.
/// </remarks>
public static class DataProcessorDefaults
{
	/// <summary>
	/// Default table name for data task requests.
	/// </summary>
	public const string DataProcessorDefaultTableName = "DataProcessor.DataTaskRequests";

	/// <summary>
	/// Default dispatcher timeout in milliseconds.
	/// </summary>
	public const int DataProcessorDefaultDispatcherTimeout = 60000;

	/// <summary>
	/// Default maximum number of processing attempts.
	/// </summary>
	public const int DataProcessorDefaultMaxAttempts = 3;

	/// <summary>
	/// Default queue size for data processing operations.
	/// </summary>
	public const int DataProcessorDefaultQueueSize = 5000;

	/// <summary>
	/// Default batch size for producer operations.
	/// </summary>
	public const int DataProcessorDefaultProducerBatchSize = 100;

	/// <summary>
	/// Default batch size for consumer operations.
	/// </summary>
	public const int DataProcessorDefaultConsumerBatchSize = 10;
}
