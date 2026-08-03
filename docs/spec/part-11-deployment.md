# Styrofoam Factory ERP System — Part 11: Deployment, Server Architecture, Docker, CI/CD and Backup

Version: 1.0

## 1. Introduction

The ERP runs entirely on the factory's local network. Internet access is not required for daily operation. The server sits inside the factory; all employees connect over the LAN.

Benefits: better security, faster performance, independence from internet outages, centralized data management.

## 2. System Architecture

1. **Frontend** — React application
2. **Backend** — ASP.NET Core Web API
3. **Database** — PostgreSQL
4. **File Storage** — barcode images, backups, reports

## 3. Technology Stack

| Layer | Choice |
|---|---|
| Frontend | React |
| Backend | ASP.NET Core Web API |
| Database | PostgreSQL |
| ORM | Entity Framework Core |
| Authentication | ASP.NET Identity |
| Authorization | Role-based |
| Server | Windows Server |
| Version control | GitHub |
| Containerization (future) | Docker |
| CI/CD (future) | GitHub Actions, self-hosted runner |

## 4. Local Network Architecture

```
                Factory Network
        +----------------------+
        |   Windows Server     |
        |----------------------|
        | ASP.NET Core API     |
        | PostgreSQL Database  |
        | React Frontend       |
        +----------+-----------+
                   |
   ---------------------------------------
   |             |            |          |
Admin PC   Thermo Tablet  Extruder   Recycler
                            Tablet     Tablet
```

## 5. Development Environment

`dotnet run` — development only. The application is compiled dynamically at each start.

## 6. Production Environment

Production does **not** use `dotnet run`. The application is published:

```bash
dotnet publish -c Release
```

Advantages: better performance, smaller deployment, faster startup, no Visual Studio required.

## 7. Publishing Process

1. Complete development
2. Commit changes to GitHub
3. Publish the backend (`dotnet publish`)
4. Copy published files to the server
5. Restart the application

> The source code does not need to exist on the production server.

## 8. Deployment Strategy

```
Developer Computer → GitHub Repository → Publish Build → Factory Server → Workers Use ERP
```

GitHub is the source repository; the server runs only published production files.

## 9. Remote Deployment

The developer lives approximately **two hours** from the factory, so remote deployment is highly desirable.

Options: Remote Desktop (RDP) · VPN · GitHub Actions (self-hosted) · AnyDesk · TeamViewer.

**Preferred long-term:** GitHub Actions with a self-hosted runner.

## 10. GitHub Actions

```
Developer Pushes Code → GitHub → Self-Hosted Runner Detects Changes
  → Downloads Latest Code → Builds Project → Publishes Project → Restarts Application
```

No manual copying required.

## 11. Self-Hosted Runner

The factory's Windows Server runs a GitHub Actions self-hosted runner that listens for new commits and automatically downloads source, builds, publishes and deploys — a complete CI/CD pipeline.

## 12. Docker

Planned for future deployment. Containerize production components only: ASP.NET Core API, PostgreSQL, and future services (Redis, monitoring, logging).

Development source code should **not** be containerized for production.

## 13. Docker Philosophy

```
Build Application → dotnet publish → Copy Published Files → Docker Image → Run Container
```

Keeps containers small and efficient.

## 14. Database Storage

PostgreSQL uses the server's local storage. No fixed size limit — the practical limit is available disk space (e.g. 500 GB). Additional storage can be added later.

## 15. Backup Strategy

Daily, weekly and monthly backups. Each creates a separate file; backups should **not** overwrite previous ones, allowing restore to multiple historical points.

## 16. Backup Storage

Backups stored in multiple locations:

```
Windows Server → External Hard Drive → Cloud Storage (optional)
```

Multiple copies protect against hardware failure.

## 17. Disaster Recovery

1. Install Windows Server
2. Install PostgreSQL
3. Restore database backup
4. Publish latest backend
5. Deploy React frontend
6. Reconnect users

## 18. Version Control

GitHub stores the complete project history. Every feature committed separately — "Added Inventory Module", "Implemented Roll Test Reports", "Created Barcode System", "Improved Recipe Versioning".

## 19. Future Improvements

Docker Compose · reverse proxy (Nginx or IIS) · **HTTPS certificates** · automated database backups · automatic health monitoring · logging systems · performance monitoring · zero-downtime deployment.

Optional, addable after v1.

## 20. Module Summary

Deployment is intentionally simple for v1: one Windows Server running the React frontend, the ASP.NET Core Web API and PostgreSQL, entirely inside the factory network, with source managed through GitHub. Future versions may add Docker and GitHub Actions for fully automated CI/CD without the developer travelling to the factory.

---

## Open questions raised during review

Resolves the off-machine half of [Q81](open-questions.md) — §16 adds an external drive and optional cloud copy.

New: **Q86** (EF Core migrations are never addressed), **Q87** (no rollback path), **Q88** (HTTPS explicitly deferred), **Q89** (no logging in v1), Q90–Q94.

---
*End of Part 11.*
