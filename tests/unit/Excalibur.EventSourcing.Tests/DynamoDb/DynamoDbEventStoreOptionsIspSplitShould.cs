// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.ComponentModel.DataAnnotations;

using Excalibur.EventSourcing.DynamoDb;

namespace Excalibur.EventSourcing.Tests.DynamoDb;

/// <summary>
/// Tests for DynamoDbEventStoreOptions ISP split (S560.49) -- verifies Throughput sub-option binding,
/// nested initializer syntax, backward-compatible shims, and DataAnnotations validation.
/// </summary>
[Trait("Category", "Unit")]
public sealed class DynamoDbEventStoreOptionsIspSplitShould
{
	#region Sub-Options Initialization

	[Fact]
	public void Throughput_SubOptions_IsInitializedByDefault()
	{
		var options = new DynamoDbEventStoreOptions();

		options.Throughput.ShouldNotBeNull();
	}

	[Fact]
	public void Throughput_SubOptions_HasCorrectDefaults()
	{
		var options = new DynamoDbEventStoreOptions();

		options.Throughput.ReadCapacityUnits.ShouldBe(5);
		options.Throughput.WriteCapacityUnits.ShouldBe(5);
		options.Throughput.UseOnDemandCapacity.ShouldBeTrue();
	}

	[Fact]
	public void DynamoDbThroughputOptions_IsSealed()
	{
		typeof(DynamoDbThroughputOptions).IsSealed.ShouldBeTrue(
			"Sub-option classes should be sealed for immutability");
	}

	#endregion

	#region Nested Initializer Syntax

	[Fact]
	public void NestedInitializer_SetsThroughputSubOptions()
	{
		var options = new DynamoDbEventStoreOptions
		{
			Throughput =
			{
				ReadCapacityUnits = 25,
				WriteCapacityUnits = 50,
				UseOnDemandCapacity = false
			}
		};

		options.Throughput.ReadCapacityUnits.ShouldBe(25);
		options.Throughput.WriteCapacityUnits.ShouldBe(50);
		options.Throughput.UseOnDemandCapacity.ShouldBeFalse();
	}

	[Fact]
	public void NestedInitializer_WithRootProperties_WorksTogether()
	{
		var options = new DynamoDbEventStoreOptions
		{
			EventsTableName = "ProdEvents",
			MaxBatchSize = 50,
			UseTransactionalWrite = false,
			Throughput =
			{
				ReadCapacityUnits = 100,
				WriteCapacityUnits = 200
			}
		};

		options.EventsTableName.ShouldBe("ProdEvents");
		options.MaxBatchSize.ShouldBe(50);
		options.UseTransactionalWrite.ShouldBeFalse();
		options.Throughput.ReadCapacityUnits.ShouldBe(100);
		options.Throughput.WriteCapacityUnits.ShouldBe(200);
	}

	#endregion

	#region Backward-Compatible Shims

	[Fact]
	public void Shim_ReadCapacityUnits_DelegatesToThroughput()
	{
		var options = new DynamoDbEventStoreOptions();

		// Set via shim
		options.ReadCapacityUnits = 25;

		// Read from Throughput
		options.Throughput.ReadCapacityUnits.ShouldBe(25);

		// Set via Throughput
		options.Throughput.ReadCapacityUnits = 50;

		// Read from shim
		options.ReadCapacityUnits.ShouldBe(50);
	}

	[Fact]
	public void Shim_WriteCapacityUnits_DelegatesToThroughput()
	{
		var options = new DynamoDbEventStoreOptions();

		// Set via shim
		options.WriteCapacityUnits = 30;

		// Read from Throughput
		options.Throughput.WriteCapacityUnits.ShouldBe(30);

		// Set via Throughput
		options.Throughput.WriteCapacityUnits = 60;

		// Read from shim
		options.WriteCapacityUnits.ShouldBe(60);
	}

	[Fact]
	public void Shim_UseOnDemandCapacity_DelegatesToThroughput()
	{
		var options = new DynamoDbEventStoreOptions();

		// Set via shim
		options.UseOnDemandCapacity = false;

		// Read from Throughput
		options.Throughput.UseOnDemandCapacity.ShouldBeFalse();

		// Set via Throughput
		options.Throughput.UseOnDemandCapacity = true;

		// Read from shim
		options.UseOnDemandCapacity.ShouldBeTrue();
	}

	[Fact]
	public void Shim_DefaultValues_MatchThroughputDefaults()
	{
		var options = new DynamoDbEventStoreOptions();

		// Shim defaults should mirror Throughput defaults
		options.ReadCapacityUnits.ShouldBe(options.Throughput.ReadCapacityUnits);
		options.WriteCapacityUnits.ShouldBe(options.Throughput.WriteCapacityUnits);
		options.UseOnDemandCapacity.ShouldBe(options.Throughput.UseOnDemandCapacity);
	}

	#endregion

	#region ISP Gate Compliance

	[Fact]
	public void Throughput_SubOptions_PropertyCount_ShouldBeWithinGate()
	{
		// ISP gate: sub-option class should have <= 10 properties
		var throughputType = typeof(DynamoDbThroughputOptions);
		var props = throughputType.GetProperties();

		props.Length.ShouldBeLessThanOrEqualTo(10,
			"DynamoDbThroughputOptions should have <= 10 properties per ISP gate");
	}

	#endregion

	#region DataAnnotations Validation

	[Fact]
	public void Throughput_ReadCapacityUnits_HasRangeAnnotation()
	{
		var prop = typeof(DynamoDbThroughputOptions).GetProperty(nameof(DynamoDbThroughputOptions.ReadCapacityUnits));
		var rangeAttr = prop.GetCustomAttributes(typeof(RangeAttribute), false).FirstOrDefault() as RangeAttribute;

		_ = rangeAttr.ShouldNotBeNull();
		rangeAttr.Minimum.ShouldBe(1);
	}

	[Fact]
	public void Throughput_WriteCapacityUnits_HasRangeAnnotation()
	{
		var prop = typeof(DynamoDbThroughputOptions).GetProperty(nameof(DynamoDbThroughputOptions.WriteCapacityUnits));
		var rangeAttr = prop.GetCustomAttributes(typeof(RangeAttribute), false).FirstOrDefault() as RangeAttribute;

		_ = rangeAttr.ShouldNotBeNull();
		rangeAttr.Minimum.ShouldBe(1);
	}

	[Fact]
	public void Throughput_Validation_RejectsInvalidReadCapacity()
	{
		var throughput = new DynamoDbThroughputOptions { ReadCapacityUnits = 0 };

		var results = new List<ValidationResult>();
		var isValid = Validator.TryValidateObject(throughput, new ValidationContext(throughput), results, true);

		isValid.ShouldBeFalse("ReadCapacityUnits of 0 should fail validation");
		results.ShouldContain(r => r.MemberNames.Contains(nameof(DynamoDbThroughputOptions.ReadCapacityUnits)));
	}

	[Fact]
	public void Throughput_Validation_RejectsInvalidWriteCapacity()
	{
		var throughput = new DynamoDbThroughputOptions { WriteCapacityUnits = 0 };

		var results = new List<ValidationResult>();
		var isValid = Validator.TryValidateObject(throughput, new ValidationContext(throughput), results, true);

		isValid.ShouldBeFalse("WriteCapacityUnits of 0 should fail validation");
		results.ShouldContain(r => r.MemberNames.Contains(nameof(DynamoDbThroughputOptions.WriteCapacityUnits)));
	}

	[Fact]
	public void Throughput_Validation_AcceptsValidOptions()
	{
		var throughput = new DynamoDbThroughputOptions
		{
			ReadCapacityUnits = 10,
			WriteCapacityUnits = 20,
			UseOnDemandCapacity = false
		};

		var results = new List<ValidationResult>();
		var isValid = Validator.TryValidateObject(throughput, new ValidationContext(throughput), results, true);

		isValid.ShouldBeTrue("Valid throughput options should pass validation");
	}

	#endregion
}
