#nullable enable
using System.Text.Json;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using CS397.Trace; // AddJsonConsoleExporter()

class Program
{
    // ---- service identity (keep your naming) ----
    const string serviceName = "HelloWorldWebServer";
    const string serviceVersion = "1.0.0";

    private readonly Tracer _tracer;

    // Common attribute keys
    private const string AttrRoute = "http.route";
    private const string AttrMsg   = "app.message";

    public Program()
    {
        _tracer = TracerProvider.Default.GetTracer(serviceName);
    }

    // GET /hello?message=Hi
    private async Task HelloWorldDelegate(HttpContext context)
    {
        using var span = _tracer.StartActiveSpan("GET /hello", SpanKind.Server);
        span.SetAttribute(AttrRoute, "/hello");
        span.AddEvent("handler.start");

        var msg = context.Request.Query["message"].FirstOrDefault();
        if (!string.IsNullOrEmpty(msg))
        {
            span.SetAttribute(AttrMsg, msg);
            span.AddEvent("query.message.present");
        }
        else
        {
            span.AddEvent("query.message.absent");
        }

        Console.WriteLine($"Hello endpoint called with message: {msg ?? "(none)"}");
        await context.Response.WriteAsync("Hello World!!!");

        span.AddEvent("response.sent");
    }

    // GET /goodbye
    private async Task GoodbyeWorldDelegate(HttpContext context)
    {
        using var span = _tracer.StartActiveSpan("GET /goodbye", SpanKind.Server);
        span.SetAttribute(AttrRoute, "/goodbye");
        span.AddEvent("handler.start");

        Console.WriteLine("Goodbye endpoint called");
        await context.Response.WriteAsync("Goodbye World! :)");

        span.AddEvent("response.sent");
    }

    // POST /hellojson   body: { "message": "Hello to you" }
    private async Task HelloJsonDelegate(HttpContext context)
    {
        using var span = _tracer.StartActiveSpan("POST /hellojson", SpanKind.Server);
        span.SetAttribute(AttrRoute, "/hellojson");
        span.AddEvent("payload.read.start");

        using var reader = new StreamReader(context.Request.Body);
        var body = await reader.ReadToEndAsync();

        span.SetAttribute("app.body.length", body?.Length ?? 0);
        span.AddEvent("payload.read.complete");

        string? message = null;
        try
        {
            var req = JsonSerializer.Deserialize<HelloRequest?>(body);
            message = req?.Message;

            if (!string.IsNullOrEmpty(message))
            {
                span.SetAttribute(AttrMsg, message);
                span.AddEvent("message.parsed.success");
            }
            else
            {
                span.AddEvent("message.parsed.empty");
            }
        }
        catch (Exception ex)
        {
            span.SetAttribute("parse.error", true);
            span.RecordException(ex);
            span.SetStatus(Status.Error);
            span.AddEvent("json.deserialize.failed");
        }

        Console.WriteLine($"HelloJson endpoint called with Message: {message ?? "(none)"}");

        context.Response.ContentType = "application/json";
        await context.Response.WriteAsync(JsonSerializer.Serialize(new { Echo = message }));

        span.AddEvent("response.sent");
    }

    static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Console logs (captured by Azure)
        builder.Logging.ClearProviders();
        builder.Logging.AddConsole();

        // OTEL + CS397 JSON console exporter, per guide
        builder.Services.AddOpenTelemetry().WithTracing(tcb =>
        {
            tcb
                .AddSource(serviceName)
                .SetResourceBuilder(
                    ResourceBuilder.CreateDefault()
                        .AddService(serviceName: serviceName, serviceVersion: serviceVersion))
                .AddAspNetCoreInstrumentation()
                .AddJsonConsoleExporter(); // from CS397
        });

        var instance = new Program();

        var app = builder.Build();

        // non-static handlers
        app.MapGet("/hello", instance.HelloWorldDelegate);
        app.MapGet("/goodbye", instance.GoodbyeWorldDelegate);
        app.MapPost("/hellojson", instance.HelloJsonDelegate);

        app.Run();
    }

    private record HelloRequest(string? Message);
}
