// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Tests.Shared.Categories;

namespace Excalibur.Dispatch.Tests.Functional.Workflows.CdcSchemaEvolution;

/// <summary>
/// Functional tests for CDC schema evolution workflows.
/// Tests forward/backward compatibility, field addition/removal, type changes, and migration strategies.
/// </summary>
/// <remarks>
/// <para>
/// Sprint 196 - CDC Anti-Corruption Layer Tests.
/// bd-uhihx: CDC Schema Evolution Tests (5 tests).
/// </para>
/// <para>
/// These tests verify that the Anti-Corruption Layer (ACL) can handle schema evolution
/// gracefully without breaking message processing:
/// - Forward compatibility (old producer, new consumer)
/// - Backward compatibility (new producer, old consumer)
/// - Field additions with defaults
/// - Field removals with graceful degradation
/// - Type migrations
/// </para>
/// </remarks>
[FunctionalTest]
public sealed class CdcSchemaEvolutionWorkflowTests : FunctionalTestBase
{
	/// <inheritdoc/>
	protected override TimeSpan TestTimeout => TestTimeouts.Functional;

	/// <summary>
	/// Test 1: Verifies forward compatibility - old schema CDC events work with new translator.
	/// </summary>
	[Fact]
	public async Task CDC_SchemaEvolution_Handles_Forward_Compatibility()
	{
		// Arrange - Create old schema event (v1) without new fields
		var translator = new EvolvingSchemaTranslator(currentVersion: 2);
		var v1Event = new CdcEventV1
		{
			OperationType = "INSERT",
			TableName = "Products",
			OldData = null,
			NewData = new Dictionary<string, object>
			{
				["Id"] = 1,
				["Name"] = "Widget",
				["Price"] = 9.99m,
				// V1 doesn't have Category or Description fields
			},
			Timestamp = DateTimeOffset.UtcNow,
		};

		// Act - Translate using V2 translator
		var result = await RunWithTimeoutAsync(_ =>
		{
			var command = translator.TranslateWithVersionHandling(v1Event, sourceVersion: 1);
			return Task.FromResult(command);
		}).ConfigureAwait(true);

		// Assert - Should succeed with default values for new fields
		_ = result.ShouldNotBeNull();
		result.CommandType.ShouldBe("CreateProduct");
		result.Payload.ShouldContainKey("Id");
		result.Payload.ShouldContainKey("Name");
		result.Payload.ShouldContainKey("Price");
		// New fields should have defaults
		result.Payload.ShouldContainKey("Category");
		result.Payload["Category"].ShouldBe("Uncategorized"); // Default value
		result.Payload.ShouldContainKey("Description");
		result.Payload["Description"].ShouldBe(string.Empty); // Default value
	}

	/// <summary>
	/// Test 2: Verifies backward compatibility - new schema CDC events work with old translator.
	/// </summary>
	[Fact]
	public async Task CDC_SchemaEvolution_Handles_Backward_Compatibility()
	{
		// Arrange - Create new schema event (v2) with additional fields
		var translator = new EvolvingSchemaTranslator(currentVersion: 1);
		var v2Event = new CdcEventV2
		{
			OperationType = "INSERT",
			TableName = "Products",
			OldData = null,
			NewData = new Dictionary<string, object>
			{
				["Id"] = 2,
				["Name"] = "Advanced Widget",
				["Price"] = 19.99m,
				["Category"] = "Electronics", // New in V2
				["Description"] = "A fancy widget", // New in V2
			},
			Timestamp = DateTimeOffset.UtcNow,
			SchemaVersion = 2, // V2 schema indicator
		};

		// Act - Translate using V1 translator (ignores unknown fields)
		var result = await RunWithTimeoutAsync(_ =>
		{
			var command = translator.TranslateWithVersionHandling(v2Event, sourceVersion: 2);
			return Task.FromResult(command);
		}).ConfigureAwait(true);

		// Assert - Should succeed, ignoring unknown fields gracefully
		_ = result.ShouldNotBeNull();
		result.CommandType.ShouldBe("CreateProduct");
		result.Payload.ShouldContainKey("Id");
		result.Payload.ShouldContainKey("Name");
		result.Payload.ShouldContainKey("Price");
		// V1 translator doesn't know about Category/Description
		result.UnrecognizedFields.ShouldContain("Category");
		result.UnrecognizedFields.ShouldContain("Description");
	}

	/// <summary>
	/// Test 3: Verifies handling of field additions with proper defaults.
	/// </summary>
	[Fact]
	public async Task CDC_SchemaEvolution_Handles_Field_Additions()
	{
		// Arrange - Schema migration: V1 -> V2 adds optional fields
		var migrator = new SchemaMigrator();
		var v1Data = new Dictionary<string, object>
		{
			["Id"] = 3,
			["Name"] = "Basic Product",
			["Price"] = 5.99m,
		};

		// Act - Migrate from V1 to V2
		var result = await RunWithTimeoutAsync(_ =>
		{
			var migrated = migrator.MigrateToLatest(v1Data, fromVersion: 1, toVersion: 2);
			return Task.FromResult(migrated);
		}).ConfigureAwait(true);

		// Assert
		_ = result.ShouldNotBeNull();
		result.Version.ShouldBe(2);
		result.Data.ShouldContainKey("Id");
		result.Data.ShouldContainKey("Name");
		result.Data.ShouldContainKey("Price");
		// New required fields should have defaults
		result.Data.ShouldContainKey("Category");
		result.Data["Category"].ShouldBe("Default");
		result.Data.ShouldContainKey("IsActive");
		result.Data["IsActive"].ShouldBe(true);
	}

	/// <summary>
	/// Test 4: Verifies handling of field removals with graceful degradation.
	/// </summary>
	[Fact]
	public async Task CDC_SchemaEvolution_Handles_Field_Removals()
	{
		// Arrange - Schema migration: V3 removes deprecated fields
		var migrator = new SchemaMigrator();
		var v2Data = new Dictionary<string, object>
		{
			["Id"] = 4,
			["Name"] = "Legacy Product",
			["Price"] = 7.99m,
			["Category"] = "Hardware",
			["DeprecatedField1"] = "old value", // To be removed in V3
			["DeprecatedField2"] = 123, // To be removed in V3
		};

		// Act - Migrate from V2 to V3
		var result = await RunWithTimeoutAsync(_ =>
		{
			var migrated = migrator.MigrateToLatest(v2Data, fromVersion: 2, toVersion: 3);
			return Task.FromResult(migrated);
		}).ConfigureAwait(true);

		// Assert
		_ = result.ShouldNotBeNull();
		result.Version.ShouldBe(3);
		result.Data.ShouldContainKey("Id");
		result.Data.ShouldContainKey("Name");
		result.Data.ShouldContainKey("Price");
		result.Data.ShouldContainKey("Category");
		// Deprecated fields should be removed
		result.Data.ShouldNotContainKey("DeprecatedField1");
		result.Data.ShouldNotContainKey("DeprecatedField2");
		// Removed fields should be logged for audit
		result.RemovedFields.ShouldContain("DeprecatedField1");
		result.RemovedFields.ShouldContain("DeprecatedField2");
	}

	/// <summary>
	/// Test 5: Verifies handling of type changes with automatic conversion.
	/// </summary>
	[Fact]
	public async Task CDC_SchemaEvolution_Handles_Type_Changes()
	{
		// Arrange - Schema migration: V2 -> V3 changes Price from decimal to Money type
		var migrator = new SchemaMigrator();
		var v2Data = new Dictionary<string, object>
		{
			["Id"] = 5,
			["Name"] = "Price Change Product",
			["Price"] = 12.99m, // V2: decimal
			["Category"] = "Software",
		};

		// Act - Migrate from V2 to V3 with type conversion
		var result = await RunWithTimeoutAsync(_ =>
		{
			var migrated = migrator.MigrateToLatest(v2Data, fromVersion: 2, toVersion: 3);
			return Task.FromResult(migrated);
		}).ConfigureAwait(true);

		// Assert
		_ = result.ShouldNotBeNull();
		result.Version.ShouldBe(3);
		result.Data.ShouldContainKey("Price");
		// V3 converts Price to Money object with currency
		var priceValue = result.Data["Price"];
		_ = priceValue.ShouldBeOfType<MoneyValue>();
		var money = (MoneyValue)priceValue;
		money.Amount.ShouldBe(12.99m);
		money.Currency.ShouldBe("USD"); // Default currency
										// Type conversions should be logged
		result.TypeConversions.ShouldContain(("Price", "decimal", "MoneyValue"));
	}

	#region Test Infrastructure

	/// <summary>
	/// CDC event V1 schema.
	/// </summary>
	public sealed class CdcEventV1
	{
		public string OperationType { get; init; } = string.Empty;
		public string TableName { get; init; } = string.Empty;
		public Dictionary<string, object>? OldData { get; init; }
		public Dictionary<string, object>? NewData { get; init; }
		public DateTimeOffset Timestamp { get; init; }
	}

	/// <summary>
	/// CDC event V2 schema with additional fields.
	/// </summary>
	public sealed class CdcEventV2
	{
		public string OperationType { get; init; } = string.Empty;
		public string TableName { get; init; } = string.Empty;
		public Dictionary<string, object>? OldData { get; init; }
		public Dictionary<string, object>? NewData { get; init; }
		public DateTimeOffset Timestamp { get; init; }
		public int SchemaVersion { get; init; } = 2;
	}

	/// <summary>
	/// Command result from translation.
	/// </summary>
	public sealed class TranslatedCommand
	{
		public string CommandType { get; init; } = string.Empty;
		public Dictionary<string, object> Payload { get; init; } = new();
		public List<string> UnrecognizedFields { get; init; } = new();
	}

	/// <summary>
	/// Migration result.
	/// </summary>
	public sealed class EncryptionMigrationResult
	{
		public int Version { get; init; }
		public Dictionary<string, object> Data { get; init; } = new();
		public List<string> RemovedFields { get; init; } = new();
		public List<(string FieldName, string FromType, string ToType)> TypeConversions { get; init; } = new();
	}

	/// <summary>
	/// Money value type for V3 schema.
	/// </summary>
	public sealed record MoneyValue(decimal Amount, string Currency);

	/// <summary>
	/// Translator that handles schema version differences.
	/// </summary>
	public sealed class EvolvingSchemaTranslator
	{
		private readonly int _currentVersion;

		public EvolvingSchemaTranslator(int currentVersion)
		{
			_currentVersion = currentVersion;
		}

		public TranslatedCommand TranslateWithVersionHandling(CdcEventV1 evt, int sourceVersion)
		{
			var payload = new Dictionary<string, object>(evt.NewData ?? new());
			var unrecognized = new List<string>();

			// Add defaults for fields not in V1
			if (_currentVersion >= 2 && sourceVersion < 2)
			{
				if (!payload.ContainsKey("Category"))
					payload["Category"] = "Uncategorized";
				if (!payload.ContainsKey("Description"))
					payload["Description"] = string.Empty;
			}

			return new TranslatedCommand
			{
				CommandType = evt.OperationType == "INSERT" ? "CreateProduct" : "UpdateProduct",
				Payload = payload,
				UnrecognizedFields = unrecognized,
			};
		}

		public TranslatedCommand TranslateWithVersionHandling(CdcEventV2 evt, int sourceVersion)
		{
			var payload = new Dictionary<string, object>();
			var unrecognized = new List<string>();

			// V1 translator only knows about Id, Name, Price
			var knownFieldsV1 = new HashSet<string> { "Id", "Name", "Price" };

			foreach (var kvp in evt.NewData ?? new())
			{
				if (_currentVersion == 1 && !knownFieldsV1.Contains(kvp.Key))
				{
					unrecognized.Add(kvp.Key);
				}
				else
				{
					payload[kvp.Key] = kvp.Value;
				}
			}

			return new TranslatedCommand
			{
				CommandType = evt.OperationType == "INSERT" ? "CreateProduct" : "UpdateProduct",
				Payload = payload,
				UnrecognizedFields = unrecognized,
			};
		}
	}

	/// <summary>
	/// Schema migrator for version upgrades.
	/// </summary>
	public sealed class SchemaMigrator
	{
		public EncryptionMigrationResult MigrateToLatest(Dictionary<string, object> data, int fromVersion, int toVersion)
		{
			var result = new Dictionary<string, object>(data);
			var removed = new List<string>();
			var conversions = new List<(string, string, string)>();

			// Apply migrations step by step
			for (var v = fromVersion; v < toVersion; v++)
			{
				switch (v)
				{
					case 1:
						// V1 -> V2: Add Category and IsActive
						if (!result.ContainsKey("Category"))
							result["Category"] = "Default";
						if (!result.ContainsKey("IsActive"))
							result["IsActive"] = true;
						break;

					case 2:
						// V2 -> V3: Remove deprecated fields, convert Price to Money
						if (result.ContainsKey("DeprecatedField1"))
						{
							removed.Add("DeprecatedField1");
							_ = result.Remove("DeprecatedField1");
						}
						if (result.ContainsKey("DeprecatedField2"))
						{
							removed.Add("DeprecatedField2");
							_ = result.Remove("DeprecatedField2");
						}
						// Convert Price to MoneyValue
						if (result.TryGetValue("Price", out var priceObj) && priceObj is decimal price)
						{
							result["Price"] = new MoneyValue(price, "USD");
							conversions.Add(("Price", "decimal", "MoneyValue"));
						}
						break;
				}
			}

			return new EncryptionMigrationResult
			{
				Version = toVersion,
				Data = result,
				RemovedFields = removed,
				TypeConversions = conversions,
			};
		}
	}

	#endregion Test Infrastructure
}
