#!/bin/sh
set -e

if [ -z "$1" ]; then
    echo "Please provide a version number"
    exit 1
fi

rm -rf publish/*

for runtime in win-x64 win-x86 win-arm win-arm64 linux-x64 linux-musl-x64 linux-arm linux-arm64 osx-x64; do
    DIR=publish/mlr_"$1"_"$runtime"
    mkdir -p "$DIR"
    rm -rf "$DIR"/*
    # See https://github.com/dotnet/sdk/issues/5575#issuecomment-271062056
    # for -p:DebugType=None. This prevent the .pdb files from being added to the publish output
    dotnet publish -r "$runtime" -o "$DIR" -c Release -p:DebugType=None --self-contained

    zip -r -j "$DIR".zip -r "$DIR"/*
    rm -rf "$DIR"
done
