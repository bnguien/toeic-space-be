# Classroom Google Meet integration

Classroom uses each assigned Teacher's Google account to create a Meet space for an Online ClassSession. The browser starts OAuth through Classroom, Google returns to Classroom, and a Hangfire job creates the space. Classroom stores only encrypted refresh tokens and the resulting space name and meeting URL. Google access tokens are used in memory and are never returned by Classroom APIs.

## Google Cloud setup

1. Enable the **Google Meet REST API** in a Google Cloud project.
2. Configure an OAuth consent screen. Add the Teachers as test users while the app is in Testing status.
3. Create a **Web application** OAuth client. Register the Classroom callback URL exactly, for example `https://your-classroom-host/api/v1/teachers/me/google-meet/callback`.
4. Grant `https://www.googleapis.com/auth/meetings.space.created`. Classroom also requests `openid email` to identify the connected Google account and requires offline access to refresh its token.
5. Configure the Classroom process with:

| Environment variable | Meaning |
| --- | --- |
| `GoogleMeet__ClientId` | OAuth web client ID |
| `GoogleMeet__ClientSecret` | OAuth web client secret |
| `GoogleMeet__RedirectUri` | Exact registered callback URL |
| `GoogleMeet__EncryptionKey` | Base64 encoding of a persistent 32-byte random key |
| `GoogleMeet__StudentJoinMinutesBefore` | Minutes before start to expose Student link; default 15 |
| `GoogleMeet__StudentJoinMinutesAfter` | Minutes after end to expose Student link; default 30 |

Generate the encryption key with `openssl rand -base64 32`. Store the same key securely across all Classroom instances and restarts; rotating it without re-encrypting stored tokens requires Teachers to reconnect. Keep the client secret and key outside source control. The existing `ConnectionStrings__ClassroomDatabase` is also required for Classroom and its Hangfire tables.

Apply the Classroom migration before starting the service:

```sh
dotnet ef database update --project src/Services/Classroom/ToeicSpace.Classroom.Infrastructure --startup-project src/Services/Classroom/ToeicSpace.Classroom.Infrastructure
```

The command uses the existing `ClassroomDbContextFactory` and reads `ConnectionStrings__ClassroomDatabase` from the environment. Hangfire uses the same Classroom MySQL database with a `Hangfire` table prefix. No other service database is accessed.

## API flow

| Route | Role | Behavior |
| --- | --- | --- |
| `POST /api/v1/teachers/me/google-meet/connect` | Teacher | Returns `authorizationUrl`; open it in the browser |
| `GET /api/v1/teachers/me/google-meet/callback` | Google redirect | Validates one-time state, exchanges code, stores encrypted refresh token |
| `GET /api/v1/teachers/me/google-meet/status` | Teacher | Returns connected flag and Google email |
| `DELETE /api/v1/teachers/me/google-meet` | Teacher | Disconnects and hides existing links |
| `GET /api/v1/class-sessions/{sessionId}/meet/status` | Admin, assigned Teacher, enrolled Student | Returns provisioning status without a token or URL |
| `GET /api/v1/class-sessions/{sessionId}/meet/link` | Admin, assigned Teacher, enrolled Student | Returns URL only when Ready; Students also need to be inside the join window |
| `POST /api/v1/class-sessions/{sessionId}/meet/retry` | Admin | Retries a Failed provisioning attempt |

The OAuth callback returns a small JSON success response; it does not receive the ToeicSpace access token. A random one-time state is stored as a SHA-256 hash with the initiating Teacher ID and a 10-minute expiry. A Google connection is bound to that Teacher ID. A newly assigned Teacher needs their own connection.

## Provisioning and consistency

Session creation, updates, recurring generation, schedule updates, and cancellations enqueue a Hangfire job after the Classroom transaction commits. A recurring reconciliation job also scans upcoming Online Sessions every minute, recovering missed enqueue events after a process restart. MySQL rows track `NeedsConnection`, `Pending`, `Provisioning`, `Ready`, `Failed`, and `Inactive`, plus an ownership lease, attempts, the Teacher connection ID, and the Session version.

Jobs reuse a Ready space for the same Teacher connection. A changed Teacher or Google connection invalidates the old association. A changed Session version prevents an older in-flight job from publishing a link. Offline and Cancelled Sessions return an inactive status and cannot expose a join URL. Existing ClassSession audit records and scheduling validation remain in the current handlers.

Token refresh failures retry with bounded backoff. A Google space creation request that times out or produces an uncertain response is marked `Failed` with `uncertain_google_result` and is **not automatically retried**: Google does not provide an idempotency key for `spaces.create`, so a second request may create another space. An Admin can review and retry it manually. A worker that dies while `Provisioning` also moves to this manual-review state after its lease expires.

The mock Google API tests run with the existing Classroom test project. Real Google OAuth and space creation require valid Cloud credentials and an externally reachable callback, so they are not part of the automated suite.

Google references: [OAuth web server flow](https://developers.google.com/identity/protocols/oauth2/web-server), [Meet spaces.create](https://developers.google.com/workspace/meet/api/reference/rest/v2/spaces/create).
