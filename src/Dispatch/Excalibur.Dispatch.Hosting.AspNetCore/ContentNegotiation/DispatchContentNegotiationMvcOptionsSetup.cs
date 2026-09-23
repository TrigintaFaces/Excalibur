// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch.Serialization;

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Hosting.AspNetCore.ContentNegotiation;

/// <summary>
/// Inserts the Dispatch input and output formatters into <see cref="MvcOptions" />.
/// </summary>
/// <remarks>
/// <para>
/// This exists so that the formatters receive the <b>application's</b> <see cref="ISerializerRegistry" />.
/// The registry arrives by constructor injection, which means the host resolves it from the real
/// container when <see cref="MvcOptions" /> is first materialised -- after every
/// <c>AddDispatchSerialization</c> registration has run, not at the moment
/// <c>AddDispatchContentNegotiation</c> was called.
/// </para>
/// <para>
/// The previous implementation called <c>BuildServiceProvider()</c> inside the options callback. That
/// built a SECOND root container, so the formatters held a different registry from the rest of the
/// application, the provider owned every disposable singleton it created and was never disposed, and the
/// service collection was snapshotted at callback time so a serializer registered later was silently
/// absent. ASP.NET Core ships an analyzer for that call (ASP0000) precisely because of those three
/// outcomes; <see cref="IConfigureOptions{TOptions}" /> is the supported way to take a dependency while
/// configuring options.
/// </para>
/// </remarks>
/// <param name="registry"> The application's serializer registry. </param>
internal sealed class DispatchContentNegotiationMvcOptionsSetup(ISerializerRegistry registry)
	: IConfigureOptions<MvcOptions>
{
	private readonly ISerializerRegistry _registry =
		registry ?? throw new ArgumentNullException(nameof(registry));

	/// <inheritdoc />
	public void Configure(MvcOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		options.InputFormatters.Insert(0, new DispatchInputFormatter(_registry));
		options.OutputFormatters.Insert(0, new DispatchOutputFormatter(_registry));
	}
}
