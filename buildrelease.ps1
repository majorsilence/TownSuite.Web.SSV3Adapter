#!/usr/bin/env pwsh
$ErrorActionPreference = "Stop"
$CURRENTPATH=$pwd.Path

function delete_files([string]$path)
{
	If (Test-Path $path){
		Write-Host "Deleting path $path" -ForegroundColor Green
		Remove-Item -recurse -force $path
	}
}

function clean_bin_obj()
{
	cd "$CURRENTPATH"
	# DELETE ALL "BIN" and "OBJ" FOLDERS
	get-childitem -Include bin -Recurse -force | Remove-Item -Force -Recurse
	get-childitem -Include obj -Recurse -force | Remove-Item -Force -Recurse
}

function clean_build()
{
	# DELETE ALL ITEMS IN "BUILD" OUTPUT FOLDER
	Write-Host "Begin clean" -ForegroundColor Green
	if(!(Test-Path "build")){
		mkdir build
	}
	cd build
	Remove-Item * -Recurse -Force
	cd ..

	clean_bin_obj
}

function nuget_restore()
{
	if ($env:NUGET_CONFIG_FILE) {
		Write-Output "Using custom NuGet.Config file at $env:NUGET_CONFIG_FILE"
		dotnet restore "TownSuite.Web.SSV3Adapter.sln" --configfile "$env:NUGET_CONFIG_FILE"
	}
	else {
		Write-Output "Using default NuGet.Config file"
		dotnet restore "TownSuite.Web.SSV3Adapter.sln"
	}
}

function build()
{
	Write-Host "build TownSuite.Web.SSV3Adapter.sln" -ForegroundColor Green
	& dotnet build -c Release TownSuite.Web.SSV3Adapter.sln -p:GeneratePackageOnBuild=false
	if ($LastExitCode -ne 0) { throw "Building solution, TownSuite.Web.SSV3Adapter.sln, failed" }
}

function package_parameterproperties() {
	Write-Output "package_parameterproperties"

	#GITHASH="$(git rev-parse --short HEAD)"
	$GITHASH = git rev-parse --short HEAD
	[xml]$props = Get-Content "$CURRENTPATH/Directory.Build.props"
	$VERSION = ($props.Project.PropertyGroup.Version | Where-Object { $_ }) | Select-Object -First 1
	if ([string]::IsNullOrWhiteSpace($VERSION)) { throw "Could not read Version from Directory.Build.props" }

	delete_files "$CURRENTPATH/build/parameterproperties.txt"
	Add-Content "$CURRENTPATH/build/parameterproperties.txt" "VERSION=$VERSION"
	Add-Content "$CURRENTPATH/build/parameterproperties.txt" "GITHASH=$GITHASH"
	Add-Content "$CURRENTPATH/build/parameterproperties.txt" "GITHASH_FULL=$(git rev-parse HEAD)"

	Set-Location "$CURRENTPATH"
	$pwd.Path
}


clean_build
nuget_restore
build
package_parameterproperties
