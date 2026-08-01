# Repository Rules

These rules are mandatory for every human contributor and every AI coding agent working in this repository.

## 1. Branch Rule

- Work directly on `main` unless the repository owner explicitly instructs otherwise.
- Never create or switch branches without explicit permission.
- Keep `main` buildable after every meaningful change.

## 2. Read Before Coding

Before changing code:

1. Read this file completely.
2. Read `README.md`.
3. Read `prompts/README.md`.
4. Read the current phase prompt.
5. Inspect the existing repository structure and relevant files.
6. Do not recreate or replace working code without a clear reason.

## 3. Approved Stack

### Backend
- ASP.NET Core 10 Web API
- C# with nullable reference types enabled
- EF Core 10
- PostgreSQL through Npgsql
- ASP.NET Core Identity
- SignalR
- OpenAPI

### Frontend
- React
- TypeScript with strict mode
- Vite
- Ant Design
- React Router
- TanStack Query
- React Hook Form
- Zod
- Axios
- i18next
- Apache ECharts

### Future Mobile
- React Native with TypeScript
- Same backend API and contracts

Do not introduce an alternative framework, database, ORM, UI kit, charting library, validation library, state library, or authentication architecture unless explicitly approved.

## 4. Open-Source-Only Rule

- Every dependency must be free and open source.
- Do not use trial software, commercial licenses, paid tiers required for production features, or feature-gated premium components.
- Before adding a dependency, verify its license and maintenance status.
- Prefer mature libraries with active maintenance and clear documentation.

## 5. Architecture Rules

- Build API-first.
- Keep Domain independent of Infrastructure, UI, database, and external services.
- Keep business rules in Domain/Application, not controllers or React components.
- Controllers/endpoints must be thin.
- React must never connect directly to PostgreSQL.
- Web and future mobile applications must use the same versioned API.
- Use dependency injection and interfaces at external boundaries.
- Avoid circular project references.
- Avoid generic repository abstractions that merely duplicate EF Core unless a real boundary requires one.

## 6. Security Rules

- Never commit passwords, connection strings, private keys, access tokens, refresh tokens, or production secrets.
- Use environment variables, user secrets, or secret managers.
- Enforce authorization in the backend for every protected action.
- Frontend route guards and hidden buttons are usability features, not security controls.
- Validate uploaded file type, extension, size, signature where practical, and storage path.
- Prevent path traversal and unsafe file names.
- Use secure cookies or another explicitly approved token strategy.
- Apply rate limiting to authentication and sensitive endpoints.
- Record security-relevant actions in the audit trail.

## 7. Database Rules

- PostgreSQL is mandatory.
- Use EF Core migrations.
- Never modify an already-applied production migration; create a new migration.
- Use UTC for stored timestamps and convert only at presentation boundaries.
- Define foreign keys, unique constraints, indexes, precision, maximum lengths, and deletion behavior explicitly.
- Avoid cascade delete for important administrative records unless explicitly required.
- Prefer soft deletion or archival for business records that must remain auditable.

## 8. API Rules

- Use consistent route naming and versioning.
- Use DTOs/contracts; never expose EF entities directly.
- Return consistent success and error shapes.
- Use RFC 7807 Problem Details for errors.
- Support pagination, filtering, sorting, and search for list endpoints.
- Use cancellation tokens for async I/O operations.
- Generate and maintain OpenAPI documentation.
- Do not silently change an existing API contract.

## 9. Frontend and UX Rules

- Arabic is the primary language.
- Full RTL support is mandatory.
- All visible text must be translated through i18next; no scattered hard-coded UI strings.
- The desktop interface must use a horizontal top menu.
- Optimize administrative forms for fast keyboard entry.
- Every form must show clear validation and server errors.
- Every list page must define loading, empty, error, and success states.
- Unexpected global API and client failures must navigate to the shared error page and display a safe error code, HTTP status when available, and correlation ID when supplied. Do not use a generic toast as the only indication of an unexpected failure. Expected validation errors remain within their form.
- All pages must be responsive, but desktop productivity has priority.
- Use Ant Design as the UI component system.
- Do not mix another full UI framework into the project.

### Mandatory Page Layout and Grid Standard

Every application page must use the same approved visual structure and spacing. A page is not complete when its data works but its layout is inconsistent, unaligned, excessively wide, or visually unfinished.

- Every protected page must start with the shared `PageHeader` containing a clear page title, optional short description, and primary page actions aligned in one ordered header row.
- Page actions such as **Add**, **Create**, **Export**, and **Refresh** belong in the page header or the list toolbar. Do not place a large creation form above a data grid.
- Every content section must use the shared card/section patterns with consistent border radius, border, background, padding, vertical rhythm, and responsive behavior.
- All list and administration pages must use the shared grid/table presentation. Table headers, row height, cell alignment, actions, empty state, loading state, error state, and horizontal overflow must look the same across the application.
- Table headers must use the centrally configured visual-identity colors through shared theme tokens or CSS variables. Never hard-code a separate page-specific table-header color.
- Administrative grids must be compact and desktop-friendly, with readable column widths, non-wrapping identifiers where appropriate, and a fixed or clearly grouped actions column when practical.
- Every grid must provide pagination. Client-side pagination is acceptable only for an already bounded small result set; normal business lists must use API pagination with page, page size, total count, filters, search, and sorting.
- Every grid must provide ascending and descending sorting for every meaningful data column. Action, selection, and purely presentational columns are exempt. Bounded fully loaded lists may sort client-side; server-paginated lists must send an allowlisted sort field and direction to the API so the complete result set is sorted before pagination. Use a deterministic secondary key when values can tie.
- Every route transition must open the destination page at the top of its scroll viewport. Implement this centrally so it applies consistently to authenticated, anonymous, error, and future pages.
- Standard page-size options are `10`, `20`, and `50`, unless an approved requirement specifies otherwise.
- Search, filters, result count, refresh, export, and other list controls belong in one consistent toolbar above the grid.
- Do not render an empty search bar when a page has no search behavior.
- Status values must use shared translated badges/tags and semantic colors. Raw backend enum names such as `NotEnrolled` or `Enabled` must not be shown directly to Arabic users.
- Row actions must be grouped consistently. Use compact buttons or a dropdown when actions are numerous; do not allow action links to spread unpredictably across the row.
- Forms must use a responsive grid: two or more aligned columns on wide desktop screens when fields permit, one column on narrow screens, consistent label placement, and a clearly separated footer for submit/cancel actions.
- All page styling must work in Arabic RTL and English LTR without page-specific directional hacks.
- Before declaring a page complete, manually inspect it at desktop, tablet, and mobile widths and verify header alignment, table overflow, pagination, spacing, and action placement.

### Create and Edit Interaction Standard

- Every create/add workflow must open in either a dedicated routed page or a shared Ant Design `Modal`/`Drawer`. Do not embed a full create form permanently above or inside the listing grid.
- Use a dedicated page for long, multi-section, attachment-heavy, workflow-heavy, or deep-linkable forms.
- Use a Modal for short forms that can be completed safely without losing list context.
- Use a Drawer for medium forms that benefit from retaining list context or showing related record information.
- Edit workflows must follow the same rule: dedicated page, Modal, or Drawer. Do not turn table cells into large uncontrolled edit forms.
- Create and edit surfaces must have a clear title, optional context text, validation summary, scroll-safe body, and consistent footer buttons for save and cancel.
- Successful create/edit actions must close or navigate back, refresh affected queries, preserve useful list filters, and show clear success feedback.
- Destructive and security-sensitive actions must continue to use the shared confirmation pattern and backend authorization.

## 10. Mandatory Visual Identity

A System Settings page named **Visual Identity** is mandatory. It must manage:

- Organization name
- Short organization name
- Primary color
- Secondary color
- Light-background logo
- Dark-background logo
- Compact logo/icon
- Favicon
- Live preview
- Restore defaults

Rules:

- Branding values must be centrally stored and retrieved.
- Do not hard-code brand colors or organization logos inside pages.
- Apply branding to login, navigation, dashboard, reports, print layouts, exports, and future mobile app.
- Validate colors and uploaded image formats.
- Provide safe defaults when a setting is unavailable.

## 11. Coding Standards

### C#
- Enable nullable reference types.
- Use async/await for I/O.
- Do not use `.Result` or `.Wait()`.
- Use meaningful names and small focused methods.
- Prefer records for immutable contracts when appropriate.
- Add XML documentation only where it adds meaningful API or architectural value.

### TypeScript
- Enable strict mode.
- Do not use `any` unless isolated, documented, and unavoidable.
- Keep server state in TanStack Query.
- Use React Hook Form and Zod for forms.
- Avoid duplicating API types manually when generated contracts are available.
- Keep components focused; move reusable logic to hooks/services.

## 12. Quality Gates

Before declaring a phase complete:

- Backend builds without errors.
- Frontend builds without errors.
- Relevant tests pass.
- Linting passes.
- Database migrations are valid.
- No secrets or generated build artifacts are committed.
- The feature is manually checked in Arabic RTL.
- Every changed page follows the mandatory page-layout, grid, pagination, and create/edit interaction standards.
- README or docs are updated when behavior or setup changes.

## 13. Change Discipline

- Implement only the requested phase and required supporting changes.
- Do not perform unrelated refactoring.
- Preserve backward compatibility unless an approved prompt explicitly changes it.
- Explain important architectural decisions in `docs/architecture/`.
- Use clear commit messages describing the completed outcome.

## 14. AI Agent Completion Report

At the end of every prompt, report:

1. Files created or changed.
2. Main implementation decisions.
3. Commands executed.
4. Test/build results.
5. Migrations created.
6. Known limitations or follow-up work.
7. Confirmation that work remained on `main`.
