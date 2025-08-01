#!/usr/bin/dotnet run
#:sdk Microsoft.NET.Sdk.Web
#:package LLamaSharp@0.*
#:package LLamaSharp.Backend.Cpu@0.*
#:property LangVersion=preview
#:property PublishAot=true

using LLama.Common;
using LLama;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Diagnostics;

// Configuration and startup
var builder = WebApplication.CreateSlimBuilder(args);

// Configure JSON serialization for AOT
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.TypeInfoResolverChain.Insert(0, AppJsonSerializerContext.Default);
});
var config = builder.Configuration;

// Load model configuration
var modelPath = config["Model:Path"] ?? "./Models/model.gguf";
var modelType = config["Model:Type"]?.ToLower() ?? "gguf";
var maxTokens = config.GetValue<int>("Model:MaxTokens", 2048);
var temperature = config.GetValue<float>("Model:Temperature", 0.7f);

// Validate model file exists
if (!File.Exists(modelPath))
{
    Console.WriteLine($"Error: Model file not found at {modelPath}");
    Environment.Exit(1);
}

// Initialize model provider
Console.WriteLine($"Loading {modelType} model from: {modelPath}");
var modelParams = new ModelParams(modelPath)
{
    ContextSize = (uint)maxTokens,
    GpuLayerCount = 0 // CPU only for maximum compatibility
};

LLamaWeights? model = null;
LLamaContext? context = null;
InteractiveExecutor? executor = null;

try
{
    model = LLamaWeights.LoadFromFile(modelParams);
    context = model.CreateContext(modelParams);
    executor = new InteractiveExecutor(context);
    Console.WriteLine("Model loaded successfully!");
}
catch (Exception ex)
{
    Console.WriteLine($"Failed to load model: {ex.Message}");
    Environment.Exit(1);
}

var app = builder.Build();

// API Endpoints
app.MapPost("/generate", async (GenerateRequest request) =>
{
    try
    {
        var stopwatch = Stopwatch.StartNew();
        var inferenceParams = new InferenceParams()
        {
            MaxTokens = request.MaxTokens,
            AntiPrompts = request.StopSequences?.ToList() ?? new()
        };

        var response = new List<string>();
        await foreach (var token in executor!.InferAsync(request.Prompt, inferenceParams))
        {
            response.Add(token);
        }

        stopwatch.Stop();
        var generatedText = string.Join("", response);
        
        return Results.Ok(new GenerateResponse(
            Text: generatedText,
            TokensGenerated: response.Count,
            ProcessingTimeMs: (float)stopwatch.Elapsed.TotalMilliseconds
        ));
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Generation error: {ex.Message}");
        return Results.Problem($"Text generation failed: {ex.Message}", statusCode: 500);
    }
});

app.MapGet("/health", () =>
{
    var isHealthy = model != null && context != null && executor != null;
    return Results.Ok(new HealthResponse(
        Status: isHealthy ? "healthy" : "unhealthy",
        Model: modelType,
        ModelPath: modelPath
    ));
});

app.MapGet("/info", () =>
{
    try
    {
        var fileInfo = new FileInfo(modelPath);
        return Results.Ok(new ModelInfoResponse(
            Type: modelType,
            Path: modelPath,
            MaxTokens: maxTokens,
            ContextWindow: maxTokens,
            ModelSizeMB: fileInfo.Length / (1024 * 1024),
            Temperature: temperature
        ));
    }
    catch (Exception ex)
    {
        return Results.Problem($"Failed to get model info: {ex.Message}", statusCode: 500);
    }
});

// Graceful shutdown
var cancellationTokenSource = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cancellationTokenSource.Cancel();
};

Console.WriteLine($"PorcelAIn REST API starting on http://localhost:5000");
Console.WriteLine("Available endpoints:");
Console.WriteLine("  POST /generate - Generate text from prompt");
Console.WriteLine("  GET  /health   - Check API status");
Console.WriteLine("  GET  /info     - Get model information");
Console.WriteLine("Press Ctrl+C to shutdown");

try
{
    await app.RunAsync(cancellationTokenSource.Token);
}
finally
{
    // Cleanup resources
    // Note: InteractiveExecutor doesn't implement IDisposable
    context?.Dispose();
    model?.Dispose();
    Console.WriteLine("Resources cleaned up. Goodbye!");
}

// Request/Response models
record GenerateRequest(
    string Prompt,
    int MaxTokens = 100,
    float Temperature = 0.7f,
    float TopP = 0.9f,
    string[]? StopSequences = null
);

record GenerateResponse(
    string Text,
    int TokensGenerated,
    float ProcessingTimeMs
);

record HealthResponse(
    string Status,
    string Model,
    string ModelPath
);

record ModelInfoResponse(
    string Type,
    string Path,
    int MaxTokens,
    int ContextWindow,
    long ModelSizeMB,
    float Temperature
);

// JSON serialization context for AOT
[JsonSerializable(typeof(GenerateRequest))]
[JsonSerializable(typeof(GenerateResponse))]
[JsonSerializable(typeof(HealthResponse))]
[JsonSerializable(typeof(ModelInfoResponse))]
internal partial class AppJsonSerializerContext : JsonSerializerContext
{
}
