// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

namespace Excalibur.Dispatch.Exceptions;

/// <summary>Delivery acceptance could not be established; the operation may have taken effect.</summary>
/// <remarks>The default bounded retry policy permits replay. Consumers must tolerate duplicates.
/// This exception does not establish that delivery was rejected.</remarks>
public sealed class DispatchDeliveryUncertainException : DispatchException
{
    /// <summary>Initializes a new instance of the <see cref="DispatchDeliveryUncertainException"/> class.</summary>
    public DispatchDeliveryUncertainException()
        : this("Delivery acceptance is unestablished.") { }

    /// <summary>Initializes a new instance with an explanation of the uncertain outcome.</summary>
    /// <param name="message">The explanation of the uncertain outcome.</param>
    public DispatchDeliveryUncertainException(string message)
        : base(500, "DLV001", message, null) { }

    /// <summary>Initializes a new instance preserving the original transport fault.</summary>
    /// <param name="message">The explanation of the uncertain outcome.</param>
    /// <param name="innerException">The original transport fault.</param>
    public DispatchDeliveryUncertainException(string message, Exception? innerException)
        : base(500, "DLV001", message, innerException) { }
}
