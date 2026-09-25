// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

// xunit.v3 4.x: CollectionBehavior.DisableTestParallelization is obsolete. ParallelMode.None
// is its direct replacement -- it disables all parallelism within the test assembly.
[assembly: Xunit.v3.Parallelization(Mode = Xunit.Sdk.ParallelMode.None)]
