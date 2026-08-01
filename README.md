# AppCore

AppCore is a reusable, Arabic-first enterprise application template built with ASP.NET Core 10, React, PostgreSQL, and Ant Design. It supplies authentication, MFA, server-side sessions, roles and permissions, user and security administration, audit, visual identity, shared RTL page patterns, tests, and CI without embedding an application-specific business domain.

## Use this template

1. Choose **Use this template** on GitHub.
2. Clone the generated repository.
3. Run:

```powershell
pwsh ./scripts/Initialize-App.ps1 `
  -RootNamespace Contoso.Hr `
  -ProductName "Contoso HR" `
  -ProductNameArabic "نظام الموارد البشرية" `
  -ShortProductName "HR" `
  -ShortProductNameArabic "الموارد البشرية" `
  -DatabaseName contoso_hr
```

4. Complete and approve the requirement documents in `docs/requirements/` before adding business modules.

## Included foundation

- ASP.NET Core Web API, EF Core/Npgsql, PostgreSQL, OpenAPI, health checks, RFC 7807 errors.
- React, TypeScript, Vite, Ant Design, TanStack Query, React Hook Form, Zod, Axios, i18next.
- Arabic RTL and English LTR, horizontal navigation, shared headers, cards, grids, pagination, modals, drawers, forms, loading, empty, and error states.
- Secure cookies, antiforgery, login throttling, TOTP MFA and recovery, password lifecycle, protected bootstrap owner.
- Code-controlled permissions, built-in/custom roles, assurance levels, sessions, immutable redacted security audit.
- Database-backed organization names, colors, appearance, patterns, logos, and favicon.
- GitHub Actions backend/frontend CI and PostgreSQL integration tests.

## Structure

```text
backend/AppCore.sln
backend/src/AppCore.*
backend/tests/AppCore.*
frontend/
docs/
scripts/
prompts/
```

## Setup and validation

```powershell
Copy-Item frontend/.env.example frontend/.env
dotnet restore backend/AppCore.sln
dotnet build backend/AppCore.sln --configuration Release --no-restore
dotnet test backend/AppCore.sln --configuration Release --no-build
Set-Location frontend
npm ci
npm run format:check
npm run lint
npm test
npm run build
```

Never commit connection strings, passwords, HMAC keys, Data Protection certificates, or tokens. Production must configure explicit hosts, CORS origins, trusted proxies, persistent protected Data Protection keys, and persistent branding storage.
