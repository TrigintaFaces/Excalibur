// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.CloudNative;

/// <summary>
/// Interface for managing pattern observers.
/// </summary>
public interface IPatternObservable
{
	/// <summary>
	/// Subscribe an observer to pattern changes.
	/// </summary>
	void Subscribe(IPatternObserver observer);

	/// <summary>
	/// Unsubscribe an observer from pattern changes.
	/// </summary>
	void Unsubscribe(IPatternObserver observer);
}
