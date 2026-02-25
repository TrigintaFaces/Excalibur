// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Dispatch.Hosting.Serverless;

/// <summary>
/// Serverless context implementation for scenarios where we need to construct from an envelope.
/// </summary>
public sealed class ServerlessContext : ServerlessContextBase
{
	/// <summary>
	/// Initializes a new instance of the <see cref="ServerlessContext" /> class.
	/// </summary>
	/// <param name="envelope"> The unified message envelope containing context data. </param>
	/// <param name="logger"> The logger instance. </param>
	public ServerlessContext(MessageEnvelope envelope, ILogger logger)
		: base(new object(), DeterminePlatform(envelope), logger)
	{
		ArgumentNullException.ThrowIfNull(envelope);

		RequestId = envelope.RequestId ?? Guid.NewGuid().ToString();
		FunctionName = envelope.FunctionName ?? "unknown";
		FunctionVersion = envelope.FunctionVersion ?? "1.0";
		InvokedFunctionArn = envelope.Source ?? string.Empty;
		MemoryLimitInMB = envelope.GetProviderMetadata<int>("MemoryLimitInMB");
		LogGroupName = envelope.GetProviderMetadata<string>("LogGroupName") ?? string.Empty;
		LogStreamName = envelope.GetProviderMetadata<string>("LogStreamName") ?? string.Empty;
		CloudProvider = envelope.CloudProvider ?? "unknown";
		Region = envelope.Region ?? "unknown";
		AccountId = envelope.GetProviderMetadata<string>("AccountId") ?? string.Empty;
		ExecutionDeadline = envelope.GetProviderMetadata<DateTimeOffset?>("ExecutionDeadline") ?? DateTimeOffset.UtcNow.AddMinutes(15);

		// Restore trace context if available
		if (!string.IsNullOrEmpty(envelope.TraceParent))
		{
			TraceContext = new TraceContext { TraceParent = envelope.TraceParent };
		}
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

	private static ServerlessPlatform DeterminePlatform(MessageEnvelope envelope)
	{
		ArgumentNullException.ThrowIfNull(envelope);

		return envelope.CloudProvider?.ToUpperInvariant() switch
		{
			"AWS" => ServerlessPlatform.AwsLambda,
			"AZURE" => ServerlessPlatform.AzureFunctions,
			"GOOGLE" => ServerlessPlatform.GoogleCloudFunctions,
			_ => ServerlessPlatform.Unknown,
		};
	}
}
