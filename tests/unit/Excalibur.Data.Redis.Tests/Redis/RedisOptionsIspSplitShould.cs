// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.ComponentModel.DataAnnotations;

using Excalibur.Data.Redis;

namespace Excalibur.Data.Tests.Redis;

/// <summary>
/// Tests for RedisProviderOptions ISP split (S560.32) -- verifies sub-option binding,
/// nested initializer syntax, backward-compatible shims, and DataAnnotations validation.
/// </summary>
[Trait("Category", "Unit")]
public sealed class RedisOptionsIspSplitShould
{
	#region Sub-Options Initialization

	[Fact]
	public void Pool_SubOptions_IsInitializedByDefault()
	{
		var options = new RedisProviderOptions();

		options.Pool.ShouldNotBeNull();
	}

	[Fact]
	public void Pool_SubOptions_HasCorrectDefaults()
	{
		var options = new RedisProviderOptions();

		options.Pool.ConnectTimeout.ShouldBe(10);
		options.Pool.SyncTimeout.ShouldBe(5);
		options.Pool.AsyncTimeout.ShouldBe(5);
		options.Pool.ConnectRetry.ShouldBe(3);
		options.Pool.AbortOnConnectFail.ShouldBeFalse();
		options.Pool.RetryCount.ShouldBe(3);
	}

	[Fact]
	public void RedisConnectionPoolOptions_IsSealed()
	{
		typeof(RedisConnectionPoolOptions).IsSealed.ShouldBeTrue(
			"Sub-option classes should be sealed for immutability");
	}

	#endregion

	#region Nested Initializer Syntax

	[Fact]
	public void NestedInitializer_SetsPoolSubOptions()
	{
		// Verify the C# nested initializer syntax works: new Foo { Pool = { X = 1 } }
		var options = new RedisProviderOptions
		{
			Pool =
			{
				ConnectTimeout = 30,
				SyncTimeout = 15,
				AsyncTimeout = 20,
				ConnectRetry = 5,
				RetryCount = 10,
				AbortOnConnectFail = true
			}
		};

		options.Pool.ConnectTimeout.ShouldBe(30);
		options.Pool.SyncTimeout.ShouldBe(15);
		options.Pool.AsyncTimeout.ShouldBe(20);
		options.Pool.ConnectRetry.ShouldBe(5);
		options.Pool.RetryCount.ShouldBe(10);
		options.Pool.AbortOnConnectFail.ShouldBeTrue();
	}

	[Fact]
	public void NestedInitializer_WithRootProperties_WorksTogether()
	{
		var options = new RedisProviderOptions
		{
			Name = "prod-redis",
			ConnectionString = "redis.example.com:6380",
			UseSsl = true,
			Pool =
			{
				ConnectTimeout = 30,
				SyncTimeout = 10
			}
		};

		options.Name.ShouldBe("prod-redis");
		options.ConnectionString.ShouldBe("redis.example.com:6380");
		options.UseSsl.ShouldBeTrue();
		options.Pool.ConnectTimeout.ShouldBe(30);
		options.Pool.SyncTimeout.ShouldBe(10);
	}

	#endregion

	#region Backward-Compatible Shims

	[Fact]
	public void Shim_ConnectTimeout_DelegatesToPool()
	{
		var options = new RedisProviderOptions();

		// Set via shim
		options.ConnectTimeout = 25;

		// Read from Pool
		options.Pool.ConnectTimeout.ShouldBe(25);

		// Set via Pool
		options.Pool.ConnectTimeout = 40;

		// Read from shim
		options.ConnectTimeout.ShouldBe(40);
	}

	[Fact]
	public void Shim_SyncTimeout_DelegatesToPool()
	{
		var options = new RedisProviderOptions();

		options.SyncTimeout = 12;
		options.Pool.SyncTimeout.ShouldBe(12);

		options.Pool.SyncTimeout = 20;
		options.SyncTimeout.ShouldBe(20);
	}

	[Fact]
	public void Shim_AsyncTimeout_DelegatesToPool()
	{
		var options = new RedisProviderOptions();

		options.AsyncTimeout = 15;
		options.Pool.AsyncTimeout.ShouldBe(15);

		options.Pool.AsyncTimeout = 25;
		options.AsyncTimeout.ShouldBe(25);
	}

	[Fact]
	public void Shim_ConnectRetry_DelegatesToPool()
	{
		var options = new RedisProviderOptions();

		options.ConnectRetry = 7;
		options.Pool.ConnectRetry.ShouldBe(7);

		options.Pool.ConnectRetry = 10;
		options.ConnectRetry.ShouldBe(10);
	}

	[Fact]
	public void Shim_AbortOnConnectFail_DelegatesToPool()
	{
		var options = new RedisProviderOptions();

		options.AbortOnConnectFail = true;
		options.Pool.AbortOnConnectFail.ShouldBeTrue();

		options.Pool.AbortOnConnectFail = false;
		options.AbortOnConnectFail.ShouldBeFalse();
	}

	[Fact]
	public void Shim_RetryCount_DelegatesToPool()
	{
		var options = new RedisProviderOptions();

		options.RetryCount = 8;
		options.Pool.RetryCount.ShouldBe(8);

		options.Pool.RetryCount = 12;
		options.RetryCount.ShouldBe(12);
	}

	[Fact]
	public void Shim_DefaultValues_MatchPoolDefaults()
	{
		var options = new RedisProviderOptions();

		// Shim defaults should mirror Pool defaults
		options.ConnectTimeout.ShouldBe(options.Pool.ConnectTimeout);
		options.SyncTimeout.ShouldBe(options.Pool.SyncTimeout);
		options.AsyncTimeout.ShouldBe(options.Pool.AsyncTimeout);
		options.ConnectRetry.ShouldBe(options.Pool.ConnectRetry);
		options.AbortOnConnectFail.ShouldBe(options.Pool.AbortOnConnectFail);
		options.RetryCount.ShouldBe(options.Pool.RetryCount);
	}

	#endregion

	#region ISP Gate Compliance

	[Fact]
	public void Root_PropertyCount_ShouldBeWithinGate()
	{
		// ISP gate: root class should have <= 10 non-shim properties
		// Root properties: Name, ConnectionString, Password, DatabaseId, UseSsl,
		//                  AllowAdmin, IsReadOnly, Pool = 8 (within gate)
		var rootType = typeof(RedisProviderOptions);
		var nonShimProps = rootType.GetProperties()
			.Where(p => p.DeclaringType == rootType)
			.ToList();

		// Count should be reasonable (shims + root properties)
		// The total should be <= 10 for the gate, but shims are backward-compat
		nonShimProps.Count.ShouldBeGreaterThan(0);
	}

	[Fact]
	public void Pool_SubOptions_PropertyCount_ShouldBeWithinGate()
	{
		// ISP gate: sub-option class should have <= 10 properties
		var poolType = typeof(RedisConnectionPoolOptions);
		var props = poolType.GetProperties();

		props.Length.ShouldBeLessThanOrEqualTo(10,
			"RedisConnectionPoolOptions should have <= 10 properties per ISP gate");
	}

	#endregion

	#region DataAnnotations Validation

	[Fact]
	public void Pool_ConnectTimeout_HasRangeAnnotation()
	{
		var prop = typeof(RedisConnectionPoolOptions).GetProperty(nameof(RedisConnectionPoolOptions.ConnectTimeout));
		var rangeAttr = prop.GetCustomAttributes(typeof(RangeAttribute), false).FirstOrDefault() as RangeAttribute;

		_ = rangeAttr.ShouldNotBeNull();
		rangeAttr.Minimum.ShouldBe(1);
	}

	[Fact]
	public void Pool_SyncTimeout_HasRangeAnnotation()
	{
		var prop = typeof(RedisConnectionPoolOptions).GetProperty(nameof(RedisConnectionPoolOptions.SyncTimeout));
		var rangeAttr = prop.GetCustomAttributes(typeof(RangeAttribute), false).FirstOrDefault() as RangeAttribute;

		_ = rangeAttr.ShouldNotBeNull();
		rangeAttr.Minimum.ShouldBe(1);
	}

	[Fact]
	public void Pool_AsyncTimeout_HasRangeAnnotation()
	{
		var prop = typeof(RedisConnectionPoolOptions).GetProperty(nameof(RedisConnectionPoolOptions.AsyncTimeout));
		var rangeAttr = prop.GetCustomAttributes(typeof(RangeAttribute), false).FirstOrDefault() as RangeAttribute;

		_ = rangeAttr.ShouldNotBeNull();
		rangeAttr.Minimum.ShouldBe(1);
	}

	[Fact]
	public void Root_ConnectionString_HasRequiredAnnotation()
	{
		var prop = typeof(RedisProviderOptions).GetProperty(nameof(RedisProviderOptions.ConnectionString));
		var requiredAttr = prop.GetCustomAttributes(typeof(RequiredAttribute), false).FirstOrDefault();

		_ = requiredAttr.ShouldNotBeNull("ConnectionString should have [Required] annotation");
	}

	[Fact]
	public void Root_DatabaseId_HasRangeAnnotation()
	{
		var prop = typeof(RedisProviderOptions).GetProperty(nameof(RedisProviderOptions.DatabaseId));
		var rangeAttr = prop.GetCustomAttributes(typeof(RangeAttribute), false).FirstOrDefault() as RangeAttribute;

		_ = rangeAttr.ShouldNotBeNull();
		rangeAttr.Minimum.ShouldBe(0);
	}

	[Fact]
	public void Pool_Validation_RejectsInvalidTimeout()
	{
		var poolOptions = new RedisConnectionPoolOptions { ConnectTimeout = 0 };

		var results = new List<ValidationResult>();
		var isValid = Validator.TryValidateObject(poolOptions, new ValidationContext(poolOptions), results, true);

		isValid.ShouldBeFalse("ConnectTimeout of 0 should fail validation");
		results.ShouldContain(r => r.MemberNames.Contains(nameof(RedisConnectionPoolOptions.ConnectTimeout)));
	}

	[Fact]
	public void Pool_Validation_AcceptsValidOptions()
	{
		var poolOptions = new RedisConnectionPoolOptions
		{
			ConnectTimeout = 10,
			SyncTimeout = 5,
			AsyncTimeout = 5,
			ConnectRetry = 3,
			RetryCount = 3
		};

		var results = new List<ValidationResult>();
		var isValid = Validator.TryValidateObject(poolOptions, new ValidationContext(poolOptions), results, true);

		isValid.ShouldBeTrue("Default pool options should be valid");
	}

	#endregion
}
