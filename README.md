# OSDU Client

[![SCM Compliance](https://scm-compliance-api.radix.equinor.com/repos/equinor/b7f13276-ed4d-4e8f-a084-9dd9c2377080/badge)](https://developer.equinor.com/governance/scm-policy/)

## Installation

The packages are published to [GitHub Packages](https://github.com/equinor/osdu-client/packages):

- `Osdu.Client` - the core OSDU API client.
- `Osdu.Client.Extensions` - optional extensions (e.g. caching, querying helpers) built on top of `Osdu.Client`.

Add the Equinor NuGet feed (once per machine), then install the package(s) you need:

```sh
dotnet nuget add source "https://nuget.pkg.github.com/equinor/index.json" \
  --name equinor-github \
  --username <your-github-username> \
  --password <your-github-personal-access-token>

dotnet add package Osdu.Client
dotnet add package Osdu.Client.Extensions
```

> The personal access token needs the `read:packages` scope. Generate one at [github.com/settings/tokens](https://github.com/settings/tokens).

## Contributing

See [CONTRIBUTING.md](./CONTRIBUTING.md) for guidelines on how to contribute to this project.
