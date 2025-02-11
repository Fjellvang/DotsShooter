# Overview

Simple entrypoint program for the runtime Docker image that allows starting either the game server or the botclient.

Implemented in Go so it can be compiled to a binary and thus run on a Docker image without a shell present, e.g., .NET chiseled images.

## Run Locally

To help development, the `entrypoint` can be run locally with the following:

```bash
MetaplaySDK/entrypoint$ go run main.go --working-dir ../../Samples/Idler/Backend/Server dotnet run
```

Note that keyboard input does not work when running through the supervisor. You can still Ctrl-C to exit the app.

## Build Instructions

The pre-built binaries are found in the `output/` directory for convenience. These built binaries are used in the game-server build.

The binaries can be build with:

```bash
MetaplaySDK/entrypoint$ ./build.sh
```

## Health Probe Proxy

### Overview

This is a simple proxy for Kubernetes health and readiness probes that allows temporarily overriding the checks to return either success or failure.

The intended use case of this program is to proxy the health checks going into the game server and allow short-circuiting the checks to return success while a memory heap dump is being taken, to prevent failing Kubernetes health checks from terminating the pod.

This program serves the endpoints on port 8585.

### Endpoints

The health and readiness probes forward to the default game server endpoints.

* `/healthz` forwards to `http://localhost:8888/healthz`.
* `/isReady` forwards to `http://Localhost:8888/isReady`.

The `/setOverride/<endpoint>?mode=<mode>` endpoint can be used to set temporary (30 min) overrides for a given endpoint:

* The valid values for `<endpoint>` are `healthz` and `isReady`.
* The valid values for `<mode>` are `Success`, `Failure` and `Disable`.
