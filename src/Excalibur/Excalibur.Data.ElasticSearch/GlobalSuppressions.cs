// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;

// CA1034: Public nested types are intentionally part of the API surface for context/metrics/configuration classes
// These types are returned from public methods or used as public property types and cannot be made internal
// without breaking the public API contract.
[assembly: SuppressMessage("Design", "CA1034:Nested types should not be visible", Justification = "Part of public API - returned from StartOperation() and metrics methods", Scope = "type", Target = "~T:Excalibur.Data.ElasticSearch.Monitoring.ElasticsearchMetrics.ElasticsearchOperationContext")]
[assembly: SuppressMessage("Design", "CA1034:Nested types should not be visible", Justification = "Part of public API - returned from StartOperation()", Scope = "type", Target = "~T:Excalibur.Data.ElasticSearch.Monitoring.ElasticsearchMonitoringService.ElasticsearchMonitoringContext")]
[assembly: SuppressMessage("Design", "CA1034:Nested types should not be visible", Justification = "Part of public API - used in public GetPerformanceMetrics() return type", Scope = "type", Target = "~T:Excalibur.Data.ElasticSearch.Monitoring.ElasticsearchPerformanceDiagnostics.PerformanceMetrics")]
[assembly: SuppressMessage("Design", "CA1034:Nested types should not be visible", Justification = "Part of public API - exposed through PerformanceMetrics", Scope = "type", Target = "~T:Excalibur.Data.ElasticSearch.Monitoring.ElasticsearchPerformanceDiagnostics.MemoryUsageInfo")]
[assembly: SuppressMessage("Design", "CA1034:Nested types should not be visible", Justification = "Part of public API - returned from StartOperation()", Scope = "type", Target = "~T:Excalibur.Data.ElasticSearch.Monitoring.ElasticsearchPerformanceDiagnostics.PerformanceContext")]
[assembly: SuppressMessage("Design", "CA1034:Nested types should not be visible", Justification = "Part of public API - used as public property type in SecurityAlertRequest", Scope = "type", Target = "~T:Excalibur.Data.ElasticSearch.Security.SecurityAlertRequest.AlertCriteria")]
[assembly: SuppressMessage("Design", "CA1034:Nested types should not be visible", Justification = "Part of public API - used as public property type in SecurityMonitoringStatus", Scope = "type", Target = "~T:Excalibur.Data.ElasticSearch.Security.SecurityMonitoringStatus.MonitoringConfiguration")]
[assembly: SuppressMessage("Design", "CA1034:Nested types should not be visible", Justification = "Part of public API - used as public property type in SecurityMonitoringStatus", Scope = "type", Target = "~T:Excalibur.Data.ElasticSearch.Security.SecurityMonitoringStatus.MonitoringStatistics")]

// CA1506: Excessive class coupling is acceptable for comprehensive monitoring and auditing classes
// These classes integrate with multiple Elasticsearch APIs and security systems by design
[assembly: SuppressMessage("Maintainability", "CA1506:Avoid excessive class coupling", Justification = "Elasticsearch integration requires coupling with multiple ES and security types")]

// CA1308: ToLowerInvariant is required for Elasticsearch index names which must be lowercase per ES specification
[assembly: SuppressMessage("Globalization", "CA1308:Normalize strings to uppercase", Justification = "Elasticsearch index names must be lowercase per ES specification")]

// CA5394: Random is used for jitter in retry policies, not for security purposes
[assembly: SuppressMessage("Security", "CA5394:Do not use insecure randomness", Justification = "Random is used for retry jitter, not security purposes")]

// CA2213: Disposable fields are managed by DI container lifetime
[assembly: SuppressMessage("Usage", "CA2213:Disposable fields should be disposed", Justification = "Disposable fields are managed by DI container lifetime")]

// CA2000: Objects are transferred to DI container which manages their lifetime
[assembly: SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "Objects are transferred to DI container or returned to caller for lifecycle management")]

// CA2254: Log message templates may vary in resilience scenarios where message includes dynamic context
[assembly: SuppressMessage("Usage", "CA2254:Template should be a static expression", Justification = "Log message templates include dynamic exception context in resilience scenarios")]

// CA2021: Query builder patterns use explicit casts which are safe in context
[assembly: SuppressMessage("Usage", "CA2021:Do not call Enumerable.Cast<T> or Enumerable.OfType<T> with incompatible types", Justification = "Query builder patterns use safe explicit casts")]

// CA1724: Namespace matches Elasticsearch terminology
[assembly: SuppressMessage("Naming", "CA1724:Type names should not match namespaces", Justification = "Type names align with Elasticsearch domain terminology")]

// IDE0060: Parameters reserved for future implementation or API consistency
[assembly: SuppressMessage("Style", "IDE0060:Remove unused parameter", Justification = "Parameters reserved for future implementation or required for API consistency")]

// IDE0005: Unnecessary usings will be cleaned up in future refactoring
[assembly: SuppressMessage("Style", "IDE0005:Using directive is unnecessary", Justification = "Using directives retained for code organization")]
