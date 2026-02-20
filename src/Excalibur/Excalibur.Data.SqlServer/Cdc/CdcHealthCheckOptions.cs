// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Data.SqlServer.Cdc;

/// <summary>
/// SQL Server-specific configuration options for the CDC processor health check.
/// Inherits general CDC health check thresholds from <see cref="Abstractions.CloudNative.CdcHealthCheckOptions"/>.
/// </summary>
public sealed class CdcHealthCheckOptions : Abstractions.CloudNative.CdcHealthCheckOptions;
