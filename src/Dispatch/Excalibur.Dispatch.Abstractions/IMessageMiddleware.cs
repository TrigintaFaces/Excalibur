// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0



namespace Excalibur.Dispatch.Abstractions;

/// <summary>
/// Represents middleware in the message processing pipeline.
/// </summary>
/// <remarks>
/// <para>The <see cref="Order"/> property is derived from <see cref="IDispatchMiddleware.Stage"/> to
/// eliminate the previous duplication between the two ordering concepts. Middleware ordering should be
/// controlled exclusively via <see cref="DispatchMiddlewareStage"/>.</para>
/// </remarks>
public interface IMessageMiddleware : IDispatchMiddleware
{
	/// <summary>
	/// Gets the order of this middleware in the pipeline, derived from <see cref="IDispatchMiddleware.Stage"/>.
	/// </summary>
	/// <value> The numeric value of <see cref="IDispatchMiddleware.Stage"/>, or <see cref="int.MaxValue"/> if Stage is null. </value>
	int Order => Stage.HasValue ? (int)Stage.Value : int.MaxValue;
}
