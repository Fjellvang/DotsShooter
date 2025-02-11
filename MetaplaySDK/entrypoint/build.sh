#!/bin/bash

build_entrypoint() {
  ARCH=$1
  OUTPUT_BINARY_PATH="output/entrypoint-${ARCH}"
  IMAGE_NAME="entrypoint-${ARCH}"

  # Build entrypoint using docker
  docker buildx build --platform linux/${ARCH} -t ${IMAGE_NAME} .

  # Extract the binary from the image
  CONTAINER_ID=$(docker create $IMAGE_NAME)
  docker cp ${CONTAINER_ID}:/entrypoint ${OUTPUT_BINARY_PATH}
  docker rm $CONTAINER_ID

  echo "Statically linked entrypoint ${ARCH} binary has been copied to ${OUTPUT_BINARY_PATH}"
}

# Remove the old binaries (in case the build fails)
rm -rf output
mkdir -p output

# Build for amd64 and arm64
build_entrypoint amd64
build_entrypoint arm64
