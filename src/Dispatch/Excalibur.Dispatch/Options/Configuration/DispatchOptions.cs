// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0


using System.ComponentModel.DataAnnotations;

namespace Excalibur.Dispatch.Options.Configuration;

/// <summary>
/// Configuration options for the Excalibur framework.
/// </summary>
public sealed class DispatchOptions
{
	/// <summary>
	/// Gets or sets the default timeout for message processing.
	/// </summary>
	/// <value> The default timeout for message processing. </value>
	public TimeSpan DefaultTimeout { get; set; } = TimeSpan.FromSeconds(30);

	/// <summary>
	/// Gets or sets the maximum concurrent messages to process.
	/// </summary>
	/// <value>The current <see cref="MaxConcurrency"/> value.</value>
	[Range(1, int.MaxValue)]
	public int MaxConcurrency { get; set; } = Environment.ProcessorCount * 2;

	/// <summary>
	/// Gets or sets a value indicating whether to use light mode (no persistence).
	/// </summary>
	/// <value>The current <see cref="UseLightMode"/> value.</value>
	public bool UseLightMode { get; set; }

	/// <summary>
	/// Gets or sets the message buffer size for pooling.
	/// </summary>
	/// <value>The current <see cref="MessageBufferSize"/> value.</value>
	[Range(1, int.MaxValue)]
	public int MessageBufferSize { get; set; } = 1024;

	/// <summary>
	/// Gets or sets a value indicating whether to synthesize pipeline profiles from the enabled
	/// feature set when no profile has been registered.
	/// <para>
	/// <b>This setting currently has no observable effect, in either position.</b> Profile synthesis is
	/// guarded on no profile being registered, and the profile registry registers six default profiles
	/// in its constructor, so that condition never holds and synthesis does not run whether this is
	/// <see langword="true"/> or <see langword="false"/>. Setting it changes nothing about the
	/// pipelines your host builds. It is documented here rather than silently, because the property is
	/// part of the shipped surface and reads as a feature switch.
	/// </para>
	/// </summary>
	/// <value>The current <see cref="EnablePipelineSynthesis"/> value.</value>
	public bool EnablePipelineSynthesis { get; set; } = true;

	/// <summary>
	/// Gets or sets feature flag configuration for optional Dispatch capabilities.
	/// </summary>
	/// <value> The feature flag sub-options. </value>
	public DispatchFeatureOptions Features { get; set; } = new();

	/// <summary>
	/// Gets or sets inbox configuration options.
	/// </summary>
	/// <value> Inbox configuration options. </value>
	public InboxConfigurationOptions Inbox { get; set; } = new();

	/// <summary>
	/// Gets or sets outbox configuration options.
	/// </summary>
	/// <value> Outbox configuration options. </value>
	public OutboxConfigurationOptions Outbox { get; set; } = new();

	/// <summary>
	/// Gets or sets consumer configuration options.
	/// </summary>
	/// <value> Consumer configuration options. </value>
	public ConsumerOptions Consumer { get; set; } = new();

	/// <summary>
	/// Gets or sets cross-cutting concern configuration (security, observability, resilience, caching, performance).
	/// </summary>
	/// <value> The cross-cutting sub-options. </value>
	public DispatchCrossCuttingOptions CrossCutting { get; set; } = new();

}

/// <summary>
/// Feature flag options for optional Dispatch capabilities.
/// </summary>
public sealed class DispatchFeatureOptions
{
	/// <summary>
	/// Gets or sets a value indicating whether to enable message correlation.
	/// </summary>
	/// <value><see langword="true"/> by default.</value>
	public bool EnableCorrelation { get; set; } = true;

	/// <summary>
	/// Gets or sets a value indicating whether to enable metrics collection.
	/// </summary>
	/// <value><see langword="true"/> by default.</value>
	public bool EnableMetrics { get; set; } = true;

	/// <summary>
	/// Gets or sets a value indicating whether to validate message schemas.
	/// </summary>
	/// <value><see langword="true"/> by default.</value>
	public bool ValidateMessageSchemas { get; set; } = true;
	/// <summary>
	/// Gets or sets a value indicating whether to enable multi-tenancy support.
	/// </summary>
	/// <value><see langword="false"/> by default.</value>
	public bool EnableMultiTenancy { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether to enable message versioning support.
	/// </summary>
	/// <value><see langword="true"/> by default.</value>
	public bool EnableVersioning { get; set; } = true;

	/// <summary>
	/// Gets or sets a value indicating whether to enable authorization checks.
	/// </summary>
	/// <value><see langword="true"/> by default.</value>
	public bool EnableAuthorization { get; set; } = true;

	/// <summary>
	/// Gets or sets a value indicating whether to enable transaction support.
	/// </summary>
	/// <value><see langword="false"/> by default.</value>
	public bool EnableTransactions { get; set; }
}

/// <summary>
/// Cross-cutting concern configuration for the Excalibur framework.
/// </summary>
/// <remarks>
/// Groups observability, resilience, caching, and performance
/// to keep the root <see cref="DispatchOptions"/> within the 10-property gate.
/// </remarks>
public sealed class DispatchCrossCuttingOptions
{
	/// <summary>
	/// Gets or sets performance optimization configuration.
	/// </summary>
	/// <value> Performance optimization configuration. </value>
	public PerformanceOptions Performance { get; set; } = new();
}
