// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.ComponentModel.DataAnnotations;

namespace Excalibur.Cdc;

/// <summary>
/// Bindable configuration for a single CDC-tracked table.
/// </summary>
/// <remarks>
/// <para>
/// This is a pure configuration POCO (no behavior — no delegates, factories, or
/// <see cref="Type"/> maps) so it binds cleanly from
/// <see cref="Microsoft.Extensions.Configuration.IConfiguration"/> and is trim/AOT-safe.
/// </para>
/// <para>
/// It is the shared shape for both CDC configuration paths: the fluent builder's
/// <see cref="CdcTableTrackingOptions"/> derives from it, and the config-driven job path
/// (<c>DatabaseConfigs[].Tables</c>) binds to it directly. This guarantees handlers identify
/// tables by the same logical <see cref="TableName"/> regardless of how CDC was configured.
/// </para>
/// </remarks>
public class CdcTableConfig
{
	/// <summary>
	/// Gets or sets the logical (consumer-facing) table name, e.g. <c>Account</c> or <c>dbo.Account</c>.
	/// </summary>
	/// <value>The logical table name. This is the value handlers match via <c>IDataChangeHandler.TableNames</c>.</value>
	[Required]
	public string TableName { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the SQL Server CDC capture instance name (e.g. <c>dbo_Account</c>).
	/// </summary>
	/// <value>
	/// The capture instance name. When <see langword="null"/> or empty, the capture instance is
	/// assumed to equal <see cref="TableName"/>. Set this when the capture instance differs from
	/// the logical table name — for example the SQL Server default <c>{schema}_{table}</c>
	/// (<c>dbo_Account</c>) for a table whose logical name is <c>Account</c>.
	/// </value>
	public string? CaptureInstance { get; set; }
}
