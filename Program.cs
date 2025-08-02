#!/usr/bin/dotnet run
#:sdk Microsoft.NET.Sdk.Web
#:package Microsoft.ML.OnnxRuntimeGenAI@0.*
#:package Microsoft.ML.OnnxRuntimeGenAI.DirectML@0.*
#:package Microsoft.ML.OnnxRuntimeGenAI.CUDA@0.*
#:property LangVersion=preview
#:property PublishAot=false

using Microsoft.ML.OnnxRuntimeGenAI;
using System.Text.Json;
using System.Diagnostics;
using System.Collections.Generic;
using System.Text;

// Configuration and startup
var builder = WebApplication.CreateSlimBuilder(args);
var config = builder.Configuration;

// Load model configuration - point to the Phi-3 model directory
var modelDir = config["Model:Path"] ?? "./Models/cpu_and_mobile/cpu-int4-rtn-block-32-acc-level-4";
var maxTokens = config.GetValue<int>("Model:MaxTokens", 2048);
var temperature = config.GetValue<float>("Model:Temperature", 0.8f);

// Validate model directory exists
if (!Directory.Exists(modelDir))
{
    Console.WriteLine($"Error: Model directory not found at {modelDir}");
    Environment.Exit(1);
}

// Validate required model files exist
var modelFile = Path.Combine(modelDir, "model.onnx");
if (!File.Exists(modelFile))
{
    Console.WriteLine($"Error: Model file not found at {modelFile}");
    Environment.Exit(1);
}

Model? model = null;
Tokenizer? tokenizer = null;
OgaHandle? ogaHandle = null;

try
{
    Console.WriteLine($"Loading Phi-3 model from: {modelDir}");
    var sw = Stopwatch.StartNew();
    
    ogaHandle = new OgaHandle();
    
    using var modelConfig = new Config(modelDir);
    // Don't clear providers - use default CPU provider
    
    model = new Model(modelConfig);
    tokenizer = new Tokenizer(model);
    
    sw.Stop();
    Console.WriteLine($"Phi-3 model loaded successfully in {sw.ElapsedMilliseconds} ms!");
}
catch (Exception ex)
{
    Console.WriteLine($"Failed to load Phi-3 model: {ex.Message}");
    Environment.Exit(1);
}

var app = builder.Build();

// API Endpoints
app.MapPost("/generate", (GenerateRequest request) =>
{
    try
    {
        var stopwatch = Stopwatch.StartNew();
        
        // Format prompt for Phi-3 model
        var systemPrompt = "You are a helpful assistant.";
        var formattedPrompt = $"<|system|>{systemPrompt}<|end|><|user|>{request.Prompt}<|end|><|assistant|>";
        
        // Encode the prompt using the exact pattern from the official example
        var sequences = tokenizer!.Encode(formattedPrompt);
        
        // Create generator parameters using the official pattern
        using var generatorParams = new GeneratorParams(model!);
        generatorParams.SetSearchOption("min_length", 10);
        generatorParams.SetSearchOption("max_length", request.MaxTokens);
        
        // Generate text using the official streaming pattern
        using var tokenizerStream = tokenizer.CreateStream();
        using var generator = new Generator(model, generatorParams);
        generator.AppendTokenSequences(sequences);
        
        var generatedText = new StringBuilder();
        var tokensGenerated = 0;
        
        while (!generator.IsDone() && tokensGenerated < request.MaxTokens)
        {
            generator.GenerateNextToken();
            var newToken = tokenizerStream.Decode(generator.GetSequence(0)[^1]);
            generatedText.Append(newToken);
            tokensGenerated++;
            
            // Check for stop sequences
            var currentOutput = generatedText.ToString();
            if (request.StopSequences?.Any(stop => currentOutput.Contains(stop)) == true ||
                currentOutput.Contains("<|end|>") || 
                currentOutput.Contains("<|user|>") ||
                currentOutput.Contains("<|system|>"))
            {
                break;
            }
        }
        
        var outputText = generatedText.ToString();
        
        stopwatch.Stop();
        
        // Clean up the generated text (remove any special tokens that leaked through)
        var cleanedText = outputText
            .Replace("<|end|>", "")
            .Replace("<|user|>", "")
            .Replace("<|system|>", "")
            .Trim();
        
        return Results.Ok(new GenerateResponse(
            Text: cleanedText,
            TokensGenerated: tokensGenerated,
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
    var isHealthy = model != null && tokenizer != null;
    return Results.Ok(new
    {
        status = isHealthy ? "healthy" : "unhealthy",
        model = "phi3",
        modelPath = modelDir
    });
});

app.MapGet("/info", () =>
{
    try
    {
        var modelFile = Path.Combine(modelDir, "model.onnx");
        var fileInfo = new FileInfo(modelFile);
        return Results.Ok(new
        {
            type = "phi3",
            path = modelDir,
            maxTokens = maxTokens,
            contextWindow = 32768, // Phi-3 context window
            modelSizeMB = fileInfo.Length / (1024 * 1024),
            temperature = temperature
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
    model?.Dispose();
    tokenizer?.Dispose();
    ogaHandle?.Dispose();
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
