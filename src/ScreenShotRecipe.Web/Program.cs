using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ScreenShotRecipe.Application.Services;
using ScreenShotRecipe.Domain.Interfaces;
using ScreenShotRecipe.Infrastructure.Ocr;
using ScreenShotRecipe.Infrastructure.Parsing;
using ScreenShotRecipe.Infrastructure.Repositories;
using ScreenShotRecipe.Infrastructure.Storage;
using ScreenShotRecipe.Infrastructure.Persistence;
using ScreenShotRecipe.Application.Contracts.Dtos;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Configure services
builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseSqlite("Data Source=app.db"));

builder.Services.AddScoped<IRecipeRepository, RecipeRepository>();
builder.Services.AddSingleton<IStorage>(_ => new FileSystemStorage("./data/images"));

// Use fake implementations by default, switch to Azure if USE_REAL_SERVICES=true
var useRealServices = builder.Configuration.GetValue<bool>("USE_REAL_SERVICES");
if (useRealServices)
{
    builder.Services.AddSingleton<IOcrClient, AzureVisionOcrClient>();
    builder.Services.AddSingleton<ILLMParser, AzureOpenAIParser>();
}
else
{
    builder.Services.AddSingleton<IOcrClient, FakeOcrClient>();
    builder.Services.AddSingleton<ILLMParser, FakeLLMParser>();
}

builder.Services.AddScoped<ImportService>();
builder.Services.AddHttpClient();

builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();

var app = builder.Build();

// Initialize database
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
}

app.UseStaticFiles();
app.UseRouting();

app.MapPost("/api/import", async (HttpRequest req, ImportService importService) =>
{
    if (!req.HasFormContentType) return Results.BadRequest("Expected form-data");

    var form = await req.ReadFormAsync();
    var files = form.Files;
    var list = new System.Collections.Generic.List<(byte[] Bytes, string FileName)>();
    foreach (var file in files)
    {
        using var ms = new System.IO.MemoryStream();
        await file.CopyToAsync(ms);
        list.Add((ms.ToArray(), file.FileName));
    }

    var dto = await importService.ImportImagesAsync(list);
    return Results.Ok(dto);
});

app.MapGet("/api/recipes", async (IRecipeRepository repo) =>
{
    var recipes = await repo.GetAllAsync();
    var dtos = recipes.Select(r => new RecipeDto
    {
        Id = r.Id,
        Title = r.Title,
        Notes = r.Notes,
        Ingredients = r.Ingredients.Select(i => new IngredientDto
        {
            RawText = i.RawText,
            Name = i.Name,
            Quantity = i.Quantity,
            Unit = i.Unit
        }).ToList(),
        Steps = r.Steps.Select(s => new StepDto
        {
            Ordinal = s.Ordinal,
            Text = s.Text
        }).ToList(),
        Tags = r.Tags.ToList()
    }).ToList();
    return Results.Ok(dtos);
});

app.MapGet("/api/recipes/{id:guid}", async (Guid id, IRecipeRepository repo) =>
{
    var recipe = await repo.GetAsync(id);
    if (recipe == null)
        return Results.NotFound();

    var dto = new RecipeDto
    {
        Id = recipe.Id,
        Title = recipe.Title,
        Notes = recipe.Notes,
        Ingredients = recipe.Ingredients.Select(i => new IngredientDto
        {
            RawText = i.RawText,
            Name = i.Name,
            Quantity = i.Quantity,
            Unit = i.Unit
        }).ToList(),
        Steps = recipe.Steps.Select(s => new StepDto
        {
            Ordinal = s.Ordinal,
            Text = s.Text
        }).ToList(),
        Tags = recipe.Tags.ToList()
    };
    return Results.Ok(dto);
});

app.MapGet("/", () => Results.Redirect("/recipes"));

app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

app.Run();
