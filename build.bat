@ECHO OFF
set DOTNET_CLI_TELEMETRY_OPTOUT=1
dotnet restore .\RabbitMQDotNetClient.sln
dotnet build .\RabbitMQDotNetClient.sln
