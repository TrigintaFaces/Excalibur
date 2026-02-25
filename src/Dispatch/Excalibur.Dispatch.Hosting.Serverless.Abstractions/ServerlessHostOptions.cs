// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.ComponentModel.DataAnnotations;

namespace Excalibur.Dispatch.Hosting.Serverless;

/// <summary>
/// Configuration options for serverless hosting.
/// </summary>
public sealed class ServerlessHostOptions
{
	/// <summary>
	/// Gets or sets the preferred serverless platform. If null, auto-detection will be used.
	/// </summary>
	/// <value>The preferred serverless platform, or <see langword="null"/> to use auto-detection.</value>
	public ServerlessPlatform? PreferredPlatform { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether to enable cold start optimization.
	/// </summary>
	/// <value><see langword="true"/> to enable cold start optimization; otherwise, <see langword="false"/>.</value>
	public bool EnableColdStartOptimization { get; set; } = true;

	/// <summary>
	/// Gets or sets a value indicating whether to enable distributed tracing.
	/// </summary>
	/// <value><see langword="true"/> to enable distributed tracing; otherwise, <see langword="false"/>.</value>
	public bool EnableDistributedTracing { get; set; } = true;

	/// <summary>
	/// Gets or sets a value indicating whether to enable metrics collection.
	/// </summary>
	/// <value><see langword="true"/> to enable metrics collection; otherwise, <see langword="false"/>.</value>
	public bool EnableMetrics { get; set; } = true;

	/// <summary>
	/// Gets or sets a value indicating whether to enable structured logging.
	/// </summary>
	/// <value><see langword="true"/> to enable structured logging; otherwise, <see langword="false"/>.</value>
	public bool EnableStructuredLogging { get; set; } = true;

	/// <summary>
	/// Gets or sets the timeout for function execution.
	/// </summary>
	/// <value>The timeout for function execution, or <see langword="null"/> to use the default timeout.</value>
	public TimeSpan? ExecutionTimeout { get; set; }

	/// <summary>
	/// Gets or sets the memory limit in MB.
	/// </summary>
	/// <value>The memory limit in MB, or <see langword="null"/> to use the default limit.</value>
	[Range(1, int.MaxValue)]
	public int? MemoryLimitMB { get; set; }

	/// <summary>
	/// Gets custom environment variables.
	/// </summary>
	/// <value>The custom environment variables.</value>
	public IDictionary<string, string> EnvironmentVariables { get; } = new Dictionary<string, string>(StringComparer.Ordinal);

	/// <summary>
	/// Gets or sets AWS Lambda specific options.
	/// </summary>
	/// <value>The AWS Lambda specific options.</value>
	public AwsLambdaOptions AwsLambda { get; set; } = new();

	/// <summary>
	/// Gets or sets Azure Functions specific options.
	/// </summary>
	/// <value>The Azure Functions specific options.</value>
	public AzureFunctionsOptions AzureFunctions { get; set; } = new();

	/// <summary>
	/// Gets or sets Google Cloud Functions specific options.
	/// </summary>
	/// <value>The Google Cloud Functions specific options.</value>
	public GoogleCloudFunctionsOptions GoogleCloudFunctions { get; set; } = new();
}
