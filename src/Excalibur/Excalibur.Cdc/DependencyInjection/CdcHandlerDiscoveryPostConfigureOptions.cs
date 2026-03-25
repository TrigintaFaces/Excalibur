// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Excalibur.Cdc;

/// <summary>
/// Post-configures <see cref="CdcOptions"/> by discovering all registered
/// <see cref="ICdcTableProvider"/> implementations and adding their declared
/// tables to <see cref="CdcOptions.TrackedTables"/>. Tables already registered
/// by code or configuration take precedence — duplicates by table name
/// (case-insensitive) are skipped.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage(
	"Performance",
	"CA1812:AvoidUninstantiatedInternalClasses",
	Justification = "Instantiated by the options infrastructure.")]
internal sealed class CdcHandlerDiscoveryPostConfigureOptions : IPostConfigureOptions<CdcOptions>
{
	private readonly IServiceProvider _serviceProvider;

	public CdcHandlerDiscoveryPostConfigureOptions(IServiceProvider serviceProvider)
	{
		_serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
	}

	public void PostConfigure(string? name, CdcOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		var providers = _serviceProvider.GetService<IEnumerable<ICdcTableProvider>>();
		if (providers is null)
		{
			return;
		}

		var discoveredTables = new List<CdcTableTrackingOptions>();
		foreach (var provider in providers)
		{
			var tableNames = provider.TableNames;
			if (tableNames is null)
			{
				continue;
			}

			foreach (var tableName in tableNames)
			{
				if (!string.IsNullOrEmpty(tableName))
				{
					discoveredTables.Add(new CdcTableTrackingOptions { TableName = tableName });
				}
			}
		}

		if (discoveredTables.Count > 0)
		{
			CdcTrackedTablesPostConfigureOptions.MergeTrackedTables(options.TrackedTables, discoveredTables);
		}
	}
}
