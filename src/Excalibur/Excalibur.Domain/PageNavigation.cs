// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Domain;

/// <summary>
/// Defines the navigation direction for pagination operations.
/// </summary>
public enum PageNavigation
{
	/// <summary>
	/// Navigate to the first page.
	/// </summary>
	First = 0,

	/// <summary>
	/// Navigate to the previous page.
	/// </summary>
	Previous = 1,

	/// <summary>
	/// Navigate to the next page.
	/// </summary>
	Next = 2,

	/// <summary>
	/// Navigate to the last page.
	/// </summary>
	Last = 3,
}
