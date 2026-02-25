// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Saga.Visualization;

/// <summary>
/// Specifies the output format for saga diagram generation.
/// </summary>
public enum SagaVisualizationFormat
{
	/// <summary>
	/// Generate a Mermaid stateDiagram-v2 diagram.
	/// </summary>
	Mermaid = 0,

	/// <summary>
	/// Generate a PlantUML state diagram.
	/// </summary>
	PlantUml = 1,
}
