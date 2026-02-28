# ScreenShotRecipe

Minimal multi-project .NET 9 Clean Architecture scaffold for importing recipes from multiple images.

Quick start (build locally):

```bash
# Build and run (requires .NET 9 SDK)
dotnet build
dotnet run --project src/ScreenShotRecipe.Web
```

Docker (recommended for self-hosting inside LXC on Proxmox):

```bash
docker build -t screenshotrecipe:latest .
docker run -v $(pwd)/data:/data -p 8080:80 screenshotrecipe:latest
```

Notes:
- Production extraction uses Azure OpenAI GPT-4o multimodal for combined OCR and recipe parsing.
- A fake extraction service is available for development without API keys (set `ServiceImplementation:UseRealExtractionService` to `false`).
- SQLite database file and images are stored under `./data` by default when running via Docker with the provided compose override.
