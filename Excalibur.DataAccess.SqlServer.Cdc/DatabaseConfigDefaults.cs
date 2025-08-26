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

namespace Excalibur.DataAccess.SqlServer.Cdc;

public static class DatabaseConfigDefaults
{
	public const int CdcDefaultBatchTimeInterval = 5000;
	public const int CdcDefaultQueueSize = 20000;
	public const int CdcDefaultProducerBatchSize = 500;
	public const int CdcDefaultConsumerBatchSize = 250;
	public const bool CdcDefaultStopOnMissingTableHandler = true;

	public static readonly string[] CdcDefaultCaptureInstances = [];
}
