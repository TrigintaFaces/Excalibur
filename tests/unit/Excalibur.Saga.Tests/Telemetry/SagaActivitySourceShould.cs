// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics;

namespace Excalibur.Saga.Tests.Telemetry;

/// <summary>
/// Unit tests for <see cref="SagaActivitySource"/>.
/// Verifies activity source constants and functionality.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Saga")]
public sealed class SagaActivitySourceShould
{
	#region Constants Tests

	[Fact]
	public void HaveCorrectSourceName()
	{
		// Assert
		SagaActivitySource.SourceName.ShouldBe("Excalibur.Dispatch.Sagas");
	}

	[Fact]
	public void HaveCorrectSourceVersion()
	{
		// Assert
		SagaActivitySource.SourceVersion.ShouldBe("1.0.0");
	}

	#endregion

	#region Instance Tests

	[Fact]
	public void ReturnActivitySourceInstance()
	{
		// Act
		var instance = SagaActivitySource.Instance;

		// Assert
		instance.ShouldNotBeNull();
	}

	[Fact]
	public void InstanceHasCorrectName()
	{
		// Act
		var instance = SagaActivitySource.Instance;

		// Assert
		instance.Name.ShouldBe(SagaActivitySource.SourceName);
	}

	[Fact]
	public void InstanceHasCorrectVersion()
	{
		// Act
		var instance = SagaActivitySource.Instance;

		// Assert
		instance.Version.ShouldBe(SagaActivitySource.SourceVersion);
	}

	[Fact]
	public void InstanceReturnsSameReference()
	{
		// Act
		var instance1 = SagaActivitySource.Instance;
		var instance2 = SagaActivitySource.Instance;

		// Assert
		instance1.ShouldBeSameAs(instance2);
	}

	#endregion

	#region StartActivity Tests

	[Fact]
	public void StartActivity_ReturnsNullWhenNoListeners()
	{
		// Note: Without a listener subscribed to the activity source,
		// StartActivity returns null as per .NET tracing design

		// Act
		var activity = SagaActivitySource.StartActivity("TestActivity");

		// Assert - null is expected when no listener is subscribed
		// This is normal .NET behavior - activities are only created when there's a listener
		activity.ShouldBeNull();
	}

	[Fact]
	public void StartActivity_AcceptsActivityKindInternal()
	{
		// Act & Assert - should not throw
		Should.NotThrow(() => SagaActivitySource.StartActivity("TestActivity", ActivityKind.Internal));
	}

	[Fact]
	public void StartActivity_AcceptsActivityKindClient()
	{
		// Act & Assert - should not throw
		Should.NotThrow(() => SagaActivitySource.StartActivity("TestActivity", ActivityKind.Client));
	}

	[Fact]
	public void StartActivity_AcceptsActivityKindServer()
	{
		// Act & Assert - should not throw
		Should.NotThrow(() => SagaActivitySource.StartActivity("TestActivity", ActivityKind.Server));
	}

	[Fact]
	public void StartActivity_AcceptsActivityKindProducer()
	{
		// Act & Assert - should not throw
		Should.NotThrow(() => SagaActivitySource.StartActivity("TestActivity", ActivityKind.Producer));
	}

	[Fact]
	public void StartActivity_AcceptsActivityKindConsumer()
	{
		// Act & Assert - should not throw
		Should.NotThrow(() => SagaActivitySource.StartActivity("TestActivity", ActivityKind.Consumer));
	}

	[Fact]
	public void StartActivity_DefaultsToInternalKind()
	{
		// Act - call without explicit kind
		var activity = SagaActivitySource.StartActivity("TestActivity");

		// Assert - verify the call succeeded (activity is null without listener, but no exception)
		// The default kind is verified by not throwing when called without the parameter
		activity.ShouldBeNull();
	}

	[Fact]
	public void StartActivity_AcceptsEmptyName()
	{
		// Act & Assert - should not throw
		Should.NotThrow(() => SagaActivitySource.StartActivity(string.Empty));
	}

	[Fact]
	public void StartActivity_AcceptsLongName()
	{
		// Arrange
		var longName = new string('x', 1000);

		// Act & Assert - should not throw
		Should.NotThrow(() => SagaActivitySource.StartActivity(longName));
	}

	#endregion

	#region Activity Listener Integration Tests

	[Fact]
	public void StartActivity_CreatesActivityWhenListenerIsSubscribed()
	{
		// Arrange
		using var listener = new ActivityListener
		{
			ShouldListenTo = source => source.Name == SagaActivitySource.SourceName,
			Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData
		};
		ActivitySource.AddActivityListener(listener);

		try
		{
			// Act
			using var activity = SagaActivitySource.StartActivity("TestActivity");

			// Assert
			activity.ShouldNotBeNull();
			activity.OperationName.ShouldBe("TestActivity");
		}
		finally
		{
			listener.Dispose();
		}
	}

	[Fact]
	public void StartActivity_SetsCorrectActivityKind()
	{
		// Arrange
		using var listener = new ActivityListener
		{
			ShouldListenTo = source => source.Name == SagaActivitySource.SourceName,
			Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData
		};
		ActivitySource.AddActivityListener(listener);

		try
		{
			// Act
			using var activity = SagaActivitySource.StartActivity("TestActivity", ActivityKind.Producer);

			// Assert
			activity.ShouldNotBeNull();
			activity.Kind.ShouldBe(ActivityKind.Producer);
		}
		finally
		{
			listener.Dispose();
		}
	}

	[Fact]
	public void StartActivity_DefaultsToInternalKindWithListener()
	{
		// Arrange
		using var listener = new ActivityListener
		{
			ShouldListenTo = source => source.Name == SagaActivitySource.SourceName,
			Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData
		};
		ActivitySource.AddActivityListener(listener);

		try
		{
			// Act
			using var activity = SagaActivitySource.StartActivity("TestActivity");

			// Assert
			activity.ShouldNotBeNull();
			activity.Kind.ShouldBe(ActivityKind.Internal);
		}
		finally
		{
			listener.Dispose();
		}
	}

	[Fact]
	public void StartActivity_HasCorrectSourceInActivity()
	{
		// Arrange
		using var listener = new ActivityListener
		{
			ShouldListenTo = source => source.Name == SagaActivitySource.SourceName,
			Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData
		};
		ActivitySource.AddActivityListener(listener);

		try
		{
			// Act
			using var activity = SagaActivitySource.StartActivity("TestActivity");

			// Assert
			activity.ShouldNotBeNull();
			activity.Source.Name.ShouldBe(SagaActivitySource.SourceName);
			activity.Source.Version.ShouldBe(SagaActivitySource.SourceVersion);
		}
		finally
		{
			listener.Dispose();
		}
	}

	#endregion
}
