# Updating API Documentation

Updating the content in the `_site` submodule (which is this repository's `gh-pages` branch) is a manual process that must be done on Windows (as of docfx `2.31`):

```
cd C:\path\to\rabbitmq-dotnet-client`
git submodule update --init
pushd _site
git remote add origin-ssh git@github.com:rabbitmq/rabbitmq-dotnet-client.git
git fetch --all
git checkout --track origin-ssh/gh-pages
popd
.\build.bat
docfx.exe
pushd _site
git commit -a -m 'rabbitmq-dotnet-client docs vX.Y.Z'
git push origin-ssh
popd
git commit -a -m 'rabbitmq-dotnet-client docs vX.Y.Z'
git push
```
