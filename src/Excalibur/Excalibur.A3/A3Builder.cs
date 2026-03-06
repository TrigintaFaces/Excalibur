// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.A3.Abstractions.Authorization;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Excalibur.A3;

/// <summary>
/// Default implementation of <see cref="IA3Builder"/>.
/// </summary>
/// <remarks>
/// Follows the Microsoft ASP.NET Core Identity <c>IdentityBuilder</c> pattern.
/// </remarks>
internal sealed class A3Builder : IA3Builder
{
	/// <summary>
	/// Initializes a new instance of the <see cref="A3Builder"/> class.
	/// </summary>
	/// <param name="services">The service collection being configured.</param>
	public A3Builder(IServiceCollection services)
	{
		ArgumentNullException.ThrowIfNull(services);
		Services = services;
	}

	/// <inheritdoc />
	public IServiceCollection Services { get; }

	/// <inheritdoc />
	public IA3Builder UseGrantStore<TStore>() where TStore : class, IGrantStore
	{
		Services.Replace(ServiceDescriptor.Scoped<IGrantStore, TStore>());
		return this;
	}

	/// <inheritdoc />
	public IA3Builder UseActivityGroupStore<TStore>() where TStore : class, IActivityGroupStore
	{
		Services.Replace(ServiceDescriptor.Scoped<IActivityGroupStore, TStore>());
		return this;
	}
}
