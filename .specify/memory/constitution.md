<!--
Sync Impact Report

- Version change: UNKNOWN -> 1.0.0
- Modified principles:
	- (new) Reliability First -> Reliability First (defined)
	- (new) GPT-4o Multimodal Extraction
	- (new) Pluggable Extraction Interface
	- (new) Clean Architecture (.NET 9 multi-project)
	- (new) Separation of Concerns: Domain/Application/Application.Contracts/Infrastructure/Web
	- (new) Service-based Use Cases & DI/IoC
	- (new) Blazor Server UI + Minimal APIs backend
	- (new) SQLite persistence (swappable) + local filesystem image storage
	- (new) Self-hosting via Docker in LXC on Proxmox
	- (new) Testability, Maintainability, Extensibility
- Added sections: Core Principles, Technical Constraints, Development Workflow, Governance (detailed)
- Removed sections: None
- Templates reviewed: \n+  - .specify/templates/plan-template.md ✅ aligned
	- .specify/templates/spec-template.md ✅ aligned
	- .specify/templates/tasks-template.md ✅ aligned
	- .specify/templates/constitution-template.md ✅ consumed
- Follow-up TODOs:
	- TODO(RATIFICATION_REVIEW): Confirm the official ratification approver and update the ratification date if different.
	- TODO(OWNERS): Add maintainers list and decision workflow to a separate MAINTAINERS.md if desired.
-->

# ScreenShotRecipe Constitution

## Core Principles

### 1. Reliability First (NON-NEGOTIABLE)
All user-facing features and background processing MUST be designed for reliability. Systems must be resilient to transient failures, include retry policies with exponential backoff, clear idempotency semantics for repeated operations, and observable health checks. Rationale: The application processes user screenshots and text extraction; correctness and uptime are critical.

### 2. GPT-4o Multimodal Extraction as Primary Engine
Official production OCR and parsing MUST use Azure OpenAI GPT-4o multimodal vision API for combined image text extraction and structured recipe parsing. Integration MUST validate results using confidence thresholds and structured result checks; extraction failures or low-confidence results MUST trigger retry flows and clear diagnostics. Rationale: GPT-4o multimodal provides superior accuracy by combining OCR and semantic parsing in a single API call, eliminating separate LLM parsing step.

### 3. Pluggable LLM-Based Parsing (Provider-Replaceable)
LLM parsing logic (transforming raw OCR output into structured domain entities) MUST be implemented behind an abstraction that allows swapping providers. Providers MUST be replaceable via configuration and DI. The parsing interface MUST be deterministic when given identical inputs and include explicit contracts for retries, timeouts, and error classification. Rationale: Enables experimentation and risk mitigation across LLM vendors.

### 4. Clean Architecture - Multi-Project .NET 9 Solution
The codebase MUST be organized as a multi-project .NET 9 solution following Clean Architecture: distinct projects for `Domain`, `Application`, `Application.Contracts`, `Infrastructure`, and `Web`. Each layer has clearly defined responsibilities; dependencies flow inward (Web → Application → Domain). Rationale: Separation of concerns improves testability and long-term maintainability.

### 5. Strict Separation of Concerns (Layer Contracts)
Each layer MUST expose only minimal, versioned contracts. `Application.Contracts` MUST contain DTOs and interfaces shared across layers. `Infrastructure` MUST implement the interfaces and be replaceable. `Domain` MUST contain entities and domain logic only. Rationale: Prevents leakage of implementation details and simplifies swapping components.

### 6. Service-Based Use Cases & Dependency Injection
Each user action and business capability MUST be modeled as a service/use-case in `Application`. All external dependencies (OCR client, LLM provider, database, file storage) MUST be injected via the DI container and resolved at composition root. No static singletons for external services. Rationale: Enables clear unit testing and runtime substitution.

### 7. UI & API Choices
The primary UI MUST be Blazor Server. The backend surface MUST expose Minimal APIs for programmatic access (file uploads, OCR jobs, parsing). UI and API MUST rely on the same `Application` services to avoid duplication. Rationale: Blazor Server offers a thin-client experience while Minimal APIs keep the HTTP surface minimal and testable.

### 8. Persistence & Storage - SQLite with Swappable Implementations
Default persistence MUST be SQLite for simplicity and single-file deployments. The data access layer MUST be abstracted to allow replacement (e.g., PostgreSQL) without changing domain/application code. Image binaries MUST be stored on the local filesystem by default with an interchangeable storage provider interface (S3-compatible or other). Rationale: Keeps the system lightweight while supporting future scaling.

### 9. Local Filesystem Image Storage (Default)
By default, images and derived artifacts (thumbnails, OCR cache) MUST be stored on the host filesystem under a configurable root path. Storage access MUST be behind an interface to support swappable backends. Rationale: Simplifies running in single-node/self-hosted environments.

### 10. Self-Hosting & Deployment: Docker in LXC on Proxmox
The recommended deployment model is containerized via Docker, running inside an LXC container on Proxmox for simple self-hosting. The repository MUST include a minimal Dockerfile and a docker-compose override for local/Proxmox deployment. Rationale: Low-cost, reproducible self-hosting for users/operators.

### 11. Testability & CI Requirements
All code MUST be unit-testable. `Domain` and `Application` layers MUST have unit tests covering business rules. Integration tests MUST cover extraction integration (using mocked GPT-4o contracts) and end-to-end parsing flows. CI MUST run tests and the Constitution Check (see Plan template) on PRs. Rationale: Prevent regressions and ensure correctness.

### 12. Maintainability & Extensibility
Code MUST favor readability and small modules. Public interfaces and contracts MUST be versioned. Backwards-incompatible changes to `Application.Contracts` or public APIs MUST result in a MAJOR version bump and a documented migration plan. Rationale: Ensures long-term evolution without surprising consumers.

## Technical Constraints

- Language/Runtime: .NET 9
- Primary UI: Blazor Server
- API: Minimal APIs
- Extraction: Azure OpenAI GPT-4o multimodal (combined OCR + parsing)
- DB: SQLite (default) with swappable provider implementations
- Storage: Local filesystem (default) with swappable provider interface
- Containerization: Docker (required for deployment artifacts)
- Hosting: LXC on Proxmox recommended for self-hosting

## Development Workflow

- All work MUST follow Pull Request workflow. PRs that change behavior described in this constitution MUST include a migration plan and tests.
- Every PR MUST pass: unit tests, integration tests (where applicable), and automated checks enforcing DI usage and layered dependency rules when feasible.
- Breaking changes to public contracts MUST be proposed as a documented amendment to this constitution and accompanied by a version bump following our versioning policy below.

## Governance

Amendments: Constitutional changes MUST be made by a PR titled `docs: amend constitution to vX.Y.Z` that includes: a succinct rationale, a migration/compatibility plan, tests or validation steps, and the proposed version bump. Approval requires at least one maintainer approval and CI green on the amendment PR.

Versioning Policy: Semantic versioning applied to the constitution and public contracts:
- MAJOR: Backwards-incompatible changes to governance or public contracts (removing/renaming principles, or breaking `Application.Contracts`).
- MINOR: Addition of a principle or material new guidance that expands obligations.
- PATCH: Wording clarifications, typo fixes, or non-semantic refinements.

Compliance Review: All PRs touching runtime behavior MUST include a short checklist referencing applicable principles (OCR accuracy, LLM provider abstraction, DI, storage). The `plan-template.md` Constitution Check MUST be consulted during Phase 0 research.

**Version**: 1.0.0 | **Ratified**: 2026-02-21 | **Last Amended**: 2026-02-21
