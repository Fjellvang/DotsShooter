#!/bin/bash

build_toybox() {
  ARCH=$1
  OUTPUT_BINARY_PATH="output/toybox-${ARCH}"
  IMAGE_NAME="toybox-${ARCH}"

  # Build toybox using docker
  docker buildx build --platform linux/${ARCH} -t ${IMAGE_NAME} .

  # Extract the toybox binary from the image
  CONTAINER_ID=$(docker create $IMAGE_NAME)
  docker cp ${CONTAINER_ID}:/toybox ${OUTPUT_BINARY_PATH}
  docker rm $CONTAINER_ID

  echo "Statically linked toybox ${ARCH} binary has been copied to ${OUTPUT_BINARY_PATH}"
}

# Remove the old binaries (in case the build fails)
rm -rf output
mkdir -p output

# Build for amd64 and arm64
build_toybox amd64
build_toybox arm64
