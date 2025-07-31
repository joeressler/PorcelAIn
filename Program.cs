#!/usr/bin/dotnet run
#:sdk Microsoft.NET.Sdk.Web
#:package TorchSharp@0.*
#:package TorchSharp.libtorch-cpu@0.*
#:property LangVersion=preview
#:property PublishAot=true

using TorchSharp;
using static TorchSharp.torch;
using System.Text.Json;
using System.Diagnostics;

// Configuration and startup
var builder = WebApplication.CreateSlimBuilder(args);
var config = builder.Configuration;

// Load model configuration
var modelPath = config["Model:Path"] ?? "./Models/model.pt";
var modelType = config["Model:Type"]?.ToLower() ?? "pytorch";
var maxTokens = config.GetValue<int>("Model:MaxTokens", 2048);
var temperature = config.GetValue<float>("Model:Temperature", 0.7f);
var vocabPath = config["Model:VocabPath"] ?? "./Models/vocab.json";

// Validate model file exists
if (!File.Exists(modelPath))
{
    Console.WriteLine($"Error: Model file not found at {modelPath}");
    Environment.Exit(1);
}

// Initialize TorchSharp
Console.WriteLine($"Initializing TorchSharp with device: {(torch.cuda.is_available() ? "CUDA" : "CPU")}");
var device = torch.cuda.is_available() ? torch.CUDA : torch.CPU;

// Load PyTorch model
Console.WriteLine($"Loading {modelType} model from: {modelPath}");
torch.jit.ScriptModule? model = null;
Dictionary<string, int>? tokenizer = null;
Dictionary<int, string>? reverseTokenizer = null;

try
{
    // Load the PyTorch model
    model = torch.jit.load(modelPath, device);
    model.eval(); // Set to evaluation mode
    
    // Load tokenizer (assuming a simple JSON vocab file)
    if (File.Exists(vocabPath))
    {
        var vocabJson = await File.ReadAllTextAsync(vocabPath);
        tokenizer = JsonSerializer.Deserialize<Dictionary<string, int>>(vocabJson);
        reverseTokenizer = tokenizer?.ToDictionary(kvp => kvp.Value, kvp => kvp.Key);
    }
    
    Console.WriteLine("Model loaded successfully!");
}
catch (Exception ex)
{
    Console.WriteLine($"Failed to load model: {ex.Message}");
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

// Helper functions for tokenization
long[] TokenizeText(string text, Dictionary<string, int>? tokenizer)
{
    if (tokenizer == null) 
    {
        // Simple character-level tokenization as fallback
        return text.Select(c => (long)c).ToArray();
    }
    
    // Simple word-level tokenization (in real scenarios, use proper tokenizer like tiktoken)
    var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
    var tokens = new List<long>();
    
    foreach (var word in words)
    {
        if (tokenizer.TryGetValue(word, out var tokenId))
        {
            tokens.Add(tokenId);
        }
        else if (tokenizer.TryGetValue("<unk>", out var unkId))
        {
            tokens.Add(unkId);
        }
        else
        {
            tokens.Add(0); // Default unknown token
        }
    }
    
    return tokens.ToArray();
}

string DetokenizeText(long[] tokens, Dictionary<int, string>? reverseTokenizer)
{
    if (reverseTokenizer == null)
    {
        // Simple character-level detokenization as fallback
        return new string(tokens.Select(t => (char)t).ToArray());
    }
    
    var words = tokens.Select(t => reverseTokenizer.TryGetValue((int)t, out var word) ? word : "<unk>").ToArray();
    return string.Join(" ", words);
}

// API Endpoints
app.MapPost("/generate", async (GenerateRequest request) =>
{
    try
    {
        var stopwatch = Stopwatch.StartNew();
        
        // Tokenize input
        var inputTokens = TokenizeText(request.Prompt, tokenizer);
        var inputTensor = torch.tensor(inputTokens, dtype: torch.int64, device: device).unsqueeze(0);
        
        var generatedTokens = new List<long>(inputTokens);
        var maxNewTokens = Math.Min(request.MaxTokens, maxTokens - inputTokens.Length);
        
        // Generate tokens one by one
        using (torch.no_grad())
        {
            for (int i = 0; i < maxNewTokens; i++)
            {
                // Forward pass through the model
                var currentInput = torch.tensor(generatedTokens.ToArray(), dtype: torch.int64, device: device).unsqueeze(0);
                var outputs = model!.forward(currentInput);
                
                // Get logits for the last token
                var logits = outputs.ToTensor();
                var lastTokenLogits = logits[0, -1, ..]; // Shape: [vocab_size]
                
                // Apply temperature
                if (request.Temperature > 0)
                {
                    lastTokenLogits = lastTokenLogits / request.Temperature;
                }
                
                // Apply top-p sampling if specified
                long nextToken;
                if (request.TopP < 1.0f)
                {
                    var probabilities = torch.softmax(lastTokenLogits, dim: 0);
                    var sortedProbs = torch.sort(probabilities, descending: true);
                    var cumulativeProbs = torch.cumsum(sortedProbs.values, dim: 0);
                    
                    // Find cutoff for top-p
                    var mask = cumulativeProbs <= request.TopP;
                    var topPIndices = sortedProbs.indices[mask];
                    
                    if (topPIndices.numel() > 0)
                    {
                        var sampledIndex = torch.multinomial(probabilities[topPIndices], 1);
                        nextToken = topPIndices[sampledIndex].item<long>();
                    }
                    else
                    {
                        nextToken = torch.argmax(probabilities).item<long>();
                    }
                }
                else
                {
                    var probabilities = torch.softmax(lastTokenLogits, dim: 0);
                    nextToken = torch.multinomial(probabilities, 1).item<long>();
                }
                
                generatedTokens.Add(nextToken);
                
                // Check for stop sequences
                if (request.StopSequences?.Any() == true)
                {
                    var currentText = DetokenizeText(generatedTokens.Skip(inputTokens.Length).ToArray(), reverseTokenizer);
                    if (request.StopSequences.Any(stop => currentText.Contains(stop)))
                    {
                        break;
                    }
                }
                
                // Break on EOS token (assuming token 2 is EOS, adjust as needed)
                if (nextToken == 2)
                {
                    break;
                }
            }
        }
        
        stopwatch.Stop();
        var newTokensOnly = generatedTokens.Skip(inputTokens.Length).ToArray();
        var generatedText = DetokenizeText(newTokensOnly, reverseTokenizer);
        
        return Results.Ok(new GenerateResponse(
            Text: generatedText,
            TokensGenerated: newTokensOnly.Length,
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
        model = modelType,
        modelPath = modelPath,
        device = device.ToString(),
        tokenizerLoaded = tokenizer != null
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
            vocabPath = vocabPath,
            maxTokens = maxTokens,
            contextWindow = maxTokens,
            modelSizeMB = fileInfo.Length / (1024 * 1024),
            temperature = temperature,
            device = device.ToString(),
            torchVersion = torch.version,
            cudaAvailable = torch.cuda.is_available(),
            vocabSize = tokenizer?.Count ?? 0
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
    // Cleanup TorchSharp resources
    model?.Dispose();
    torch.DisposeTorch();
    Console.WriteLine("TorchSharp resources cleaned up. Goodbye!");
}
