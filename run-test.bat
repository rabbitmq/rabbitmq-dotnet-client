@echo off
set DOTNET_CLI_TELEMETRY_OPTOUT=1
dotnet test --no-build --logger "console;verbosity=detailed" ./RabbitMQDotNetClient.sln
