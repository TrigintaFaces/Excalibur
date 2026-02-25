// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


#if AZURE_FUNCTIONS_SUPPORT
using Microsoft.Azure.Functions.Worker;
#endif

using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Dispatch.Hosting.AzureFunctions;

/// <summary>
/// Azure Functions specific implementation of serverless context.
/// </summary>
public class AzureFunctionsServerlessContext : ServerlessContextBase
{
#if AZURE_FUNCTIONS_SUPPORT
	private readonly FunctionContext _functionContext;
#endif

	/// <summary>
	/// Initializes a new instance of the <see cref="AzureFunctionsServerlessContext" /> class.
	/// </summary>
	/// <param name="functionContext"> The Azure Functions context. </param>
	/// <param name="logger"> The logger instance. </param>
	public AzureFunctionsServerlessContext(object functionContext, ILogger logger)
		: base(functionContext, ServerlessPlatform.AzureFunctions, logger)
	{
#if AZURE_FUNCTIONS_SUPPORT
		if (functionContext is FunctionContext context)
		{
			_functionContext = context;
		}
		else
		{
			throw new ArgumentException(ErrorConstants.FunctionContextMustBeFunctionContext, nameof(functionContext));
		}
#else
		throw new NotSupportedException(ErrorConstants.AzureFunctionsSupportNotAvailable);
#endif
	}

	/// <inheritdoc />
	public override string RequestId =>
#if AZURE_FUNCTIONS_SUPPORT
		_functionContext.FunctionId;
#else
		"unknown";

#endif

	/// <inheritdoc />
	public override string FunctionName =>
#if AZURE_FUNCTIONS_SUPPORT
		_functionContext.FunctionDefinition?.Name ?? Environment.GetEnvironmentVariable("WEBSITE_SITE_NAME") ?? "unknown";
#else
		"unknown";

#endif

	/// <inheritdoc />
	public override string FunctionVersion =>
		Environment.GetEnvironmentVariable("WEBSITE_SLOT_NAME") ?? "Production";

	/// <inheritdoc />
	public override string InvokedFunctionArn
	{
		get
		{
			var siteName = Environment.GetEnvironmentVariable("WEBSITE_SITE_NAME");
			var resourceGroup = Environment.GetEnvironmentVariable("WEBSITE_RESOURCE_GROUP");
			var subscriptionId = Environment.GetEnvironmentVariable("WEBSITE_OWNER_NAME")?.Split('+').FirstOrDefault();

			if (!string.IsNullOrEmpty(siteName) && !string.IsNullOrEmpty(resourceGroup) && !string.IsNullOrEmpty(subscriptionId))
			{
				return
					$"/subscriptions/{subscriptionId}/resourceGroups/{resourceGroup}/providers/Microsoft.Web/sites/{siteName}/functions/{FunctionName}";
			}

			return
				$"/subscriptions/unknown/resourceGroups/unknown/providers/Microsoft.Web/sites/{siteName ?? "unknown"}/functions/{FunctionName}";
		}
	}

	/// <inheritdoc />
	public override int MemoryLimitInMB
	{
		get
		{
			// Azure Functions memory limit varies by plan
			var planType = Environment.GetEnvironmentVariable("WEBSITE_SKU");
			return planType switch
			{
				"Dynamic" => 1536, // Consumption plan
				"ElasticPremium" => 3584, // Premium plan
				_ => 1536, // Default to consumption
			};
		}
	}

	/// <inheritdoc />
	public override string LogGroupName
	{
		get
		{
			var siteName = Environment.GetEnvironmentVariable("WEBSITE_SITE_NAME");
			return $"/subscriptions/{AccountId}/resourceGroups/{Region}/providers/Microsoft.Web/sites/{siteName}/functions/{FunctionName}";
		}
	}

	/// <inheritdoc />
	public override string LogStreamName =>
		$"{FunctionName}-{DateTimeOffset.UtcNow:yyyyMMdd-HHmm}";

	/// <inheritdoc />
	public override string CloudProvider => "Azure";

	/// <inheritdoc />
	public override string Region =>
		Environment.GetEnvironmentVariable("WEBSITE_SITE_NAME")?.Split('-').LastOrDefault() ??
		Environment.GetEnvironmentVariable("REGION_NAME") ??
		"eastus";

	/// <inheritdoc />
	public override string AccountId
	{
		get
		{
			var ownerName = Environment.GetEnvironmentVariable("WEBSITE_OWNER_NAME");
			if (!string.IsNullOrEmpty(ownerName))
			{
				var parts = ownerName.Split('+');
				if (parts.Length > 0)
				{
					return parts[0];
				}
			}

			return "unknown";
		}
	}

	/// <inheritdoc />
	public override DateTimeOffset ExecutionDeadline
	{
		get
		{
			// Azure Functions timeout varies by plan
			var planType = Environment.GetEnvironmentVariable("WEBSITE_SKU");
			var timeout = planType switch
			{
				"Dynamic" => TimeSpan.FromMinutes(5), // Consumption plan
				"ElasticPremium" => TimeSpan.FromMinutes(30), // Premium plan
				_ => TimeSpan.FromMinutes(5), // Default to consumption
			};

			return DateTimeOffset.UtcNow.Add(timeout);
		}
	}

#if AZURE_FUNCTIONS_SUPPORT
	/// <summary>
	/// Gets the Azure-specific Function context.
	/// </summary>
	public FunctionContext FunctionContext => _functionContext;

	/// <summary>
	/// Gets the trace context for distributed tracing.
	/// </summary>
	public override Serverless.TraceContext? TraceContext
	{
		get
		{
			var traceParent = _functionContext.TraceContext?.TraceParent;
			if (string.IsNullOrEmpty(traceParent))
			{
				return null;
			}

			// Parse W3C trace context format: version-traceid-spanid-flags
			var parts = traceParent.Split('-');
			if (parts.Length >= 4)
			{
				return new Serverless.TraceContext
				{
					TraceId = parts[1],
					SpanId = parts[2],
					TraceFlags = parts[3],
					TraceState = _functionContext.TraceContext?.TraceState
				};
			}

			return null;
		}
	}
#endif

	/// <inheritdoc />
	protected override void Dispose(bool disposing)
	{
		if (disposing)
		{
			// Azure Functions context doesn't require explicit disposal but we can perform any cleanup here if needed
		}

		base.Dispose(disposing);
	}
}
