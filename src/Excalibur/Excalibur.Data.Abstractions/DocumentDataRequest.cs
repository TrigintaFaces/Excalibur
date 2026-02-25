// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.Abstractions;

/// <summary>
/// Convenience base class for document requests that work with generic document connections.
/// </summary>
/// <typeparam name="TResult"> The type of the result returned by the request. </typeparam>
public abstract class DocumentDataRequest<TResult> : DocumentDataRequestBase<object, TResult>;
