// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using CdcAntiCorruption.Models;

using Excalibur.Data.SqlServer.Cdc;

using Microsoft.Extensions.Logging;

namespace CdcAntiCorruption.SchemaAdapters;

/// <summary>
/// Adapts legacy customer schemas from the CDC source to the current domain model.
/// </summary>
/// <remarks>
/// <para>
/// This adapter handles schema evolution by supporting multiple legacy column names
/// and providing sensible defaults for missing fields. It implements the anti-corruption
/// layer pattern to isolate the domain model from external schema changes.
/// </para>
/// <para>
/// Supported schema versions:
/// <list type="bullet">
/// <item><description>V1: CustomerName, CustId, Email</description></item>
/// <item><description>V2: Name, ExternalId, Email, Phone</description></item>
/// <item><description>V3 (Current): Name, ExternalId, Email, Phone, IsActive</description></item>
/// </list>
/// </para>
/// </remarks>
public sealed class LegacyCustomerSchemaAdapter : ILegacyCustomerSchemaAdapter
{
	/// <summary>
	/// Column name mappings from legacy schemas to current schema.
	/// Maps legacy column names to their current equivalents.
	/// </summary>
	private static readonly Dictionary<string, string> ColumnMappings = new(StringComparer.OrdinalIgnoreCase)
	{
		// V1 legacy names → Current names
		["CustomerName"] = "Name",
		["CustId"] = "ExternalId",
		["CustomerEmail"] = "Email",
		["PhoneNumber"] = "Phone",
		["Active"] = "IsActive",
	};

	private readonly ILogger<LegacyCustomerSchemaAdapter> _logger;

	/// <summary>
	/// Initializes a new instance of the <see cref="LegacyCustomerSchemaAdapter"/> class.
	/// </summary>
	/// <param name="logger">The logger instance.</param>
	public LegacyCustomerSchemaAdapter(ILogger<LegacyCustomerSchemaAdapter> logger)
	{
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
	}

	/// <inheritdoc />
	public AdaptedCustomerData? Adapt(DataChangeEvent changeEvent)
	{
		ArgumentNullException.ThrowIfNull(changeEvent);

		if (!CanAdapt(changeEvent))
		{
			_logger.LogWarning(
				"Cannot adapt CDC event for table {TableName}: missing required fields",
				changeEvent.TableName);
			return null;
		}

		try
		{
			var externalId = GetValue<string>(changeEvent, "ExternalId", "CustId", "Id");
			var name = GetValue<string>(changeEvent, "Name", "CustomerName");
			var email = GetValue<string>(changeEvent, "Email", "CustomerEmail");

			if (string.IsNullOrEmpty(externalId) || string.IsNullOrEmpty(name))
			{
				_logger.LogWarning(
					"Cannot adapt CDC event: ExternalId or Name is missing for table {TableName}",
					changeEvent.TableName);
				return null;
			}

			return new AdaptedCustomerData
			{
				ExternalId = externalId,
				Name = name,
				Email = email ?? "unknown@legacy.system",
				Phone = GetValue<string>(changeEvent, "Phone", "PhoneNumber"),
				IsActive = GetValue<bool?>(changeEvent, "IsActive", "Active") ?? true,
				ChangedAt = changeEvent.CommitTime,
			};
		}
		catch (Exception ex)
		{
			_logger.LogError(
				ex,
				"Failed to adapt CDC event for table {TableName}",
				changeEvent.TableName);
			return null;
		}
	}

	/// <inheritdoc />
	public bool CanAdapt(DataChangeEvent changeEvent)
	{
		ArgumentNullException.ThrowIfNull(changeEvent);

		// Check if we have at least the minimum required fields
		var hasId = HasColumn(changeEvent, "ExternalId", "CustId", "Id");
		var hasName = HasColumn(changeEvent, "Name", "CustomerName");

		return hasId && hasName;
	}

	/// <summary>
	/// Gets a value from the change event, trying multiple column name aliases.
	/// </summary>
	/// <typeparam name="T">The expected type of the value.</typeparam>
	/// <param name="changeEvent">The change event containing the data.</param>
	/// <param name="columnNames">Column name aliases to try, in order of preference.</param>
	/// <returns>The value if found and convertible; otherwise, the default value for the type.</returns>
	private static T? GetValue<T>(DataChangeEvent changeEvent, params string[] columnNames)
	{
		foreach (var columnName in columnNames)
		{
			var normalizedName = NormalizeColumnName(columnName);
			var change = changeEvent.Changes.FirstOrDefault(c =>
				string.Equals(NormalizeColumnName(c.ColumnName), normalizedName, StringComparison.OrdinalIgnoreCase));

			if (change is not null)
			{
				var value = changeEvent.ChangeType == DataChangeType.Delete
					? change.OldValue
					: change.NewValue;

				if (value is not null)
				{
					return ConvertValue<T>(value);
				}
			}
		}

		return default;
	}

	/// <summary>
	/// Checks if the change event has any of the specified column names.
	/// </summary>
	private static bool HasColumn(DataChangeEvent changeEvent, params string[] columnNames)
	{
		foreach (var columnName in columnNames)
		{
			var normalizedName = NormalizeColumnName(columnName);
			if (changeEvent.Changes.Any(c =>
					string.Equals(NormalizeColumnName(c.ColumnName), normalizedName, StringComparison.OrdinalIgnoreCase)))
			{
				return true;
			}
		}

		return false;
	}

	/// <summary>
	/// Normalizes a column name by applying known mappings.
	/// </summary>
	private static string NormalizeColumnName(string columnName)
	{
		return ColumnMappings.TryGetValue(columnName, out var mapped) ? mapped : columnName;
	}

	/// <summary>
	/// Converts a value to the target type with appropriate handling.
	/// </summary>
	private static T? ConvertValue<T>(object value)
	{
		if (value is T typedValue)
		{
			return typedValue;
		}

		if (value is DBNull)
		{
			return default;
		}

		try
		{
			var targetType = Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T);

			if (targetType == typeof(bool) && value is int intValue)
			{
				return (T)(object)(intValue != 0);
			}

			if (targetType == typeof(bool) && value is string strValue)
			{
				return (T)(object)(strValue == "1" || strValue.Equals("true", StringComparison.OrdinalIgnoreCase));
			}

			return (T)Convert.ChangeType(value, targetType);
		}
		catch
		{
			return default;
		}
	}
}
