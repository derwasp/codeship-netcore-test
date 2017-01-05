#!/usr/bin/env bash

echo "Running inside dind"

docker --version

docker run busybox echo hello

