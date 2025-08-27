# Cumberland Storm Status

A small full-stack app for monitoring storm impacts in **Cumberland County, NC**.

- **Backend:** ASP.NET Core Web API (.NET 8), EF Core, SQLite (dev)
- **Frontend:** React + TypeScript + Vite + Leaflet
- **Data:** Local `RoadClosure` records + live NCDOT incidents (county feed)
- **Tests:** xUnit (unit + integration, mocked HttpClient)
- **CI:** GitHub Actions (build, test, typecheck, lint)

[![CI](https://github.com/samuelramdial/cumberland-storm-status/actions/workflows/ci.yml/badge.svg)](https://github.com/samuelramdial/cumberland-storm-status/actions/workflows/ci.yml)

---

## Contents

- [Quick Start](#quick-start)
- [Project Structure](#project-structure)
- [Architecture (Short Note)](#architecture-short-note)
- [Configuration](#configuration)
- [API Endpoints](#api-endpoints)
- [Frontend Notes](#frontend-notes)
- [Running Tests](#running-tests)
- [CI / CD](#ci--cd)
- [Troubleshooting](#troubleshooting)
- [Future Work (Closures, Debris, Zones)](#future-work-closures-debris-zones)
- [License & Acknowledgements](#license--acknowledgements)

---

## Quick Start

### Prereqs
- **.NET 8 SDK**
- **Node 20+** and **npm**
- Windows/macOS/Linux

### 1) Backend (API)

```powershell
# from repo root
cd src/server

# first time setup (optional if migrations exist)
dotnet build
dotnet ef database update

# run API (Swagger at http://localhost:5052/swagger)
dotnet watch run --urls "http://localhost:5052"
```

> Local DB file: `stormstatus.db` in `src/server` (configurable via `appsettings.json`).

### 2) Frontend (client)

```powershell
cd src/client
npm install
npm run dev   # Vite dev server at http://localhost:5173
```

The client proxies `/api/*` to the backend during development.

---

## Project Structure

```
src/
  server/
    Api/
      DebrisRequestsController.cs
      RoadClosuresController.cs
    Domain/
      DebrisRequest.cs
      RequestUpdate.cs
      RoadClosure.cs
      Zone.cs
    Infrastructure/
      AppDbContext.cs
      NcTrafficService.cs         # live NCDOT feed reader/parsing
      NcdotFeedSync.cs            # optional: JSON-driven sync/upsert
    Migrations/
    Program.cs
    server.csproj

  client/
    src/
      App.tsx
      main.tsx
      index.css
    package.json
    tsconfig.json
    eslint.config.js

  tests/
    ClosuresUnitTests.cs           # smoke test (can be skipped in CI)
    IntegrationTests.cs            # end-to-end POST /api/debris-requests
    NcTrafficServiceTests.cs       # unit tests with mocked HttpClient
    Http/Stubs/StubHttpMessageHandler.cs
    Tests.csproj

.github/
  workflows/
    ci.yml                         # CI: backend & frontend jobs
```

---

## Architecture (Short Note)

**High-level flow**

1. **NCDOT incidents feed** → `NcTrafficService` → mapped to internal `RoadClosure` shape.
2. **Persisted closures** live in SQLite via `AppDbContext`.
3. **API** exposes:
   - `GET /api/closures` (filterable by `status`)
   - `POST /api/debris-requests` (public intake form)
4. **Frontend** shows a **list of closures** and a **Leaflet map** with markers; includes a **Debris Request** form.

**Key components**

- **Controllers**
  - `RoadClosuresController`: returns closures; can incorporate live feed data.
  - `DebrisRequestsController`: saves a request and returns `{ id }`.
- **Domain**
  - `RoadClosure` (name, status, note, `UpdatedAt`, optional `Lat/Lng`)
  - `DebrisRequest` (name, address, optional email/notes)
  - `Zone` (future: polygons for spatial filtering)
- **Data**
  - `AppDbContext` (EF Core) with SQLite by default for local dev.
- **Integration**
  - `NcTrafficService` retrieves `Feeds:Ncdot:Url` and maps/incorporates incidents.

---

## Configuration

**`src/server/appsettings.json`** (example)
```json
{
  "ConnectionStrings": {
    "Default": "Data Source=stormstatus.db"
  },
  "Feeds": {
    "Ncdot": {
      "Url": "https://eapps.ncdot.gov/services/traffic-prod/v1/counties/26/incidents?verbose=true&recent=true"
    }
  },
  "Serilog": {
    "MinimumLevel": "Information",
    "WriteTo": [ { "Name": "Console" } ]
  }
}
```

---

## API Endpoints

```
GET  /api/closures
GET  /api/closures?status=OPEN|PARTIAL|CLOSED

POST /api/debris-requests
Body:
{
  "fullName": "Jane Doe",
  "address": "123 Main St, Fayetteville NC",
  "email": "jane@example.com",
  "notes": "Tree blocking the road"
}
→ { "id": 42 }
```

---

## Frontend Notes

- **Map & Icons:** Uses `react-leaflet` + `leaflet`. Marker icons are fixed via Vite’s `import.meta.url` in `App.tsx`.  
- **Status filter:** UI filter drives `GET /api/closures?status=...`.  
- **Debris form:** Posts to the API; shows a simple success message (ticket id).

Dev scripts:
```bash
# in src/client
npm run dev        # local dev server
npm run typecheck  # TS check (no emit)
npm run lint       # ESLint (flat config)
npm run build      # production build
```

---

## Running Tests

```powershell
# from repo root
dotnet test src/tests
```

Covered:
- **Unit (NcTrafficService)**  
  - Stubs `HttpMessageHandler` to inject JSON (no network).  
  - Asserts parsing + filtering behavior.
- **Integration (DebrisRequests)**  
  - In-memory host with `WebApplicationFactory`, POSTs to controller, expects `{ id }`.

---

## CI / CD

**Workflow:** `.github/workflows/ci.yml`

Jobs:
- **Backend (.NET)**  
  - Restore → Build (server & tests) → Test → (advisory) `dotnet format`.
- **Frontend (Vite/React)**  
  - Install → Typecheck → Lint → Build.  
  - Steps are tolerant if scripts are missing; remove the “skip” guards to enforce.

Add a README badge:
```md
[![CI](https://github.com/samuelramdial/cumberland-storm-status/actions/workflows/ci.yml/badge.svg)](https://github.com/samuelramdial/cumberland-storm-status/actions/workflows/ci.yml)
```

---

## Troubleshooting

- **Locked `server.exe` during build/tests**  
  Close `dotnet watch run` or kill the process:
  ```powershell
  taskkill /IM server.exe /F
  dotnet clean src/server
  dotnet build src/server
  ```

- **Leaflet markers not visible**  
  Ensure `import 'leaflet/dist/leaflet.css'` and the icon URLs using `import.meta.url` are present in `App.tsx`.

- **TypeScript can’t find Leaflet types**  
  `npm i -D @types/leaflet` and use `import * as L from 'leaflet'`.

---

## Future Work (Closures, Debris, Zones)

### A) Process Debris Requests into Map Updates
- **Geocode** addresses (e.g., a geocoding API) → save `Lat/Lng` on `DebrisRequest`.
- **Normalize & deduplicate** (same address within N hours → single “active” request).
- **Severity tagging** (keywords → levels; later, ML classification).
- **Surface on the map** as a separate layer (cluster/heatmap), distinct from official closures.
- **Feedback loop**: staff actions (picked up / deferred) generate `RequestUpdate` rows → adjust markers.

### B) Keep Closures Fresh (From Feeds → DB)
- Background job (`IHostedService`, Hangfire, or Quartz) that:
  - Pulls NCDOT feed every N minutes.
  - Maps items → `RoadClosure` with dedupe (e.g., by `RoadName` + `Lat/Lng`).
  - **Expires** stale items (auto-reopen or mark as resolved after threshold).
- **Caching**: `IMemoryCache` around feed parsing to minimize external calls.

### C) Utilize Zones for Targeted Views
- Store **Zone** polygons (GeoJSON/WKT).  
  Use **EF Core + NetTopologySuite** for spatial queries:
  - `GET /api/zones` — list + rollups (counts by status).
  - `GET /api/zones/{id}/closures` — closures within zone polygon.
  - `GET /api/closures?zoneId=...` — server-side spatial filtering.
- **UI**: overlay zone polygons, click to filter list/map; show zone summaries.

### D) Analytics & Notifications
- **Hotspots**: combine closures + debris requests to highlight problem corridors.
- **Alerts**: webhook/email when:
  - a zone exceeds N closed roads,
  - a closure persists > 4 hours,
  - debris requests spike in a zone.

### E) Hardening & Ops
- Input validation & rate limiting on debris requests.
- Audit log for status changes.
- More tests: status-mapper unit tests, zone filter controller tests, end-to-end feed parsing with a fixed fixture.

---

## License & Acknowledgements

- **License:** MIT
- **Thanks:** OpenStreetMap contributors (tiles & data) and NCDOT incidents feed for public road disruption data.
