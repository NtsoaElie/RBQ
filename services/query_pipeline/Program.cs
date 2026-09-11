using System.Net.Http.Headers;
using Rbq.QueryApi;

DotEnv.Load(Path.Combine(AppContext.BaseDirectory, "../../../../../.env"));

var builder = WebApplication.CreateBuilder(args);
var webOrigin = DotEnv.Get("WEB_ORIGIN", "http://localhost:5173");

builder.Services.AddCors(options => options.AddDefaultPolicy(policy => policy
    .WithOrigins(webOrigin)
    .AllowAnyHeader()
    .AllowAnyMethod()));

builder.Services.AddHttpClient<OpenAiClient>(client =>
{
    client.BaseAddress = new Uri("https://api.openai.com/v1/");
    client.DefaultRequestHeaders.Authorization =
        new AuthenticationHeaderValue("Bearer", DotEnv.Require("OPEN_API_KEY"));
    client.Timeout = TimeSpan.FromSeconds(60);
});

builder.Services.AddHttpClient<SupabaseClient>(client =>
{
    var key = DotEnv.Require("SUPABASE_PRIVATE_KEY");
    client.BaseAddress = new Uri(DotEnv.Require("SUPABASE_URL").TrimEnd('/') + "/");
    client.DefaultRequestHeaders.Add("apikey", key);
    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", key);
    client.Timeout = TimeSpan.FromSeconds(30);
});

builder.Services.AddScoped<RagService>();

var app = builder.Build();
app.UseCors();

app.MapGet("/api/health", () => Results.Ok(new { status = "ok" }));

app.MapPost("/api/ask", async (AskRequest request, RagService rag, ILoggerFactory logs, CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(request.Question))
        return Results.BadRequest(new { error = "Question is required." });

    try
    {
        return Results.Ok(await rag.AskAsync(request, ct));
    }
    catch (HttpRequestException ex)
    {
        logs.CreateLogger("ask").LogError(ex, "Query pipeline failed.");
        return Results.Json(new { error = "The query pipeline is unavailable. Try again." }, statusCode: 502);
    }
});

app.Run();
