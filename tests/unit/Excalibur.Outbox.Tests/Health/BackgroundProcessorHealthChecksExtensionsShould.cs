// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Outbox.Health;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace Excalibur.Outbox.Tests.Health;

/// <summary>
/// Unit tests for <see cref="BackgroundProcessorHealthChecksExtensions"/>.
/// Verifies health check registration behavior.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Outbox")]
public sealed class BackgroundProcessorHealthChecksExtensionsShould
{
	#region AddOutboxHealthCheck Tests

	[Fact]
	public void ThrowArgumentNullException_WhenBuilderIsNull_ForOutbox()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			BackgroundProcessorHealthChecksExtensions.AddOutboxHealthCheck(null!));
	}

	[Fact]
	public void RegisterOutboxHealthCheck_WithDefaultName()
	{
		// Arrange
		var services = new ServiceCollection();
		var builder = services.AddHealthChecks();

		// Act
		builder.AddOutboxHealthCheck();
		var provider = services.BuildServiceProvider();
		var healthCheckOptions = provider.GetRequiredService<IOptions<HealthCheckServiceOptions>>();

		// Assert
		var registration = healthCheckOptions.Value.Registrations.SingleOrDefault(r => r.Name == "outbox");
		registration.ShouldNotBeNull();
	}

	[Fact]
	public void RegisterOutboxHealthCheck_WithCustomName()
	{
		// Arrange
		var services = new ServiceCollection();
		var builder = services.AddHealthChecks();

		// Act
		builder.AddOutboxHealthCheck(name: "custom-outbox");
		var provider = services.BuildServiceProvider();
		var healthCheckOptions = provider.GetRequiredService<IOptions<HealthCheckServiceOptions>>();

		// Assert
		var registration = healthCheckOptions.Value.Registrations.SingleOrDefault(r => r.Name == "custom-outbox");
		registration.ShouldNotBeNull();
	}

	[Fact]
	public void RegisterBackgroundServiceHealthState_ForOutbox()
	{
		// Arrange
		var services = new ServiceCollection();
		var builder = services.AddHealthChecks();

		// Act
		builder.AddOutboxHealthCheck();
		var provider = services.BuildServiceProvider();

		// Assert
		var state = provider.GetService<BackgroundServiceHealthState>();
		state.ShouldNotBeNull();
	}

	[Fact]
	public void ApplyOutboxConfiguration_WhenConfigureProvided()
	{
		// Arrange
		var services = new ServiceCollection();
		var builder = services.AddHealthChecks();

		// Act
		builder.AddOutboxHealthCheck(configure: options =>
		{
			options.DegradedFailureRatePercent = 10.0;
		});
		var provider = services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptions<OutboxHealthCheckOptions>>();

		// Assert
		options.Value.DegradedFailureRatePercent.ShouldBe(10.0);
	}

	[Fact]
	public void NotApplyConfiguration_WhenConfigureIsNull()
	{
		// Arrange
		var services = new ServiceCollection();
		var builder = services.AddHealthChecks();

		// Act
		builder.AddOutboxHealthCheck(configure: null);
		var provider = services.BuildServiceProvider();

		// Assert - should not throw and should use defaults
		var options = provider.GetRequiredService<IOptions<OutboxHealthCheckOptions>>();
		options.Value.ShouldNotBeNull();
	}

	[Fact]
	public void ReturnBuilder_ForChaining_Outbox()
	{
		// Arrange
		var services = new ServiceCollection();
		var builder = services.AddHealthChecks();

		// Act
		var result = builder.AddOutboxHealthCheck();

		// Assert
		result.ShouldBeSameAs(builder);
	}

	[Fact]
	public void AcceptTags_ForOutboxHealthCheck()
	{
		// Arrange
		var services = new ServiceCollection();
		var builder = services.AddHealthChecks();
		var tags = new[] { "outbox", "background" };

		// Act
		builder.AddOutboxHealthCheck(tags: tags);
		var provider = services.BuildServiceProvider();
		var healthCheckOptions = provider.GetRequiredService<IOptions<HealthCheckServiceOptions>>();

		// Assert
		var registration = healthCheckOptions.Value.Registrations.SingleOrDefault(r => r.Name == "outbox");
		registration.ShouldNotBeNull();
		registration.Tags.ShouldContain("outbox");
		registration.Tags.ShouldContain("background");
	}

	[Fact]
	public void AcceptFailureStatus_ForOutboxHealthCheck()
	{
		// Arrange
		var services = new ServiceCollection();
		var builder = services.AddHealthChecks();

		// Act
		builder.AddOutboxHealthCheck(failureStatus: HealthStatus.Degraded);
		var provider = services.BuildServiceProvider();
		var healthCheckOptions = provider.GetRequiredService<IOptions<HealthCheckServiceOptions>>();

		// Assert
		var registration = healthCheckOptions.Value.Registrations.SingleOrDefault(r => r.Name == "outbox");
		registration.ShouldNotBeNull();
		registration.FailureStatus.ShouldBe(HealthStatus.Degraded);
	}

	#endregion

	#region AddInboxHealthCheck Tests

	[Fact]
	public void ThrowArgumentNullException_WhenBuilderIsNull_ForInbox()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			BackgroundProcessorHealthChecksExtensions.AddInboxHealthCheck(null!));
	}

	[Fact]
	public void RegisterInboxHealthCheck_WithDefaultName()
	{
		// Arrange
		var services = new ServiceCollection();
		var builder = services.AddHealthChecks();

		// Act
		builder.AddInboxHealthCheck();
		var provider = services.BuildServiceProvider();
		var healthCheckOptions = provider.GetRequiredService<IOptions<HealthCheckServiceOptions>>();

		// Assert
		var registration = healthCheckOptions.Value.Registrations.SingleOrDefault(r => r.Name == "inbox");
		registration.ShouldNotBeNull();
	}

	[Fact]
	public void RegisterInboxHealthCheck_WithCustomName()
	{
		// Arrange
		var services = new ServiceCollection();
		var builder = services.AddHealthChecks();

		// Act
		builder.AddInboxHealthCheck(name: "custom-inbox");
		var provider = services.BuildServiceProvider();
		var healthCheckOptions = provider.GetRequiredService<IOptions<HealthCheckServiceOptions>>();

		// Assert
		var registration = healthCheckOptions.Value.Registrations.SingleOrDefault(r => r.Name == "custom-inbox");
		registration.ShouldNotBeNull();
	}

	[Fact]
	public void RegisterBackgroundServiceHealthState_ForInbox()
	{
		// Arrange
		var services = new ServiceCollection();
		var builder = services.AddHealthChecks();

		// Act
		builder.AddInboxHealthCheck();
		var provider = services.BuildServiceProvider();

		// Assert
		var state = provider.GetService<BackgroundServiceHealthState>();
		state.ShouldNotBeNull();
	}

	[Fact]
	public void ApplyInboxConfiguration_WhenConfigureProvided()
	{
		// Arrange
		var services = new ServiceCollection();
		var builder = services.AddHealthChecks();

		// Act
		builder.AddInboxHealthCheck(configure: options =>
		{
			options.DegradedInactivityTimeout = TimeSpan.FromMinutes(10);
		});
		var provider = services.BuildServiceProvider();
		var options = provider.GetRequiredService<IOptions<InboxHealthCheckOptions>>();

		// Assert
		options.Value.DegradedInactivityTimeout.ShouldBe(TimeSpan.FromMinutes(10));
	}

	[Fact]
	public void NotApplyConfiguration_WhenConfigureIsNull_ForInbox()
	{
		// Arrange
		var services = new ServiceCollection();
		var builder = services.AddHealthChecks();

		// Act
		builder.AddInboxHealthCheck(configure: null);
		var provider = services.BuildServiceProvider();

		// Assert - should not throw and should use defaults
		var options = provider.GetRequiredService<IOptions<InboxHealthCheckOptions>>();
		options.Value.ShouldNotBeNull();
	}

	[Fact]
	public void ReturnBuilder_ForChaining_Inbox()
	{
		// Arrange
		var services = new ServiceCollection();
		var builder = services.AddHealthChecks();

		// Act
		var result = builder.AddInboxHealthCheck();

		// Assert
		result.ShouldBeSameAs(builder);
	}

	[Fact]
	public void AcceptTags_ForInboxHealthCheck()
	{
		// Arrange
		var services = new ServiceCollection();
		var builder = services.AddHealthChecks();
		var tags = new[] { "inbox", "messaging" };

		// Act
		builder.AddInboxHealthCheck(tags: tags);
		var provider = services.BuildServiceProvider();
		var healthCheckOptions = provider.GetRequiredService<IOptions<HealthCheckServiceOptions>>();

		// Assert
		var registration = healthCheckOptions.Value.Registrations.SingleOrDefault(r => r.Name == "inbox");
		registration.ShouldNotBeNull();
		registration.Tags.ShouldContain("inbox");
		registration.Tags.ShouldContain("messaging");
	}

	[Fact]
	public void AcceptFailureStatus_ForInboxHealthCheck()
	{
		// Arrange
		var services = new ServiceCollection();
		var builder = services.AddHealthChecks();

		// Act
		builder.AddInboxHealthCheck(failureStatus: HealthStatus.Unhealthy);
		var provider = services.BuildServiceProvider();
		var healthCheckOptions = provider.GetRequiredService<IOptions<HealthCheckServiceOptions>>();

		// Assert
		var registration = healthCheckOptions.Value.Registrations.SingleOrDefault(r => r.Name == "inbox");
		registration.ShouldNotBeNull();
		registration.FailureStatus.ShouldBe(HealthStatus.Unhealthy);
	}

	#endregion

	#region Combined Tests

	[Fact]
	public void ShareBackgroundServiceHealthState_BetweenInboxAndOutbox()
	{
		// Arrange
		var services = new ServiceCollection();
		var builder = services.AddHealthChecks();

		// Act
		builder.AddOutboxHealthCheck();
		builder.AddInboxHealthCheck();
		var provider = services.BuildServiceProvider();

		// Assert - same singleton instance
		var state1 = provider.GetService<BackgroundServiceHealthState>();
		var state2 = provider.GetService<BackgroundServiceHealthState>();
		state1.ShouldBeSameAs(state2);
	}

	#endregion
}
