# SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
# SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

# Excalibur A3 Authorization Policy for Open Policy Agent (OPA)
#
# This policy implements the Excalibur A3 (Authentication, Authorization, Auditing)
# authorization model. It evaluates whether a user is authorized to perform an
# activity, optionally scoped to a specific resource.
#
# Data dependencies (loaded via OPA data binding):
#   - data.activities:      Map of activity names to their definitions (including resourceTypes).
#   - data.activityGroups:  Map of group names to their member activities.
#   - data.userGrants:      Map of grant URNs ("tenantId:type:qualifier") to grant objects.
#
# Input schema:
#   - input.activity:       The activity being requested (e.g., "Orders.Read").
#   - input.resource:       The resource path being accessed (e.g., "orders/12345").
#   - input.resourceType:   The resource type for direct resource grants.
#   - input.tenantId:       The tenant context for multi-tenant authorization.
#   - input.now:            Current timestamp for grant expiration checks.
#
# Entrypoint output:
#   - isAuthorized:         True if the user is authorized for the requested operation.
#   - hasActivityGrant:     True if the user holds a matching activity or activity-group grant.
#   - hasResourceGrant:     True if the user holds a matching resource-level grant.
#
# Grant URN format: "tenantId:grantType:qualifier"
#   Wildcards ("*") are supported at each segment for broad grants.
#
# Grant expiration:
#   Grants with a null expiresOn never expire. Otherwise, expiresOn is compared
#   against input.now to determine validity.

package authorization

import data.activities
import data.activityGroups
import data.userGrants

default authorized = false
default user_has_activity_grant = false
default user_has_resource_grant = false

# Entrypoint: returns the authorization decision as a structured object.
entrypoint = {
    "isAuthorized": authorized,
    "hasActivityGrant": user_has_activity_grant,
    "hasResourceGrant": user_has_resource_grant
}

# A user is authorized if they have an activity grant and the activity
# does not require resource-level authorization.
authorized {
    user_has_activity_grant
    not activity_has_resource_types
}

# Activity grant: direct grant on the requested activity.
user_has_activity_grant {
    user_has_grant("Activity", input.Activity)
}

# Activity grant: indirect grant via an activity group containing the activity.
user_has_activity_grant {
    activity_in_group[activityGroup]
    user_has_grant("ActivityGroup", activityGroup)
}

# Resource grant: the activity defines resource types and the user has a
# grant on the specific resource for one of those types.
user_has_resource_grant {
    resourceType := activities[input.activity].resourceTypes[_]
    user_has_grant(resource_type, input.resource)
}

# Resource grant: direct resource check when no activity is specified.
user_has_resource_grant {
    input.activity == null
    user_has_grant(input.resourceType, input.Resource)
}

# Helper: checks whether the requested activity defines resource types.
activity_has_resource_types {
    activities[input.activity].resourceTypes != null
}

# Helper: resolves which activity groups contain the requested activity.
activity_in_group[activityGroup] {
    activity = activityGroups[activityGroup].activities[_]
    activity.acttivityName == input.activity
    activity.tenantId = input.tenantId
}

# Grant resolution: checks for exact or wildcard matches on tenant, type,
# and qualifier segments.
user_has_grant(t, q) {
    tenantId := ["*", input.tenantId][_]
    type := ["*", t][_]
    qualifier := ["*", q][_]
    grant_is_active(concat(":", [tenantId, type, qualifier]))
}

# Grant resolution: checks for hierarchical resource path matching using
# wildcard suffixes (e.g., "orders/*" matches "orders/12345").
user_has_grant(t, q) {
    tenantId := ["*", input.tenantId][_]
    qualifier := resource_qualifiers[_]
    grant_is_active(concat(":", [tenantId, t, qualifier]))
}

# Helper: generates wildcard path qualifiers from a resource path.
# For "a/b/c", generates {"a/*", "a/b/*"}.
resource_qualifiers = { q |
    parts = split(input.resource, "/")
    part = parts[i]
    i > 0
    path = concat("/", array.splice(parts, 0, i))
    q := concat("/", [path, "*"])
}

# A grant is active if it has no expiration date.
grant_is_active(urn) {
    grant := userGrants[urn]
    grant.expiresOn == null
}

# A grant is active if its expiration date is in the future.
grant_is_active(urn) {
    grant := userGrants[urn]
    grant.expiresOn >= input.now
}
