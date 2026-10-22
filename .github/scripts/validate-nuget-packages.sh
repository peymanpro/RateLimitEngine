#!/usr/bin/env bash
set -euo pipefail

PACKAGE_VERSION="${1:?Usage: validate-nuget-packages.sh <version> [artifact-directory]}"
ARTIFACT_DIR="${2:-artifacts}"
PACKAGE_IDS=(
  "RateLimitEngine.Core"
  "RateLimitEngine.Algorithms"
  "RateLimitEngine.Redis"
  "RateLimitEngine.AspNetCore"
)

for package_id in "${PACKAGE_IDS[@]}"; do
  package="${ARTIFACT_DIR}/${package_id}.${PACKAGE_VERSION}.nupkg"
  symbols="${ARTIFACT_DIR}/${package_id}.${PACKAGE_VERSION}.snupkg"

  if [[ ! -s "$package" ]]; then
    echo "::error::Missing or empty NuGet package: $package" >&2
    exit 1
  fi

  if [[ ! -s "$symbols" ]]; then
    echo "::error::Missing or empty symbol package: $symbols" >&2
    exit 1
  fi
done

workspace_root="$PWD"
artifact_feed="$(cd "$ARTIFACT_DIR" && pwd)"
smoke_dir="$(mktemp -d)"
trap 'rm -rf "$smoke_dir"' EXIT

cat > "$smoke_dir/PackageSmokeTest.csproj" <<EOF
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFrameworks>net8.0;net10.0</TargetFrameworks>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>
  <ItemGroup>
    <FrameworkReference Include="Microsoft.AspNetCore.App" />
    <PackageReference Include="RateLimitEngine.Core" Version="$PACKAGE_VERSION" />
    <PackageReference Include="RateLimitEngine.Algorithms" Version="$PACKAGE_VERSION" />
    <PackageReference Include="RateLimitEngine.Redis" Version="$PACKAGE_VERSION" />
    <PackageReference Include="RateLimitEngine.AspNetCore" Version="$PACKAGE_VERSION" />
  </ItemGroup>
</Project>
EOF

cat > "$smoke_dir/Program.cs" <<'EOF'
using System.Reflection;

var assemblies = new[]
{
    "RateLimitEngine.Core",
    "RateLimitEngine.Algorithms",
    "RateLimitEngine.Redis",
    "RateLimitEngine.AspNetCore"
};

foreach (var name in assemblies)
{
    Assembly.Load(name);
    Console.WriteLine($"Loaded {name}");
}
EOF

dotnet restore "$smoke_dir/PackageSmokeTest.csproj" \
  --source "$artifact_feed" \
  --source "https://api.nuget.org/v3/index.json"

dotnet build "$smoke_dir/PackageSmokeTest.csproj" --configuration Release --no-restore
dotnet run --project "$smoke_dir/PackageSmokeTest.csproj" --framework net8.0 --configuration Release --no-build
dotnet run --project "$smoke_dir/PackageSmokeTest.csproj" --framework net10.0 --configuration Release --no-build

echo "Package validation and net8.0/net10.0 consumer smoke tests passed for version $PACKAGE_VERSION."
