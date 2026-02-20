// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Serialization;
using Excalibur.Dispatch.Abstractions.Validation;

namespace Excalibur.Dispatch.Validation;

/// <summary>
/// Lightweight validation rules for the "internal-event" profile used with internal Events.
/// </summary>
public sealed class InternalEventProfileValidationRules : IProfileValidationRules
{
	private readonly List<ICustomValidator> _customValidators;
	private readonly List<IFieldConstraint> _fieldConstraints;

	/// <summary>
	/// Initializes a new instance of the <see cref="InternalEventProfileValidationRules"/> class with lightweight validation settings for internal events.
	/// </summary>
	public InternalEventProfileValidationRules()
	{
		_customValidators = [new EventTimestampValidator(), new EventSourceValidator()];

		_fieldConstraints =
		[
			new NonEmptyStringConstraint("EventType", "EventType cannot be empty for events"),
			new PositiveNumberConstraint("SequenceNumber", "SequenceNumber must be non-negative for events"),
		];
	}

	/// <summary>
	/// Gets the name identifier for this validation profile.
	/// </summary>
	/// <value>The current <see cref="ProfileName"/> value.</value>
	public string ProfileName => "internal-event";

	/// <summary>
	/// Gets the validation level that determines strictness of validation rules.
	/// </summary>
	/// <value>The current <see cref="ValidationLevel"/> value.</value>
	public ValidationLevel ValidationLevel => ValidationLevel.Basic;

	/// <summary>
	/// Gets the maximum allowed message size in bytes for this profile.
	/// </summary>
	/// <value>
	/// The maximum allowed message size in bytes for this profile.
	/// </value>
	public int MaxMessageSize => 2_097_152; // 2MB for events (can be larger)

	/// <summary>
	/// Gets the list of field names that are required to be present in internal event messages using this profile.
	/// </summary>
	/// <value>The current <see cref="RequiredFields"/> value.</value>
	public IReadOnlyList<string> RequiredFields => new[] { "EventType", "Timestamp" };

	/// <summary>
	/// Gets the collection of custom validators that perform event-specific validation logic.
	/// </summary>
	/// <value>The current <see cref="CustomValidators"/> value.</value>
	public IReadOnlyList<ICustomValidator> CustomValidators => _customValidators;

	/// <summary>
	/// Gets the collection of field constraints that define validation rules for specific internal event fields.
	/// </summary>
	/// <value>The current <see cref="FieldConstraints"/> value.</value>
	public IReadOnlyList<IFieldConstraint> FieldConstraints => _fieldConstraints;

	private sealed class EventTimestampValidator : ICustomValidator
	{
		public IValidationResult Validate(IDispatchMessage message, IMessageContext context)
		{
			if (message is IDispatchEvent)
			{
				// Events must have a timestamp
				if (context.ReceivedTimestampUtc == default)
				{
					return SerializableValidationResult.Failed("Events must have a timestamp");
				}

				// Timestamp should not be too far in the future (clock skew tolerance)
				var maxFuture = DateTimeOffset.UtcNow.AddMinutes(5);
				if (context.ReceivedTimestampUtc > maxFuture)
				{
					return SerializableValidationResult.Failed("Event timestamp cannot be more than 5 minutes in the future");
				}

				// Timestamp should not be too old (for internal events)
				var maxPast = DateTimeOffset.UtcNow.AddDays(-7);
				if (context.ReceivedTimestampUtc < maxPast)
				{
					return SerializableValidationResult.Failed("Internal event timestamp cannot be older than 7 days");
				}
			}

			return SerializableValidationResult.Success();
		}
	}

	private sealed class EventSourceValidator : ICustomValidator
	{
		public IValidationResult Validate(IDispatchMessage message, IMessageContext context)
		{
			if (message is IDispatchEvent)
			{
				// Internal events should have a source identifier
				if (string.IsNullOrWhiteSpace(context.Source))
				{
					return SerializableValidationResult.Failed("Internal events must have a source identifier");
				}

				// Source should follow naming convention for internal services
				if (!IsValidInternalSource(context.Source))
				{
					return SerializableValidationResult.Failed(
						$"Source '{context.Source}' does not follow internal service naming convention");
				}
			}

			return SerializableValidationResult.Success();
		}

		private static bool IsValidInternalSource(string source) =>

			// Internal sources should follow pattern: service.component or service/component
			source.Contains('.', StringComparison.Ordinal) || source.Contains('/', StringComparison.Ordinal);
	}
}
