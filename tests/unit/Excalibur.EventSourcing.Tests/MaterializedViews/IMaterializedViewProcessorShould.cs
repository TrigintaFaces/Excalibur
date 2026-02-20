// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;

namespace Excalibur.EventSourcing.Tests.MaterializedViews;

/// <summary>
/// Unit tests for <see cref="IMaterializedViewProcessor"/> interface contract.
/// </summary>
/// <remarks>
/// Sprint 516: Materialized Views foundation tests.
/// Tests verify interface structure, method signatures, and CancellationToken requirements.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "MaterializedViews")]
[Trait("Feature", "Processor")]
public sealed class IMaterializedViewProcessorShould
{
	#region Interface Structure Tests

	[Fact]
	public void BeAnInterface()
	{
		// Assert
		typeof(IMaterializedViewProcessor).IsInterface.ShouldBeTrue();
	}

	[Fact]
	public void BePublic()
	{
		// Assert
		typeof(IMaterializedViewProcessor).IsPublic.ShouldBeTrue();
	}

	[Fact]
	public void HaveProcessEventAsyncMethod()
	{
		// Assert
		var method = typeof(IMaterializedViewProcessor).GetMethod("ProcessEventAsync");
		method.ShouldNotBeNull();
	}

	[Fact]
	public void HaveProcessEventsAsyncMethod()
	{
		// Assert
		var method = typeof(IMaterializedViewProcessor).GetMethod("ProcessEventsAsync");
		method.ShouldNotBeNull();
	}

	[Fact]
	public void HaveRebuildAsyncMethod()
	{
		// Assert
		var method = typeof(IMaterializedViewProcessor).GetMethod("RebuildAsync");
		method.ShouldNotBeNull();
	}

	[Fact]
	public void HaveCatchUpAsyncMethod()
	{
		// Assert
		var method = typeof(IMaterializedViewProcessor).GetMethod("CatchUpAsync");
		method.ShouldNotBeNull();
	}

	#endregion

	#region Method Parameter Tests

	[Fact]
	public void ProcessEventAsync_RequireCancellationToken()
	{
		// Arrange
		var method = typeof(IMaterializedViewProcessor).GetMethod("ProcessEventAsync");
		var parameters = method.GetParameters();

		// Assert
		parameters.Length.ShouldBe(3);
		parameters[0].Name.ShouldBe("event");
		parameters[0].ParameterType.ShouldBe(typeof(IDomainEvent));
		parameters[1].Name.ShouldBe("position");
		parameters[1].ParameterType.ShouldBe(typeof(long));
		parameters[2].Name.ShouldBe("cancellationToken");
		parameters[2].ParameterType.ShouldBe(typeof(CancellationToken));
	}

	[Fact]
	public void ProcessEventsAsync_RequireCancellationToken()
	{
		// Arrange
		var method = typeof(IMaterializedViewProcessor).GetMethod("ProcessEventsAsync");
		var parameters = method.GetParameters();

		// Assert
		parameters.Length.ShouldBe(2);
		parameters[0].Name.ShouldBe("events");
		parameters[1].Name.ShouldBe("cancellationToken");
		parameters[1].ParameterType.ShouldBe(typeof(CancellationToken));
	}

	[Fact]
	public void ProcessEventsAsync_TakeEnumerableOfTuples()
	{
		// Arrange
		var method = typeof(IMaterializedViewProcessor).GetMethod("ProcessEventsAsync");
		var parameters = method.GetParameters();
		var eventParam = parameters[0];

		// Assert - IEnumerable<(IDomainEvent Event, long Position)>
		eventParam.ParameterType.IsGenericType.ShouldBeTrue();
		eventParam.ParameterType.GetGenericTypeDefinition().ShouldBe(typeof(IEnumerable<>));
	}

	[Fact]
	public void RebuildAsync_RequireCancellationToken()
	{
		// Arrange
		var method = typeof(IMaterializedViewProcessor).GetMethod("RebuildAsync");
		var parameters = method.GetParameters();

		// Assert
		parameters.Length.ShouldBe(1);
		parameters[0].Name.ShouldBe("cancellationToken");
		parameters[0].ParameterType.ShouldBe(typeof(CancellationToken));
	}

	[Fact]
	public void CatchUpAsync_RequireCancellationToken()
	{
		// Arrange
		var method = typeof(IMaterializedViewProcessor).GetMethod("CatchUpAsync");
		var parameters = method.GetParameters();

		// Assert
		parameters.Length.ShouldBe(2);
		parameters[0].Name.ShouldBe("viewName");
		parameters[0].ParameterType.ShouldBe(typeof(string));
		parameters[1].Name.ShouldBe("cancellationToken");
		parameters[1].ParameterType.ShouldBe(typeof(CancellationToken));
	}

	#endregion

	#region Return Type Tests

	[Fact]
	public void ProcessEventAsync_ReturnTask()
	{
		// Arrange
		var method = typeof(IMaterializedViewProcessor).GetMethod("ProcessEventAsync");

		// Assert
		method.ReturnType.ShouldBe(typeof(Task));
	}

	[Fact]
	public void ProcessEventsAsync_ReturnTask()
	{
		// Arrange
		var method = typeof(IMaterializedViewProcessor).GetMethod("ProcessEventsAsync");

		// Assert
		method.ReturnType.ShouldBe(typeof(Task));
	}

	[Fact]
	public void RebuildAsync_ReturnTask()
	{
		// Arrange
		var method = typeof(IMaterializedViewProcessor).GetMethod("RebuildAsync");

		// Assert
		method.ReturnType.ShouldBe(typeof(Task));
	}

	[Fact]
	public void CatchUpAsync_ReturnTask()
	{
		// Arrange
		var method = typeof(IMaterializedViewProcessor).GetMethod("CatchUpAsync");

		// Assert
		method.ReturnType.ShouldBe(typeof(Task));
	}

	#endregion
}
