// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Domain;

/// <summary>
/// Represents a cursor-based page request for pagination.
/// </summary>
/// <typeparam name="TCursor"> The type of the cursor used for pagination. </typeparam>
/// <param name="pageSize"> The number of items per page. </param>
/// <param name="navigation"> The page navigation direction. </param>
public abstract class CursorPageRequest<TCursor>(int pageSize, PageNavigation navigation)
{
	/// <summary>
	/// Gets or sets the number of items per page.
	/// </summary>
	/// <value>
	/// The number of items per page.
	/// </value>
	public int PageSize { get; } = pageSize;

	/// <summary>
	/// Gets the page navigation direction.
	/// </summary>
	/// <value>
	/// The page navigation direction.
	/// </value>
	public PageNavigation Navigation { get; } = navigation;

	/// <summary>
	/// Deconstructs the page request into its component parts.
	/// </summary>
	/// <param name="pageSize"> The number of items per page. </param>
	/// <param name="navigation"> The page navigation direction. </param>
	/// <param name="cursor"> The cursor value for pagination. </param>
	public void Deconstruct(out int pageSize, out PageNavigation navigation, out TCursor? cursor)
	{
		pageSize = PageSize;
		navigation = Navigation;
		cursor = GetCursor();
	}

	/// <summary>
	/// Gets the cursor value for this page request.
	/// </summary>
	/// <returns> The cursor value, or null if not available. </returns>
	protected abstract TCursor? GetCursor();
}
