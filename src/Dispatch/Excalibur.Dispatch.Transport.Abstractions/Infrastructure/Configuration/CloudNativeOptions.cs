// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.ComponentModel.DataAnnotations;

namespace Excalibur.Dispatch.Transport;

/// <summary>
/// Comprehensive configuration options for enabling and customizing cloud-native features across multi-cloud messaging infrastructure.
/// Provides centralized control over observability, resource tagging, environment-specific settings, and cloud provider integration.
/// </summary>
/// <remarks>
/// <para>
/// This configuration class serves as the foundation for cloud-native capabilities in the Excalibur framework. It enables
/// consistent configuration across different cloud providers (AWS, Azure, Google Cloud) while maintaining provider-specific optimizations.
/// </para>
/// <para>
/// The options support enterprise deployment patterns including:
/// - Multi-environment configurations (development, staging, production)
/// - Cross-region deployments with regional customization
/// - Comprehensive observability through logging, metrics, and distributed tracing
/// - Resource organization through flexible tagging strategies
/// - Performance tuning through configurable flush intervals and batching.
/// </para>
/// <para>
/// Configuration can be bound from various sources including appsettings.json, environment variables, Azure Key Vault, AWS Systems Manager
/// Parameter Store, or Google Cloud Secret Manager for secure and flexible deployment scenarios.
/// </para>
/// </remarks>
public sealed class CloudNativeOptions
{
	/// <summary>
	/// Gets or sets a value indicating whether cloud-native features are enabled globally. When disabled, the messaging system falls back
	/// to local implementations without cloud integrations.
	/// </summary>
	/// <value>
	/// <c> true </c> to enable cloud-native features including observability, distributed tracing, and cloud-specific optimizations;
	/// otherwise, <c> false </c>. Default is <c> true </c>.
	/// </value>
	/// <remarks>
	/// <para>
	/// This master switch controls all cloud-native functionality. When disabled, individual feature flags (UseCloudLogging,
	/// UseCloudMetrics, UseCloudTracing) are ignored.
	/// </para>
	/// <para>
	/// Disabling cloud-native features is useful for local development, testing scenarios, or on-premises deployments where cloud services
	/// are not available.
	/// </para>
	/// </remarks>
	/// <value>The current <see cref="Enabled"/> value.</value>
	public bool Enabled { get; set; } = true;

	/// <summary>
	/// Gets or sets a value indicating whether to use cloud-native structured logging capabilities. Enables integration with cloud logging
	/// services for centralized log aggregation and analysis.
	/// </summary>
	/// <value>
	/// <c> true </c> to enable cloud-native logging with structured formats, correlation IDs, and cloud service integration; otherwise, <c>
	/// false </c>. Default is <c> true </c>.
	/// </value>
	/// <remarks>
	/// <para>
	/// Cloud logging provides enhanced capabilities including:
	/// - Structured logging with JSON formatting for better searchability
	/// - Automatic correlation ID injection for request tracing
	/// - Integration with cloud monitoring services (CloudWatch, Azure Monitor, Google Cloud Logging)
	/// - Enhanced metadata including source location, environment, and deployment information.
	/// </para>
	/// <para>
	/// When enabled, log entries are enriched with cloud-specific metadata and formatted for optimal integration with cloud observability platforms.
	/// </para>
	/// </remarks>
	/// <value>The current <see cref="UseCloudLogging"/> value.</value>
	public bool UseCloudLogging { get; set; } = true;

	/// <summary>
	/// Gets or sets a value indicating whether to enable cloud-native metrics collection and reporting. Activates integration with cloud
	/// monitoring services for comprehensive performance monitoring.
	/// </summary>
	/// <value>
	/// <c> true </c> to enable cloud metrics including custom metrics, performance counters, and business metrics; otherwise, <c> false
	/// </c>. Default is <c> true </c>.
	/// </value>
	/// <remarks>
	/// <para>
	/// Cloud metrics provide comprehensive monitoring capabilities including:
	/// - Application performance metrics (message throughput, processing latency, error rates)
	/// - Infrastructure metrics (CPU, memory, network utilization)
	/// - Business metrics (transaction counts, revenue tracking, user engagement)
	/// - Custom metrics specific to messaging patterns and business logic.
	/// </para>
	/// <para>
	/// Metrics are automatically batched and flushed according to the configured MetricsFlushInterval to optimize performance and reduce
	/// cloud service costs.
	/// </para>
	/// </remarks>
	/// <value>The current <see cref="UseCloudMetrics"/> value.</value>
	public bool UseCloudMetrics { get; set; } = true;

	/// <summary>
	/// Gets or sets a value indicating whether to enable distributed tracing for request correlation across service boundaries. Provides
	/// end-to-end visibility into message processing workflows.
	/// </summary>
	/// <value>
	/// <c> true </c> to enable distributed tracing with automatic span creation and correlation; otherwise, <c> false </c>. Default is <c>
	/// true </c>.
	/// </value>
	/// <remarks>
	/// <para>
	/// Cloud tracing enables comprehensive request flow visualization including:
	/// - Automatic span creation for message processing operations
	/// - Cross-service correlation through trace context propagation
	/// - Performance bottleneck identification through timing analysis
	/// - Error correlation and root cause analysis across distributed systems.
	/// </para>
	/// <para>
	/// Integrates with cloud tracing services including AWS X-Ray, Azure Application Insights, Google Cloud Trace, and
	/// OpenTelemetry-compatible systems for vendor-neutral observability.
	/// </para>
	/// </remarks>
	/// <value>The current <see cref="UseCloudTracing"/> value.</value>
	public bool UseCloudTracing { get; set; } = true;

	/// <summary>
	/// Gets or sets the cloud provider identifier for provider-specific optimizations and integrations. Enables automatic configuration of
	/// provider-specific features and service endpoints.
	/// </summary>
	/// <value>
	/// A string identifying the cloud provider. Standard values include "aws", "azure", "gcp", or empty string for provider-agnostic
	/// configuration. Default is empty string.
	/// </value>
	/// <remarks>
	/// <para>
	/// Provider-specific configurations enable optimizations including:
	/// - Automatic service endpoint discovery and regional routing
	/// - Provider-specific authentication and credential management
	/// - Optimized batching and retry strategies for each cloud service
	/// - Integration with provider-specific monitoring and logging services.
	/// </para>
	/// <para>
	/// Supported providers: "aws" (Amazon Web Services), "azure" (Microsoft Azure), "gcp" (Google Cloud Platform). Leave empty for
	/// multi-cloud or provider-agnostic deployments.
	/// </para>
	/// </remarks>
	/// <value>The current <see cref="Provider"/> value.</value>
	public string Provider { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets the deployment environment identifier for environment-specific configurations. Enables different behavior and resource
	/// allocation based on deployment stage.
	/// </summary>
	/// <value>
	/// A string identifying the deployment environment. Common values include "development", "staging", "production". Default is "production".
	/// </value>
	/// <remarks>
	/// <para>
	/// Environment-specific configurations enable:
	/// - Different logging levels and verbosity for development vs. production
	/// - Environment-appropriate resource allocation and scaling policies
	/// - Conditional feature enablement based on deployment stage
	/// - Environment-specific monitoring and alerting thresholds.
	/// </para>
	/// <para>
	/// The environment setting influences security policies, performance tuning, and observability configuration to match operational requirements.
	/// </para>
	/// </remarks>
	/// <value>The current <see cref="Environment"/> value.</value>
	[Required]
	public string Environment { get; set; } = "production";

	/// <summary>
	/// Gets or sets the cloud region identifier for regional deployments and data residency requirements. Enables region-specific
	/// optimizations and compliance with data sovereignty regulations.
	/// </summary>
	/// <value>
	/// A cloud provider-specific region identifier (e.g., "us-east-1", "westeurope", "asia-northeast1"). Default is empty string for
	/// region-agnostic configuration.
	/// </value>
	/// <remarks>
	/// <para>
	/// Regional configuration enables:
	/// - Data residency compliance for regulatory requirements (GDPR, CCPA, etc.)
	/// - Latency optimization through regional service endpoints
	/// - Disaster recovery and cross-region failover strategies
	/// - Regional cost optimization and resource allocation.
	/// </para>
	/// <para>
	/// Region identifiers should match the cloud provider's standard naming conventions. Leave empty for applications that don't require
	/// region-specific behavior.
	/// </para>
	/// </remarks>
	/// <value>The current <see cref="Region"/> value.</value>
	public string Region { get; set; } = string.Empty;

	/// <summary>
	/// Gets custom metadata tags for cloud resource organization, cost allocation, and governance. Provides flexible resource
	/// categorization for operational and financial management.
	/// </summary>
	/// <value>
	/// A dictionary of key-value pairs representing resource tags. Keys and values should follow cloud provider naming conventions. Default
	/// is an empty dictionary.
	/// </value>
	/// <remarks>
	/// <para>
	/// Resource tags enable comprehensive cloud governance including:
	/// - Cost allocation and chargeback tracking by department, project, or environment
	/// - Resource organization and filtering in cloud management consoles
	/// - Automated compliance scanning and policy enforcement
	/// - Lifecycle management and automated cleanup based on tag criteria.
	/// </para>
	/// <para>
	/// Common tag patterns include: "Environment", "Project", "Owner", "CostCenter", "Application", "Version", "Criticality". Follow your
	/// organization's tagging strategy for consistent resource management across deployments.
	/// </para>
	/// </remarks>
	/// <value>The current <see cref="Tags"/> value.</value>
	public Dictionary<string, string> Tags { get; init; } = [];

	/// <summary>
	/// Gets or sets the interval for batching and flushing metrics data to cloud monitoring services. Balances real-time observability with
	/// performance optimization and cost management.
	/// </summary>
	/// <value>
	/// A <see cref="TimeSpan" /> representing the flush interval. Minimum recommended value is 10 seconds, maximum is 300 seconds (5
	/// minutes). Default is 60 seconds.
	/// </value>
	/// <remarks>
	/// <para>
	/// The flush interval directly impacts:
	/// - Observability latency: shorter intervals provide more real-time metrics
	/// - Performance overhead: frequent flushes consume more CPU and network resources
	/// - Cloud service costs: more frequent API calls increase monitoring service charges
	/// - Memory usage: longer intervals require larger metric buffering.
	/// </para>
	/// <para>
	/// Recommended intervals by environment:
	/// - Development: 30-60 seconds for quick feedback
	/// - Staging: 60-120 seconds for cost-effective testing
	/// - Production: 60-300 seconds based on monitoring requirements and SLA needs.
	/// </para>
	/// </remarks>
	public TimeSpan MetricsFlushInterval { get; set; } = TimeSpan.FromSeconds(60);
}
