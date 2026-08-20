# Contributing guidelines

This document provides guidelines for contributing to the osdu-client project.

## Requesting changes

[Open a new issue](https://github.com/equinor/osdu-client/issues/new/choose).

## Making changes

1. Create a new branch. For external contributors, create a fork.

1. Make your changes.

1. Build and test your changes.

    ```sh
    dotnet build
    dotnet test source/Osdu.Client.Generator.Tests
    ```

1. Commit your changes.

    Use the [Conventional Commits](https://www.conventionalcommits.org/en/v1.0.0/) specification for semantic commit messages, where scope is the name of the affected package (e.g. `Osdu.Client` or `Osdu.Client.Extensions`).

    For example, if you've fixed a bug in the `Osdu.Client` package:

    ```plaintext
    fix(Osdu.Client): handle null response body in SearchApiClient
    ```

    If you've updated multiple packages or none in particular, don't specify a scope:

    ```plaintext
    chore: update .editorconfig
    ```

1. Create a pull request to merge your changes into the `main` branch.

    Use the Conventional Commits specification for semantic pull request titles.

## Reviewing changes

1. Ensure that the PR title follows the [Conventional Commits specification](https://www.conventionalcommits.org/en/v1.0.0/) and uses one of the types allowed in [`.commitlintrc.yml`](./.commitlintrc.yml).
1. Ensure the build and tests pass.
1. Ensure the change doesn't introduce a breaking change to `Osdu.Client` or `Osdu.Client.Extensions` without being called out as such.
