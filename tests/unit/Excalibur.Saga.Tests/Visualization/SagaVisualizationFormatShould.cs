// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Saga.Visualization;

namespace Excalibur.Saga.Tests.Visualization;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class SagaVisualizationFormatShould
{
	[Fact]
	public void DefineMermaidAsZero()
	{
		((int)SagaVisualizationFormat.Mermaid).ShouldBe(0);
	}

	[Fact]
	public void DefinePlantUmlAsOne()
	{
		((int)SagaVisualizationFormat.PlantUml).ShouldBe(1);
	}

	[Fact]
	public void HaveExactlyTwoValues()
	{
		Enum.GetValues<SagaVisualizationFormat>().Length.ShouldBe(2);
	}
}
