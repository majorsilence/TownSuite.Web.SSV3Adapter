$cwd = $PSScriptRoot


function RunVerionUpdater($loc, $path){
	If ($IsWindows) {
	    & "./tools/AssemblyInfoUtil.exe" -inc:$loc "$path"
	}
	else {
		assemblyinfoutil -inc:$loc "$path"
	}	
}

RunVerionUpdater 3 "$cwd/Directory.Build.props"
