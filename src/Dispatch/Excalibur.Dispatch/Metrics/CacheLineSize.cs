// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Metrics;

/// <summary>
/// Provides CPU cache line size constants for performance optimization and false sharing prevention.
/// </summary>
/// <remarks>
/// <para> <strong> Cache Line Fundamentals: </strong> </para>
/// CPU cache lines are the smallest units of data that can be transferred between main memory and CPU caches. Understanding cache line size
/// is crucial for optimizing data structure layouts in high-performance applications.
/// <para> <strong> False Sharing Prevention: </strong> </para>
/// When multiple threads access different variables that reside in the same cache line, the CPU cache coherency protocol causes unnecessary
/// performance overhead. Aligning data structures to cache line boundaries eliminates this "false sharing" problem.
/// <para> <strong> Platform Considerations: </strong> </para>
/// - x86/x64: 64 bytes (Intel, AMD)
/// - ARM64: Typically 64 bytes, but can vary by implementation
/// - Some specialized processors may use 32 bytes or 128 bytes.
/// <para> <strong> Usage Patterns: </strong> </para>
/// This constant is used for padding structures, aligning memory layouts, and optimizing data access patterns in performance-critical
/// messaging components.
/// </remarks>
internal static class CacheLineSize
{
	/// <summary>
	/// The standard cache line size in bytes for modern processors.
	/// </summary>
	/// <remarks>
	/// <para>
	/// 64 bytes is the standard cache line size on:
	/// - Intel x86/x64 processors (Core, Xeon families)
	/// - AMD x86/x64 processors (Ryzen, EPYC families)
	/// - Most ARM64 implementations (Apple Silicon, AWS Graviton)
	/// </para>
	/// <para>
	/// This value is used for structure padding in cache-aligned data types to prevent false sharing and optimize memory access patterns in
	/// high-performance messaging scenarios.
	/// </para>
	/// </remarks>
	public const int Size = 64;
}
