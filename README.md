# Home Maintenance Software

A clean-architecture home maintenance tracking application.
See [ARCHITECTURE.md](./ARCHITECTURE.md) for all project rules and decisions.

## Prerequisites

- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9)
- [Node.js 22+](https://nodejs.org/)
- [Docker Desktop](https://www.docker.com/products/docker-desktop/)

## Quick Start (local dev)

### 1. Start MongoDB

```bash
docker compose up mongodb -d
```

### 2. Run the backend API

```bash
cd backend
dotnet run --project src/HomeMaintenance.API
# API available at http://localhost:5000
# Health check: http://localhost:5000/health
```

### 3. Run the frontend

```bash
cd frontend
npm install
npm run dev
# App available at http://localhost:3000
```

### Run everything via Docker Compose

```bash
docker compose up --build
```

## Running Tests

### Backend unit tests

```bash
cd backend
dotnet test tests/HomeMaintenance.Unit.Tests
```

### Backend integration tests (requires Docker for Testcontainers)

```bash
cd backend
dotnet test tests/HomeMaintenance.Integration.Tests
```

### Frontend tests

```bash
cd frontend
npm test
```

## Project Structure

```
home-maintenance/
├── ARCHITECTURE.md         - project rules & decisions
├── docker-compose.yml
├── backend/
│   ├── HomeMaintenance.sln
│   ├── src/
│   │   ├── HomeMaintenance.Domain/
│   │   ├── HomeMaintenance.Application/
│   │   ├── HomeMaintenance.Infrastructure/
│   │   └── HomeMaintenance.API/
│   └── tests/
│       ├── HomeMaintenance.Unit.Tests/
│       └── HomeMaintenance.Integration.Tests/
└── frontend/               - Next.js 15 + TypeScript + Tailwind CSS
```
