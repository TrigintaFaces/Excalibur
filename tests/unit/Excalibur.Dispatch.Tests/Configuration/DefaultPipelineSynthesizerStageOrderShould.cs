// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Reflection;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Configuration;

namespace Excalibur.Dispatch.Tests.Configuration;

/// <summary>
/// Verifies the pipeline stage ordering in DefaultPipelineSynthesizer.
/// Sprint 694 T.18: Authorization must come before Serialization.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Dispatch.Core")]
public sealed class DefaultPipelineSynthesizerStageOrderShould
{
	private static DispatchMiddlewareStage[] GetStageOrder()
	{
		// Access the internal StageOrder array via reflection
		var field = typeof(DefaultPipelineSynthesizer)
			.GetField("StageOrder", BindingFlags.NonPublic | BindingFlags.Static);
		field.ShouldNotBeNull("Expected StageOrder static field on DefaultPipelineSynthesizer");
		var stageOrder = field.GetValue(null) as DispatchMiddlewareStage[];
		stageOrder.ShouldNotBeNull();
		return stageOrder;
	}

	[Fact]
	public void PlaceAuthorizationBeforeSerialization()
	{
		// Arrange
		var stages = GetStageOrder();

		var authIndex = Array.IndexOf(stages, DispatchMiddlewareStage.Authorization);
		var serializationIndex = Array.IndexOf(stages, DispatchMiddlewareStage.Serialization);

		// Assert -- Authorization must execute before Serialization
		authIndex.ShouldBeGreaterThan(-1, "Authorization stage not found in StageOrder");
		serializationIndex.ShouldBeGreaterThan(-1, "Serialization stage not found in StageOrder");
		authIndex.ShouldBeLessThan(serializationIndex,
			"Authorization must come before Serialization to reject unauthorized requests before paying serialization cost");
	}

	[Fact]
	public void PlaceAuthenticationBeforeAuthorization()
	{
		// Arrange
		var stages = GetStageOrder();

		var authnIndex = Array.IndexOf(stages, DispatchMiddlewareStage.Authentication);
		var authzIndex = Array.IndexOf(stages, DispatchMiddlewareStage.Authorization);

		// Assert
		authnIndex.ShouldBeLessThan(authzIndex,
			"Authentication must come before Authorization");
	}

	[Fact]
	public void StartWithStartStage()
	{
		var stages = GetStageOrder();
		stages[0].ShouldBe(DispatchMiddlewareStage.Start);
	}

	[Fact]
	public void EndWithEndStage()
	{
		var stages = GetStageOrder();
		stages[^1].ShouldBe(DispatchMiddlewareStage.End);
	}

	[Fact]
	public void PlacePreProcessingBeforeValidation()
	{
		var stages = GetStageOrder();

		var preIndex = Array.IndexOf(stages, DispatchMiddlewareStage.PreProcessing);
		var valIndex = Array.IndexOf(stages, DispatchMiddlewareStage.Validation);

		preIndex.ShouldBeLessThan(valIndex);
	}

	[Fact]
	public void PlaceValidationBeforeAuthorization()
	{
		var stages = GetStageOrder();

		var valIndex = Array.IndexOf(stages, DispatchMiddlewareStage.Validation);
		var authzIndex = Array.IndexOf(stages, DispatchMiddlewareStage.Authorization);

		valIndex.ShouldBeLessThan(authzIndex);
	}

	[Fact]
	public void PlaceProcessingAfterRouting()
	{
		var stages = GetStageOrder();

		var routingIndex = Array.IndexOf(stages, DispatchMiddlewareStage.Routing);
		var processingIndex = Array.IndexOf(stages, DispatchMiddlewareStage.Processing);

		routingIndex.ShouldBeLessThan(processingIndex);
	}

	[Fact]
	public void ContainAllExpectedStages()
	{
		var stages = GetStageOrder();

		stages.ShouldContain(DispatchMiddlewareStage.Start);
		stages.ShouldContain(DispatchMiddlewareStage.RateLimiting);
		stages.ShouldContain(DispatchMiddlewareStage.PreProcessing);
		stages.ShouldContain(DispatchMiddlewareStage.Instrumentation);
		stages.ShouldContain(DispatchMiddlewareStage.Authentication);
		stages.ShouldContain(DispatchMiddlewareStage.Validation);
		stages.ShouldContain(DispatchMiddlewareStage.Authorization);
		stages.ShouldContain(DispatchMiddlewareStage.Serialization);
		stages.ShouldContain(DispatchMiddlewareStage.Cache);
		stages.ShouldContain(DispatchMiddlewareStage.Routing);
		stages.ShouldContain(DispatchMiddlewareStage.Processing);
		stages.ShouldContain(DispatchMiddlewareStage.PostProcessing);
		stages.ShouldContain(DispatchMiddlewareStage.ErrorHandling);
		stages.ShouldContain(DispatchMiddlewareStage.End);
	}
}
