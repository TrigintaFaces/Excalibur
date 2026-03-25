// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace Excalibur.Cdc;

/// <summary>
/// Post-configures <see cref="CdcOptions"/> by merging tracked tables
/// from an <see cref="IConfiguration"/> section. Tables already registered
/// by code (via <c>TrackTable</c>) take precedence — duplicates by
/// <see cref="CdcTableTrackingOptions.TableName"/> (case-insensitive)
/// are skipped.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage(
	"Performance",
	"CA1812:AvoidUninstantiatedInternalClasses",
	Justification = "Instantiated by the options infrastructure.")]
internal sealed class CdcTrackedTablesPostConfigureOptions : IPostConfigureOptions<CdcOptions>
{
	private readonly IConfiguration _configuration;
	private readonly string _sectionPath;

	public CdcTrackedTablesPostConfigureOptions(IConfiguration configuration, string sectionPath)
	{
		_configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
		_sectionPath = sectionPath ?? throw new ArgumentNullException(nameof(sectionPath));
	}

	public void PostConfigure(string? name, CdcOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		var section = _configuration.GetSection(_sectionPath);
		if (!section.Exists())
		{
			return;
		}

		var configTables = section.Get<List<CdcTableTrackingOptions>>();
		if (configTables is null || configTables.Count == 0)
		{
			return;
		}

		MergeTrackedTables(options.TrackedTables, configTables);
	}

	/// <summary>
	/// Merges source tables into the target list, skipping duplicates by TableName (case-insensitive).
	/// </summary>
	internal static void MergeTrackedTables(
		List<CdcTableTrackingOptions> target,
		IReadOnlyList<CdcTableTrackingOptions> source)
	{
		// Build a set of existing table names for O(1) lookup.
		var existingNames = new HashSet<string>(target.Count, StringComparer.OrdinalIgnoreCase);
		foreach (var table in target)
		{
			if (!string.IsNullOrEmpty(table.TableName))
			{
				_ = existingNames.Add(table.TableName);
			}
		}

		foreach (var table in source)
		{
			if (!string.IsNullOrEmpty(table.TableName) && existingNames.Add(table.TableName))
			{
				target.Add(table);
			}
		}
	}
}
