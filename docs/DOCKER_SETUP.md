# Docker setup for PBL-28 / PBL-29

Run Backend commands from `toeic-space-be` and Frontend commands from its sibling
`toeic-space-fe` directory. Docker Desktop must be running with Linux containers.
The migration helper uses the existing Compose network and MySQL volumes. No host .NET
SDK, EF CLI, Node installation, database reset, or volume deletion is required.
The migration script is pinned to LF in `.gitattributes` so Windows Git checkouts can run it in Docker.

## Get the code and configure local files

The PBL-28/PBL-29 changes must first be published to the branch your team shares.
Uncommitted local changes cannot be obtained with `git pull`. In each existing clone,
switch to that agreed branch and run `git pull --ff-only`. Preserve local work before switching.
New teammates can clone the two repositories with `git clone --branch <shared-branch> <repository-url>`.

Create each local `.env` only if it is missing. In PowerShell, from each repository:

```powershell
if (!(Test-Path .env)) { Copy-Item .env.example .env }
```

In Bash, use `[ -f .env ] || cp .env.example .env`. Do not replace an existing `.env`
with the example: existing MySQL volume credentials do not change when `.env` changes.

Configure the existing database, RabbitMQ, JWT, SMTP and R2 values in the Backend `.env`.
Use your own local database passwords and ES256 key pair, and obtain R2/SMTP credentials
privately. The existing key generator can be run entirely in Docker from the Backend root:

```powershell
docker run --rm --mount "type=bind,source=$($PWD.Path),target=/source,readonly" -w /tmp mcr.microsoft.com/dotnet/sdk:10.0 dotnet run /source/scripts/GenerateJwtKeys.cs
```

Copy the generated private/public pair into your local `.env` privately. Do not send its output
to GitHub, issues, logs, or chat. Keep an existing working pair when updating this feature.
`IDENTITY_OTP_HMAC_SECRET` also needs at least 32 random characters.

The only new PBL-28/PBL-29 Backend environment variable is:

```dotenv
PRACTICE_MEDIA_ALLOWED_ORIGIN=https://chill-zone.studychill.net
```

This is the public source origin used by the currently imported question bank. Configure the exact
scheme, host and port for your own bank. The allowlist is an administrator-controlled fetch policy.
For multiple trusted origins, add `PracticeMedia__AllowedOrigins__1`, `__2`, etc. to the
Assessment service's Compose `environment` section or a local Compose override. A comma-separated
value in `PRACTICE_MEDIA_ALLOWED_ORIGIN` does not create multiple origins.

The existing `R2_ENDPOINT`, `R2_ACCESS_KEY`, `R2_SECRET_KEY`, and `R2_BUCKET_NAME` are now
also passed to Assessment. The bucket must already exist and allow object read/write access.
Keep objects under `practice-retained/` without lifecycle expiry or source-media deletion.
Start intentionally fails if required media cannot be preserved. Do not put R2 credentials in the Frontend.
The seven-day inactivity threshold, 60-second worker interval and batch size of 100 need no `.env` settings.

Frontend `.env` for the current Docker Desktop setup:

```dotenv
VITE_API_BASE_URL=
VITE_DEV_PROXY_TARGET=http://localhost:5050
DOCKER_API_GATEWAY_URL=http://api-gateway:8080
DOCKER_GATEWAY_NETWORK=toeic-space-be_frontend-network
VITE_AVATAR_CDN_URL=
```

Leave the browser base URL empty so Vite forwards `/identity`, `/assessment`, `/classroom`
and `/content` on the same origin. Frontend Compose uses the two public `DOCKER_*` settings
to connect directly to the gateway on its existing frontend network. Start the Backend first
so that network exists. If you changed the Backend Compose project name, update
`DOCKER_GATEWAY_NETWORK` to `<backend-project-name>_frontend-network`. These settings have
defaults and can be omitted from an existing `.env`. For Vite running on the host,
`VITE_DEV_PROXY_TARGET` must match Backend `API_GATEWAY_PORT`.

## Existing database: apply the new migration, then start

API startup and `docker compose up` do **not** run EF migrations. Run this explicit command
once for the update. It applies pending Assessment migrations, including
`20261108041120_AddPracticeAttempts`, and preserves existing data. It can be run again when up to date.

From the Backend root:

```powershell
docker compose --profile identity --profile assessment --profile tools config --quiet
docker compose --profile identity --profile assessment up -d --wait identity-db assessment-db redis messagebroker
docker compose --profile identity --profile assessment stop assessment-api
docker compose --profile tools run --rm migrations Assessment
docker compose --profile identity --profile assessment up -d --build --wait identity-api assessment-api api-gateway
docker compose --profile identity --profile assessment ps
```

From the Frontend root:

```powershell
docker compose config --quiet
docker compose up -d --build --wait frontend
docker compose ps
```

Open `http://localhost:5173`. Gateway is `http://localhost:5050`; Assessment is published on
port 6004 and Identity on 6001 with the example configuration. `VITE_*` changes require
restarting the Frontend container. Backend `.env` changes require `up -d` to recreate the
affected service; `docker compose restart` alone does not load new environment values.

For daily use after migration, run the two `up` commands above. Rebuild after pulling code.
Use both `identity` and `assessment` profiles for the Practice flow: Assessment depends on
Redis and RabbitMQ, which are provided by the Identity profile. The existing Classroom services
can stay running throughout the PBL update.

## First installation / all services

For a brand-new database, the other services also need their own initial migrations. From Backend:

```powershell
docker compose --profile full up -d --wait identity-db assessment-db classroom-db content-db redis messagebroker
docker compose --profile tools run --rm migrations Identity
docker compose --profile tools run --rm migrations Assessment
docker compose --profile tools run --rm migrations Classroom
docker compose --profile tools run --rm migrations Content
docker compose --profile full up -d --build --wait
```

Then start Frontend as above. The example uses distinct ports: Assessment 6004 and Content 6005.
Migrations create/upgrade schema; they do not copy another developer's question bank or practice sets.
Import approved existing data or create and publish content through the Teacher workflow on a fresh database.
Provide initial administrator settings using the existing bootstrap mechanism when needed.

## Configuration sharing

Safe to share: `.env.example`, variable names, the public media origin, local port numbers,
`VITE_API_BASE_URL`, `VITE_DEV_PROXY_TARGET`, the public avatar CDN URL, environment/profile
names, JWT issuer/audience and the JWT **public** key. Bucket names and endpoints are not
credentials, but the example keeps the R2 account identifier as a placeholder.

Keep private: actual `.env` files, all database/root passwords, connection strings containing
passwords, RabbitMQ/SMTP passwords, `R2_ACCESS_KEY`, `R2_SECRET_KEY`, `JWT_PRIVATE_KEY`, OTP
HMAC secrets and bootstrap administrator passwords. Every `VITE_*` value is public to browser
users. Share needed server credentials through your team's private secret-sharing channel.
Both repositories ignore `.env`; only sanitized `.env.example` files should be committed.
Do not publish raw `docker compose config` output or unsanitized development logs; these may contain
resolved credentials or application data. Use `docker compose config --quiet` for validation.

Do not delete Docker volumes to update this feature. Stop the normal stack with
`docker compose --profile full stop` (Backend) and `docker compose stop` (Frontend) when needed.
