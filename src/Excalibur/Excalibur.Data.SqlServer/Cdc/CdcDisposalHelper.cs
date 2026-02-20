// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.SqlServer.Cdc;

/// <summary>
/// Shared disposal helper for CDC classes that need polymorphic async/sync disposal.
/// </summary>
internal static class CdcDisposalHelper
{
	/// <summary>
	/// Safely disposes a resource that may implement <see cref="IAsyncDisposable"/> or <see cref="IDisposable"/>.
	/// </summary>
	/// <param name="resource">The resource to dispose.</param>
	internal static async ValueTask SafeDisposeAsync(object resource)
	{
		switch (resource)
		{
			case IAsyncDisposable resourceAsyncDisposable:
				await resourceAsyncDisposable.DisposeAsync().ConfigureAwait(false);
				break;

			case IDisposable disposable:
				disposable.Dispose();
				break;
		}
	}
}
