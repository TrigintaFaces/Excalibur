// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Saga.Abstractions;
using Excalibur.Saga.Models;
using Excalibur.Saga.Visualization;

namespace Excalibur.Saga.Tests.Core.Visualization;

#pragma warning disable CA1034
public sealed class MermaidTestSagaData
{
	public string OrderId { get; set; } = "";
}
#pragma warning restore CA1034

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class SagaMermaidGeneratorShould
{
	private readonly SagaMermaidGenerator<MermaidTestSagaData> _sut = new();

	[Fact]
	public void GenerateDiagram_ContainStateDiagramHeader()
	{
		// Arrange
		var definition = CreateDefinition("OrderSaga", CreateStep("CreateOrder"), CreateStep("ProcessPayment"));

		// Act
		var diagram = _sut.GenerateDiagram(definition, SagaVisualizationFormat.Mermaid);

		// Assert
		diagram.ShouldContain("stateDiagram-v2");
		diagram.ShouldContain("OrderSaga");
	}

	[Fact]
	public void GenerateDiagram_EmitTransitionsBetweenSteps()
	{
		// Arrange
		var definition = CreateDefinition("OrderSaga", CreateStep("CreateOrder"), CreateStep("ProcessPayment"));

		// Act
		var diagram = _sut.GenerateDiagram(definition, SagaVisualizationFormat.Mermaid);

		// Assert
		diagram.ShouldContain("[*] --> CreateOrder");
		diagram.ShouldContain("CreateOrder --> ProcessPayment");
		diagram.ShouldContain("ProcessPayment --> [*]");
	}

	[Fact]
	public void GenerateDiagram_HandleEmptySteps()
	{
		// Arrange
		var definition = new SagaDefinition<MermaidTestSagaData> { Name = "EmptySaga" };

		// Act
		var diagram = _sut.GenerateDiagram(definition, SagaVisualizationFormat.Mermaid);

		// Assert
		diagram.ShouldContain("No steps defined");
	}

	[Fact]
	public void GenerateDiagram_EmitCompensationPaths()
	{
		// Arrange
		var definition = CreateDefinition("OrderSaga",
			CreateStep("CreateOrder", canCompensate: true),
			CreateStep("ProcessPayment", canCompensate: true));

		// Act
		var diagram = _sut.GenerateDiagram(definition, SagaVisualizationFormat.Mermaid);

		// Assert
		diagram.ShouldContain("Compensation paths");
		diagram.ShouldContain("failure");
	}

	[Fact]
	public void GenerateDiagram_SanitizeStateIds()
	{
		// Arrange - step names with spaces and dots
		var definition = CreateDefinition("OrderSaga",
			CreateStep("Create Order"),
			CreateStep("Process.Payment"));

		// Act
		var diagram = _sut.GenerateDiagram(definition, SagaVisualizationFormat.Mermaid);

		// Assert - spaces and dots replaced with underscores
		diagram.ShouldContain("Create_Order");
		diagram.ShouldContain("Process_Payment");
	}

	[Fact]
	public void GenerateDiagram_ThrowOnNullDefinition()
	{
		Should.Throw<ArgumentNullException>(
			() => _sut.GenerateDiagram(null!, SagaVisualizationFormat.Mermaid));
	}

	[Fact]
	public void GenerateDiagram_ThrowOnUnsupportedFormat()
	{
		// Arrange
		var definition = CreateDefinition("Saga", CreateStep("Step1"));

		// Act & Assert
		Should.Throw<NotSupportedException>(
			() => _sut.GenerateDiagram(definition, SagaVisualizationFormat.PlantUml));
	}

	[Fact]
	public void GenerateDiagram_UseFallbackName_WhenNameEmpty()
	{
		// Arrange
		var definition = new SagaDefinition<MermaidTestSagaData> { Name = "" };
		definition.Steps.Add(CreateStep("Step1"));

		// Act
		var diagram = _sut.GenerateDiagram(definition, SagaVisualizationFormat.Mermaid);

		// Assert
		diagram.ShouldContain("Saga");
	}

	[Fact]
	public void GenerateDiagram_AnnotateCompensatableSteps()
	{
		// Arrange
		var definition = CreateDefinition("Saga", CreateStep("Step1", canCompensate: true));

		// Act
		var diagram = _sut.GenerateDiagram(definition, SagaVisualizationFormat.Mermaid);

		// Assert
		diagram.ShouldContain("compensatable");
	}

	[Fact]
	public void GenerateDiagram_AnnotateTimeouts()
	{
		// Arrange
		var step = A.Fake<ISagaStep<MermaidTestSagaData>>();
		A.CallTo(() => step.Name).Returns("Step1");
		A.CallTo(() => step.CanCompensate).Returns(false);
		A.CallTo(() => step.Timeout).Returns(TimeSpan.FromSeconds(30));

		var definition = new SagaDefinition<MermaidTestSagaData> { Name = "Saga" };
		definition.Steps.Add(step);

		// Act
		var diagram = _sut.GenerateDiagram(definition, SagaVisualizationFormat.Mermaid);

		// Assert
		diagram.ShouldContain("timeout: 30s");
	}

	private static SagaDefinition<MermaidTestSagaData> CreateDefinition(string name, params ISagaStep<MermaidTestSagaData>[] steps)
	{
		var def = new SagaDefinition<MermaidTestSagaData> { Name = name };
		foreach (var step in steps)
		{
			def.Steps.Add(step);
		}

		return def;
	}

	private static ISagaStep<MermaidTestSagaData> CreateStep(string name, bool canCompensate = false)
	{
		var step = A.Fake<ISagaStep<MermaidTestSagaData>>();
		A.CallTo(() => step.Name).Returns(name);
		A.CallTo(() => step.CanCompensate).Returns(canCompensate);
		A.CallTo(() => step.Timeout).Returns(TimeSpan.Zero);
		return step;
	}
}
