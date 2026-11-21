# REST API v1 - endpoint list

Owner: MAIN. This is the contract for BACKEND and for the Bruno collection. Request and response bodies are defined by the BACKEND chat inside these routes; changing a route, role or meaning goes through MAIN.

## Conventions
- Base path `/api/v1`. JSON. `Authorization: Bearer <JWT>` except where "anon".
- IDs are GUIDs. Timestamps UTC ISO-8601. Money is an integer in minor units plus the Location currency.
- Errors are `ProblemDetails` (RFC 9457) with a `type` slug (table below). Lists use cursor pagination (`?cursor=&limit=`); a list response is `{ items, nextCursor }`, the cursor is opaque, `limit` defaults to 50 and ranges from 1 to 100. Validation errors carry an `errors` object (field name to messages, camelCase).
- `Idempotency-Key` header is required on `POST /bookings`, `POST /holds` and `POST /sessions/{id}/start`.
- Roles: **M** member of the Location, **R** resident, **T** technician, **A** location admin. Roles are per Location.
- Login returns `accessToken`, `tokenType` (`Bearer`) and `expiresAtUtc`, with `Cache-Control: no-store`. Passwords are 10 to 128 characters. Five failed logins lock the account for 5 minutes (429 `too-many-attempts`).
- Swagger UI at `/swagger`, liveness at `/health/live`, readiness (database check) at `/health`. Both are anonymous, as are the per-module `ping` routes.
- The ProblemDetails `type` is the bare slug from the table at the end. Unknown problem types map to 500.

## Identity
| Method | Path | Who | Purpose |
|---|---|---|---|
| POST | /auth/register | anon | email, password, displayName, accountType `Resident` or `Admin` |
| POST | /auth/login | anon | returns access token and expiry |
| POST | /auth/refresh | anon | Tier 2 |
| GET | /auth/external/{provider} | anon | Tier 2 (Google) |
| GET | /me | any | profile and memberships |
| POST | /locations | Admin account | create Location (name, timeZone) |
| GET | /locations | any | my Locations |
| GET | /locations/{locationId} | M | details and config |
| PATCH | /locations/{locationId} | A | name, timeZone, config (minLeadMinutes, maxHorizonDays, loadMinutes, unloadMinutes, noShowMinutes, holdPrices for 5/10/15, rescheduleSearchHours, currency) |
| POST | /locations/{locationId}/invites | A | create invite (role Resident or Technician, maxUses, expiresAt) |
| GET | /locations/{locationId}/invites | A | list invites |
| DELETE | /locations/{locationId}/invites/{inviteId} | A | revoke |
| POST | /locations/join | any | join with an invite code |
| GET | /locations/{locationId}/members | A | list members |
| DELETE | /locations/{locationId}/members/{userId} | A | remove member |

## Resources
| Method | Path | Who | Purpose |
|---|---|---|---|
| POST | /locations/{locationId}/programs | A | name, kind, minutes (multiple of 15), priceMinor |
| GET | /locations/{locationId}/programs | M | list |
| PATCH | /programs/{programId} | A | change |
| DELETE | /programs/{programId} | A | retire (soft) |
| POST | /locations/{locationId}/machines | A | name, kind washer or dryer, deviceId (unique) |
| GET | /locations/{locationId}/machines | M | list with OutOfService flag |
| GET | /machines/{machineId} | M | details |
| PATCH | /machines/{machineId} | A | rename, change programs |
| DELETE | /machines/{machineId} | A | decommission (soft) |

The OutOfService flag changes only through Maintenance tickets. Device certificates are issued by `tools/` scripts, not by the API.

## Telemetry
| Method | Path | Who | Purpose |
|---|---|---|---|
| GET | /machines/{machineId}/state | M | cycle state, online, lastSeen, remainingSec |
| GET | /machines/{machineId}/readings | A, T | raw readings (`from`, `to`, cursor), retention 3 days |
| GET | /locations/{locationId}/devices/status | A, T | online and offline devices |

## Booking
| Method | Path | Who | Purpose |
|---|---|---|---|
| GET | /locations/{locationId}/availability | M | per machine: operational state and free slots for a day (`date`, optional `kind`) |
| GET | /machines/{machineId}/slots | M | 15-minute grid, free or taken (`from`, `to`) |
| POST | /bookings | M | Scheduled booking: machineId, programId, startsAt |
| POST | /holds | M | Hold: machineId, minutes 5, 10 or 15 |
| GET | /bookings | any | my bookings (`status`, `from`, `to`) |
| GET | /bookings/{bookingId} | owner, A | details with change log |
| POST | /bookings/{bookingId}/cancel | owner, A | cancel |
| POST | /sessions/claim | M | body `code`; returns sessionId, machineId, mode (WalkUp, Booking, Hold), bookingId when applicable |
| POST | /sessions/{sessionId}/start | owner | body programId (required for WalkUp); sends `start` |
| GET | /sessions/{sessionId} | owner, A | session status |
| POST | /sessions/{sessionId}/stop | owner, A | stop the cycle |
| GET | /locations/{locationId}/bookings | A | all bookings (`machineId`, `status`, `from`, `to`) |
| GET | /locations/{locationId}/reports/utilization | A | occupied hours per machine per day, fault count (`from`, `to`) |

## Maintenance
| Method | Path | Who | Purpose |
|---|---|---|---|
| POST | /locations/{locationId}/tickets | A, T | open a ticket manually: machineId, description |
| GET | /locations/{locationId}/tickets | A, T | list (`status`) |
| GET | /tickets/{ticketId} | A, T | details with timeline |
| PUT | /tickets/{ticketId}/eta | A, T | set or change `etaAt`, note |
| POST | /tickets/{ticketId}/resolve | A, T | resolve with note |
| POST | /tickets/{ticketId}/cancel | A | cancel a false alarm |

## Problem types (`type` slugs) and HTTP status (D-14)
| Status | Slugs |
|---|---|
| 400 | `validation`, `invite-invalid`, `code-invalid`, `code-expired` |
| 401 | `unauthorized` |
| 403 | `forbidden`, `not-member` |
| 404 | `not-found` |
| 409 | `slot-conflict`, `machine-unavailable`, `machine-busy`, `hold-limit`, `program-not-fit` |
| 422 | `lead-time`, `horizon`, `idempotency-conflict` |
| 429 | `too-many-attempts` |
| 502 | `command-failed` |
| 500 | any unknown type |
