// Copyright (c) 2025 The Excalibur Project Authors
//
// Licensed under multiple licenses:
// - Excalibur License 1.0 (see LICENSE-EXCALIBUR.txt)
// - GNU Affero General Public License v3.0 or later (AGPL-3.0) (see LICENSE-AGPL-3.0.txt)
// - Server Side Public License v1.0 (SSPL-1.0) (see LICENSE-SSPL-1.0.txt)
// - Apache License 2.0 (see LICENSE-APACHE-2.0.txt)
//
// You may not use this file except in compliance with the License terms above. You may obtain copies of the licenses in
// the project root or online.
//
// Unless required by applicable law or agreed to in writing, software distributed under the License is distributed on
// an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.

namespace Excalibur.DataAccess.DataProcessing;

/// <summary>
///   Provides default configuration values for the <see cref="DataProcessor{TRecord}" /> class.
/// </summary>
/// <remarks>
///   These constants are intended to be used as default parameters in data processing classes, ensuring consistent
///   behavior across the application when custom values are not provided.
/// </remarks>
public static class DataProcessorDefaults
{
	public const string DataProcessorDefaultTableName = "DataProcessor.DataTaskRequests";
	public const int DataProcessorDefaultDispatcherTimeout = 60000;
	public const int DataProcessorDefaultMaxAttempts = 3;
	public const int DataProcessorDefaultQueueSize = 20000;
	public const int DataProcessorDefaultProducerBatchSize = 500;
	public const int DataProcessorDefaultConsumerBatchSize = 250;
}
