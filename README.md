# PorcelAIn - Minimal Local LLM REST API

A lightweight .NET REST API for hosting local Large Language Models (LLMs) using ONNX Runtime GenAI with Microsoft Phi-3 models.

## Features

- **ONNX Runtime GenAI**: Optimized inference using Microsoft's ONNX Runtime
- **Phi-3 Support**: Pre-configured for Microsoft Phi-3 models
- **Configuration-driven**: Model selection via `appsettings.json`
- **REST endpoints**: Simple HTTP API for text generation
- **CPU Optimized**: Runs efficiently on CPU without GPU requirements
- **Streaming Generation**: Token-by-token text generation

## Quick Start

### 1. Prerequisites

- .NET Preview with C# 12+ support
- Microsoft Phi-3 ONNX model files
- Git LFS (for downloading large model files)

### 2. Download Phi-3 Model

Download the Phi-3 model using Hugging Face CLI:

```bash
# Install Hugging Face CLI
pip install huggingface-hub[cli]

# Download Phi-3 mini CPU model
huggingface-cli download microsoft/Phi-3-mini-4k-instruct-onnx \
  --include cpu_and_mobile/cpu-int4-rtn-block-32-acc-level-4/* \
  --local-dir ./Models
```

### 3. Configure Model

Edit `appsettings.json`:

```json
{
  "Model": {
    "Path": "./Models/cpu_and_mobile/cpu-int4-rtn-block-32-acc-level-4",
    "MaxTokens": 2048,
    "Temperature": 0.8
  }
}
```

### 4. Project Structure

```
PorcelAIn/
├── Models/
│   └── cpu_and_mobile/
│       └── cpu-int4-rtn-block-32-acc-level-4/
│           ├── model.onnx           <- ONNX model file
│           ├── model.onnx.data      <- Model weights
│           ├── tokenizer.json       <- Tokenizer
│           ├── genai_config.json    <- Generation config
│           └── ...                  <- Other model files
├── Program.cs                       <- Complete application
├── appsettings.json                 <- Model configuration
└── README.md                        <- This file
```

### 5. Run

```bash
dotnet run Program.cs
```

The API will start on `http://localhost:5000` (model loading takes ~30-60 seconds)

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
  "processingTimeMs": 2450.8
}
```

### GET /health
Check API and model status

**Response:**
```json
{
  "status": "healthy",
  "model": "phi3", 
  "modelPath": "./Models/cpu_and_mobile/cpu-int4-rtn-block-32-acc-level-4"
}
```

### GET /info  
Get model information

**Response:**
```json
{
  "type": "phi3",
  "path": "./Models/cpu_and_mobile/cpu-int4-rtn-block-32-acc-level-4",
  "maxTokens": 2048,
  "contextWindow": 32768,
  "modelSizeMB": 2500,
  "temperature": 0.8
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

# Generate text with Phi-3
response = requests.post('http://localhost:5000/generate', json={
    'prompt': 'Explain quantum computing in simple terms:',
    'maxTokens': 150,
    'temperature': 0.7
})

result = response.json()
print(f"Generated {result['tokensGenerated']} tokens in {result['processingTimeMs']:.1f}ms")
print(result['text'])
```

## Deployment

### Standard Deployment

```bash
# Build release version
dotnet publish -c Release -r win-x64 --self-contained

# Or for Linux
dotnet publish -c Release -r linux-x64 --self-contained
```

**Note**: Native AOT is disabled due to ONNX Runtime GenAI requirements.

### Development

```bash
# Run with hot reload
dotnet watch run Program.cs

# Run with custom model path
Model__Path="./Models/different-phi3-model" dotnet run Program.cs
```

## Configuration

Environment variables override `appsettings.json`:

- `Model__Path`: Path to model directory
- `Model__MaxTokens`: Maximum generation length
- `Model__Temperature`: Default temperature (0.0-2.0)

## Requirements

- **Memory**: 8GB+ RAM recommended for Phi-3 mini (3.8B parameters)
- **CPU**: 64-bit processor (x64 or ARM64)
- **Storage**: ~2.5GB for Phi-3 mini model + application files
- **Network**: Internet connection for initial model download

## Troubleshooting

### "Model directory not found"
- Verify the path in `appsettings.json` points to the model directory
- Ensure all model files (model.onnx, tokenizer.json, etc.) are present

### "Failed to load Phi-3 model"  
- Check if you have enough RAM (8GB+ recommended)
- Verify model files are not corrupted (re-download if needed)
- Ensure ONNX Runtime GenAI package is properly installed

### Slow generation
- Phi-3 mini on CPU typically generates 2-5 tokens/second
- Consider using GPU-optimized models for faster inference
- Reduce `MaxTokens` for shorter responses

### "Unknown provider name" error
- The application uses default CPU execution provider
- GPU providers (CUDA, DirectML) require additional setup

## License

MIT License - Use freely for any purpose.