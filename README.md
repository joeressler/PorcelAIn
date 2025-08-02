# PorcelAIn - Minimal Local LLM REST API

A lightweight .NET 10 REST API for hosting a single local Large Language Model (LLM) using GGUF format via LLamaSharp.

## Features

- **Minimal footprint**: Single-file application (~202 lines)
- **Configuration-driven**: Model selection via `appsettings.json`
- **GGUF support**: Optimized for llama.cpp models via LLamaSharp
- **REST endpoints**: Simple HTTP API for text generation 
- **Native AOT ready**: Fast startup, small deployment size with proper JSON serialization

## Quick Start

### 1. Prerequisites

- .NET 10 Preview or later
- A GGUF model file (e.g., from Hugging Face)

### 2. Configure Model

Edit `appsettings.json`:

```json
{
  "Model": {
    "Type": "gguf",
    "Path": "./Models/phi-4-Q2_K.gguf", 
    "MaxTokens": 4096,
    "Temperature": 0.7
  }
}
```

### 3. Add Model File

Place your GGUF model in the `Models/` directory:

```
PorcelAIn/
├── Models/
│   └── phi-4-Q2_K.gguf    <- Your model file here
├── Program.cs             <- Complete application (202 lines)
├── appsettings.json       <- Model configuration
└── README.md              <- This file
```

### 4. Run

```bash
dotnet run Program.cs
```

The API will start on `http://localhost:5000`

## API Endpoints

### POST /generate
Generate text from a prompt

**Request:**
```json
{
  "prompt": "<|user|>\nHello, how are you?<|end|>\n<|assistant|>\n",
  "maxTokens": 100,
  "temperature": 0.7,
  "topP": 0.9,
  "stopSequences": ["<|end|>", "<|user|>"]
}
```

**Response:**
```json
{
  "text": "I'm doing well, thank you for asking! How can I help you today?",
  "tokensGenerated": 15,
  "processingTimeMs": 342.5
}
```

### GET /health
Check API and model status

**Response:**
```json
{
  "status": "healthy",
  "model": "gguf", 
  "modelPath": "./Models/phi-4-Q2_K.gguf"
}
```

### GET /info  
Get model information

**Response:**
```json
{
  "type": "gguf",
  "path": "./Models/phi-4-Q2_K.gguf",
  "maxTokens": 4096,
  "contextWindow": 4096,
  "modelSizeMB": 5286,
  "temperature": 0.7
}
```

## Example Usage

### cURL Examples

```bash
# Generate text (Phi-4 format)
curl -X POST http://localhost:5000/generate \
  -H "Content-Type: application/json" \
  -d '{"prompt": "<|user|>\nWhat is the meaning of life?<|end|>\n<|assistant|>\n", "maxTokens": 100, "stopSequences": ["<|end|>", "<|user|>"]}'

# Generate text (simple)
curl -X POST http://localhost:5000/generate \
  -H "Content-Type: application/json" \
  -d '{"prompt": "The meaning of life is", "maxTokens": 50}'

# Check health
curl http://localhost:5000/health

# Get model info  
curl http://localhost:5000/info
```

### Python Example

```python
import requests

# Generate text (Phi-4 optimized format)
response = requests.post('http://localhost:5000/generate', json={
    'prompt': '<|user|>\nWrite a haiku about programming:<|end|>\n<|assistant|>\n',
    'maxTokens': 100,
    'temperature': 0.7,
    'stopSequences': ['<|end|>', '<|user|>']
})

result = response.json()
print(result['text'])
```

## Deployment

### Native AOT (Recommended)

```bash
# Build self-contained executable
dotnet publish -c Release -r win-x64 --self-contained

# Single file deployment  
dotnet publish -c Release -r win-x64 --self-contained -p:PublishSingleFile=true
```

Creates a single executable (20-50MB) with no external dependencies.

### Development

```bash
# Run with hot reload
dotnet watch run Program.cs

# Run with custom model (Windows)
$env:Model__Path="./Models/different-model.gguf"; dotnet run Program.cs
```

## Configuration

### Basic Settings

Environment variables override `appsettings.json`:

- `Model__Path`: Path to model file
- `Model__Type`: Model type (currently only "gguf")  
- `Model__MaxTokens`: Maximum context size (up to 16384 for Phi-4)
- `Model__Temperature`: Default temperature

### GPU Acceleration

To enable GPU acceleration, modify `Program.cs` line 42:

```csharp
GpuLayerCount = 25 // Use 0 for CPU only, 20-35 for GPU acceleration
```

### Phi-4 Optimized Prompting

For best results with Phi-4 models, use this format:

```json
{
  "prompt": "<|user|>\nYour question here<|end|>\n<|assistant|>\n",
  "maxTokens": 100,
  "stopSequences": ["<|end|>", "<|user|>"]
}
```

## Requirements

- **Memory**: Varies by model size (6GB+ recommended for Phi-4-Q2_K)
- **CPU**: Any 64-bit processor (ARM64 supported)
- **GPU**: Optional CUDA-compatible GPU for acceleration
- **Storage**: Model file size (~5.3GB for Phi-4-Q2_K) + ~50MB for application

## Troubleshooting

### "Model file not found"
- Verify the path in `appsettings.json` 
- Ensure the model file exists and is readable

### "Failed to load model"
- Check if you have enough RAM for the model (6GB+ for Phi-4)
- Verify the GGUF file is not corrupted
- Try a smaller quantized model (Q4_0, Q4_1, or Q2_K)

### Model generates infinitely / doesn't stop
- Phi-4 requires proper end tokens - the API automatically includes:
  - `<|end|>`, `<|endoftext|>`, `<|user|>`, `<|assistant|>`, `<|system|>`
- Use proper Phi-4 prompt format: `<|user|>\nYour prompt<|end|>\n<|assistant|>\n`
- Always include `stopSequences` in your requests

### GPU acceleration not working
- Ensure you have CUDA installed and compatible GPU
- Modify `GpuLayerCount` in `Program.cs` (line 42): `GpuLayerCount = 25`
- Check GPU memory - Phi-4 requires 8GB+ VRAM for full GPU inference

### High memory usage
- Reduce `MaxTokens` in configuration (try 2048 instead of 4096)
- Use a more quantized model variant (Q2_K < Q4_0 < Q4_1 < Q5_0)
- Set `GpuLayerCount = 0` to use CPU only
- Enable `InvariantGlobalization=true` for smaller footprint

## License

MIT License - Use freely for any purpose.