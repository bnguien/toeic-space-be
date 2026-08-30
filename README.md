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

3. Restore dependencies:

   ```bash
   dotnet restore ToeicSpace.slnx
   ```

4. Build the solution:

   ```bash
   dotnet build ToeicSpace.slnx
   ```

5. Start the local environment:

   ```bash
   docker compose up -d --build
   ```

6. Verify the running containers:

   ```bash
   docker compose ps
   ```

## 🚀 Usage

Start all configured services:

```bash
docker compose up -d
```

Run a specific API directly with hot reload:

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

View logs:

```bash
docker compose logs -f
```

Stop the local environment:

```bash
docker compose down
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
