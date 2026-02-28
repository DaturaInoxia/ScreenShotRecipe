```markdown
# Feature Specification: Import recipe from multiple images

**Feature Branch**: `001-import-recipe`  
**Created**: 2026-02-21  
**Status**: Draft  
**Input**: User description: "Build an application that lets my wife and I upload one or more screenshots or photos that together represent a single recipe, such as multiple Facebook screenshots or photos of a recipe card. The system should send all images to a cloud OCR service, combine the extracted text in order, use an LLM to parse the combined text into a structured recipe (title, ingredients, steps, tags, notes), and store both the structured recipe and the original images. The application should provide a Blazor Server UI to browse, search, and view recipes, including the original images that produced them. The import process should be a single-button workflow where the user uploads multiple images and receives a complete parsed recipe automatically."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Import images and receive parsed recipe (Priority: P1)

As a user, I upload multiple images that together represent a single recipe (screenshots or photos). I press a single "Import" button and receive a structured recipe (title, ingredients, steps, tags, notes) automatically. The original images are stored and associated with the recipe.

**Why this priority**: This is the core value — turn multi-image recipe sources into a single editable recipe with minimal effort.

**Independent Test**: Upload 1–6 images that form a recipe (e.g., title image + ingredients screenshot + steps screenshot). Confirm that the returned recipe contains a title, a non-empty ingredients list, and at least one step; verify original images are linked and retrievable.

**Acceptance Scenarios**:

1. **Given** the user is authenticated (or the app is running locally with single-user mode), **When** the user selects multiple images and clicks Import, **Then** the system creates an ImportJob, sends images to OCR, concatenates OCR outputs in upload order, calls the LLM parser, persists the structured recipe and the original images, and displays the parsed recipe in the UI.
2. **Given** OCR returns low-confidence results for some pages, **When** parsing completes, **Then** the recipe is created with confidence metadata and the UI highlights fields with low confidence for manual edit.
3. **Given** an OCR or LLM transient error, **When** it occurs, **Then** the system retries according to retry policy, and if still failing marks the ImportJob as failed with diagnostic details.

---

### User Story 2 - Browse, view and search recipes (Priority: P2)

As a user, I browse and search my saved recipes through the Blazor Server UI. I can open a recipe to see structured fields and the original images that produced it.

**Why this priority**: Makes parsed recipes discoverable and usable after import.

**Independent Test**: Create several recipes (via import or seed data). Use the search box to query by title, ingredient, or tag and verify results include the expected recipes. Open a recipe and confirm images and parsed fields display.

**Acceptance Scenarios**:

1. **Given** recipes exist, **When** the user searches for "chocolate", **Then** recipes containing "chocolate" in title, ingredients, or tags are returned and ranked by relevance.
2. **Given** a recipe, **When** the user opens it, **Then** the UI shows the recipe fields and the original images (with image viewer/thumbnail).

---

### User Story 3 - Manual review and edit after import (Priority: P3)

As a user, after import I want to edit parsed fields (fix ingredient amounts, reorder steps, add tags) and save edits while keeping original images intact.

**Why this priority**: Real OCR/LLM outputs are not perfect; editing ensures correctness and long-term utility.

**Independent Test**: Import a recipe, edit the title/ingredients/steps/tags, save, and verify changes persist and are reflected in search.

**Acceptance Scenarios**:

1. **Given** a parsed recipe, **When** the user edits fields and clicks Save, **Then** the updated structured recipe is persisted and the audit history records the change.

---

### Edge Cases

- Images uploaded out of intended reading order.
- Duplicate or near-duplicate images in a single import.
- Handwritten text or low-quality images resulting in poor OCR confidence.
- Recipes spanning many images (>10) or containing non-recipe content (ads, comments).
- Multilingual recipes (language detection and OCR language hints).
- Partial imports where some images succeed and others fail.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST accept multi-file image uploads in one import action and preserve original upload order unless user reorders before submission.
- **FR-002**: System MUST send images to the configured extraction provider (Azure OpenAI GPT-4o multimodal) and receive structured recipe data with confidence metadata.
- **FR-003**: System MUST process all uploaded images together via the multimodal extraction service, which combines OCR and semantic parsing in a single API call.
- **FR-004**: System MUST persist the parsed recipe (title, ingredients, steps, tags, notes) and keep a referential link to the original image files and the ImportJob record.
- **FR-005**: System MUST present parsed recipes in the Blazor Server UI and via Minimal API endpoints for search and retrieval.
- **FR-006**: All external services (OCR, LLM, storage, DB) MUST be injected via DI and replaceable with alternative implementations.
- **FR-007**: System MUST store images on the local filesystem by default, behind a storage provider interface allowing swapping (e.g., S3).
- **FR-008**: System MUST persist data to SQLite by default, with data access abstracted to enable database replacement.
- **FR-009**: The import workflow MUST implement retries with exponential backoff for transient failures and mark jobs with clear diagnostics if permanently failing.
- **FR-010**: System MUST include confidence metadata for fields derived from OCR/LLM and surface low-confidence items for user review.

### Key Entities

- **Recipe**: title, description/notes, ingredients (ordered list of Ingredient items), steps (ordered list), tags, created/modified timestamps, source ImportJob id, confidence scores.
- **Ingredient**: name, quantity (optional), unit (optional), raw text.
- **Step**: ordinal, text, optional estimated time.
- **ImageAsset**: file path, filename, mime type, size, upload order, OCR raw output reference.
- **ImportJob**: id, user/context, list of ImageAsset ids, status (queued/running/succeeded/failed), timestamps, diagnostics, version of parser used.
- **OCRResult**: per-image raw text, language, per-line bounding boxes (if available), confidence metrics.

## Success Criteria *(mandatory & measurable)*

### Measurable Outcomes

- **SC-001 (Parsing Quality)**: On a test set of 200 representative multi-image recipes, at least 90% must produce a parsed recipe containing a non-empty title, at least three distinct ingredients, and at least one step without manual edits.
- **SC-002 (End-to-End Latency)**: Typical imports of up to 6 images complete (end-to-end, from upload to displayed parsed recipe) within 30 seconds under normal network conditions and when cloud services respond within SLAs.
- **SC-003 (Reliability)**: ImportJob transient failures are retried and at least 99% of imports succeed without operator intervention over a 30-day rolling window in normal conditions.
- **SC-004 (Storage Integrity)**: 100% of persisted recipes must have valid references to their original ImageAssets; image retrieval operations return the original image bytes.
- **SC-005 (Search Relevance)**: For title/ingredient/tag queries on seeded test data, search precision @10 must be >= 0.9 for exact matches.

## Testing Guidance

- Unit tests for `Domain` and `Application` layers covering parsing logic, entity invariants, and ImportJob state transitions.
- Integration tests mocking Azure Vision responses and LLM parsing to validate the import pipeline, including retry logic and diagnostics.
- End-to-end smoke test that uploads sample image sets and verifies UI displays parsed recipe and images.
- Performance test to validate SC-002 with representative network conditions.

## Assumptions

- Primary extraction provider is Azure OpenAI GPT-4o multimodal, which combines OCR and parsing in a single API call.
- Extraction provider is configurable and invoked through an abstraction interface (`IRecipeExtractionService`) that returns structured recipe data.
- The application will run in single-tenant or family-shared mode; user authentication is out of scope for MVP but should be pluggable.
- Typical recipe imports consist of 1–6 images; very large imports (>10 images) are supported but may be slower.

## Open Questions (none required for MVP)

- No critical clarifications required; defaults chosen: Azure Vision OCR, SQLite + local filesystem storage, Blazor Server UI.

```