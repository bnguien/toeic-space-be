# Password recovery

Identity uses the existing MediatR validation pipeline, PBKDF2 password hasher,
Redis, RabbitMQ, SMTP sender and HTML template renderer. No schema migration or
additional package is required. Login and registration routes/policies are unchanged.

## HTTP contract

The gateway adds `/identity` before each route below. Browser clients use the
same-origin Vite/nginx proxy, credentials included. All reset POST requests require
`X-CSRF-Protection: 1`.

| Method / route | JSON body | Success |
| --- | --- | --- |
| `POST /api/v1/auth/password-reset/otp` | `{ "email": "user@example.com" }` | `200 { "message": "...", "cooldownSeconds": 60 }` |
| `POST /api/v1/auth/password-reset/verify` | `{ "email": "user@example.com", "otp": "123456" }` | `200 { "expiresAt": "<UTC ISO timestamp>" }` + HttpOnly cookie |
| `POST /api/v1/auth/password-reset/confirm` | `{ "newPassword": "...", "confirmPassword": "..." }` | `204`, cookies cleared |
| `POST /api/v1/auth/change-password/otp` | `{ "currentPassword": "..." }` | `200 { "cooldownSeconds": 60 }`; Bearer authentication required |
| `PUT /api/v1/auth/change-password` | `{ "currentPassword": "...", "newPassword": "...", "confirmPassword": "...", "otp": "123456" }` | `204`, cookies cleared; Bearer authentication required |

The reset credential is only transported in `__Secure-ts_pr` (HttpOnly, Secure,
SameSite=Strict, path `/identity/api/v1/auth/password-reset`). It is never returned
in JSON, accepted from a URL, or stored in frontend storage. Use the gateway path
for browser testing; direct Identity URLs do not match the cookie path.

New passwords require 8–128 characters, an uppercase ASCII letter (A–Z), and a
character outside Unicode letters, numbers and whitespace. Confirmation must match.
A matching existing password is rejected. Registration retains its existing policy.

Errors use the existing ProblemDetails format. `400 OTP_INVALID_OR_EXPIRED` covers
wrong, expired, exhausted and consumed OTPs. `400 TOKEN_INVALID` covers missing,
expired, consumed or invalidated reset cookies. `400 AUTH_PASSWORD_SAME_AS_OLD`
and `400 AUTH_INVALID_CREDENTIALS` explain password errors; the latter is used
for an incorrect current password without triggering frontend token refresh.
`400 VALIDATION_ERROR` includes the existing `errors` field map. Unauthenticated
change requests return `401`; the existing credential rate limiter returns `429`.

## Delivery and security

Signed-in password changes first validate the current password and request an OTP.
The recipient is resolved server-side from the authenticated user id, and must be
verified and active. A separate consumer sends the change-password email. Challenges
are keyed by user id, independently from recovery challenges keyed by email, so
resending or verifying one flow cannot replace or consume the other's OTP.
The final change request verifies and consumes the OTP before updating the password;
it also rechecks the current password and the challenge's credential fingerprint.
Passwords and OTPs remain in component memory only; reloading restarts the change flow.

The reset request endpoint does not look up accounts. It applies the same per-email
cooldown to every address and publishes an event without credentials. Its response
is identical for unknown, locked, deleted, unverified and eligible accounts, including
requests during cooldown. The consumer only sends mail to verified active accounts
with a password, using the configured `Smtp` provider. SMTP, Redis and RabbitMQ must
be available. Configure SMTP as for registration; no new email provider is needed.

Redis stores HMAC OTP hashes, hashed token indexes, and an opaque fingerprint of
the password hash. OTP lifetime starts when requested, including queue delay.
Lua scripts make verification/consumption atomic and cap failed OTP attempts.
Resending after cooldown replaces the active request, invalidating its previous
OTP and grant. Duplicate/out-of-order broker deliveries cannot replace a newer request.

On reset/change, a conditional database update prevents concurrent changes using
the old password hash. Token revocation happens in the same database transaction.
Old reset credentials fail the password fingerprint check even if still present
in Redis. A database failure after grant consumption requires a new OTP (fail closed).
Existing access JWTs retain their current short lifetime; this feature does not
change the stateless JWT validation used by other services.

`PasswordReset` options in Identity appsettings (or `PasswordReset__...` environment
variables) control OTP/grant lifetime, resend cooldown and maximum attempts. Compose
maps the four `PASSWORD_RESET_*` variables in `.env.example`; defaults are 5 minutes,
5 minutes, 60 seconds and 5 attempts. Use the existing `Otp:HmacSecret` secret.

## Manual verification checklist

When running the stack, verify:

- `/forgot-password` → email → `/reset-password` → OTP → new password → login success notice.
- Unknown/unverified/locked emails and repeated requests return the same request response.
- Wrong OTP, expired OTP, five wrong attempts, replay and superseded OTP are rejected.
- Two concurrent verifications/confirmations cannot reuse one OTP/grant.
- Reload after verification keeps the form, without storing any OTP/password/reset credential.
- Missing/expired reset cookie, missing CSRF header and old grants after change are rejected.
- Weak/mismatched/unchanged passwords show errors on both client and API.
- `/change-password` requires authentication and a valid current password, then
  shows an OTP step before updating the password. Request/resend sends only to
  the signed-in account's verified email. Missing/wrong/expired/replayed OTPs
  cannot change the password, and recovery OTPs cannot be used for this flow.
- After reset/change, the previous password and old refresh tokens fail; a fresh login works.
- Email content renders correctly and no OTP/password/reset credential appears in JSON or logs.
- Check keyboard navigation, show/hide controls and mobile/tablet/desktop layouts.
