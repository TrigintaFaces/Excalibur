// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Abstractions;

/// <summary>
/// Represents a document-style message that contains structured data for processing.
/// </summary>
/// <remarks>
/// Document messages are used for transferring larger, self-contained data structures through the messaging pipeline. Unlike events or
/// actions, documents typically represent complete business entities or data transfer objects. Common use cases include:
/// <list type="bullet">
/// <item> Batch processing of records </item>
/// <item> ETL (Extract, Transform, Load) operations </item>
/// <item> Import/export of complex data structures </item>
/// <item> Document-oriented workflows (e.g., invoice processing, report generation) </item>
/// <item> Transferring snapshots of aggregate state </item>
/// </list>
/// Documents may be processed by multiple handlers for different aspects of the data (validation, transformation, storage, etc.) and
/// support both synchronous and asynchronous processing patterns.
/// </remarks>
public interface IDispatchDocument : IDispatchMessage;
