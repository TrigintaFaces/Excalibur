// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Saga.Models;

namespace Excalibur.Saga.Visualization;

/// <summary>
/// Generates visual diagram representations of saga definitions.
/// </summary>
/// <typeparam name="TData">The type of data flowing through the saga.</typeparam>
public interface ISagaVisualizer<TData>
	where TData : class
{
	/// <summary>
	/// Generates a diagram string from a saga definition in the specified format.
	/// </summary>
	/// <param name="definition">The saga definition to visualize.</param>
	/// <param name="format">The output diagram format.</param>
	/// <returns>A string containing the diagram markup.</returns>
	string GenerateDiagram(SagaDefinition<TData> definition, SagaVisualizationFormat format);
}
