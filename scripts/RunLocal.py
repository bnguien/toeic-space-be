"""Run the TOEIC Space APIs on this machine (dotnet, local MySQL / Redis / RabbitMQ) with the settings docker compose
would give them, taken from `.env`. Values are passed to the child processes only; nothing is printed.

    python scripts/RunLocal.py restart            # stop → build → start identity, assessment, classroom, content
    python scripts/RunLocal.py start | stop | build | status
    python scripts/RunLocal.py probe identity     # start one service on a spare port (+200) to check its settings

`.env` is read like compose reads it (KEY=VALUE, optional quotes, no shell expansion). Hosts become localhost (the
compose names `messagebroker`, `redis`, `*-db` only exist inside compose). Classroom and Content (no user-secrets)
get their connection string from .env; Identity and Assessment keep theirs (user-secrets) unless `--db-from-env`. Empty values are never exported, so they cannot blank out a setting that
user-secrets already holds. The gateway (:5050) is not touched. Logs: %TEMP%/toeicspace/<service>.log."""

from __future__ import annotations

import argparse
import os
import re
import subprocess
import sys
import tempfile
import time
import urllib.error
import urllib.request
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
LOGS = Path(tempfile.gettempdir()) / "toeicspace"
SERVICES = {
    "identity": ("src/Services/Identity/ToeicSpace.Identity.API", 5086, "IDENTITY", "IdentityDatabase"),
    "assessment": ("src/Services/Assessment/ToeicSpace.Assessment.API", 5237, "ASSESSMENT", "AssessmentDatabase"),
    "classroom": ("src/Services/Classroom/ToeicSpace.Classroom.API", 5027, "CLASSROOM", "ClassroomDatabase"),
    "content": ("src/Services/Content/ToeicSpace.Content.API", 5218, "CONTENT", "ContentDatabase"),
}
# services without user-secrets: their connection string can only come from .env (local MySQL on localhost)
DB_FROM_ENV = {"classroom", "content"}
LINE = re.compile(r"^\s*(?:export\s+)?([A-Za-z_][A-Za-z0-9_]*)\s*=\s*(.*?)\s*$")


def read_env(path: Path) -> dict[str, str]:
    values: dict[str, str] = {}
    if not path.exists():
        return values
    for raw in path.read_text(encoding="utf-8-sig").splitlines():
        if not raw.strip() or raw.lstrip().startswith("#"):
            continue
        match = LINE.match(raw)
        if not match:
            continue
        key, value = match.groups()
        if len(value) >= 2 and value[0] == value[-1] and value[0] in "\"'":
            value = value[1:-1]
            if raw.split("=", 1)[1].strip().startswith('"'):
                value = value.replace("\\n", "\n")
        elif " #" in value:
            value = value.split(" #", 1)[0].rstrip()
        values[key] = value
    return values


def shared_jwt() -> dict[str, str]:
    """Issuer / audience / PUBLIC key as Assessment already has them (appsettings.json + its user-secrets), for the
    services that have no user-secrets of their own. Only the public half: it verifies tokens, it cannot sign."""
    import json

    found: dict[str, str] = {}
    settings = ROOT / "src/Services/Assessment/ToeicSpace.Assessment.API/appsettings.json"
    secrets = Path(os.environ.get("APPDATA", "")) / "Microsoft/UserSecrets/toeicspace-assessment-local/secrets.json"
    try:
        jwt = json.loads(settings.read_text(encoding="utf-8-sig")).get("Jwt") or {}
        found |= {f"JWT_{k.upper()}": str(v) for k, v in jwt.items() if k in ("Issuer", "Audience") and v}
    except (OSError, ValueError):
        pass
    try:
        public = json.loads(secrets.read_text(encoding="utf-8-sig")).get("Jwt:PublicKey")
        if public:
            found["JWT_PUBLIC_KEY"] = str(public)
    except (OSError, ValueError):
        pass
    return found


def settings_for(service: str, env: dict[str, str], *, db_from_env: bool) -> dict[str, str]:
    """The compose mapping of one service, hosts set to localhost; only non-empty values."""
    if service in DB_FROM_ENV:
        env = {**shared_jwt(), **{k: v for k, v in env.items() if v}}  # .env wins when it has them

    def pick(name: str, default: str = "") -> str:
        return env.get(name, "") or default

    _, _, prefix, db_key = SERVICES[service]
    out = {
        "ASPNETCORE_ENVIRONMENT": pick("ASPNETCORE_ENVIRONMENT", "Development"),
        "Jwt__Issuer": pick("JWT_ISSUER"),
        "Jwt__Audience": pick("JWT_AUDIENCE"),
        "ForwardedHeaders__KnownNetworks__0": pick("TRUSTED_PROXY_NETWORK"),
    }
    if service in ("identity", "assessment", "classroom") and pick("RABBITMQ_USER"):
        out |= {
            "RabbitMQ__Host": "localhost",
            "RabbitMQ__Username": pick("RABBITMQ_USER"),
            "RabbitMQ__Password": pick("RABBITMQ_PASSWORD"),
        }
    if service == "assessment":  # same default as docker-compose.yml; practice attempts cannot preserve media without it
        out["PracticeMedia__AllowedOrigins__0"] = pick("PRACTICE_MEDIA_ALLOWED_ORIGIN", "https://chill-zone.studychill.net")
    if service in ("identity", "content"):
        out |= {
            "CloudflareR2__Endpoint": pick("R2_ENDPOINT"),
            "CloudflareR2__AccessKey": pick("R2_ACCESS_KEY"),
            "CloudflareR2__SecretKey": pick("R2_SECRET_KEY"),
            "CloudflareR2__BucketName": pick("R2_BUCKET_NAME"),
        }
    if service == "identity":
        out |= {
            "Jwt__PrivateKey": pick("JWT_PRIVATE_KEY"),
            "Jwt__AccessTokenLifetime": "00:15:00",
            "Session__RefreshTokenLifetime": "7.00:00:00",
            "Smtp__Server": pick("SMTP_SERVER"),
            "Smtp__Port": pick("SMTP_PORT"),
            "Smtp__SenderName": pick("SMTP_SENDER_NAME"),
            "Smtp__SenderEmail": pick("SMTP_SENDER_EMAIL"),
            "Smtp__Password": pick("SMTP_PASSWORD"),
            "Smtp__SupportEmail": pick("SMTP_SUPPORT_EMAIL"),
            "Otp__HmacSecret": pick("IDENTITY_OTP_HMAC_SECRET"),
            "Otp__ChallengeTtlMinutes": pick("OTP_EXPIRATION_MINUTES", "5"),
            "Otp__ResendCooldownSeconds": "60",
            "PasswordReset__OtpLifetimeMinutes": pick("PASSWORD_RESET_OTP_LIFETIME_MINUTES", "5"),
            "PasswordReset__ResetLifetimeMinutes": pick("PASSWORD_RESET_LIFETIME_MINUTES", "5"),
            "PasswordReset__ResendCooldownSeconds": pick("PASSWORD_RESET_COOLDOWN_SECONDS", "60"),
            "PasswordReset__MaximumFailedAttempts": pick("PASSWORD_RESET_MAX_FAILED_ATTEMPTS", "5"),
            "Identity__InactiveAccountRetentionDays": pick("IDENTITY_INACTIVE_ACCOUNT_RETENTION_DAYS", "3"),
        }
    else:
        out["Jwt__PublicKey"] = pick("JWT_PUBLIC_KEY")
    if service == "content":
        out |= {
            "Pixabay__ApiKey": pick("PIXABAY_API_KEY"),
            "Gemini__ApiKey": pick("GEMINI_API_KEY"),
            "Gemini__ApiKeys": pick("GEMINI_API_KEYS"),
            "Gemini__InteractiveModel": pick("DICT_AI_MODEL"),
            "Gemini__BatchModel": pick("FLASHCARD_AI_MODEL"),
            "GoogleTranslate__ApiKey": pick("GOOGLE_TRANSLATE_API_KEY"),
        }
    # Identity / Assessment keep the connection string of their user-secrets (the .env one may be stale)
    own = pick(f"ConnectionStrings__{db_key}") if (service in DB_FROM_ENV or db_from_env) else ""
    if own:
        out[f"ConnectionStrings__{db_key}"] = own
    elif (db_from_env or service in DB_FROM_ENV) and pick(f"{prefix}_DB_NAME"):
        out[f"ConnectionStrings__{db_key}"] = (
            f"Server=localhost;Port={pick(f'{prefix}_DB_PORT', '3306')};Database={pick(f'{prefix}_DB_NAME')};"
            f"User={pick(f'{prefix}_DB_USER')};Password={pick(f'{prefix}_DB_PASSWORD')};"
        )
    elif service in DB_FROM_ENV and pick("ConnectionStrings__IdentityDatabase"):
        # same local MySQL server as Identity, the service's own database (names of .env.example)
        out[f"ConnectionStrings__{db_key}"] = re.sub(
            r"(?i)(database=)[^;]*",
            lambda m: m.group(1) + f"toeic_space_{service}",
            pick("ConnectionStrings__IdentityDatabase"),
        )
    return {k: v for k, v in out.items() if v}


def pids_on(port: int) -> set[int]:
    lines = subprocess.run(["netstat", "-ano"], capture_output=True, text=True, check=False).stdout.splitlines()
    found = set()
    for line in lines:
        parts = line.split()
        if len(parts) >= 5 and parts[0] == "TCP" and parts[3] == "LISTENING" and parts[1].endswith(f":{port}"):
            found.add(int(parts[4]))
    return found


def parent_of(pid: int) -> tuple[int, str]:
    query = f"(Get-CimInstance Win32_Process -Filter 'ProcessId={pid}') | ForEach-Object {{ \"$($_.ParentProcessId)|$($_.Name)\" }}"
    out = subprocess.run(["powershell", "-NoProfile", "-Command", query], capture_output=True, text=True, check=False)
    ppid, _, name = out.stdout.strip().partition("|")
    return (int(ppid), name) if ppid.isdigit() else (0, "")


def stop(names: list[str]) -> None:
    for name in names:
        port = SERVICES[name][1]
        pids = pids_on(port)
        for pid in pids:
            ppid, parent = parent_of(pid)
            targets = [pid] + ([ppid] if parent.lower() == "dotnet.exe" else [])  # the `dotnet run` host too
            for target in targets:
                subprocess.run(["taskkill", "/PID", str(target), "/T", "/F"], capture_output=True, check=False)
        if not pids and healthy(port):
            # started from another terminal / job: its PID is not visible from here, its executable name is
            image = Path(SERVICES[name][0]).name + ".exe"
            subprocess.run(["taskkill", "/IM", image, "/F"], capture_output=True, check=False)
        for _ in range(20):
            if not healthy(port):
                break
            time.sleep(0.5)
        print(f"stopped {name} (:{port})" if not healthy(port) else f"{name} (:{port}) is STILL running")


def build(names: list[str]) -> None:
    for name in names:
        project = SERVICES[name][0]
        done = subprocess.run(["dotnet", "build", project, "-c", "Debug", "-nologo", "-v", "q"], cwd=ROOT, check=False)
        if done.returncode != 0:
            raise SystemExit(f"build of {name} failed")
        print(f"built {name}")


def healthy(port: int) -> bool:
    try:
        with urllib.request.urlopen(f"http://127.0.0.1:{port}/", timeout=2):
            return True
    except urllib.error.HTTPError:
        return True  # any HTTP answer means Kestrel is up (most routes need a token)
    except (urllib.error.URLError, OSError):
        return False


BREAKAWAY = 0x01000000 if os.name == "nt" else 0  # CREATE_BREAKAWAY_FROM_JOB


def start(names: list[str], *, db_from_env: bool, offset: int = 0) -> bool:
    env_file = read_env(ROOT / ".env")
    LOGS.mkdir(parents=True, exist_ok=True)
    flags = subprocess.CREATE_NEW_PROCESS_GROUP | subprocess.DETACHED_PROCESS if os.name == "nt" else 0
    ports = {}
    for name in names:
        project, port, _, _ = SERVICES[name]
        port += offset
        env = {**os.environ, **settings_for(name, env_file, db_from_env=db_from_env)}
        log = (LOGS / f"{name}{'-probe' if offset else ''}.log").open("wb")
        command = ["dotnet", "run", "--no-build", "--no-launch-profile", "--project", project, "--urls", f"http://localhost:{port}"]
        try:  # leave the launching terminal's job, so closing that terminal does not take the API down with it
            subprocess.Popen(
                command, cwd=ROOT, env=env, stdout=log, stderr=subprocess.STDOUT, creationflags=flags | BREAKAWAY
            )
        except OSError:  # the job forbids breaking away: start inside it
            subprocess.Popen(command, cwd=ROOT, env=env, stdout=log, stderr=subprocess.STDOUT, creationflags=flags)
        ports[name] = port
    deadline, ok = time.monotonic() + 120, True
    for name, port in ports.items():
        while not healthy(port) and time.monotonic() < deadline:
            time.sleep(1)
        up = healthy(port)
        ok = ok and up
        print(f"{name} :{port} {'up' if up else 'NOT UP — see ' + str(LOGS / (name + '.log'))}")
    return ok


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("action", choices=["start", "stop", "build", "restart", "status", "probe"])
    parser.add_argument("services", nargs="*", default=list(SERVICES))
    parser.add_argument("--db-from-env", action="store_true", help="also build connection strings from .env")
    args = parser.parse_args()
    names = [s for s in args.services if s in SERVICES] or list(SERVICES)
    if args.action == "status":
        for name in names:
            print(f"{name} :{SERVICES[name][1]} {'up' if healthy(SERVICES[name][1]) else 'down'}")
        return 0
    if args.action == "probe":
        return 0 if start(names[:1], db_from_env=args.db_from_env, offset=200) else 1
    if args.action in ("stop", "restart"):
        stop(names)
    if args.action in ("build", "restart"):
        build(names)
    if args.action in ("start", "restart"):
        return 0 if start(names, db_from_env=args.db_from_env) else 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
