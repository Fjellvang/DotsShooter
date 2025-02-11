# Use an official Ubuntu as a parent image
FROM ubuntu:24.04 AS build

# Configure toybox version to use
ARG TOYBOX_VERSION=0.8.11

# Install necessary packages for building toybox
RUN apt-get update && apt-get install -y build-essential wget zlib1g-dev musl-tools

# Download and extract toybox source code
WORKDIR /build
RUN <<EOF
  wget https://github.com/landley/toybox/archive/refs/tags/${TOYBOX_VERSION}.tar.gz
  tar xf ${TOYBOX_VERSION}.tar.gz
EOF

# Build the toybox binary and copy to root
# \todo I wasn't able to get the gzipping to work and gave up on it as 'kubectl cp' does not compress the archives:
#       https://github.com/kubernetes/kubectl/blob/85119a1e563810081ee6f092b3265eea42ad2f40/pkg/cmd/cp/cp.go#L374
RUN <<EOF
  echo "Build Toybox v${TOYBOX_VERSION}..."
  cd toybox-${TOYBOX_VERSION}
  make allnoconfig
  echo "CONFIG_TOYBOX=y" >> .config
  echo "CONFIG_TAR=y" >> .config
  # echo "CONFIG_GZIP=y" >> .config
  CC=musl-gcc CFLAGS="-Os -static" LDFLAGS="--static" make
  cp ./toybox /toybox
EOF

# Test the binary
RUN <<EOF
  ln -sf /toybox /usr/bin/tar
  # ln -sf /toybox /usr/bin/gzip
  tar -cf test-out.tar /usr/bin/gre*
  # tar -cf test-out.tgz /usr/bin/gre*
  ls -la
EOF
