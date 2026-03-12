// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

// HttpJsonSerializer was deleted in Sprint 586 serialization consolidation.
// Its functionality is now covered by:
//   - SystemTextJsonSerializer (ISerializer + ISerializer) for core serialization
//   - IHttpSerializer interface retained for HTTP-specific Type-based serialization
//   - SerializerExtensions for convenience overloads (SerializeToBytes, DeserializeAsync, etc.)
// See SystemTextJsonPluggableSerializerShould.cs for comprehensive SystemTextJsonSerializer tests.
