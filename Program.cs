using TextToSpeechServer.Services;

namespace TextToSpeechServer
{
    public class Program
    {
        public async static Task Main(string[] args)
        {
            var version = "v1.0 25092026";

            var builder = WebApplication.CreateBuilder(args);
            builder.Services.AddGrpc();
            builder.Services.AddSingleton<TextToSpeechService>();

            var app = builder.Build();

            var logger = app.Logger;
            logger.LogInformation($"{Utils.GetDdMmYyyy()}; {Utils.GetHhMmSs()}; TTS Server {version} started. DO NOT close this window.");

            app.MapGrpcService<TextToSpeechService>();
            app.MapGet("/", () => "Communication with gRPC endpoints must be made through a gRPC client.");

            logger.LogInformation($"{Utils.GetHhMmSs()}; Warming up TTS model..");
            var ttsService = app.Services.GetRequiredService<TextToSpeechService>();
            await ttsService.WarmUpAsync();
            logger.LogInformation($"{Utils.GetHhMmSs()}; Warm up completed");

            app.Run();
        }
    }
}
