using Excalibur.Dispatch.Compliance;

namespace Excalibur.Dispatch.Compliance.Tests.KeyRotation;

[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class KeyRotationOptionsShould
{
	[Fact]
	public void Be_enabled_by_default()
	{
		var options = new KeyRotationOptions();

		options.Enabled.ShouldBeTrue();
	}

	[Fact]
	public void Have_default_check_interval_of_one_hour()
	{
		var options = new KeyRotationOptions();

		options.CheckInterval.ShouldBe(TimeSpan.FromHours(1));
	}

	[Fact]
	public void Have_default_rotation_policy()
	{
		var options = new KeyRotationOptions();

		options.DefaultPolicy.ShouldNotBeNull();
		options.DefaultPolicy.Name.ShouldBe("Default");
	}

	[Fact]
	public void Have_empty_policies_by_purpose_dictionary()
	{
		var options = new KeyRotationOptions();

		options.PoliciesByPurpose.ShouldNotBeNull();
		options.PoliciesByPurpose.ShouldBeEmpty();
	}

	[Fact]
	public void Continue_on_error_by_default()
	{
		var options = new KeyRotationOptions();

		options.ContinueOnError.ShouldBeTrue();
	}

	[Fact]
	public void Have_default_max_concurrent_rotations_of_4()
	{
		var options = new KeyRotationOptions();

		options.MaxConcurrentRotations.ShouldBe(4);
	}

	[Fact]
	public void Have_default_rotation_timeout_of_5_minutes()
	{
		var options = new KeyRotationOptions();

		options.RotationTimeout.ShouldBe(TimeSpan.FromMinutes(5));
	}

	[Fact]
	public void Enable_metrics_by_default()
	{
		var options = new KeyRotationOptions();

		options.EnableMetrics.ShouldBeTrue();
	}

	[Fact]
	public void Retry_failed_rotations_by_default()
	{
		var options = new KeyRotationOptions();

		options.RetryFailedRotations.ShouldBeTrue();
	}

	[Fact]
	public void Have_default_max_retry_attempts_of_3()
	{
		var options = new KeyRotationOptions();

		options.MaxRetryAttempts.ShouldBe(3);
	}

	[Fact]
	public void Have_default_retry_delay_of_5_minutes()
	{
		var options = new KeyRotationOptions();

		options.RetryDelay.ShouldBe(TimeSpan.FromMinutes(5));
	}

	[Fact]
	public void Skip_locked_keys_by_default()
	{
		var options = new KeyRotationOptions();

		options.SkipLockedKeys.ShouldBeTrue();
	}

	[Fact]
	public void Have_default_lock_duration_of_10_minutes()
	{
		var options = new KeyRotationOptions();

		options.LockDuration.ShouldBe(TimeSpan.FromMinutes(10));
	}

	[Fact]
	public void Return_default_policy_when_purpose_is_null()
	{
		var options = new KeyRotationOptions();

		var policy = options.GetPolicyForPurpose(null);

		policy.ShouldBe(options.DefaultPolicy);
	}

	[Fact]
	public void Return_default_policy_when_purpose_not_found()
	{
		var options = new KeyRotationOptions();

		var policy = options.GetPolicyForPurpose("nonexistent");

		policy.ShouldBe(options.DefaultPolicy);
	}

	[Fact]
	public void Return_specific_policy_when_purpose_matches()
	{
		var options = new KeyRotationOptions();
		var customPolicy = new KeyRotationPolicy
		{
			Name = "Custom",
			MaxKeyAge = TimeSpan.FromDays(7)
		};
		options.AddPolicy("api-keys", customPolicy);

		var result = options.GetPolicyForPurpose("api-keys");

		result.ShouldBe(customPolicy);
	}

	[Fact]
	public void Match_purpose_case_insensitively()
	{
		var options = new KeyRotationOptions();
		var customPolicy = new KeyRotationPolicy
		{
			Name = "Custom",
			MaxKeyAge = TimeSpan.FromDays(14)
		};
		options.AddPolicy("API-Keys", customPolicy);

		var result = options.GetPolicyForPurpose("api-keys");

		result.ShouldBe(customPolicy);
	}

	[Fact]
	public void Add_policy_and_return_self_for_chaining()
	{
		var options = new KeyRotationOptions();
		var policy = new KeyRotationPolicy
		{
			Name = "Test",
			MaxKeyAge = TimeSpan.FromDays(60)
		};

		var result = options.AddPolicy("test-purpose", policy);

		result.ShouldBeSameAs(options);
		options.PoliciesByPurpose.ShouldContainKey("test-purpose");
	}

	[Fact]
	public void Add_high_security_policy_with_purpose()
	{
		var options = new KeyRotationOptions();

		var result = options.AddHighSecurityPolicy("payment-keys");

		result.ShouldBeSameAs(options);
		options.PoliciesByPurpose.ShouldContainKey("payment-keys");
		var policy = options.PoliciesByPurpose["payment-keys"];
		policy.Name.ShouldBe("HighSecurity");
		policy.Purpose.ShouldBe("payment-keys");
		policy.MaxKeyAge.ShouldBe(TimeSpan.FromDays(30));
		policy.RequireFipsCompliance.ShouldBeTrue();
	}

	[Fact]
	public void Add_archival_policy_with_purpose()
	{
		var options = new KeyRotationOptions();

		var result = options.AddArchivalPolicy("backup-keys");

		result.ShouldBeSameAs(options);
		options.PoliciesByPurpose.ShouldContainKey("backup-keys");
		var policy = options.PoliciesByPurpose["backup-keys"];
		policy.Name.ShouldBe("Archival");
		policy.Purpose.ShouldBe("backup-keys");
		policy.MaxKeyAge.ShouldBe(TimeSpan.FromDays(365));
	}

	[Fact]
	public void Override_existing_policy_for_same_purpose()
	{
		var options = new KeyRotationOptions();
		var firstPolicy = new KeyRotationPolicy
		{
			Name = "First",
			MaxKeyAge = TimeSpan.FromDays(30)
		};
		var secondPolicy = new KeyRotationPolicy
		{
			Name = "Second",
			MaxKeyAge = TimeSpan.FromDays(60)
		};

		options.AddPolicy("purpose", firstPolicy);
		options.AddPolicy("purpose", secondPolicy);

		options.PoliciesByPurpose["purpose"].ShouldBe(secondPolicy);
	}
}
