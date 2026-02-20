// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Options.Configuration;

namespace Excalibur.Dispatch.Configuration;

/// <summary>
/// Interface for default pipeline synthesis.
/// </summary>
public interface IDefaultPipelineSynthesizer
{
	/// <summary>
	/// Synthesizes a default pipeline for the given message kinds and options.
	/// </summary>
	IReadOnlyList<Type> SynthesizePipeline(MessageKinds messageKinds, DispatchOptions options);

	/// <summary>
	/// Registers middleware for automatic inclusion in synthesized pipelines.
	/// </summary>
	void RegisterMiddleware(
		Type middlewareType,
		DispatchMiddlewareStage stage,
		int priority = 0,
		MessageKinds? applicableKinds = null);
}
