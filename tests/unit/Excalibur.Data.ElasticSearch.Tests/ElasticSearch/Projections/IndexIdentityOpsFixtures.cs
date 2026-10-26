// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

namespace Excalibur.Data.Tests.ElasticSearch.Projections.Ops;

/// <summary>
/// The operational employee read model. Shares its SIMPLE name with the HR model in the sibling
/// namespace and is unrelated to it in shape, which is exactly why they must not share an index.
/// </summary>
public sealed class EmployeeProjection
{
	/// <summary>Gets or sets the document id.</summary>
	public string Id { get; set; } = string.Empty;

	/// <summary>Gets or sets the employee number.</summary>
	public string EmployeeNumber { get; set; } = string.Empty;
}
