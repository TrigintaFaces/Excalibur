// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


#if GOOGLE_CLOUD_FUNCTIONS_SUPPORT
using Microsoft.AspNetCore.Http;
#endif

namespace Excalibur.Dispatch.Hosting.GoogleCloud;

/// <summary>
/// Google Cloud Functions specific implementation of serverless context.
/// </summary>
public class GoogleCloudFunctionsServerlessContext : ServerlessContextBase
{
#if GOOGLE_CLOUD_FUNCTIONS_SUPPORT
    private readonly HttpContext? _typedHttpContext;
#endif

	/// <summary>
	/// Initializes a new instance of the <see cref="GoogleCloudFunctionsServerlessContext" /> class.
	/// </summary>
	/// <param name="httpContext"> The HTTP context from Google Cloud Functions. </param>
	/// <param name="logger"> The logger instance. </param>
	public GoogleCloudFunctionsServerlessContext(object httpContext, ILogger logger)
		: base(httpContext, ServerlessPlatform.GoogleCloudFunctions, logger)
	{
#if GOOGLE_CLOUD_FUNCTIONS_SUPPORT
        if (httpContext is HttpContext context)
        {
            _typedHttpContext = context;
        }
#endif
	}

	/// <inheritdoc />
	public override string RequestId
	{
		get
		{
#if GOOGLE_CLOUD_FUNCTIONS_SUPPORT
            if (_typedHttpContext != null)
            {
                return _typedHttpContext.TraceIdentifier;
            }
#endif
			return Environment.GetEnvironmentVariable("FUNCTION_EXECUTION_ID") ?? Guid.NewGuid().ToString();
		}
	}

	/// <inheritdoc />
	public override string FunctionName =>
		Environment.GetEnvironmentVariable("FUNCTION_NAME") ??
		Environment.GetEnvironmentVariable("K_SERVICE") ??
		"unknown";

	/// <inheritdoc />
	public override string FunctionVersion =>
		Environment.GetEnvironmentVariable("K_REVISION") ?? "1";

	/// <inheritdoc />
	public override string InvokedFunctionArn
	{
		get
		{
			var projectId = Environment.GetEnvironmentVariable("GOOGLE_CLOUD_PROJECT");
			var region = Environment.GetEnvironmentVariable("GOOGLE_CLOUD_REGION") ?? Region;
			var functionName = FunctionName;

			if (!string.IsNullOrEmpty(projectId))
			{
				return $"projects/{projectId}/locations/{region}/functions/{functionName}";
			}

			return $"projects/unknown/locations/{region}/functions/{functionName}";
		}
	}

	/// <inheritdoc />
	public override int MemoryLimitInMB
	{
		get
		{
			// Google Cloud Functions memory limit from environment variable
			var memoryStr = Environment.GetEnvironmentVariable("FUNCTION_MEMORY_MB");
			if (int.TryParse(memoryStr, out var memory))
			{
				return memory;
			}

			return 256; // Default memory limit
		}
	}

	/// <inheritdoc />
	public override string LogGroupName
	{
		get
		{
			var projectId = Environment.GetEnvironmentVariable("GOOGLE_CLOUD_PROJECT");
			return $"projects/{projectId}/logs/cloudfunctions.googleapis.com%2Fcloud-functions";
		}
	}

	/// <inheritdoc />
	public override string LogStreamName =>
		$"{FunctionName}-{DateTimeOffset.UtcNow:yyyyMMdd-HHmm}";

	/// <inheritdoc />
	public override string CloudProvider => "Google Cloud";

	/// <inheritdoc />
	public override string Region =>
		Environment.GetEnvironmentVariable("GOOGLE_CLOUD_REGION") ??
		Environment.GetEnvironmentVariable("FUNCTION_REGION") ??
		"us-central1";

	/// <inheritdoc />
	public override string AccountId =>
		Environment.GetEnvironmentVariable("GOOGLE_CLOUD_PROJECT") ?? "unknown";

	/// <inheritdoc />
	public override DateTimeOffset ExecutionDeadline
	{
		get
		{
			// Google Cloud Functions timeout varies by configuration. Default is 60 seconds, maximum is 540 seconds (9 minutes)
			var timeoutStr = Environment.GetEnvironmentVariable("FUNCTION_TIMEOUT_SEC");
			var timeout = int.TryParse(timeoutStr, out var timeoutSec) ? TimeSpan.FromSeconds(timeoutSec) : TimeSpan.FromMinutes(1);

			return DateTimeOffset.UtcNow.Add(timeout);
		}
	}

#if GOOGLE_CLOUD_FUNCTIONS_SUPPORT
/// <summary>
/// Gets the Google Cloud-specific HTTP context.
/// </summary>
    public HttpContext? HttpContext => _typedHttpContext;

/// <summary>
/// Gets the execution environment information.
/// </summary>
    public string ExecutionId => Environment.GetEnvironmentVariable("FUNCTION_EXECUTION_ID") ?? RequestId;

/// <summary>
/// Gets the service name for Cloud Run functions.
/// </summary>
    public string ServiceName => Environment.GetEnvironmentVariable("K_SERVICE") ?? FunctionName;

/// <summary>
/// Gets the revision name for Cloud Run functions.
/// </summary>
    public string RevisionName => Environment.GetEnvironmentVariable("K_REVISION") ?? "1";

/// <summary>
/// Gets the configuration name for Cloud Run functions.
/// </summary>
    public string ConfigurationName => Environment.GetEnvironmentVariable("K_CONFIGURATION") ?? ServiceName;
#endif

	/// <inheritdoc />
	protected override void Dispose(bool disposing)
	{
		if (disposing)
		{
			// Google Cloud Functions context doesn't require explicit disposal
		}

		base.Dispose(disposing);
	}
}
