// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Alba;

using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Hosting;

namespace Excalibur.Integration.Tests.DataElasticSearch.Infrastructure.TestBaseClasses.Host;

/// <summary>
///     Extension methods to bridge Alba API changes.
/// </summary>
public static class AlbaExtensions
{
	/// <summary>
	///     Starts Alba from a WebApplicationBuilder.
	/// </summary>
	public static async Task<IAlbaHost> StartAlbaAsync(
		this WebApplicationBuilder builder,
		Action<WebApplication>? configure = null)
	{
		return await AlbaHost.For(builder, configure).ConfigureAwait(true);
	}

	/// <summary>
	///     Gets the server from an IAlbaHost.
	/// </summary>
	public static IHost Server(this IAlbaHost host) => host;
}
