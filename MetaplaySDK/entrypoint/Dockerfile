FROM golang:1.22.7
ARG TARGETARCH
SHELL ["/bin/bash", "-e", "-o", "pipefail", "-c"]

# Copy sources
WORKDIR /build
COPY . .

# Build entrypoint binary, use static linking so it works on chiseled .NET images
# \note Building with -ldflags removes debug info, reducing binary size by about 1MB
RUN echo "Building for architecture ${TARGETARCH}"
RUN CGO_ENABLED=0 GOOS=linux GOARCH=${TARGETARCH} go build -ldflags "-s -w" -o /entrypoint main.go
