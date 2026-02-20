// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Saga.Abstractions;
using Excalibur.Saga.Models;
using Excalibur.Saga.Visualization;

namespace Excalibur.Saga.Tests.Core.Visualization;

public sealed class MermaidDepthTestSagaData
{
	public string OrderId { get; set; } = string.Empty;
}

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class SagaMermaidGeneratorDepthShould
{
	private readonly SagaMermaidGenerator<MermaidDepthTestSagaData> _sut = new();

	[Fact]
	public void GenerateEmptyDiagramForNoSteps()
	{
		// Arrange
		var definition = new SagaDefinition<MermaidDepthTestSagaData> { Name = "EmptySaga", Version = "1" };

		// Act
		var result = _sut.GenerateDiagram(definition, SagaVisualizationFormat.Mermaid);

		// Assert
		result.ShouldContain("stateDiagram-v2");
		result.ShouldContain("[*] --> [*] : No steps defined");
	}

	[Fact]
	public void GenerateDiagramWithSingleStep()
	{
		// Arrange
		var step = A.Fake<ISagaStep<MermaidDepthTestSagaData>>();
		A.CallTo(() => step.Name).Returns("ProcessOrder");
		A.CallTo(() => step.CanCompensate).Returns(false);
		A.CallTo(() => step.Timeout).Returns(TimeSpan.Zero);

		var definition = new SagaDefinition<MermaidDepthTestSagaData> { Name = "OrderSaga", Version = "1" };
		definition.Steps.Add(step);

		// Act
		var result = _sut.GenerateDiagram(definition, SagaVisualizationFormat.Mermaid);

		// Assert
		result.ShouldContain("stateDiagram-v2");
		result.ShouldContain("ProcessOrder");
		result.ShouldContain("[*] --> ProcessOrder");
		result.ShouldContain("ProcessOrder --> [*]");
	}

	[Fact]
	public void GenerateDiagramWithMultipleSteps()
	{
		// Arrange
		var step1 = A.Fake<ISagaStep<MermaidDepthTestSagaData>>();
		A.CallTo(() => step1.Name).Returns("Step1");
		A.CallTo(() => step1.CanCompensate).Returns(false);
		A.CallTo(() => step1.Timeout).Returns(TimeSpan.Zero);

		var step2 = A.Fake<ISagaStep<MermaidDepthTestSagaData>>();
		A.CallTo(() => step2.Name).Returns("Step2");
		A.CallTo(() => step2.CanCompensate).Returns(false);
		A.CallTo(() => step2.Timeout).Returns(TimeSpan.Zero);

		var definition = new SagaDefinition<MermaidDepthTestSagaData> { Name = "MultiStepSaga", Version = "1" };
		definition.Steps.Add(step1);
		definition.Steps.Add(step2);

		// Act
		var result = _sut.GenerateDiagram(definition, SagaVisualizationFormat.Mermaid);

		// Assert
		result.ShouldContain("[*] --> Step1");
		result.ShouldContain("Step1 --> Step2");
		result.ShouldContain("Step2 --> [*]");
	}

	[Fact]
	public void IncludeCompensationPaths()
	{
		// Arrange
		var step1 = A.Fake<ISagaStep<MermaidDepthTestSagaData>>();
		A.CallTo(() => step1.Name).Returns("ChargeCard");
		A.CallTo(() => step1.CanCompensate).Returns(true);
		A.CallTo(() => step1.Timeout).Returns(TimeSpan.Zero);

		var step2 = A.Fake<ISagaStep<MermaidDepthTestSagaData>>();
		A.CallTo(() => step2.Name).Returns("ShipOrder");
		A.CallTo(() => step2.CanCompensate).Returns(true);
		A.CallTo(() => step2.Timeout).Returns(TimeSpan.Zero);

		var definition = new SagaDefinition<MermaidDepthTestSagaData> { Name = "CompensatableSaga", Version = "1" };
		definition.Steps.Add(step1);
		definition.Steps.Add(step2);

		// Act
		var result = _sut.GenerateDiagram(definition, SagaVisualizationFormat.Mermaid);

		// Assert
		result.ShouldContain("Compensation paths");
		result.ShouldContain("(compensatable)");
		result.ShouldContain("failure");
	}

	[Fact]
	public void IncludeTimeoutAnnotation()
	{
		// Arrange
		var step = A.Fake<ISagaStep<MermaidDepthTestSagaData>>();
		A.CallTo(() => step.Name).Returns("WaitForPayment");
		A.CallTo(() => step.CanCompensate).Returns(false);
		A.CallTo(() => step.Timeout).Returns(TimeSpan.FromSeconds(30));

		var definition = new SagaDefinition<MermaidDepthTestSagaData> { Name = "TimeoutSaga", Version = "1" };
		definition.Steps.Add(step);

		// Act
		var result = _sut.GenerateDiagram(definition, SagaVisualizationFormat.Mermaid);

		// Assert
		result.ShouldContain("[timeout: 30s]");
	}

	[Fact]
	public void IncludeVersionInComment()
	{
		// Arrange
		var step = A.Fake<ISagaStep<MermaidDepthTestSagaData>>();
		A.CallTo(() => step.Name).Returns("Step1");
		A.CallTo(() => step.CanCompensate).Returns(false);
		A.CallTo(() => step.Timeout).Returns(TimeSpan.Zero);

		var definition = new SagaDefinition<MermaidDepthTestSagaData> { Name = "VersionedSaga", Version = "3" };
		definition.Steps.Add(step);

		// Act
		var result = _sut.GenerateDiagram(definition, SagaVisualizationFormat.Mermaid);

		// Assert
		result.ShouldContain("VersionedSaga (v3)");
	}

	[Fact]
	public void ThrowWhenDefinitionIsNull()
	{
		Should.Throw<ArgumentNullException>(() =>
			_sut.GenerateDiagram(null!, SagaVisualizationFormat.Mermaid));
	}

	[Fact]
	public void ThrowForUnsupportedFormat()
	{
		// Arrange
		var definition = new SagaDefinition<MermaidDepthTestSagaData> { Name = "Test", Version = "1" };

		// Act & Assert
		Should.Throw<NotSupportedException>(() =>
			_sut.GenerateDiagram(definition, (SagaVisualizationFormat)999));
	}

	[Fact]
	public void SanitizeStepNamesWithSpaces()
	{
		// Arrange
		var step = A.Fake<ISagaStep<MermaidDepthTestSagaData>>();
		A.CallTo(() => step.Name).Returns("Process Order");
		A.CallTo(() => step.CanCompensate).Returns(false);
		A.CallTo(() => step.Timeout).Returns(TimeSpan.Zero);

		var definition = new SagaDefinition<MermaidDepthTestSagaData> { Name = "Test", Version = "1" };
		definition.Steps.Add(step);

		// Act
		var result = _sut.GenerateDiagram(definition, SagaVisualizationFormat.Mermaid);

		// Assert
		result.ShouldContain("Process_Order");
	}

	[Fact]
	public void UseDefaultNameWhenSagaNameIsEmpty()
	{
		// Arrange
		var step = A.Fake<ISagaStep<MermaidDepthTestSagaData>>();
		A.CallTo(() => step.Name).Returns("Step1");
		A.CallTo(() => step.CanCompensate).Returns(false);
		A.CallTo(() => step.Timeout).Returns(TimeSpan.Zero);

		var definition = new SagaDefinition<MermaidDepthTestSagaData> { Name = "", Version = "1" };
		definition.Steps.Add(step);

		// Act
		var result = _sut.GenerateDiagram(definition, SagaVisualizationFormat.Mermaid);

		// Assert
		result.ShouldContain("Saga (v1)");
	}
}
