// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace DataAccessIntro;

/// <summary>
/// A simple product model used to demonstrate IDataRequest CRUD operations.
/// </summary>
public sealed class Product
{
	public int Id { get; init; }
	public string Name { get; init; } = string.Empty;
	public decimal Price { get; init; }
}
