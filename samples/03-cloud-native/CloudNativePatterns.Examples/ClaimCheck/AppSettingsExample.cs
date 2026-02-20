// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.CloudNativePatterns.Examples.ClaimCheck;

/// <summary>
/// Example appsettings.json structure.
/// </summary>
public sealed class AppSettingsExample
{
	public const string ExampleJson = /*lang=json,strict*/ """

	                                                       {
	                                                        "ClaimCheck": {
	                                                        "ConnectionString": "DefaultEndpointsProtocol=https;AccountName=mystorageaccount;AccountKey=...",
	                                                        "ContainerName": "claim-checks",
	                                                        "PayloadThreshold": 65536,
	                                                        "ChunkSize": 1048576,
	                                                        "MaxConcurrency": 4,
	                                                        "EnableCompression": true,
	                                                        "CompressionThreshold": 1024,
	                                                        "EnableCleanup": true,
	                                                        "CleanupInterval": "00:15:00",
	                                                        "RetentionPeriod": "7.00:00:00",
	                                                        "OperationTimeout": "00:05:00"
	                                                        }
	                                                       }
	                                                       """;
}
