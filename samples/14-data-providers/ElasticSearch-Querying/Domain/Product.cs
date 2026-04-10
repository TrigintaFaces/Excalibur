// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace ElasticSearchQuerying.Domain;

/// <summary>
/// Represents a product document stored in Elasticsearch.
/// </summary>
public sealed class Product
{
    /// <summary>
    /// Gets or sets the unique identifier for the product.
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the product name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a detailed description of the product.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the product category (exact keyword).
    /// </summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the product price.
    /// </summary>
    public decimal Price { get; set; }

    /// <summary>
    /// Gets or sets the current stock quantity.
    /// </summary>
    public int StockQuantity { get; set; }

    /// <summary>
    /// Gets or sets the average product rating (0.0 to 5.0).
    /// </summary>
    public double Rating { get; set; }

    /// <summary>
    /// Gets or sets the tags associated with the product.
    /// </summary>
    public List<string> Tags { get; set; } = [];

    /// <summary>
    /// Gets or sets when the product was created.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }
}
