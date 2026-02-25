// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: Apache-2.0

using Excalibur.Data.SqlServer.Cdc;

namespace CdcEventStoreElasticsearch.AntiCorruption;

/// <summary>
/// Adapts legacy customer schema to domain model.
/// Handles schema evolution (column renames, type changes) as an Anti-Corruption Layer.
/// </summary>
/// <remarks>
/// <para>
/// The legacy database has evolved over time with different column naming conventions:
/// </para>
/// <list type="bullet">
/// <item>V1 (2015): CustId, CustomerName</item>
/// <item>V2 (2020): ExternalId, Name</item>
/// </list>
/// <para>
/// This adapter normalizes both schemas to a consistent domain model.
/// </para>
/// </remarks>
public sealed class LegacyCustomerAdapter
{
	/// <summary>
	/// Adapts a CDC data change event to a normalized customer model.
	/// </summary>
	/// <param name="changeEvent">The CDC change event from the legacy database.</param>
	/// <returns>The adapted customer data, or null if the event cannot be adapted.</returns>
	public AdaptedCustomerData? Adapt(DataChangeEvent changeEvent)
	{
		ArgumentNullException.ThrowIfNull(changeEvent);

		if (changeEvent.TableName != "LegacyCustomers")
		{
			return null;
		}

		var data = new AdaptedCustomerData
		{
			ChangeType = changeEvent.ChangeType
		};

		foreach (var change in changeEvent.Changes)
		{
			// Normalize column names from different schema versions
			var normalizedColumn = NormalizeColumnName(change.ColumnName);
			var value = changeEvent.ChangeType == DataChangeType.Delete
				? change.OldValue
				: change.NewValue;

			switch (normalizedColumn)
			{
				case "ExternalId":
					data.ExternalId = value?.ToString();
					break;
				case "Name":
					data.Name = value?.ToString();
					break;
				case "Email":
					data.Email = value?.ToString();
					break;
				case "Phone":
					data.Phone = value?.ToString();
					break;
			}

			// Track old values for update detection
			if (changeEvent.ChangeType == DataChangeType.Update && change.OldValue is not null)
			{
				switch (normalizedColumn)
				{
					case "Name":
						data.PreviousName = change.OldValue.ToString();
						break;
					case "Email":
						data.PreviousEmail = change.OldValue.ToString();
						break;
				}
			}
		}

		// Validate required fields
		if (string.IsNullOrWhiteSpace(data.ExternalId))
		{
			return null;
		}

		return data;
	}

	/// <summary>
	/// Normalizes column names from different schema versions.
	/// </summary>
	private static string NormalizeColumnName(string columnName) => columnName switch
	{
		// V1 schema (2015)
		"CustId" => "ExternalId",
		"CustomerName" => "Name",

		// V2 schema (2020) - current
		_ => columnName
	};
}

/// <summary>
/// Normalized customer data from the legacy system.
/// </summary>
public sealed class AdaptedCustomerData
{
	/// <summary>Gets or sets the type of change (Insert, Update, Delete).</summary>
	public DataChangeType ChangeType { get; set; }

	/// <summary>Gets or sets the external ID from the legacy system.</summary>
	public string? ExternalId { get; set; }

	/// <summary>Gets or sets the customer name.</summary>
	public string? Name { get; set; }

	/// <summary>Gets or sets the previous name (for updates).</summary>
	public string? PreviousName { get; set; }

	/// <summary>Gets or sets the email address.</summary>
	public string? Email { get; set; }

	/// <summary>Gets or sets the previous email (for updates).</summary>
	public string? PreviousEmail { get; set; }

	/// <summary>Gets or sets the phone number.</summary>
	public string? Phone { get; set; }
}
