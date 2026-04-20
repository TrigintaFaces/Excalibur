// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: Apache-2.0

using Excalibur.Dispatch.Abstractions;

namespace EnterpriseOrderProcessing.Domain.Events;

public sealed record CustomerRegistered(
	Guid CustomerId,
	string Name,
	string Email) : DomainEvent;

public sealed record CustomerAddressUpdated(
	Guid CustomerId,
	string Street,
	string City,
	string PostalCode,
	string Country) : DomainEvent;

public sealed record CustomerDeactivated(
	Guid CustomerId,
	string Reason) : DomainEvent;
