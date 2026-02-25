# RabbitMQ .NET Client release process

## Ensure builds are green:

* [GitHub Actions](https://github.com/rabbitmq/rabbitmq-dotnet-client/actions)


## Update API documentation

Note: `main` (`6.x` and later) only

Please see [this guide](https://github.com/rabbitmq/rabbitmq-dotnet-client/blob/main/APIDOCS.md).


## Create and push release tag

Note: `alpha` releases are versioned by default via the MinVer package. The version is based off of the most recent tag.

RC release:

```
git tag -a -s -u B1B82CC0CF84BA70147EBD05D99DE30E43EAE440 -m 'rabbitmq-dotnet-client v7.X.Y-rc.1' 'v7.X.Y-rc.1'
```

Final release:

```
git tag -a -s -u B1B82CC0CF84BA70147EBD05D99DE30E43EAE440 -m 'rabbitmq-dotnet-client v7.X.Y' 'v7.X.Y'
```

Push!

```
git push --tags
```

## `6.x` branch


### Trigger build locally

```
cd path\to\rabbitmq-dotnet-client
git checkout v6.X.Y
git clean -xffd
.\build.bat
dotnet build ./RabbitMQDotNetClient.sln --configuration Release --property:CONCOURSE_CI_BUILD=true
dotnet nuget push -k NUGET_API_KEY -s https://api.nuget.org/v3/index.json ./packages/RabbitMQ.Client.6.X.Y.nupkg
```

## `main` (`7.x`) branch

* Close the appropriate milestone, and make a note of the link to the milestone with closed issues visible
* Use the GitHub web UI or `gh release create` command to create the new release
* GitHub actions will build and publish the release to NuGet


## Update CHANGELOG

Note: this does not seem to work well, please examine the output carefully.

`--since-tag` should be the previous tag.

```
github_changelog_generator --token github_pat_XXXX \
    --user rabbitmq --project rabbitmq-dotnet-client \
    --no-unreleased --release-branch main --since-tag 'v7.X.Y'
```
