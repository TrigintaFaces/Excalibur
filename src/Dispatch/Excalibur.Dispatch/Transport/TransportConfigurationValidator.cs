// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Globalization;
using System.Runtime.InteropServices;

using Excalibur.Dispatch.Abstractions.Configuration;

namespace Excalibur.Dispatch.Transport;

/// <summary>
/// Default implementation of <see cref="ITransportConfigurationValidator"/> that validates
/// transport registrations before startup.
/// </summary>
/// <remarks>
/// <para>
/// This validator performs the following checks:
/// <list type="bullet">
///   <item><strong>Name uniqueness:</strong> Ensures no duplicate transport names exist.</item>
///   <item><strong>Required properties:</strong> Validates transport-specific required options.</item>
///   <item><strong>Format validation:</strong> Validates cron expressions and connection string formats.</item>
/// </list>
/// </para>
/// </remarks>
public sealed class TransportConfigurationValidator : ITransportConfigurationValidator
{
	/// <inheritdoc/>
	public TransportValidationResult Validate(IEnumerable<TransportRegistrationInfo> registrations)
	{
		ArgumentNullException.ThrowIfNull(registrations);
		var errors = new List<TransportValidationError>();
		var nameStates = new Dictionary<string, DuplicateNameState>(StringComparer.OrdinalIgnoreCase);
		var hasRegistrations = false;

		// Single-pass duplicate tracking + registration validation.
		foreach (var registration in registrations)
		{
			hasRegistrations = true;
			TrackDuplicateName(registration.Name, nameStates);
			ValidateRegistration(registration, errors);
		}

		if (!hasRegistrations)
		{
			return TransportValidationResult.Success();
		}

		AddDuplicateNameErrors(nameStates, errors);

		return errors.Count == 0
			? TransportValidationResult.Success()
			: TransportValidationResult.Failure(errors);
	}

	private static void TrackDuplicateName(
		string transportName,
		Dictionary<string, DuplicateNameState> nameStates)
	{
		ref var state = ref CollectionsMarshal.GetValueRefOrAddDefault(nameStates, transportName, out var exists);

		if (!exists)
		{
			state = new DuplicateNameState(transportName, 1);
			return;
		}

		state.Count++;
	}

	private static void AddDuplicateNameErrors(
		Dictionary<string, DuplicateNameState> nameStates,
		List<TransportValidationError> errors)
	{
		foreach (var (_, state) in nameStates)
		{
			if (state.Count <= 1)
			{
				continue;
			}

			errors.Add(new TransportValidationError(
				state.FirstSeenName,
				"Name",
				string.Format(
					CultureInfo.CurrentCulture,
					Resources.TransportConfigurationValidator_DuplicateTransportNameFormat,
					state.FirstSeenName)));
		}
	}

	private struct DuplicateNameState(string firstSeenName, int count)
	{
		public string FirstSeenName = firstSeenName;
		public int Count = count;
	}

	private static void ValidateRegistration(
		TransportRegistrationInfo registration,
		List<TransportValidationError> errors)
	{
		// Validate name is not empty
		if (string.IsNullOrWhiteSpace(registration.Name))
		{
			errors.Add(new TransportValidationError(
				registration.Name ?? "(empty)",
				"Name",
				Resources.TransportConfigurationValidator_TransportNameCannotBeNullOrEmpty));
			return;
		}

		// Validate transport type is not empty
		if (string.IsNullOrWhiteSpace(registration.TransportType))
		{
			errors.Add(new TransportValidationError(
				registration.Name,
				"TransportType",
				Resources.TransportConfigurationValidator_TransportTypeCannotBeNullOrEmpty));
			return;
		}

		// Validate transport-specific options
		var transportType = registration.TransportType;
		if (string.Equals(transportType, TransportTypes.Kafka, StringComparison.OrdinalIgnoreCase))
		{
			ValidateKafkaOptions(registration, errors);
		}
		else if (string.Equals(transportType, TransportTypes.RabbitMQ, StringComparison.OrdinalIgnoreCase) ||
			string.Equals(transportType, TransportTypes.AzureServiceBus, StringComparison.OrdinalIgnoreCase))
		{
			ValidateConnectionStringOptions(registration, errors);
		}
		else if (string.Equals(transportType, TransportTypes.CronTimer, StringComparison.OrdinalIgnoreCase))
		{
			ValidateCronTimerOptions(registration, errors);
		}
		// InMemory and unknown types have no required options
	}

	private static void ValidateKafkaOptions(
		TransportRegistrationInfo registration,
		List<TransportValidationError> errors)
	{
		if (registration.Options == null)
		{
			return; // Options might be configured elsewhere
		}

		// Check BootstrapServers if present
		if (registration.Options.TryGetValue(OptionNames.BootstrapServers, out var bootstrapServers))
		{
			if (bootstrapServers is string bootstrapServersStr && string.IsNullOrWhiteSpace(bootstrapServersStr))
			{
				errors.Add(new TransportValidationError(
					registration.Name,
					OptionNames.BootstrapServers,
					Resources.TransportConfigurationValidator_KafkaBootstrapServersCannotBeEmpty));
			}
		}
	}

	private static void ValidateConnectionStringOptions(
		TransportRegistrationInfo registration,
		List<TransportValidationError> errors)
	{
		if (registration.Options == null)
		{
			return; // Options might be configured elsewhere
		}

		// Check ConnectionString if present
		if (registration.Options.TryGetValue(OptionNames.ConnectionString, out var connectionString))
		{
			if (connectionString is string connectionStringStr && string.IsNullOrWhiteSpace(connectionStringStr))
			{
				errors.Add(new TransportValidationError(
					registration.Name,
					OptionNames.ConnectionString,
					string.Format(
						CultureInfo.CurrentCulture,
						Resources.TransportConfigurationValidator_ConnectionStringCannotBeEmptyFormat,
						GetTransportDisplayName(registration.TransportType))));
			}
		}
	}

	private static void ValidateCronTimerOptions(
		TransportRegistrationInfo registration,
		List<TransportValidationError> errors)
	{
		if (registration.Options == null)
		{
			return; // Options might be configured elsewhere
		}

		// Check CronExpression if present
		if (registration.Options.TryGetValue(OptionNames.CronExpression, out var cronExpression))
		{
			if (cronExpression is string cronExpressionStr)
			{
				if (string.IsNullOrWhiteSpace(cronExpressionStr))
				{
					errors.Add(new TransportValidationError(
						registration.Name,
						OptionNames.CronExpression,
						Resources.TransportConfigurationValidator_CronExpressionCannotBeEmpty));
				}
				else if (!IsValidCronExpression(cronExpressionStr))
				{
					errors.Add(new TransportValidationError(
						registration.Name,
						OptionNames.CronExpression,
						string.Format(
							CultureInfo.CurrentCulture,
							Resources.TransportConfigurationValidator_InvalidCronExpressionFormat,
							cronExpressionStr)));
				}
			}
		}
	}

	private static bool IsValidCronExpression(string expression)
	{
		// Basic cron expression validation: 5 or 6 space-separated fields
		var parts = expression.Split(' ', StringSplitOptions.RemoveEmptyEntries);
		return parts.Length is 5 or 6;
	}

	private static string GetTransportDisplayName(string transportType)
	{
		if (string.Equals(transportType, TransportTypes.AzureServiceBus, StringComparison.OrdinalIgnoreCase))
		{
			return "Azure Service Bus";
		}

		if (string.Equals(transportType, TransportTypes.RabbitMQ, StringComparison.OrdinalIgnoreCase))
		{
			return "RabbitMQ";
		}

		if (string.Equals(transportType, TransportTypes.Kafka, StringComparison.OrdinalIgnoreCase))
		{
			return "Kafka";
		}

		if (string.Equals(transportType, TransportTypes.CronTimer, StringComparison.OrdinalIgnoreCase))
		{
			return "CronTimer";
		}

		if (string.Equals(transportType, TransportTypes.InMemory, StringComparison.OrdinalIgnoreCase))
		{
			return "InMemory";
		}

		return transportType;
	}

	/// <summary>
	/// Well-known transport type identifiers.
	/// </summary>
	private static class TransportTypes
	{
		public const string Kafka = "kafka";
		public const string RabbitMQ = "rabbitmq";
		public const string AzureServiceBus = "azure-servicebus";
		public const string CronTimer = "crontimer";
		public const string InMemory = "inmemory";
	}

	/// <summary>
	/// Well-known option property names.
	/// </summary>
	private static class OptionNames
	{
		public const string BootstrapServers = "BootstrapServers";
		public const string ConnectionString = "ConnectionString";
		public const string CronExpression = "CronExpression";
	}

}
