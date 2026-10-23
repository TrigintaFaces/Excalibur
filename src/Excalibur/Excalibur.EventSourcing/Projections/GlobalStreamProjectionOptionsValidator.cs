// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Microsoft.Extensions.Options;

namespace Excalibur.EventSourcing.Projections;

/// <summary>
/// Validates <see cref="GlobalStreamProjectionOptions"/> at startup so a misconfigured projection host fails
/// fast instead of surfacing as a deep runtime error once the background host starts processing.
/// </summary>
internal sealed class GlobalStreamProjectionOptionsValidator : IValidateOptions<GlobalStreamProjectionOptions>
{
	/// <inheritdoc />
	public ValidateOptionsResult Validate(string? name, GlobalStreamProjectionOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		if (options.CheckpointInterval < 1)
		{
			return ValidateOptionsResult.Fail(
				$"{nameof(GlobalStreamProjectionOptions.CheckpointInterval)} must be greater than zero.");
		}

		if (string.IsNullOrWhiteSpace(options.ProjectionName))
		{
			return ValidateOptionsResult.Fail(
				$"{nameof(GlobalStreamProjectionOptions.ProjectionName)} is required and must not be empty or whitespace.");
		}

		if (options.BatchSize < 1)
		{
			return ValidateOptionsResult.Fail(
				$"{nameof(GlobalStreamProjectionOptions.BatchSize)} must be greater than zero.");
		}

		if (options.IdlePollingInterval <= TimeSpan.Zero)
		{
			return ValidateOptionsResult.Fail(
				$"{nameof(GlobalStreamProjectionOptions.IdlePollingInterval)} must be greater than zero.");
		}

		// THE SUBSCRIPTION DESCRIPTOR IS VALIDATED HERE, NOT ON THE FIRST PAGE.
		//
		// AsyncProjectionProcessingHost refuses a blank descriptor at runtime, which is correct as a
		// second line for options constructed directly, but it is the wrong place for the FIRST line: it
		// fires after the application has reported healthy, from inside a background service, in a log
		// nobody is watching. Refusing at startup names the option while the consumer is still reading
		// their own composition root.
		//
		// It also NAMES ONE OPTION RATHER THAN BOTH. The runtime message says "requires
		// SubscriptionScope and SubscriptionRevision" whichever is missing, and since SubscriptionRevision
		// defaults to "1" it is almost always Scope alone -- so the reader is sent to check something
		// that was never wrong.
		if (string.IsNullOrWhiteSpace(options.SubscriptionScope))
		{
			return ValidateOptionsResult.Fail(
				$"{nameof(GlobalStreamProjectionOptions.SubscriptionScope)} is required and has no default. "
				+ "Set it where you call EnableProjectionProcessing or AddProjectionProcessor, to a stable "
				+ "name identifying this event source and projection destination together (for example "
				+ "\"orders-sqlserver-to-elasticsearch\"). It has no default because a default would be a "
				+ "guess about someone else's topology, and the consequence of guessing wrong is the one "
				+ "the subscription binding exists to prevent: two processes that mean different things "
				+ "sharing one checkpoint, each advancing it past work the other never did.");
		}

		// Reachable despite the "1" default: configuration binding an absent key supplies blank.
		if (string.IsNullOrWhiteSpace(options.SubscriptionRevision))
		{
			return ValidateOptionsResult.Fail(
				$"{nameof(GlobalStreamProjectionOptions.SubscriptionRevision)} must not be blank. It "
				+ "defaults to \"1\", so a blank value means something explicitly set it -- most often "
				+ "configuration binding a key that is absent. Change it when fold logic, routing, "
				+ "dependencies or destination semantics change, and rebuild under a new subscription "
				+ "rather than reusing the old checkpoint.");
		}

		return ValidateOptionsResult.Success;
	}
}
