# Text-to-Speech Server (KokoroSharp)

A gRPC server that converts text to speech using [KokoroSharp](https://github.com/Lyrcaxis/KokoroSharp), an ONNX-based TTS engine with 50 voices across 10 languages. Built for the NWPT/AawSmartTrainer Unity training simulation — virtual teammates send text and a voice selection, and the server returns raw PCM audio data.

## How It Works

The server exposes a single gRPC endpoint defined in `Protos/text_to_speech.proto`:

```
service TextToSpeechHandler {
  rpc ConvertTextToSpeech (TtsRequest) returns (TtsResponse);
}
```

- **`TtsRequest`** contains `text` (the string to speak) and `voice_name` (a `KokoroVoiceName` enum value like `af_heart`, `am_adam`, etc.).
- **`TtsResponse`** contains `pcm_data` — raw PCM audio bytes at 22kHz mono. The Unity client converts these into an `AudioClip`.

On startup, the server loads the Kokoro ONNX model, attempts to use CUDA for GPU inference (falls back to CPU if unavailable), and runs a warm-up inference to eliminate cold-start latency on the first real request.

### Key Files

| File | Purpose |
|------|---------|
| `Program.cs` | Entry point — registers the service, runs warm-up, starts the server |
| `Services/TextToSpeechService.cs` | gRPC service implementation — loads model, handles synthesis requests |
| `Utils.cs` | Timestamp helpers for logging |
| `Protos/text_to_speech.proto` | The gRPC contract (source of truth) |
| `appsettings.json` | Kestrel config — HTTP/2 on port 5257 |

### Model and Voice Files (Not in Repo)

The ONNX model and voice files are **not included in this repository** due to file size. They must be added before building:

**`Models/kokoro.onnx`** — The Kokoro TTS model. Get this from https://github.com/Lyrcaxis/KokoroSharpBinaries/releases.

**`voices/`** — Voice data files required by KokoroSharp at runtime. These are bundled in the KokoroSharp NuGet package but don't always copy to the publish output. To get them: https://github.com/Lyrcaxis/KokoroSharpBinaries/releases

Your project root should look like this before building:

```
TextToSpeechServer/
├── Models/
│   └── kokoro.onnx
├── voices/
│   └── ... (voice data files)
├── Protos/
│   └── text_to_speech.proto
├── Services/
│   └── TextToSpeechService.cs
├── Program.cs
├── Utils.cs
├── TextToSpeechServer.csproj
├── appsettings.json
├── dockerfile
└── Makefile
```

## Running Locally (Without Docker)

### Prerequisites

- **.NET 10 SDK**
- **NVIDIA GPU** with drivers installed
- **CUDA 12.x** and **cuDNN 9.x** (required by OnnxRuntime.Gpu 1.22.0 — falls back to CPU if unavailable)

### Setup

```bash
git clone <repo-url>
cd TextToSpeechServer
```

Download `kokoro.onnx` into `Models/` and copy `voices/` into the project root as described above.

### Run

```bash
dotnet run
```

The server starts on `http://localhost:5257` with HTTP/2 (required for gRPC). Logs will show CUDA detection and warm-up status:

```
25-Sep-2026; 09:27:15; TTS Server v1.0 started. DO NOT close this window.
09:27:15; Warming up TTS model..
09:27:18; CUDA found. Inference via GPU.
09:27:20; Warm up completed
```

If CUDA is not available, the server falls back to CPU inference automatically. It will be slower but functional.

## Docker

The Docker image is self-contained — the ONNX model and voices are baked into the image. No volume mounts are needed.

### Prerequisites

- **NVIDIA driver** installed on the host
- **Docker Desktop** with WSL2 backend (handles NVIDIA Container Toolkit integration)

### Build

First, publish the .NET app for Linux:

```bash
dotnet publish TextToSpeechServer.csproj -c Release -r linux-x64 --self-contained true -o ./publish
```

`--self-contained true` bundles the .NET runtime into the output, so the container doesn't need .NET installed. `-r linux-x64` cross-publishes for Linux even from a Windows machine.

Then build the image:

```bash
make build
```

Or manually:

```bash
docker build -t tts-service:1.0 .
```

### Run

```bash
make run
```

Or manually:

```bash
docker run --rm --gpus device=0 -p 5257:5257 tts-service:1.0
```

No volume mounts required — the model and voices are baked into the image at build time.

### Makefile Commands

| Command | Description |
|---------|-------------|
| `make build` | Builds the Docker image as `tts-service:1.0` |
| `make run` | Runs the container with GPU access on port 5257 |

### Why `dotnet publish` Before `docker build`?

C# is a compiled language — `.cs` files can't run directly. `dotnet publish` compiles the source into a self-contained Linux executable. The Dockerfile then just copies that compiled output into a CUDA-enabled container. This is different from Python/Node projects where you copy the source code directly and install dependencies inside the container.

## Unity Client Integration

The Unity client connects to `http://localhost:5257` using gRPC with `YetAnotherHttpHandler` (Cysharp) configured for HTTP/2:

```csharp
_httpHandler = new YetAnotherHttpHandler() { Http2Only = true };
_channel = GrpcChannel.ForAddress("http://localhost:5257", new GrpcChannelOptions() { HttpHandler = _httpHandler });
_client = new TextToSpeechHandler.TextToSpeechHandlerClient(_channel);
```

The client sends a `TtsRequest` with the text and a `KokoroVoiceName` enum value. The response contains raw PCM bytes which the client converts to a Unity `AudioClip` at 22kHz mono.

## Gotchas

- **Model and voices not in repo.** You must manually add `Models/kokoro.onnx` and the `voices/` folder before building or publishing. Without them, the server crashes on startup.
- **Publish before Docker build.** The Dockerfile copies from `./publish`, so you must run `dotnet publish` before `docker build`. If you change code and rebuild the image without republishing, the image will contain stale code.
- **`voices/` missing from publish output.** KokoroSharp's NuGet package copies voices to `bin/` during build but not always during publish. The `.csproj` includes a `<None Update="voices\**\*" CopyToOutputDirectory="PreserveNewest" />` entry to handle this, but the `voices/` folder must exist in the project root first.
- **HTTP, not HTTPS.** The server runs on HTTP. gRPC requires HTTP/2, which normally negotiates via TLS (HTTPS), but `YetAnotherHttpHandler` supports HTTP/2 cleartext (h2c). Running HTTPS in Docker requires generating certificates inside the container, which adds complexity for no security benefit when client and server are on the same machine.
- **CUDA fallback is silent.** If CUDA isn't available, the server logs a warning and falls back to CPU. It still works, but inference is significantly slower.