// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Hosting.Builders;

/// <summary>
/// Default implementation of <see cref="IExcaliburBuilder"/>.
/// </summary>
/// <remarks>
/// </remarks>
internal sealed class ExcaliburBuilder : IExcaliburBuilder
{
	/// <summary>
	/// Initializes a new instance of the <see cref="ExcaliburBuilder"/> class.
	/// </summary>
	/// <param name="services">The service collection to configure.</param>
	/// <exception cref="ArgumentNullException">
	/// Thrown when <paramref name="services"/> is null.
	/// </exception>
	internal ExcaliburBuilder(IServiceCollection services)
	{
		Services = services ?? throw new ArgumentNullException(nameof(services));
	}

	/// <inheritdoc/>
	public IServiceCollection Services { get; }
}
