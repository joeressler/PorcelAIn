# PorcelAIn - Minimal Local LLM REST API Architecture

## Project Overview

PorcelAIn is a lightweight .NET 10 RESTful API for hosting a single local Large Language Model (LLM) or Small Language Model (SLM). Built with minimal APIs and zero-ceremony approach, it provides HTTP endpoints for text generation with support for PyTorch (.pt/.pth), ONNX, and Hugging Face model formats. The model type and path are configured via appsettings.json for maximum simplicity.

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
- **TorchSharp**: For PyTorch model files (.pt/.pth) - **Current Implementation**
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
├── Models/             # PyTorch model and tokenizer storage
│   ├── model.pt        # PyTorch model file
│   └── vocab.json      # Tokenizer vocabulary
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

### 1. Complete Program.cs (300+ lines total)
```csharp
// Program.cs - Complete REST API in one file
using TorchSharp;
using static TorchSharp.torch;
using System.Text.Json;

// Configuration
var builder = WebApplication.CreateSlimBuilder(args);
var config = builder.Configuration;

// Load model based on appsettings
var modelPath = config["Model:Path"] ?? "./Models/model.pt";
var modelType = config["Model:Type"]?.ToLower() ?? "pytorch";
var vocabPath = config["Model:VocabPath"] ?? "./Models/vocab.json";

// Initialize TorchSharp
var device = torch.cuda.is_available() ? torch.CUDA : torch.CPU;

// Load PyTorch model
torch.jit.ScriptModule? model = null;
Dictionary<string, int>? tokenizer = null;

try
{
    model = torch.jit.load(modelPath, device);
    model.eval();
    
    if (File.Exists(vocabPath))
    {
        var vocabJson = await File.ReadAllTextAsync(vocabPath);
        tokenizer = JsonSerializer.Deserialize<Dictionary<string, int>>(vocabJson);
    }
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
    var inputTokens = TokenizeText(request.Prompt, tokenizer);
    var generatedText = await GenerateWithTorch(model, inputTokens, request);
    return Results.Ok(new { text = generatedText });
});

app.MapGet("/health", () => Results.Ok(new { 
    status = "healthy", 
    model = modelType,
    device = device.ToString()
}));

app.MapGet("/info", () => Results.Ok(new { 
    type = modelType, 
    path = modelPath,
    torchVersion = torch.version,
    cudaAvailable = torch.cuda.is_available()
}));

app.Run();

// Simple request/response models
record GenerateRequest(string Prompt, int MaxTokens = 100, float Temperature = 0.7f);

// TorchSharp inference logic
async Task<string> GenerateWithTorch(torch.jit.ScriptModule model, long[] inputTokens, GenerateRequest request)
{
    using (torch.no_grad())
    {
        var inputTensor = torch.tensor(inputTokens, device: device).unsqueeze(0);
        var outputs = model.forward(inputTensor);
        // ... generation logic
        return generatedText;
    }
}
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

#### PyTorch Variant (Current Implementation)
```csharp
// Focus: TorchSharp + PyTorch ecosystem
// Pros: Native PyTorch models, GPU acceleration, research ecosystem
// Cons: Larger runtime, requires model conversion or training
// Models: .pt, .pth files from PyTorch training or torch.jit.script()
```

#### ONNX Variant (Cross-Platform)
```csharp
// Focus: Microsoft.ML.OnnxRuntime only
// Pros: Official Microsoft support, broad model compatibility
// Cons: Larger runtime, more setup complexity
```

#### GGUF Variant (Optimized for CPUs)
```csharp  
// Focus: LLamaSharp + llama.cpp only
// Pros: Excellent CPU performance, quantized models
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
    "Type": "pytorch",
    "Path": "./Models/model.pt",  
    "VocabPath": "./Models/vocab.json",
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
  "model": "pytorch",
  "modelPath": "./Models/model.pt",
  "device": "CPU",
  "tokenizerLoaded": true
}
```

#### GET /info  
Get model capabilities
```json
// Response
{
  "type": "pytorch", 
  "path": "./Models/model.pt",
  "vocabPath": "./Models/vocab.json",
  "maxTokens": 2048,
  "contextWindow": 2048,
  "modelSizeMB": 4096,
  "temperature": 0.7,
  "device": "CPU",
  "torchVersion": "2.1.0",
  "cudaAvailable": false,
  "vocabSize": 32000
}
```

## Conclusion

This minimal architecture prioritizes simplicity and performance over extensibility. The single-file approach eliminates architectural complexity while providing a robust REST API for local LLM inference. 

The current TorchSharp implementation provides native PyTorch model support with GPU acceleration capabilities, making it ideal for research and development scenarios. The configuration-driven model selection allows easy switching between PyTorch, ONNX, GGUF, and Hugging Face models with minimal code changes.

Native AOT compilation ensures fast startup and small deployment size, making it ideal for local development tools and edge deployment scenarios. The stateless design keeps memory usage predictable and eliminates session management complexity, while the direct model access pattern maximizes inference performance by removing abstraction overhead.

**Current Implementation**: TorchSharp provides excellent performance for PyTorch models with proper GPU utilization and supports the full PyTorch ecosystem including custom models and fine-tuned variants. 