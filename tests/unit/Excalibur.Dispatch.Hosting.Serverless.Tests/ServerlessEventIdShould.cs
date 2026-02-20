// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Hosting.Serverless;

namespace Excalibur.Dispatch.Hosting.Serverless.Tests;

/// <summary>
/// Unit tests for <see cref="ServerlessEventId"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
public sealed class ServerlessEventIdShould : UnitTestBase
{
	[Fact]
	public void HostProviderIds_AreInExpectedRange()
	{
		// Assert — 50000-50049
		ServerlessEventId.HostingServiceStarting.ShouldBeInRange(50000, 50049);
		ServerlessEventId.HostingServiceStopping.ShouldBeInRange(50000, 50049);
		ServerlessEventId.ProviderUnavailable.ShouldBeInRange(50000, 50049);
		ServerlessEventId.ProviderReady.ShouldBeInRange(50000, 50049);
		ServerlessEventId.ContextCreated.ShouldBeInRange(50000, 50049);
		ServerlessEventId.ExecutionStarted.ShouldBeInRange(50000, 50049);
		ServerlessEventId.ExecutionCompleted.ShouldBeInRange(50000, 50049);
		ServerlessEventId.ExecutionFailed.ShouldBeInRange(50000, 50049);
		ServerlessEventId.ExecutionTimedOut.ShouldBeInRange(50000, 50049);
		ServerlessEventId.ExecutionCancelled.ShouldBeInRange(50000, 50049);
	}

	[Fact]
	public void HostFactoryIds_AreInExpectedRange()
	{
		// Assert — 50050-50099
		ServerlessEventId.ProviderFactoryCreated.ShouldBeInRange(50050, 50099);
		ServerlessEventId.ProviderRegistered.ShouldBeInRange(50050, 50099);
		ServerlessEventId.ProviderResolved.ShouldBeInRange(50050, 50099);
		ServerlessEventId.ColdStartOptimizationEnabled.ShouldBeInRange(50050, 50099);
		ServerlessEventId.ColdStartOptimizationCompleted.ShouldBeInRange(50050, 50099);
		ServerlessEventId.PlatformSelected.ShouldBeInRange(50050, 50099);
		ServerlessEventId.PlatformFallback.ShouldBeInRange(50050, 50099);
		ServerlessEventId.UnableToDetectPlatform.ShouldBeInRange(50050, 50099);
	}

	[Fact]
	public void AllIds_AreUnique()
	{
		// Arrange
		var ids = new[]
		{
			ServerlessEventId.HostingServiceStarting,
			ServerlessEventId.HostingServiceStopping,
			ServerlessEventId.ProviderUnavailable,
			ServerlessEventId.ProviderReady,
			ServerlessEventId.ContextCreated,
			ServerlessEventId.ExecutionStarted,
			ServerlessEventId.ExecutionCompleted,
			ServerlessEventId.ExecutionFailed,
			ServerlessEventId.ExecutionTimedOut,
			ServerlessEventId.ExecutionCancelled,
			ServerlessEventId.ProviderFactoryCreated,
			ServerlessEventId.ProviderRegistered,
			ServerlessEventId.ProviderResolved,
			ServerlessEventId.ColdStartOptimizationEnabled,
			ServerlessEventId.ColdStartOptimizationCompleted,
			ServerlessEventId.PlatformSelected,
			ServerlessEventId.PlatformFallback,
			ServerlessEventId.UnableToDetectPlatform,
		};

		// Assert
		ids.Distinct().Count().ShouldBe(ids.Length);
	}
}
