@ECHO OFF
set DOTNET_CLI_TELEMETRY_OPTOUT=1
dotnet restore --verbosity=normal .\RabbitMQDotNetClient.sln
dotnet build --verbosity=normal .\RabbitMQDotNetClient.sln
