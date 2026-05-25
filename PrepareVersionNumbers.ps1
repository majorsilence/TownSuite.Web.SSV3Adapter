$cwd = $PSScriptRoot


function RunVerionUpdater($loc, $path){
	If ($IsWindows) {
	    & "./tools/AssemblyInfoUtil.exe" -inc:$loc "$path"
	}
	else {
		assemblyinfoutil -inc:$loc "$path"
	}	
}

RunVerionUpdater 3 "$cwd/TownSuite.Web.SSV3Adapter/TownSuite.Web.SSV3Adapter.csproj"
RunVerionUpdater 3 "$cwd/TownSuite.Web.SSV3Adapter.Interfaces/TownSuite.Web.SSV3Adapter.Interfaces.csproj"
RunVerionUpdater 3 "$cwd/TownSuite.Web.SSV3Adapter.Prometheus/TownSuite.Web.SSV3Adapter.Prometheus.csproj"
