// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.CompilerServices;

using Excalibur.Data.SqlServer.Cdc;

namespace Excalibur.Data.Tests.SqlServer.Cdc;

/// <summary>
/// Tests verifying CdcProcessor thread-safety fixes (S543.10-13):
/// - ConcurrentBag&lt;Task&gt; for background task tracking (S543.10)
/// - ConcurrentDictionary for _tracking (S543.11)
/// - volatile bool _isRunning (S543.12)
/// - CancellationTokenSource disposal timeout (S543.13)
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Data.SqlServer")]
[Trait("Feature", "CDC")]
public sealed class CdcProcessorThreadSafetyShould : UnitTestBase
{
	#region S543.10: ConcurrentBag<Task> Background Task Tracking

	[Fact]
	public void HaveBackgroundTasksBag()
	{
		// Arrange
		var field = typeof(CdcProcessor)
			.GetField("_backgroundTasks", BindingFlags.NonPublic | BindingFlags.Instance);

		// Assert
		field.ShouldNotBeNull("CdcProcessor should have _backgroundTasks field");
		field.FieldType.ShouldBe(typeof(ConcurrentBag<Task>));
	}

	#endregion

	#region S543.11: ConcurrentDictionary for Tracking

	[Fact]
	public void UseConcurrentDictionaryForTracking()
	{
		// Arrange
		var field = typeof(CdcProcessor)
			.GetField("_tracking", BindingFlags.NonPublic | BindingFlags.Instance);

		// Assert
		field.ShouldNotBeNull("CdcProcessor should have _tracking field");

		// Verify it's a ConcurrentDictionary (with string key)
		field.FieldType.IsGenericType.ShouldBeTrue();
		field.FieldType.GetGenericTypeDefinition().ShouldBe(typeof(ConcurrentDictionary<,>));
		field.FieldType.GetGenericArguments()[0].ShouldBe(typeof(string));
	}

	#endregion

	#region S543.12: Volatile _isRunning

	[Fact]
	public void HaveVolatileIsRunningField()
	{
		// Arrange
		var field = typeof(CdcProcessor)
			.GetField("_isRunning", BindingFlags.NonPublic | BindingFlags.Instance);

		// Assert
		field.ShouldNotBeNull("CdcProcessor should have _isRunning field");
		field.FieldType.ShouldBe(typeof(bool));

		// Verify volatile modifier
		var requiredModifiers = field.GetRequiredCustomModifiers();
		requiredModifiers.ShouldContain(typeof(IsVolatile),
			"_isRunning field should be marked volatile for thread-safe cross-thread reads");
	}

	[Fact]
	public void HaveVolatileProducerStoppedField()
	{
		// Arrange
		var field = typeof(CdcProcessor)
			.GetField("_producerStopped", BindingFlags.NonPublic | BindingFlags.Instance);

		// Assert
		field.ShouldNotBeNull("CdcProcessor should have _producerStopped field");
		field.FieldType.ShouldBe(typeof(bool));

		var requiredModifiers = field.GetRequiredCustomModifiers();
		requiredModifiers.ShouldContain(typeof(IsVolatile),
			"_producerStopped field should be marked volatile");
	}

	#endregion

	#region S543.13: Disposal Pattern

	[Fact]
	public void HaveDisposedFlagField()
	{
		// Arrange
		var field = typeof(CdcProcessor)
			.GetField("_disposedFlag", BindingFlags.NonPublic | BindingFlags.Instance);

		// Assert
		field.ShouldNotBeNull("CdcProcessor should have _disposedFlag field for safe disposal");
		field.FieldType.ShouldBe(typeof(int));
	}

	[Fact]
	public void HaveExecutionLock()
	{
		// Arrange
		var field = typeof(CdcProcessor)
			.GetField("_executionLock", BindingFlags.NonPublic | BindingFlags.Instance);

		// Assert
		field.ShouldNotBeNull("CdcProcessor should have _executionLock SemaphoreSlim");
		field.FieldType.ShouldBe(typeof(SemaphoreSlim));
	}

	[Fact]
	public void HaveProducerCancellationTokenSource()
	{
		// Arrange — verify CTS exists for disposal timeout
		var field = typeof(CdcProcessor)
			.GetField("_producerCancellationTokenSource", BindingFlags.NonPublic | BindingFlags.Instance);

		// Assert
		field.ShouldNotBeNull("CdcProcessor should have _producerCancellationTokenSource");
		field.FieldType.ShouldBe(typeof(CancellationTokenSource));
	}

	#endregion
}
