// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.Abstractions.Resilience;

/// <summary>
/// Configuration options for DataRequest resilience policies including retry, circuit breaker, and timeout settings.
/// </summary>
public interface IDataRequestResilienceOptions : IRetryOptions, ICircuitBreakerOptions, IResilienceGeneralOptions
{
}
