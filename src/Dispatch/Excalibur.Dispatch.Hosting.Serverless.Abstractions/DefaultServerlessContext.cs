// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Globalization;

namespace Excalibur.Dispatch.Hosting.Serverless;

/// <summary>
/// Default serverless context implementation for local development and fallback scenarios.
/// </summary>
public sealed class DefaultServerlessContext : ServerlessContextBase
{
	/// <summary>
	/// Initializes a new instance of the <see cref="DefaultServerlessContext" /> class.
	/// </summary>
	/// <param name="platform"> The serverless platform. </param>
	/// <param name="logger"> The logger instance. </param>
	public DefaultServerlessContext(ServerlessPlatform platform, ILogger logger)
		: base(new object(), platform, logger)
	{
		RequestId = Guid.NewGuid().ToString();
		FunctionName = Environment.GetEnvironmentVariable("FUNCTION_NAME") ?? "LocalFunction";
		FunctionVersion = "1.0.0";
		InvokedFunctionArn = $"local:{platform.ToString().ToUpperInvariant()}:function:{FunctionName}";
		MemoryLimitInMB = 512;
		LogGroupName = $"/local/{FunctionName}";
		LogStreamName = $"{FunctionName}-{DateTimeOffset.UtcNow.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture)}";
		CloudProvider = GetCloudProviderName(platform);
		Region = "local";
		AccountId = "local";
		ExecutionDeadline = DateTimeOffset.UtcNow.AddMinutes(15);
	}

	/// <inheritdoc />
	public override string RequestId { get; }

	/// <inheritdoc />
	public override string FunctionName { get; }

	/// <inheritdoc />
	public override string FunctionVersion { get; }

	/// <inheritdoc />
	public override string InvokedFunctionArn { get; }

	/// <inheritdoc />
	public override int MemoryLimitInMB { get; }

	/// <inheritdoc />
	public override string LogGroupName { get; }

	/// <inheritdoc />
	public override string LogStreamName { get; }

	/// <inheritdoc />
	public override string CloudProvider { get; }

	/// <inheritdoc />
	public override string Region { get; }

	/// <inheritdoc />
	public override string AccountId { get; }

	/// <inheritdoc />
	public override DateTimeOffset ExecutionDeadline { get; }

	/// <summary>
	/// Gets the cloud provider name for the specified platform.
	/// </summary>
	/// <param name="platform"> The serverless platform. </param>
	/// <returns> The cloud provider name. </returns>
	private static string GetCloudProviderName(ServerlessPlatform platform) =>
		platform switch
		{
			ServerlessPlatform.AwsLambda => "AWS",
			ServerlessPlatform.AzureFunctions => "Azure",
			ServerlessPlatform.GoogleCloudFunctions => "Google Cloud",
			_ => "Unknown",
		};
}
