// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Transport.Azure;

/// <summary>
/// Validates <see cref="AzureEventHubsCloudEventOptions"/> at startup so a misconfigured Event Hubs
/// CloudEvents pipeline fails fast rather than at first send.
/// </summary>
internal sealed class AzureEventHubsCloudEventOptionsValidator : IValidateOptions<AzureEventHubsCloudEventOptions>
{
	/// <inheritdoc/>
	public ValidateOptionsResult Validate(string? name, AzureEventHubsCloudEventOptions options)
	{
		if (options is null)
		{
			return ValidateOptionsResult.Fail($"{nameof(AzureEventHubsCloudEventOptions)} must not be null.");
		}

		var failures = new List<string>();

		if (options.MaxBatchSize <= 0)
		{
			failures.Add($"{nameof(options.MaxBatchSize)} must be greater than zero.");
		}

		if (options.MaxBatchSizeBytes <= 0)
		{
			failures.Add($"{nameof(options.MaxBatchSizeBytes)} must be greater than zero.");
		}

		if (options.UseSchemaRegistry && string.IsNullOrWhiteSpace(options.SchemaRegistryNamespace))
		{
			failures.Add(
				$"{nameof(options.SchemaRegistryNamespace)} is required when {nameof(options.UseSchemaRegistry)} is enabled.");
		}

		if (options.EnableCapture && string.IsNullOrWhiteSpace(options.CaptureFileNameFormat))
		{
			failures.Add(
				$"{nameof(options.CaptureFileNameFormat)} is required when {nameof(options.EnableCapture)} is enabled.");
		}

		return failures.Count > 0 ? ValidateOptionsResult.Fail(failures) : ValidateOptionsResult.Success;
	}
}
