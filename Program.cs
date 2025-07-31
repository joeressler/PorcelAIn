#!/usr/bin/dotnet run
#:sdk Microsoft.NET.Sdk.Web
#:package Microsoft.ML.OnnxRuntime@1.*
#:package Microsoft.ML.Tokenizers@0.*
#:property LangVersion=preview
#:property PublishAot=true

using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.Tokenizers;
using System.Text.Json;
using System.Diagnostics;
using System.Collections.Generic;

// Configuration and startup
var builder = WebApplication.CreateSlimBuilder(args);
var config = builder.Configuration;

// Load model configuration
var modelPath = config["Model:Path"] ?? "./Models/model.onnx";
var modelType = config["Model:Type"]?.ToLower() ?? "onnx";
var tokenizerPath = config["Model:TokenizerPath"] ?? "./Models/tokenizer.json";
var maxTokens = config.GetValue<int>("Model:MaxTokens", 2048);
var temperature = config.GetValue<float>("Model:Temperature", 0.7f);

// Validate model file exists
if (!File.Exists(modelPath))
{
    Console.WriteLine($"Error: Model file not found at {modelPath}");
    Environment.Exit(1);
}

// Initialize ONNX session options
var sessionOptions = new SessionOptions
{
    ExecutionMode = ExecutionMode.ORT_SEQUENTIAL,
    GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL
};

InferenceSession? onnxSession = null;
Tokenizer? tokenizer = null;

try
{
    Console.WriteLine($"Loading {modelType} model from: {modelPath}");
    onnxSession = new InferenceSession(modelPath, sessionOptions);
    
    // Load tokenizer if available
    if (File.Exists(tokenizerPath))
    {
        Console.WriteLine($"Loading tokenizer from: {tokenizerPath}");
        tokenizer = Tokenizer.CreateTiktokenForModel("gpt-4"); // Fallback tokenizer
    }
    else
    {
        Console.WriteLine("Using default tokenizer");
        tokenizer = Tokenizer.CreateTiktokenForModel("gpt-4");
    }
    
    Console.WriteLine("ONNX model loaded successfully!");
}
catch (Exception ex)
{
    Console.WriteLine($"Failed to load ONNX model: {ex.Message}");
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
        
        // Tokenize input prompt
        var encodedTokens = tokenizer!.EncodeToIds(request.Prompt);
        var inputIds = encodedTokens.ToArray();
        
        // Prepare input tensors
        var inputTensor = NamedOnnxValue.CreateFromTensor("input_ids", 
            new DenseTensor<long>(inputIds.Select(x => (long)x).ToArray(), new[] { 1, inputIds.Length }));
        
        var inputs = new List<NamedOnnxValue> { inputTensor };
        
        // Generate tokens
        var generatedTokens = new List<int>(inputIds);
        var tokensGenerated = 0;
        
        for (int i = 0; i < request.MaxTokens && tokensGenerated < request.MaxTokens; i++)
        {
            // Run inference
            using var results = onnxSession!.Run(inputs);
            var logits = results.First().AsTensor<float>();
            
            // Apply temperature sampling
            var nextTokenId = SampleWithTemperature(logits, request.Temperature);
            generatedTokens.Add(nextTokenId);
            tokensGenerated++;
            
            // Check for stop sequences
            var currentText = tokenizer.Decode(generatedTokens.Skip(inputIds.Length).ToArray());
            if (request.StopSequences?.Any(stop => currentText.Contains(stop)) == true)
                break;
            
            // Update input for next iteration (sliding window approach)
            var newInputIds = generatedTokens.TakeLast(Math.Min(generatedTokens.Count, 512)).ToArray();
            inputTensor = NamedOnnxValue.CreateFromTensor("input_ids",
                new DenseTensor<long>(newInputIds.Select(x => (long)x).ToArray(), new[] { 1, newInputIds.Length }));
            inputs[0] = inputTensor;
        }
        
        stopwatch.Stop();
        
        // Decode only the generated portion
        var generatedText = tokenizer.Decode(generatedTokens.Skip(inputIds.Length).ToArray());
        
        return Results.Ok(new GenerateResponse(
            Text: generatedText,
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

// Helper method for temperature sampling
static int SampleWithTemperature(Tensor<float> logits, float temperature)
{
    var lastTokenLogits = logits.GetSlice(new[] { 0L, logits.Dimensions[1] - 1 }).ToArray();
    
    if (temperature <= 0.0f)
    {
        // Greedy sampling - return token with highest probability
        return Array.IndexOf(lastTokenLogits, lastTokenLogits.Max());
    }
    
    // Apply temperature
    for (int i = 0; i < lastTokenLogits.Length; i++)
    {
        lastTokenLogits[i] /= temperature;
    }
    
    // Softmax
    var maxLogit = lastTokenLogits.Max();
    var expLogits = lastTokenLogits.Select(x => Math.Exp(x - maxLogit)).ToArray();
    var sumExp = expLogits.Sum();
    var probabilities = expLogits.Select(x => x / sumExp).ToArray();
    
    // Sample from probability distribution
    var random = new Random();
    var sample = random.NextDouble();
    var cumulative = 0.0;
    
    for (int i = 0; i < probabilities.Length; i++)
    {
        cumulative += probabilities[i];
        if (sample < cumulative) return i;
    }
    
    return probabilities.Length - 1;
}

app.MapGet("/health", () =>
{
    var isHealthy = onnxSession != null && tokenizer != null;
    return Results.Ok(new
    {
        status = isHealthy ? "healthy" : "unhealthy",
        model = modelType,
        modelPath = modelPath
    });
});

app.MapGet("/info", () =>
{
    try
    {
        var fileInfo = new FileInfo(modelPath);
        return Results.Ok(new
        {
            type = modelType,
            path = modelPath,
            maxTokens = maxTokens,
            contextWindow = maxTokens,
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
    onnxSession?.Dispose();
    tokenizer?.Dispose();
    Console.WriteLine("Resources cleaned up. Goodbye!");
}
