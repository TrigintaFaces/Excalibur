// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


// This file is used by Code Analysis to maintain SuppressMessage attributes that are applied to this project. Project-level suppressions
// either have no target or are given a specific target and scoped to a namespace, type, member, etc.
using System.Diagnostics.CodeAnalysis;

// CA1045: Ref parameters required for performance-critical zero-allocation patterns These are intentional design decisions for hot-path
// optimizations per R9.6-R9.9
[assembly:
	SuppressMessage("Design", "CA1045:Do not pass types by reference",
		Justification = "Required for zero-allocation hot-path patterns per R9.6")]

// CA1062: Parameter validation - many internal methods trust callers Public APIs are validated; internal trust is acceptable per
// architectural decision
[assembly:
	SuppressMessage("Design", "CA1062:Validate arguments of public methods",
		Justification = "Internal APIs trust callers; public APIs validate", Scope = "namespaceanddescendants",
		Target = "~N:Excalibur.Dispatch")]

// CA1305/CA1863: String formatting with culture - internal framework usage Framework-internal string operations don't require culture specification
[assembly:
	SuppressMessage("Globalization", "CA1305:Specify IFormatProvider", Justification = "Internal framework strings don't require culture")]
[assembly:
	SuppressMessage("Performance", "CA1863:Use 'CompositeFormat'", Justification = "Performance optimization deferred to profiling phase")]

// RS0041: Generated code from System.Text.Json.SourceGeneration uses oblivious nullable reference types This is expected and unavoidable in
// machine-generated serialization code The generated CloudEventJsonContext and similar classes use nullable-oblivious property getters
[assembly:
	SuppressMessage("ApiDesign", "RS0041:Public members should not use oblivious types",
		Justification = "System.Text.Json source generator produces oblivious types for partial context classes; not user-controllable",
		Scope = "type", Target = "~T:Excalibur.Dispatch.CloudEvents.CloudEventJsonContext")]

// Suppress CA1034 for telemetry constants grouping types
[assembly:
	SuppressMessage("Design", "CA1034:Nested types should not be visible", Justification = "Logical grouping of telemetry constants",
		Scope = "type", Target = "~T:Excalibur.Dispatch.Diagnostics.DispatchTelemetryConstants.ActivitySources")]
[assembly:
	SuppressMessage("Design", "CA1034:Nested types should not be visible", Justification = "Logical grouping of telemetry constants",
		Scope = "type", Target = "~T:Excalibur.Dispatch.Diagnostics.DispatchTelemetryConstants.Meters")]
[assembly:
	SuppressMessage("Design", "CA1034:Nested types should not be visible", Justification = "Logical grouping of telemetry constants",
		Scope = "type", Target = "~T:Excalibur.Dispatch.Diagnostics.DispatchTelemetryConstants.Activities")]
[assembly:
	SuppressMessage("Design", "CA1034:Nested types should not be visible", Justification = "Logical grouping of telemetry constants",
		Scope = "type", Target = "~T:Excalibur.Dispatch.Diagnostics.DispatchTelemetryConstants.Tags")]
[assembly:
	SuppressMessage("Design", "CA1034:Nested types should not be visible", Justification = "Logical grouping of telemetry constants",
		Scope = "type", Target = "~T:Excalibur.Dispatch.Diagnostics.DispatchTelemetryConstants.TagValues")]

// Suppress CA1815 for struct types that don't need equality
[assembly:
	SuppressMessage("Performance", "CA1815:Override equals and operator equals on value types",
		Justification = "These structs are used as handles and don't require value equality", Scope = "type",
		Target = "~T:Excalibur.Dispatch.Buffers.BufferSegment")]

// Suppress CA1062 for performance-critical paths where null checks are done at higher levels
[assembly:
	SuppressMessage("Design", "CA1062:Validate arguments of public methods", Justification = "Null checks performed at API boundary",
		Scope = "member",
		Target =
			"~M:Excalibur.Dispatch.Simd.SimdMessageParser.ParseHeaders(System.ReadOnlySpan{System.Byte},System.Collections.Generic.Dictionary{System.String,System.String})")]
[assembly:
	SuppressMessage("Design", "CA1062:Validate arguments of public methods", Justification = "Null checks performed at API boundary",
		Scope = "member",
		Target =
			"~M:Excalibur.Dispatch.Simd.SimdJsonFieldExtractor.ExtractMultipleStringFields(System.ReadOnlySpan{System.Byte},System.ReadOnlySpan{System.String},System.Collections.Generic.Dictionary{System.String,System.String})")]

// Suppress CA1823 for SIMD constants that may be used in future optimizations
[assembly:
	SuppressMessage("Performance", "CA1823:Avoid unused private fields", Justification = "SIMD constants reserved for future optimizations",
		Scope = "namespaceanddescendants", Target = "~N:Excalibur.Dispatch.Simd")]

[assembly:
	SuppressMessage("Usage", "CA2227:Collection properties should be read only", Justification = "Required for configuration binding",
		Scope = "member", Target = "~P:Excalibur.Dispatch.Diagnostics.DispatchTelemetryOptions.GlobalTags")]
[assembly:
	SuppressMessage("Usage", "CA2227:Collection properties should be read only", Justification = "Required for configuration binding",
		Scope = "member", Target = "~P:Excalibur.Dispatch.Validation.ProfileValidationConfiguration.RequiredFields")]
[assembly:
	SuppressMessage("Usage", "CA2227:Collection properties should be read only", Justification = "Required for configuration binding",
		Scope = "member", Target = "~P:Excalibur.Dispatch.Validation.ProfileValidationConfiguration.ForbiddenFields")]
[assembly:
	SuppressMessage("Usage", "CA2227:Collection properties should be read only", Justification = "Required for configuration binding",
		Scope = "member", Target = "~P:Excalibur.Dispatch.Validation.ProfileValidationConfiguration.FieldConstraints")]
[assembly:
	SuppressMessage("Usage", "CA2227:Collection properties should be read only", Justification = "Required for configuration binding",
		Scope = "member", Target = "~P:Excalibur.Dispatch.Validation.ProfileValidationOptions.Profiles")]
[assembly:
	SuppressMessage("Usage", "CA2227:Collection properties should be read only", Justification = "Required for configuration binding",
		Scope = "member", Target = "~P:Excalibur.Dispatch.ErrorHandling.DeadLetterMessage.Properties")]
[assembly:
	SuppressMessage("Usage", "CA2227:Collection properties should be read only", Justification = "Required for configuration binding",
		Scope = "member", Target = "~P:Excalibur.Dispatch.ErrorHandling.MessageProcessingInfo.ProcessingHistory")]
[assembly:
	SuppressMessage("Usage", "CA2227:Collection properties should be read only", Justification = "Required for configuration binding",
		Scope = "member", Target = "~P:Excalibur.Dispatch.ErrorHandling.PoisonDetectionResult.Details")]
[assembly:
	SuppressMessage("Usage", "CA2227:Collection properties should be read only", Justification = "Required for configuration binding",
		Scope = "member", Target = "~P:Excalibur.Dispatch.ErrorHandling.PoisonMessageStatistics.MessagesByType")]
[assembly:
	SuppressMessage("Usage", "CA2227:Collection properties should be read only", Justification = "Required for configuration binding",
		Scope = "member", Target = "~P:Excalibur.Dispatch.ErrorHandling.PoisonMessageStatistics.MessagesByReason")]
[assembly:
	SuppressMessage("Usage", "CA2227:Collection properties should be read only", Justification = "Required for configuration binding",
		Scope = "member", Target = "~P:Excalibur.Dispatch.Delivery.RecurringCronJob.Metadata")]
[assembly:
	SuppressMessage("Usage", "CA2227:Collection properties should be read only", Justification = "Required for configuration binding",
		Scope = "member", Target = "~P:Excalibur.Dispatch.Delivery.RecurringCronJob.Tags")]

// Note: Configuration stub middleware suppressions removed in Sprint 545 - stub files deleted
// Note: CorrelationMiddleware suppression removed in Sprint 70 - middleware deleted

// Suppress CA1812 for service classes registered via DI
[assembly:
	SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes",
		Justification = "Service classes are registered via dependency injection",
		Scope = "type", Target = "~T:Excalibur.Dispatch.Diagnostics.DispatchTelemetryProvider")]
[assembly:
	SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes",
		Justification = "Validator classes are registered via options validation",
		Scope = "type", Target = "~T:Excalibur.Dispatch.Diagnostics.DispatchTelemetryOptionsValidator")]

// NOTE: SqlDeadLetterStore.DeadLetterMessageDto suppression removed - class moved to Excalibur.Data.SqlServer (Sprint 306)
