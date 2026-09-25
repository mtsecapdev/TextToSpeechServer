FROM nvidia/cuda:12.8.0-cudnn-runtime-ubuntu24.04

RUN apt-get update && apt-get install -y libicu-dev && rm -rf /var/lib/apt/lists/*

WORKDIR /app

COPY ./publish .

ENV ASPNETCORE_URLS="http://+:5257"
EXPOSE 5257

ENTRYPOINT ["./TextToSpeechServer"]