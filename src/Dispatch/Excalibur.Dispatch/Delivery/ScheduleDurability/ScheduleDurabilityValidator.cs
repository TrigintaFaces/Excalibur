// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0


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

		// Resolve the selected singleton first so only its own attesting factory can authorize it.
		// The witness holds references without owning or separately registering disposable stores.
		var store = _services.GetService<IScheduleStore>();
		if (store is not null && _services.GetService<IDurableScheduleStoreCapability>()?.IsFor(store) == true)
		{
			return ValidateOptionsResult.Success;
		}

		return ValidateOptionsResult.Fail(
			"Scheduled delivery has no durability attestation for its selected schedule store. Pending schedules could be " +
			"lost when the process exits if that store is volatile, after they had already been accepted. Register a durable store via " +
			$"{nameof(DurableScheduleStoreRegistration)}.{nameof(DurableScheduleStoreRegistration.AddDurableScheduleStore)}, " +
			"or, for development and test hosts only, accept the volatile store explicitly by setting " +
			$"{nameof(ScheduleDurabilityOptions)}.{nameof(ScheduleDurabilityOptions.AllowVolatileScheduleStore)} to true.");
	}
}
