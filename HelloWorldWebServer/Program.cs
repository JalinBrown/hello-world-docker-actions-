using System.Text.Json;
using OpenTelemetry.Trace;
using OpenTelemetry.Resources;
using CS397.Trace; 

class Program
{
    // GET /hello?message=Hi
    private static async Task HelloWorldDelegate(HttpContext context)
    {
        var msg = context.Request.Query["message"].FirstOrDefault();
        Console.WriteLine($"Hello endpoint called with message: {msg ?? "(none)"}");
        await context.Response.WriteAsync("Hello World!!!");
    }

    // GET /goodbye
    private static async Task GoodbyeWorldDelegate(HttpContext context)
    {
        Console.WriteLine("Goodbye endpoint called");
        await context.Response.WriteAsync("Goodbye World! :)");
    }

    // POST /hellojson   body: { "message": "Hello to you" }
    private static async Task HelloJsonDelegate(HttpContext context)
    {
        using var reader = new StreamReader(context.Request.Body);
        var body = await reader.ReadToEndAsync();

        string? message = null;
        try
        {
            var req = JsonSerializer.Deserialize<HelloRequest?>(body);
            message = req?.Message;
        }
        catch { /* ignore parse errors */ }

        Console.WriteLine($"HelloJson endpoint called with Message: {message ?? "(none)"}");

        context.Response.ContentType = "application/json";
        await context.Response.WriteAsync(JsonSerializer.Serialize(new { Echo = message }));
    }

    static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // ship logs to stdout (Container Apps captures these)
        builder.Logging.ClearProviders();
        builder.Logging.AddConsole();

        // Enable OpenTelemetry + your CS397 JSON console exporter
        builder.Services.AddOpenTelemetry()
            .WithTracing(tp => tp
                .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("HelloWorldWebServer"))
                .AddAspNetCoreInstrumentation()
                .AddJsonConsoleExporter()); // <- CS397 helper extension

        var app = builder.Build();

        app.MapGet("/hello", HelloWorldDelegate);
        app.MapGet("/goodbye", GoodbyeWorldDelegate);
        app.MapPost("/hellojson", HelloJsonDelegate);

        app.Run();
    }

    private record HelloRequest(string? Message);
}
