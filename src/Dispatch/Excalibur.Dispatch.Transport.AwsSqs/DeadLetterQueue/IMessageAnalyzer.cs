// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Transport.Aws;

/// <summary>
/// Analyzes messages to determine DLQ eligibility.
/// </summary>
public interface IMessageAnalyzer
{
	/// <summary>
	/// Analyzes a message to determine if it should be moved to DLQ.
	/// </summary>
	/// <param name="message"> The message to analyze. </param>
	/// <param name="exception"> The error that occurred. </param>
	/// <param name="attemptCount"> The number of processing attempts. </param>
	/// <returns> Analysis result indicating DLQ eligibility. </returns>
	DlqAnalysisResult AnalyzeMessage(DlqMessage message, Exception exception, int attemptCount);
}
