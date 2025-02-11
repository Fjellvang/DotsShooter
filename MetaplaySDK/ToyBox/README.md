# Overview

This directory contains the scripts to build statically linked [ToyBox](https://github.com/landley/toybox) which is used to get `tar` application onto the runtime image.

The pre-built toybox binaries are found in the `output/` directory for convenience.

The `tar` binary is required by the `kubectl cp` command when copying files to/from the docker image.

The `tar` needs to be statically built as the chiseled runtime images do not have the dynamic runtime libraries present. We use musl-based build to create small portable binaries.

There's an [open issue](https://github.com/kubernetes/kubernetes/issues/58512) in the Kubernetes repository to remove the dependency on `tar`. Once that is fixed, this workaround is no longer needed.
