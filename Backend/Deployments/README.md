# Cloud Deployment Configuration

## Overview

This directory contains the Helm values files required for configuring your game servers in the cloud deployments.

Each environment should have a matching .yaml file to configure that specific environment. The game server Helm values files have a name that matches the environment used to configure with it: `<environmentName>-server.yaml`.

Note that not all of these are necessarily used, as typically projects start with the development environments only, and the staging and production environments are introduced at a later stage.

## Default Environments

When a new project is initialized, the Helm values files are generated for the game servers of the following Metaplay default environments:

* The `develop` environment is configured by `develop-server.yaml`.
* The `stable` environment is configured by `stable-server.yaml`.
* The `staging` environment is configured by `staging-server.yaml`.
* The `production` environment is configured by `production-server.yaml`.

## Configure a New Environment

It's easy to add new environments by copying one of the existing files. To create a Helm values file for a development environment named `feature1`, follow these steps:

1. Make a copy of `develop-server.yaml` and name it `feature1-server.yaml`.

2. Change the environment name inside the Helm values file to match the environment's name, so `environment: develop` becomes `environment: feature1`.

3. Optionally, you can modify the list of runtime options files that are used by the environment by modifying the list of files in `config.files`.

Note that you need to have a matching environment provisioned. Right now, you'll need to contact support but we are working on a feature that allows provisioning these yourself from the [developer portal](https://portal.metaplay.dev).
