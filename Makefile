IMAGE_NAME = tts-service
TAG = 1.0

build:
	docker build -t $(IMAGE_NAME):$(TAG) .

run:
	docker run --rm --gpus device=0 -p 5257:5257 $(IMAGE_NAME):$(TAG)