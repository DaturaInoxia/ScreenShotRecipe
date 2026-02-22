# Implementation Plan: Import recipe from multiple images

**Branch**: `001-import-recipe` | **Date**: 2026-02-21 | **Spec**: specs/001-import-recipe/spec.md

## Summary

Build a small, multi-project .NET 9 Clean Architecture implementation that accepts multi-file image uploads, runs Azure Vision OCR on each image, concatenates OCR text, invokes a pluggable LLM parser to produce a structured `Recipe`, stores the recipe in SQLite and images on the filesystem, and exposes a Blazor Server UI + Minimal API endpoints for import, browse, and search.

## Technical Context

- Language/Version: .NET 9
- Primary Dependencies: Blazor Server, Minimal APIs, EF Core (SQLite), Azure Cognitive Services (Vision OCR), Azure OpenAI (or pluggable LLM), DI via built-in Microsoft DI
- Storage: SQLite (default), local filesystem for images (default)
- Testing: xUnit for unit tests; integration tests will mock external services
- Target Platform: Linux containers (Docker) inside LXC on Proxmox for self-hosting
- Project Type: multi-project solution (Domain, Application, Application.Contracts, Infrastructure, Web, Tests)

## Constitution Check

This plan follows the ScreenShotRecipe Constitution (.specify/memory/constitution.md). Key gates: Azure Vision as primary OCR engine, LLM parsing behind abstraction, DI for all external dependencies, SQLite + filesystem default.

## Project Structure

```
src/
├─ ScreenShotRecipe.Domain/            # Entities, interfaces
├─ ScreenShotRecipe.Application/       # Use-case services (ImportService)
├─ ScreenShotRecipe.Application.Contracts/ # DTOs
├─ ScreenShotRecipe.Infrastructure/    # EF Core, OCR/LLM stubs, FileSystem storage
└─ ScreenShotRecipe.Web/               # Blazor Server UI + Minimal API

tests/
└─ ScreenShotRecipe.Tests/
```

## Complexity Tracking

No major constitution violations; external service integrations are isolated behind interfaces and provided as Docker-configurable implementations.
