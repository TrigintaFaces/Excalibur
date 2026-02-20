// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Jobs.Abstractions;

/// <summary>
/// Represents a job that is configured using a strongly-typed configuration object.
/// </summary>
/// <typeparam name="TConfig"> The type of the configuration object implementing <see cref="IJobConfig" />. </typeparam>
public interface IConfigurableJob<out TConfig>
	where TConfig : class, IJobConfig;
