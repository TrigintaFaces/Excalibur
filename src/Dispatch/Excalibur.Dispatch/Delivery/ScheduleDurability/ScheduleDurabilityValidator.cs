// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Delivery;

/// <summary>
/// Boot-time validation that the configured <see cref="IScheduleStore" /> keeps pending schedules across
/// restarts, unless the host has explicitly accepted a volatile one.
/// </summary>
/// <remarks>
/// This validates the <em>store's durability</em> rather than the shape of an options object, and it runs
/// at startup rather than when a schedule first comes due. By then the schedule has already been accepted
/// and silently dropped, and the failure shows up as an absence — something that was supposed to happen
/// and didn't, which is the hardest kind of fault to notice.
/// </remarks>
internal sealed class ScheduleDurabilityValidator : IValidateOptions<ScheduleDurabilityOptions>
{
	private readonly IServiceProvider _services;

	/// <summary>
	/// Initializes a new instance of the <see cref="ScheduleDurabilityValidator" /> class.
	/// </summary>
	/// <param name="services"> The provider used to inspect the configured schedule-store registration. </param>
	public ScheduleDurabilityValidator(IServiceProvider services) => _services = services;

	/// <inheritdoc />
	public ValidateOptionsResult Validate(string? name, ScheduleDurabilityOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		if (options.AllowVolatileScheduleStore)
		{
			return ValidateOptionsResult.Success;
		}

		if (_services.GetService<IDurableScheduleStoreCapability>() is not null)
		{
			return ValidateOptionsResult.Success;
		}

		return ValidateOptionsResult.Fail(
			"Scheduled delivery is configured with a volatile in-memory schedule store. Everything scheduled " +
			"but not yet due would be silently lost when the process exits, after the schedule had already " +
			"been accepted. Register a durable store via " +
			$"{nameof(DurableScheduleStoreRegistration)}.{nameof(DurableScheduleStoreRegistration.AddDurableScheduleStore)}, " +
			"or, for development and test hosts only, accept the volatile store explicitly by setting " +
			$"{nameof(ScheduleDurabilityOptions)}.{nameof(ScheduleDurabilityOptions.AllowVolatileScheduleStore)} to true.");
	}
}
