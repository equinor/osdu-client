# OSDU Client

[![SCM Compliance]()](https://developer.equinor.com/governance/scm-policy/)

## Installation

The package is published to [GitHub Packages](https://github.com/equinor/osdu-client/packages).

Add the Equinor NuGet feed (once per machine), then install the package:

```sh
dotnet nuget add source "https://nuget.pkg.github.com/equinor/index.json" \
  --name equinor-github \
  --username <your-github-username> \
  --password <your-github-personal-access-token>

dotnet add package OsduClient
```

> The personal access token needs the `read:packages` scope. Generate one at [github.com/settings/tokens](https://github.com/settings/tokens).
