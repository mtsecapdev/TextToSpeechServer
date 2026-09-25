using Google.Protobuf;
using Grpc.Core;
using KokoroSharp;
using KokoroSharp.Core;
using KokoroSharp.Processing;
using TextToSpeechServer.Protos;
using Microsoft.ML.OnnxRuntime;
using SessionOptions = Microsoft.ML.OnnxRuntime.SessionOptions;

namespace TextToSpeechServer.Services;

/// <summary>
/// Class that extends the gRPC compiled Base class in order to make real what we have defined in the proto file.
/// Implements the "rpc ConvertTextToSpeech" service that we defined as the method of the same name below: this is the method that will be used by the gRPC client to make the TTS request (string) and get back the response (bytes in pcmData).
/// There's also a WarmUpAsync method that allows quick warm up on start.
/// </summary>
public class TextToSpeechService : TextToSpeechHandler.TextToSpeechHandlerBase
{
    private readonly float TalkingSpeed = 1.3f;

    private string _kokoroModelPath;

    private KokoroWavSynthesizer _kokoroSynthesizer;

    private SessionOptions _sessionOptions;

    private KokoroTTSPipelineConfig _ttsConfig;

    private readonly ILogger<TextToSpeechService> _logger;

    public TextToSpeechService(IHostEnvironment env, ILogger<TextToSpeechService> logger)
    {
        _logger = logger;
        string[] paths = new string[3] { env.ContentRootPath, "Models", "kokoro.onnx" };
        _kokoroModelPath = Path.Combine(paths);
        _sessionOptions = new SessionOptions();
        try
        {
            _sessionOptions.AppendExecutionProvider_CUDA();
            _logger.LogInformation(Utils.GetHhMmSs() + "; CUDA found. Inference via GPU.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex.Message);
            _logger.LogInformation(Utils.GetHhMmSs() + "; CUDA not found. Defaulting to inference by CPU.");
        }
        _sessionOptions.LogSeverityLevel = OrtLoggingLevel.ORT_LOGGING_LEVEL_ERROR;
        _kokoroSynthesizer = new KokoroWavSynthesizer(_kokoroModelPath, _sessionOptions);
        _ttsConfig = new KokoroTTSPipelineConfig();
        _ttsConfig.Speed = TalkingSpeed;
    }

    public override async Task<TtsResponse> ConvertTextToSpeech(TtsRequest request, ServerCallContext context)
    {
        string text = request.Text;
        string name = Utils.KokoroVoiceDict[request.VoiceName];
        KokoroVoice voice = KokoroVoiceManager.GetVoice(name);
        _logger.LogInformation(Utils.GetHhMmSs() + "; TTS request received, generating audio..");
        ByteString pcmData = ByteString.CopyFrom(await _kokoroSynthesizer.SynthesizeAsync(text, voice, _ttsConfig));
        _logger.LogInformation(Utils.GetHhMmSs() + "; TTS completed");
        return new TtsResponse
        {
            PcmData = pcmData
        };
    }

    public async Task WarmUpAsync()
    {
        try
        {
            KokoroVoice voice = KokoroVoiceManager.GetVoice("af_heart");
            await _kokoroSynthesizer.SynthesizeAsync("Warming up.", voice, _ttsConfig);
        }
        catch (Exception ex)
        {
            _logger.LogError(Utils.GetHhMmSs() + "; Warmup failed: " + ex.Message);
        }
    }
}
