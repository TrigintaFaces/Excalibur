// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.CodeAnalysis;

namespace Excalibur.Dispatch.SourceGenerators.Interception;

/// <summary>
/// Source generator for DispatchAsync call-site interception.
/// </summary>
/// <remarks>
/// <para>
/// Generation is disabled. <see cref="StaticPipelineGenerator"/> (co-deployed in this analyzer
/// assembly) intercepts the same DispatchAsync call sites with a superset of functionality
/// (static pipeline execution + hot-reload fallback). Emitting interceptors here would cause
/// CS9153 duplicate interception errors.
/// </para>
/// <para>
/// This generator remains registered as a no-op to avoid breaking analyzer package consumers
/// who expect the type to exist.
/// </para>
/// </remarks>
[Generator]
public class DispatchInterceptorGenerator : IIncrementalGenerator
{
	/// <summary>
	/// No-op: StaticPipelineGenerator handles all DispatchAsync call sites.
	/// </summary>
	public void Initialize(IncrementalGeneratorInitializationContext context)
	{
		// Intentionally empty -- see class remarks.
	}
}
