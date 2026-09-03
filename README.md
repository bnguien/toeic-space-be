<h1 align="center">TOEICSpace Backend</h1>

<p align="center">
  <img alt=".NET" src="https://img.shields.io/badge/.NET-10.0-512BD4?style=for-the-badge&logo=dotnet&logoColor=white" />
  <img alt="ASP.NET Core" src="https://img.shields.io/badge/ASP.NET%20CORE-10.0-512BD4?style=for-the-badge&logo=dotnet&logoColor=white" />
  <img alt="MySQL" src="https://img.shields.io/badge/MYSQL-4479A1?style=for-the-badge&logo=mysql&logoColor=white" />
  <img alt="RabbitMQ" src="https://img.shields.io/badge/RABBITMQ-FF6600?style=for-the-badge&logo=rabbitmq&logoColor=white" />
  <img alt="Docker" src="https://img.shields.io/badge/DOCKER-2496ED?style=for-the-badge&logo=docker&logoColor=white" />
  <img alt="License" src="https://img.shields.io/badge/LICENSE-MIT-green?style=for-the-badge" />
</p>

TOEICSpace Backend is the backend system for an online TOEIC Listening and Reading learning and test preparation platform combined with a Learning Management System (LMS) for language centers.

The backend is designed using a microservices-oriented architecture with API Gateway routing, independent service boundaries, asynchronous messaging, and containerized local infrastructure.

## ✨ Features

- Authentication, authorization, roles, and permissions.
- Course, module, lesson, and vocabulary management.
- TOEIC Parts 1–7 practice, mini tests, mock tests, and placement tests.
- Test attempts, results, progress tracking, and learning history.
- Mistake Notebook, Smart Review, and answer explanations.
- Classroom, enrollment, assignment, schedule, attendance, and announcement management.
- Tuition payment, invoice, receipt, and refund management.
- API Gateway routing with YARP.
- Asynchronous communication with RabbitMQ and MassTransit.
- Docker-based local development environment.

The current scope focuses on TOEIC Listening and Reading. TOEIC Speaking and Writing are not currently included.

## 🛠️ Installation

### Prerequisites

- .NET SDK `10.x`
- Docker Desktop
- Git

### Local setup

1. Clone the repository:

   ```bash
   git clone <repository-url>
   cd toeic-space-be
   ```

2. Create the local environment file:

   ```bash
   cp .env.example .env
   ```

3. Update the required values in `.env`.

4. Restore dependencies:

   ```bash
   dotnet restore ToeicSpace.slnx
   ```

5. Build the solution:

   ```bash
   dotnet build ToeicSpace.slnx
   ```

6. Start the service profile you want to work with.

   Example — Identity Service:

   ```bash
   docker compose --profile identity up -d --build
   ```

7. Verify the running containers:

   ```bash
   docker compose ps
   ```

## 🚀 Docker Compose Usage

The local environment uses Docker Compose profiles so that each microservice can be started independently together with the infrastructure it requires.

### Run a specific service

Use:

```bash
docker compose --profile <service-profile> up -d --build
```

Examples:

```bash
docker compose --profile identity up -d --build
docker compose --profile learning up -d --build
docker compose --profile assessment up -d --build
docker compose --profile payment up -d --build
docker compose --profile notification up -d --build
```

Each profile starts the selected application service together with its required database and shared infrastructure such as RabbitMQ, Redis, and the API Gateway when configured for that profile.

### Run all services

To start the complete local microservices environment:

```bash
docker compose --profile full up -d --build
```

### Rebuild a profile

If Docker images need to be rebuilt:

```bash
docker compose --profile <service-profile> up -d --build
```

Example:

```bash
docker compose --profile identity up -d --build
```

### Start without rebuilding

```bash
docker compose --profile <service-profile> up -d
```

Example:

```bash
docker compose --profile identity up -d
```

### View running containers

```bash
docker compose ps
```

### View logs

All containers:

```bash
docker compose logs -f
```

A specific service:

```bash
docker compose logs -f identity-api
```

### Stop containers

Stop and remove the currently running Compose environment:

```bash
docker compose --profile full down
```

If a profile-specific command is preferred:

```bash
docker compose --profile identity down
```

### Validate Docker Compose configuration

Before starting the environment, the Compose configuration can be validated with:

```bash
docker compose --profile identity config
```

For the full environment:

```bash
docker compose --profile full config
```

### Available profiles

```text
identity
learning
assessment
payment
notification
full
```

`full` is the profile used to run all configured services.

## 💻 Run an API directly

A service can also be run directly with .NET hot reload:

```bash
dotnet watch --project <api-project-path> run
```

Example:

```bash
dotnet watch --project src/Services/Identity/ToeicSpace.Identity.API/ToeicSpace.Identity.API.csproj run
```

Build the solution:

```bash
dotnet build ToeicSpace.slnx
```

Run tests:

```bash
dotnet test ToeicSpace.slnx
```

### Local endpoints

```text
API Gateway:
http://localhost:5050

Identity API Swagger:
http://localhost:6001/swagger/index.html

Identity API through Gateway:
http://localhost:5050/identity/swagger/index.html

RabbitMQ Management:
http://localhost:15672
```

Ports can be changed through `.env`.

## 📚 Documentation

```text
docs/
├── architecture.md
├── folder-structure.md
├── coding-conventions.md
└── DEVELOP_GUILINE.md
```

## 📄 License

This project is licensed under the [MIT License](LICENSE).
