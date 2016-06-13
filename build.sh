#!/bin/sh

set -e

mono .paket/paket.bootstrapper.exe
mono .paket/paket.exe restore
mono ./packages/FAKE/tools/FAKE.exe build.fsx GenerateApi

dotnet restore ./projects/client/RabbitMQ.Client
dotnet build ./projects/client/RabbitMQ.Client
