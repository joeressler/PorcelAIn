#!/usr/bin/dotnet run
#:sdk Microsoft.NET.Sdk.Web
#:package OllamaSharp@3.*
#:property LangVersion=preview
#:property PublishAot=true

using OllamaSharp;
using OllamaSharp.Models;
using System.Text.Json;
using System.Diagnostics;

// Configuration and startup
var builder = WebApplication.CreateSlimBuilder(args);
var config = builder.Configuration;

// Load Ollama configuration
var ollamaUrl = config["Ollama:Url"] ?? "http://localhost:11434";
var modelName = config["Ollama:Model"] ?? "llama3.2";
var maxTokens = config.GetValue<int>("Ollama:MaxTokens", 2048);
var temperature = config.GetValue<float>("Ollama:Temperature", 0.7f);

// Initialize Ollama client
Console.WriteLine($"Connecting to Ollama service at: {ollamaUrl}");
var ollama = new OllamaApiClient(ollamaUrl);

// Verify Ollama service and model availability
try
{
    Console.WriteLine($"Checking Ollama service availability...");
    var models = await ollama.ListLocalModelsAsync();
    
    if (!models.Any(m => m.Name.Contains(modelName)))
    {
        Console.WriteLine($"Warning: Model '{modelName}' not found locally. Available models:");
        foreach (var availableModel in models)
        {
            Console.WriteLine($"  - {availableModel.Name}");
        }
        Console.WriteLine($"Attempting to pull model '{modelName}'...");
        await ollama.PullModelAsync(modelName);
    }
    
    Console.WriteLine($"Model '{modelName}' is ready!");
}
catch (Exception ex)
{
    Console.WriteLine($"Failed to connect to Ollama service: {ex.Message}");
    Console.WriteLine($"Make sure Ollama is running at {ollamaUrl}");
    Environment.Exit(1);
}

var app = builder.Build();

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

// API Endpoints
app.MapPost("/generate", async (GenerateRequest request) =>
{
    try
    {
        var stopwatch = Stopwatch.StartNew();
        var ollamaRequest = new OllamaSharp.Models.GenerateRequest
        {
            Model = modelName,
            Prompt = request.Prompt,
            Options = new RequestOptions
            {
                Temperature = request.Temperature,
                TopP = request.TopP,
                NumPredict = request.MaxTokens,
                Stop = request.StopSequences
            }
        };

        var response = await ollama.GenerateAsync(ollamaRequest);
        stopwatch.Stop();
        
        // Approximate token count (OllamaSharp doesn't provide exact token count)
        var estimatedTokens = response.Response?.Split(' ', StringSplitOptions.RemoveEmptyEntries)?.Length ?? 0;
        
        return Results.Ok(new GenerateResponse(
            Text: response.Response ?? "",
            TokensGenerated: estimatedTokens,
            ProcessingTimeMs: (float)stopwatch.Elapsed.TotalMilliseconds
        ));
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Generation error: {ex.Message}");
        return Results.Problem($"Text generation failed: {ex.Message}", statusCode: 500);
    }
});

app.MapGet("/health", async () =>
{
    try
    {
        // Test Ollama service connectivity
        var models = await ollama.ListLocalModelsAsync();
        var modelExists = models.Any(m => m.Name.Contains(modelName));
        
        return Results.Ok(new
        {
            status = modelExists ? "healthy" : "model_not_found",
            service = "ollama",
            serviceUrl = ollamaUrl,
            model = modelName,
            modelAvailable = modelExists
        });
    }
    catch (Exception ex)
    {
        return Results.Ok(new
        {
            status = "unhealthy",
            service = "ollama",
            serviceUrl = ollamaUrl,
            error = ex.Message
        });
    }
});

app.MapGet("/info", async () =>
{
    try
    {
        var models = await ollama.ListLocalModelsAsync();
        var currentModel = models.FirstOrDefault(m => m.Name.Contains(modelName));
        
        return Results.Ok(new
        {
            service = "ollama",
            serviceUrl = ollamaUrl,
            model = modelName,
            maxTokens = maxTokens,
            temperature = temperature,
            modelSizeMB = currentModel?.Size / (1024 * 1024) ?? 0,
            modelFamily = currentModel?.Details?.Family ?? "unknown",
            availableModels = models.Select(m => m.Name).ToArray()
        });
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
    ollama?.Dispose();
    Console.WriteLine("Resources cleaned up. Goodbye!");
}
