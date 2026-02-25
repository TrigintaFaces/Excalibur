// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Text.RegularExpressions;

namespace Excalibur.Hosting.Configuration.Validators;

/// <summary>
/// Base class for cloud provider configuration validators.
/// </summary>
/// <remarks> Initializes a new instance of the <see cref="CloudProviderValidator" /> class. </remarks>
/// <param name="configurationName"> The name of the cloud provider configuration. </param>
public abstract partial class CloudProviderValidator(string configurationName) : ConfigurationValidatorBase(configurationName, priority: 20)
{
	/// <summary>
	/// Validates a cloud region identifier.
	/// </summary>
	/// <param name="region"> The region to validate. </param>
	/// <param name="validRegions"> Set of valid region identifiers. </param>
	/// <param name="errors"> The list to add errors Excalibur.Dispatch.Transport.Aws.Sqs.LongPolling.Configuration. </param>
	/// <param name="configPath"> The configuration path for error reporting. </param>
	/// <returns> True if valid, false otherwise. </returns>
	protected static bool ValidateRegion(
		string? region,
		IReadOnlySet<string> validRegions,
		ICollection<ConfigurationValidationError> errors,
		string configPath)
	{
		ArgumentNullException.ThrowIfNull(validRegions);
		ArgumentNullException.ThrowIfNull(errors);

		if (string.IsNullOrWhiteSpace(region))
		{
			errors.Add(new ConfigurationValidationError(
				"Cloud region is missing or empty",
				configPath,
				value: null,
				$"Set to one of: {string.Join(", ", validRegions.Take(5))}..."));
			return false;
		}

		if (!validRegions.Contains(region))
		{
			errors.Add(new ConfigurationValidationError(
				$"Invalid cloud region '{region}'",
				configPath,
				region,
				$"Set to one of: {string.Join(", ", validRegions.Take(5))}..."));
			return false;
		}

		return true;
	}

	/// <summary>
	/// Validates an ARN (Amazon Resource Name) format.
	/// </summary>
	/// <param name="arn">The ARN to validate.</param>
	/// <param name="errors">The list to add validation errors to.</param>
	/// <param name="configPath">The configuration path for error reporting.</param>
	/// <returns><see langword="true"/> if the ARN is valid; otherwise, <see langword="false"/>.</returns>
	protected static bool ValidateArn(string? arn, ICollection<ConfigurationValidationError> errors, string configPath)
	{
		ArgumentNullException.ThrowIfNull(errors);

		if (string.IsNullOrWhiteSpace(arn))
		{
			errors.Add(new ConfigurationValidationError(
				"ARN is missing or empty",
				configPath,
				value: null,
				"Provide a valid AWS ARN (e.g., arn:aws:service:region:account:resource)"));
			return false;
		}

		if (!ArnRegex().IsMatch(arn))
		{
			errors.Add(new ConfigurationValidationError(
				"Invalid ARN format",
				configPath,
				arn,
				"Use format: arn:partition:service:region:account:resource"));
			return false;
		}

		return true;
	}

	/// <summary>
	/// Validates an Azure resource ID format.
	/// </summary>
	/// <param name="resourceId">The Azure resource ID to validate.</param>
	/// <param name="errors">The list to add validation errors to.</param>
	/// <param name="configPath">The configuration path for error reporting.</param>
	/// <returns><see langword="true"/> if the Azure resource ID is valid; otherwise, <see langword="false"/>.</returns>
	protected static bool ValidateAzureResourceId(
		string? resourceId,
		ICollection<ConfigurationValidationError> errors,
		string configPath)
	{
		ArgumentNullException.ThrowIfNull(errors);

		if (string.IsNullOrWhiteSpace(resourceId))
		{
			errors.Add(new ConfigurationValidationError(
				"Azure resource ID is missing or empty",
				configPath,
				value: null,
				"Provide a valid Azure resource ID"));
			return false;
		}

		if (!resourceId.StartsWith("/subscriptions/", StringComparison.OrdinalIgnoreCase))
		{
			errors.Add(new ConfigurationValidationError(
				"Invalid Azure resource ID format",
				configPath,
				resourceId,
				"Resource ID should start with /subscriptions/"));
			return false;
		}

		return true;
	}

	[GeneratedRegex(@"^arn:[\w\-]+:[\w\-]+:([\w\-]*)?:(\d{12})?:(.+)$")]
	private static partial Regex ArnRegex();
}
