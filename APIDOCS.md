# Updating API Documentation

Updating the content in the `_site` submodule (which is this repository's `gh-pages` branch) is a manual process that must be done on Windows (as of docfx `2.31`):

* `git submodule update --init` in this project
* Run `docfx.exe` 2.31 or later
* `cd _site` and commit all changes
* `cd ..` and commit updated `_site` submodule
