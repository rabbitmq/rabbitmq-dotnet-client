# Updating API Documentation

Updating the content in the `_site` submodule (which is this repository's `gh-pages` branch) is a manual process that must be done on Windows (as of docfx `2.31`):

```
cd C:\path\to\rabbitmq-dotnet-client`
git submodule update --init
.\build.bat
docfx.exe
cd _site
git commit -a -m 'rabbitmq-dotnet-client docs vX.Y.Z'
cd ..
git commit -a -m 'rabbitmq-dotnet-client docs vX.Y.Z'
```
