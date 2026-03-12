// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Cdc.MongoDB;

/// <summary>
/// Change stream configuration options for MongoDB CDC processor.
/// </summary>
/// <remarks>
/// Follows the <c>MongoClientSettings</c> pattern of separating change stream configuration from connection properties.
/// </remarks>
public sealed class MongoDbChangeStreamOptions
{
	/// <summary>
	/// Gets or sets whether to include the full document in change events.
	/// </summary>
	/// <remarks>
	/// When true, update operations include the full document after the change.
	/// When false, only the delta is included (default MongoDB behavior).
	/// </remarks>
	public bool FullDocument { get; set; } = true;

	/// <summary>
	/// Gets or sets whether to include pre-image of the document (MongoDB 6.0+).
	/// </summary>
	/// <remarks>
	/// Requires change stream pre and post images to be enabled on the collection.
	/// </remarks>
	public bool FullDocumentBeforeChange { get; set; }

	/// <summary>
	/// Gets or sets the operation types to watch for.
	/// </summary>
	/// <remarks>
	/// If empty, watches all operation types. Valid values: insert, update, replace, delete, invalidate.
	/// </remarks>
	public string[] OperationTypes { get; set; } = [];

	/// <summary>
	/// Gets or sets the maximum time to wait for new changes.
	/// </summary>
	/// <remarks>
	/// This controls the getMore behavior. If no documents are available,
	/// the server will return an empty batch after this time.
	/// </remarks>
	public TimeSpan MaxAwaitTime { get; set; } = TimeSpan.FromSeconds(5);
}
