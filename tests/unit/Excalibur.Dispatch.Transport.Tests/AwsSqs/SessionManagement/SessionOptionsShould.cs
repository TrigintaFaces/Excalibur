// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using AwsSessionOptions = Excalibur.Dispatch.Transport.Aws.SessionOptions;

namespace Excalibur.Dispatch.Transport.Tests.AwsSqs.SessionManagement;

[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
public sealed class SessionOptionsShould
{
	[Fact]
	public void HaveCorrectDefaults()
	{
		// Arrange & Act
		var options = new AwsSessionOptions();

		// Assert
		options.SessionTimeout.ShouldBe(TimeSpan.FromMinutes(5));
		options.LockRenewalInterval.ShouldBe(TimeSpan.FromSeconds(30));
		options.MaxIdleTime.ShouldBe(TimeSpan.FromMinutes(30));
		options.EnableAutoRenewal.ShouldBeTrue();
		options.MaxConcurrentSessions.ShouldBe(100);
		options.EnablePersistence.ShouldBeFalse();
		options.CleanupInterval.ShouldBe(TimeSpan.FromMinutes(10));
		options.ConnectionString.ShouldBeNull();
		options.KeyPrefix.ShouldBe("sessions:");
		options.DefaultExpiry.ShouldBe(TimeSpan.FromMinutes(30));
		options.LockTimeout.ShouldBe(TimeSpan.FromSeconds(30));
	}

	[Fact]
	public void AllowSettingAllProperties()
	{
		// Arrange & Act
		var options = new AwsSessionOptions
		{
			SessionTimeout = TimeSpan.FromMinutes(10),
			LockRenewalInterval = TimeSpan.FromSeconds(60),
			MaxIdleTime = TimeSpan.FromHours(1),
			EnableAutoRenewal = false,
			MaxConcurrentSessions = 50,
			EnablePersistence = true,
			CleanupInterval = TimeSpan.FromMinutes(5),
			ConnectionString = "redis://localhost:6379",
			KeyPrefix = "dispatch:sessions:",
			DefaultExpiry = TimeSpan.FromHours(1),
			LockTimeout = TimeSpan.FromMinutes(1),
		};

		// Assert
		options.SessionTimeout.ShouldBe(TimeSpan.FromMinutes(10));
		options.LockRenewalInterval.ShouldBe(TimeSpan.FromSeconds(60));
		options.MaxIdleTime.ShouldBe(TimeSpan.FromHours(1));
		options.EnableAutoRenewal.ShouldBeFalse();
		options.MaxConcurrentSessions.ShouldBe(50);
		options.EnablePersistence.ShouldBeTrue();
		options.CleanupInterval.ShouldBe(TimeSpan.FromMinutes(5));
		options.ConnectionString.ShouldBe("redis://localhost:6379");
		options.KeyPrefix.ShouldBe("dispatch:sessions:");
		options.DefaultExpiry.ShouldBe(TimeSpan.FromHours(1));
		options.LockTimeout.ShouldBe(TimeSpan.FromMinutes(1));
	}
}
