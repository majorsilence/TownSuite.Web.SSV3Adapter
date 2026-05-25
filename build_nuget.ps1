#!/bin/pwsh
$ErrorActionPreference = "Stop"
$CURRENTPATH = $pwd.Path

dotnet pack TownSuite.Web.SSV3Adapter.sln --no-build -c=Release -p:Platform="Any CPU" --output "$CURRENTPATH/build"