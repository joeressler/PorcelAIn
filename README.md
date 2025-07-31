# PorcelAIn - Minimal Local LLM REST API

A lightweight .NET 10 REST API for hosting Large Language Models (LLM) via OllamaSharp and the Ollama service.

## Features

- **Minimal footprint**: Single-file application (~186 lines)
- **Configuration-driven**: Ollama service and model selection via `appsettings.json`
- **Ollama integration**: Access to entire Ollama model ecosystem
- **Automatic model management**: Models are downloaded automatically if not available
- **REST endpoints**: Simple HTTP API for text generation 
- **Service separation**: API and model hosting are decoupled
- **Native AOT ready**: Fast startup, small deployment size

## Quick Start

### 1. Prerequisites

- .NET 10 Preview or later
- Ollama service installed and running ([ollama.ai](https://ollama.ai))
- At least one Ollama model pulled (e.g., `ollama pull llama3.2`)

### 2. Start Ollama Service

```bash
# Install a model (if not already done)
ollama pull llama3.2

# Start Ollama service
ollama serve
```

### 3. Configure Ollama Connection

Edit `appsettings.json`:

```json
{
  "Ollama": {
    "Url": "http://localhost:11434",
    "Model": "llama3.2", 
    "MaxTokens": 2048,
    "Temperature": 0.7
  }
}
```

### 4. Run

```bash
# Ensure Ollama is running first
ollama serve &

# Run PorcelAIn API
dotnet run Program.cs
```

The API will start on `http://localhost:5000`

**Note**: Ollama service must be running on `http://localhost:11434` (default)

## Popular Models

Here are some recommended models you can use with Ollama:

```bash
# Small, fast models (good for development)
ollama pull phi3:mini          # ~2GB - Microsoft Phi-3 Mini
ollama pull llama3.2:1b        # ~1GB - Meta Llama 3.2 1B

# Medium models (balanced performance)
ollama pull llama3.2:3b        # ~2GB - Meta Llama 3.2 3B
ollama pull mistral:7b         # ~4GB - Mistral 7B

# Large models (best quality)
ollama pull llama3.2:8b        # ~5GB - Meta Llama 3.2 8B
ollama pull codellama:13b      # ~7GB - Code Llama 13B (for coding)

# Specialized models
ollama pull codellama:7b       # ~4GB - Code generation
ollama pull deepseek-coder:6.7b # ~4GB - Advanced coding model
```

Update your `appsettings.json` with the model name:
```json
{
  "Ollama": {
    "Model": "llama3.2:3b"  // <- Use any pulled model
  }
}
```

## Project Structure

```
PorcelAIn/
├── Program.cs             <- Complete application (186 lines)
├── appsettings.json       <- Ollama configuration
└── README.md              <- This file
```

**Note**: No `Models/` directory needed - models are managed by Ollama service.

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
Check API and Ollama service status

**Response:**
```json
{
  "status": "healthy",
  "service": "ollama", 
  "serviceUrl": "http://localhost:11434",
  "model": "llama3.2",
  "modelAvailable": true
}
```

### GET /info  
Get Ollama service and model information

**Response:**
```json
{
  "service": "ollama",
  "serviceUrl": "http://localhost:11434",
  "model": "llama3.2",
  "maxTokens": 2048,
  "temperature": 0.7,
  "modelSizeMB": 4096,
  "modelFamily": "llama",
  "availableModels": ["llama3.2", "codellama", "mistral"]
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

# Run with different Ollama model
Ollama__Model="codellama" dotnet run Program.cs

# Run with different Ollama service URL
Ollama__Url="http://remote-ollama:11434" dotnet run Program.cs
```

## Configuration

Environment variables override `appsettings.json`:

- `Ollama__Url`: Ollama service URL (default: http://localhost:11434)
- `Ollama__Model`: Model name to use (e.g., "llama3.2", "codellama")  
- `Ollama__MaxTokens`: Maximum tokens to generate
- `Ollama__Temperature`: Default temperature for generation

## Requirements

- **Ollama Service**: Must be installed and running separately
- **Memory**: Managed by Ollama service (4GB+ recommended for 7B models)
- **CPU**: Any 64-bit processor (ARM64 supported)
- **Storage**: ~50MB for PorcelAIn API + models managed by Ollama
- **Network**: HTTP access to Ollama service (default: localhost:11434)

## Troubleshooting

### "Failed to connect to Ollama service"
- Verify Ollama is running: `ollama serve`
- Check the service URL in `appsettings.json`
- Ensure port 11434 is not blocked by firewall

### "Model not found"
- Pull the model: `ollama pull llama3.2`
- Check available models: `ollama list`
- Verify model name matches exactly in configuration

### "Generation failed" or timeout
- Check Ollama service logs: `ollama logs`
- Reduce `MaxTokens` in configuration
- Try a smaller/faster model (e.g., `phi3:mini`)
- Ensure Ollama has sufficient memory allocated

### High memory usage
- Memory is managed by Ollama service, not PorcelAIn
- Configure Ollama's model concurrency settings
- Use smaller models or quantized variants via Ollama

## License

MIT License - Use freely for any purpose.