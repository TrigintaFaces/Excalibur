// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Abstractions;

/// <summary>
/// Defines the stages in the dispatch middleware pipeline with their execution order.
/// </summary>
public enum DispatchMiddlewareStage
{
	/// <summary>
	/// The starting stage of the middleware pipeline.
	/// </summary>
	Start = 0,

	/// <summary>
	/// Rate limiting checks to control message throughput.
	/// </summary>
	RateLimiting = 50,

	/// <summary>
	/// Pre-processing stage for tracing and context setup.
	/// </summary>
	PreProcessing = 100,

	/// <summary>
	/// Instrumentation stage for performance metrics and telemetry.
	/// </summary>
	Instrumentation = 150,

	/// <summary>
	/// Authentication checks to verify message sender identity.
	/// </summary>
	Authentication = 175,

	/// <summary>
	/// Logging stage for audit trails and diagnostic logging.
	/// </summary>
	Logging = 190,

	/// <summary>
	/// Validation stage for schema, data annotation, and fluent validation.
	/// </summary>
	Validation = 200,

	/// <summary>
	/// Message serialization and deserialization processing.
	/// </summary>
	Serialization = 250,

	/// <summary>
	/// Authorization checks including role and grant policies.
	/// </summary>
	Authorization = 300,

	/// <summary>
	/// Cache stage to check for cached values and responses.
	/// </summary>
	Cache = 400,

	/// <summary>
	/// Optimization stage for batching, bulk operations, and performance enhancements.
	/// </summary>
	Optimization = 450,

	/// <summary>
	/// Routing stage for bus resolution, forwarders, and event publication.
	/// </summary>
	Routing = 500,

	/// <summary>
	/// Core message processing stage where handlers execute.
	/// </summary>
	Processing = 600,

	/// <summary>
	/// Post-processing stage for cleanup and result transformation.
	/// </summary>
	PostProcessing = 700,

	/// <summary>
	/// Error handling and compensation stage.
	/// </summary>
	Error = 800,

	/// <summary>
	/// Error handling and compensation stage (alias for Error).
	/// </summary>
	ErrorHandling = 801,

	/// <summary>
	/// The final stage of the middleware pipeline.
	/// </summary>
	End = 1000,
}
