# PorcelAIn - Minimal Local LLM REST API

A lightweight .NET 10 REST API for hosting a single local Large Language Model (LLM) using GGUF format via LLamaSharp.

## Features

- **Minimal footprint**: Single-file application (~167 lines)
- **Configuration-driven**: Model selection via `appsettings.json`
- **GGUF support**: Optimized for llama.cpp models
- **REST endpoints**: Simple HTTP API for text generation 
- **Native AOT ready**: Fast startup, small deployment size

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
    "Path": "./Models/your-model.gguf", 
    "MaxTokens": 2048,
    "Temperature": 0.7
  }
}
```

### 3. Add Model File

Place your GGUF model in the `Models/` directory:

```
PorcelAIn/
├── Models/
│   └── your-model.gguf    <- Your model file here
├── Program.cs             <- Complete application (167 lines)
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
  "prompt": "Hello, how are you?",
  "maxTokens": 100,
  "temperature": 0.7,
  "topP": 0.9,
  "stopSequences": ["Human:", "AI:"]
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
  "modelPath": "./Models/your-model.gguf"
}
```

### GET /info  
Get model information

**Response:**
```json
{
  "type": "gguf",
  "path": "./Models/your-model.gguf",
  "maxTokens": 2048,
  "contextWindow": 2048,
  "modelSizeMB": 4096,
  "temperature": 0.7
}
```

## Example Usage

### cURL Examples

```bash
# Generate text
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

# Generate text
response = requests.post('http://localhost:5000/generate', json={
    'prompt': 'Write a haiku about programming:',
    'maxTokens': 60,
    'temperature': 0.8
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

# Run with custom model
Model__Path="./Models/different-model.gguf" dotnet run Program.cs
```

## Configuration

Environment variables override `appsettings.json`:

- `Model__Path`: Path to model file
- `Model__Type`: Model type (currently only "gguf")  
- `Model__MaxTokens`: Maximum context size
- `Model__Temperature`: Default temperature

## Requirements

- **Memory**: Varies by model size (4GB+ recommended for 7B models)
- **CPU**: Any 64-bit processor (ARM64 supported)
- **Storage**: Model file size + ~50MB for application

## Troubleshooting

### "Model file not found"
- Verify the path in `appsettings.json` 
- Ensure the model file exists and is readable

### "Failed to load model"
- Check if you have enough RAM for the model
- Verify the GGUF file is not corrupted
- Try a smaller quantized model (Q4_0, Q4_1)

### High memory usage
- Reduce `MaxTokens` in configuration
- Use a more quantized model variant
- Enable `InvariantGlobalization=true` for smaller footprint

## License

MIT License - Use freely for any purpose.