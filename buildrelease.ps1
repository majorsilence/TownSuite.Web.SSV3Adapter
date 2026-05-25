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
	dotnet restore "TownSuite.Web.SSV3Adapter.sln"
}

function build()
{
	Write-Host "build TownSuite.Web.SSV3Adapter.sln" -ForegroundColor Green
	& dotnet build -c Release TownSuite.Web.SSV3Adapter.sln -p:GeneratePackageOnBuild=false
	if ($LastExitCode -ne 0) { throw "Building solution, TownSuite.Web.SSV3Adapter.sln, failed" }
}



clean_build
nuget_restore
build

