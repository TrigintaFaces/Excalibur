// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

// Two read models sharing a SIMPLE type name across two real sibling namespaces -- the shape the
// index-identity check exists for. These were nested types inside the test class once, which produced
// the same collision by a different route (their full names differed by a nesting marker rather than a
// namespace) and so reproduced the behaviour while misdescribing the scenario. A reader checking whether
// the reported shape is the real one should find the real one.
namespace Excalibur.Data.Tests.ElasticSearch.Projections.Hr;

/// <summary>The demonstration HR employee read model.</summary>
public sealed class EmployeeProjection
{
	/// <summary>Gets or sets the document id.</summary>
	public string Id { get; set; } = string.Empty;

	/// <summary>Gets or sets the employee name.</summary>
	public string Name { get; set; } = string.Empty;
}

/// <summary>A customer read model, whose simple name differs from every other fixture here.</summary>
public sealed class CustomerProjection
{
	/// <summary>Gets or sets the document id.</summary>
	public string Id { get; set; } = string.Empty;
}
