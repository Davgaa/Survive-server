FROM ubuntu:22.04

WORKDIR /app

# dependencies
RUN apt-get update && apt-get install -y \
    libglu1-mesa \
    libxcursor1 \
    libxrandr2 \
    libxinerama1 \
    libxi6 \
    python3 \
    && rm -rf /var/lib/apt/lists/*

COPY . .

RUN chmod +x LocalLInuxServer.x86_64
RUN chmod +x start.sh

CMD ["./start.sh"]