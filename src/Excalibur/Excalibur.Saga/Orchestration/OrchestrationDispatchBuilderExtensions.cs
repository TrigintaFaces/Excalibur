// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Abstractions.Configuration;

namespace Excalibur.Saga.Orchestration;

/// <summary>
/// Extension methods for configuring dispatch orchestration in the builder. Provides methods to register orchestration services including
/// saga management and workflow coordination.
/// </summary>
public static class OrchestrationDispatchBuilderExtensions
{
	/// <summary>
	/// Adds dispatch orchestration services to the builder. Registers saga management, workflow coordination, and related orchestration
	/// infrastructure required for complex message-driven business processes.
	/// </summary>
	/// <param name="builder"> The dispatch builder to configure. </param>
	/// <returns> The dispatch builder for method chaining. </returns>
	/// <exception cref="ArgumentNullException"> Thrown when <paramref name="builder" /> is null. </exception>
	public static IDispatchBuilder AddDispatchOrchestration(this IDispatchBuilder builder)
	{
		ArgumentNullException.ThrowIfNull(builder);

		_ = builder.Services.AddDispatchOrchestration();

		return builder;
	}
}
