// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Saga.DependencyInjection;

/// <summary>
/// Default implementation of <see cref="ISagaBuilder"/>.
/// </summary>
internal sealed class SagaBuilder : ISagaBuilder
{
	/// <summary>
	/// Initializes a new instance of the <see cref="SagaBuilder"/> class.
	/// </summary>
	/// <param name="services">The service collection.</param>
	public SagaBuilder(IServiceCollection services)
	{
		Services = services ?? throw new ArgumentNullException(nameof(services));
	}

	/// <inheritdoc />
	public IServiceCollection Services { get; }
}
