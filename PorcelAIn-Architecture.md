# PorcelAIn - Minimal Local LLM REST API Architecture

## Project Overview

PorcelAIn is a lightweight .NET 10 RESTful API for hosting a single local Large Language Model (LLM) or Small Language Model (SLM). Built with minimal APIs and zero-ceremony approach, it provides HTTP endpoints for text generation with support for ONNX, GGUF, and Hugging Face model formats. The model type and path are configured via appsettings.json for maximum simplicity.

## Architecture Philosophy

### Minimal Everything Approach
- **Single-file application**: One Program.cs file with all configuration
- **Minimal APIs**: Direct endpoint definitions without controllers
- **Configuration-driven**: Model selection via appsettings.json only
- **Zero-ceremony startup**: Absolute minimum boilerplate
- **Stateless design**: No conversation history, pure request/response
- **Single responsibility**: Host one model, do it well

### Design Principles
- **Smallest footprint**: Minimal dependencies and memory usage
- **Fast startup**: Model loaded once at application start
- **Simple deployment**: Single executable with embedded configuration
- **HTTP-only**: Pure REST API without WebSockets or real-time features
- **Configuration over code**: All model settings in appsettings.json

## Technology Stack

### Core Framework
- **.NET 10 Preview**: Latest runtime for maximum performance
- **ASP.NET Core Minimal APIs**: Lightweight HTTP framework
- **Native AOT Ready**: Optimized for single-file deployment

### LLM Integration (Choose One)
- **Microsoft.ML.OnnxRuntime**: For ONNX model files
- **LLamaSharp**: For GGUF model files (llama.cpp backend)
- **Hugging Face Transformers.NET**: For HF model files
- **System.Text.Json**: High-performance JSON serialization only

## Application Architecture

### Flat File Structure (No Layers)

```
PorcelAIn/
├── Program.cs          # Complete application in one file
├── appsettings.json    # Model configuration only
├── Models/             # Single model file storage
└── README.md           # Usage instructions
```

### Single-File Design

The entire application fits in `Program.cs` with three main sections:

#### 1. Configuration & Startup
- Model type detection from appsettings.json
- Single model loading at startup
- Minimal API endpoint registration
- Error handling setup

#### 2. Model Provider Factory
- Simple factory method based on file extension
- Direct instantiation of ONNX/GGUF/HF provider
- Model loading with basic error handling
- No abstraction layers or interfaces

#### 3. API Endpoints
- **POST /generate**: Text generation endpoint
- **GET /health**: Model status check
- **GET /info**: Model metadata and capabilities
- No authentication, no rate limiting, no middleware

## Key Components Design

### 1. Complete Program.cs (150-200 lines total)
```csharp
// Program.cs - Complete REST API in one file
using Microsoft.ML.OnnxRuntime;
using System.Text.Json;

// Configuration
var builder = WebApplication.CreateSlimBuilder(args);
var config = builder.Configuration;

// Load model based on appsettings
var modelPath = config["Model:Path"];
var modelType = config["Model:Type"]; // "onnx", "gguf", or "huggingface"

// Create model provider (simple factory)
object modelProvider = modelType.ToLower() switch
{
    "onnx" => new InferenceSession(modelPath),
    "gguf" => new LLamaSharp.LLamaWeights(modelPath),
    "huggingface" => new HuggingFace.Pipeline(modelPath),
    _ => throw new InvalidOperationException($"Unsupported model type: {modelType}")
};

var app = builder.Build();

// API Endpoints
app.MapPost("/generate", async (GenerateRequest request) => 
{
    var response = await GenerateText(modelProvider, request.Prompt);
    return Results.Ok(new { text = response });
});

app.MapGet("/health", () => Results.Ok(new { status = "healthy", model = modelType }));
app.MapGet("/info", () => Results.Ok(new { type = modelType, path = modelPath }));

app.Run();

// Simple request/response models
record GenerateRequest(string Prompt, int MaxTokens = 100, float Temperature = 0.7f);

// Model-specific generation logic
async Task<string> GenerateText(object provider, string prompt) => provider switch
{
    InferenceSession onnx => await GenerateWithOnnx(onnx, prompt),
    // ... other providers
};
```

### 2. Configuration-Only Approach
- **appsettings.json**: Single source of truth for model configuration
- **No service registration**: Direct object instantiation
- **No dependency injection**: Simple factory pattern
- **Environment variables**: Override model path for deployment

### 3. Request/Response Models
```csharp
// Input
record GenerateRequest(
    string Prompt,
    int MaxTokens = 100,
    float Temperature = 0.7f,
    float TopP = 0.9f,
    string[] StopSequences = null
);

// Output  
record GenerateResponse(
    string Text,
    int TokensGenerated,
    float ProcessingTimeMs
);
```

### 4. Model Provider Patterns
- **No interfaces**: Direct class usage for maximum performance
- **Lazy loading**: Model loaded once at startup
- **Memory management**: Single model instance, no caching needed
- **Error handling**: Simple try/catch with HTTP status codes

## Modern .NET Features Utilization

### .NET 10 Specific Features
- **Native AOT compatibility**: Single-file deployments under 50MB
- **Source generators**: Compile-time JSON serialization
- **Minimal APIs**: Zero-ceremony endpoint definition
- **Top-level programs**: No class/method ceremony
- **Record types**: Immutable request/response models

### Performance Optimizations
- **Span<T> and Memory<T>**: Zero-allocation text processing
- **ValueTask**: Synchronous path optimization
- **ArrayPool**: Token buffer reuse
- **Direct model access**: No abstraction overhead

## Data Flow Architecture

### Simple Request Flow
1. **HTTP Request** → ASP.NET Core routing
2. **Minimal API endpoint** → Direct model provider call  
3. **Model inference** → ONNX/GGUF/HF native execution
4. **Response generation** → JSON serialization
5. **HTTP Response** → Client receives generated text

### Stateless Design
- **No session state**: Each request is independent
- **No conversation history**: Client manages context
- **No caching**: Single model stays in memory
- **No background services**: Request/response only

## Security Considerations

### Minimal Attack Surface
- **No authentication**: Simplicity over security (local use only)
- **No input validation**: Basic prompt length limits only
- **No rate limiting**: Trust local usage patterns
- **File system access**: Single model directory only

### Local-Only Operation
- **No network dependencies**: Model and inference entirely local
- **No data persistence**: Stateless request/response only
- **Process isolation**: Single model, single thread execution
- **Memory safety**: Automatic garbage collection handles cleanup

## Implementation Strategy

### Single Sprint Approach (1-2 Days)
**Objective**: Get minimal viable API running immediately

**Day 1**: Core Structure
- Create .NET 10 project with single Program.cs
- Add model provider packages (choose one: ONNX, GGUF, or HF)
- Implement basic /generate endpoint
- Configure appsettings.json for model selection
- Test with simple model file

**Day 2**: Polish & Deploy
- Add /health and /info endpoints
- Basic error handling for model loading failures
- Native AOT compilation setup
- Single-file deployment configuration
- README with usage examples

### Model-Specific Implementation Variants

#### ONNX Variant (Simplest)
```csharp
// Focus: Microsoft.ML.OnnxRuntime only
// Pros: Official Microsoft support, broad model compatibility
// Cons: Larger runtime, more setup complexity
```

#### GGUF Variant (Most Popular)
```csharp  
// Focus: LLamaSharp + llama.cpp only
// Pros: Excellent performance, active community
// Cons: C++ dependencies, platform-specific builds
```

#### Hugging Face Variant (Most Models)
```csharp
// Focus: Transformers.NET only  
// Pros: Huge model ecosystem, Python compatibility
// Cons: Heaviest runtime, complex dependencies
```

## Performance Considerations

### Memory Management
- **Single model loading**: One model stays in memory entire lifetime
- **No garbage collection pressure**: Minimal object allocations
- **Direct buffer access**: Use Span<T> for token processing  
- **Model-specific optimization**: GPU acceleration where available

### Startup Optimization
- **Eager model loading**: Load model at application start
- **Native AOT**: Sub-second startup times
- **Assembly trimming**: Remove unused framework code
- **Single-file deployment**: No dependency resolution overhead

### Request Performance
- **Direct model access**: No service abstraction layers
- **Synchronous generation**: Blocking calls for simplicity
- **Minimal JSON processing**: Direct record serialization only
- **No middleware**: Direct endpoint to model pipeline

## Deployment Strategies

### Development Environment
- **dotnet run**: Instant startup for testing
- **Local model files**: Place models in `/Models` directory
- **Console logging**: Simple System.Console output
- **HTTP only**: Skip HTTPS complexity for local development

### Production Deployment
- **Native AOT executable**: Single 20-50MB file deployment
- **No external dependencies**: Completely self-contained
- **Environment variables**: Override model path via ENV vars
- **System service**: Optional Windows Service or systemd daemon

### Configuration Examples
```json
// appsettings.json - Complete configuration
{
  "Model": {
    "Type": "gguf",
    "Path": "./Models/llama-7b-q4.gguf",  
    "MaxTokens": 2048,
    "Temperature": 0.7
  },
  "Logging": {
    "LogLevel": { "Default": "Warning" }
  }
}
```

## API Documentation

### Endpoints

#### POST /generate
Generate text from prompt
```json
// Request
{
  "prompt": "Hello, how are you?",
  "maxTokens": 100,
  "temperature": 0.7
}

// Response  
{
  "text": "I'm doing well, thank you for asking!",
  "tokensGenerated": 12,
  "processingTimeMs": 342.5
}
```

#### GET /health
Check API and model status
```json
// Response
{
  "status": "healthy",
  "model": "gguf",
  "modelPath": "./Models/llama-7b-q4.gguf"
}
```

#### GET /info  
Get model capabilities
```json
// Response
{
  "type": "gguf", 
  "maxTokens": 2048,
  "contextWindow": 4096,
  "modelSize": "7B parameters"
}
```

## Conclusion

This minimal architecture prioritizes simplicity and performance over extensibility. The single-file approach eliminates architectural complexity while providing a robust REST API for local LLM inference. 

The configuration-driven model selection allows easy switching between ONNX, GGUF, and Hugging Face models without code changes. Native AOT compilation ensures fast startup and small deployment size, making it ideal for local development tools and edge deployment scenarios.

The stateless design keeps memory usage predictable and eliminates session management complexity, while the direct model access pattern maximizes inference performance by removing abstraction overhead. 