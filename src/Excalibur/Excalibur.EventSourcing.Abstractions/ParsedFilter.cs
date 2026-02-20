// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.EventSourcing.Abstractions;

/// <summary>
/// Represents a parsed filter condition extracted from a dictionary key.
/// </summary>
/// <param name="PropertyName">The property name to filter on.</param>
/// <param name="Operator">The comparison operator to apply.</param>
/// <remarks>
/// <para>
/// This record is returned by <see cref="FilterParser.Parse"/> and contains
/// the decomposed filter key components ready for query translation.
/// </para>
/// </remarks>
public sealed record ParsedFilter(string PropertyName, FilterOperator Operator);
