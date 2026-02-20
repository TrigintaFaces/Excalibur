// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Hosting.Serverless;

/// <summary>
/// Interface for cold start optimization strategies.
/// </summary>
public interface IColdStartOptimizer
{
	/// <summary>
	/// Gets a value indicating whether the optimizer is enabled.
	/// </summary>
	/// <value><see langword="true"/> if the optimizer is enabled; otherwise, <see langword="false"/>.</value>
	bool IsEnabled { get; }

	/// <summary>
	/// Optimizes the application for cold start performance.
	/// </summary>
	/// <returns> A task representing the optimization operation. </returns>
	Task OptimizeAsync();

	/// <summary>
	/// Warms up essential services and dependencies.
	/// </summary>
	/// <returns> A task representing the warmup operation. </returns>
	Task WarmupAsync();
}
