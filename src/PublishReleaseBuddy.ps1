#Declaring & setting some variables
$scriptPath = split-path -parent $MyInvocation.MyCommand.Definition
$publishPath = $scriptPath + "\_PublishTemp_\"

$changeVersion = Read-Host "Change version? Y/N"

#---------------------------------
# Pre-checks
#---------------------------------
#Checking destination folder first
if (($env:buddyReleaseDestinationFolderPath -eq $null) -or (-not (Test-Path $env:buddyReleaseDestinationFolderPath))) {
	Write-Host "Fatal error. Destination folder does not exists. Please set environment variable 'buddyReleaseDestinationFolderPath' to a valid value" -foregroundcolor "Red"
	exit
}

#-----------------------------------
# Release version management BIOSBuddy
#-----------------------------------
Write-Host "Starting release version management" -foregroundcolor "Green"
#Get Path to csproj
$projectFilePath = $scriptPath + "\BIOSBuddy\BIOSBuddy.csproj"
If (-not(Test-Path $projectFilePath)) {
	Write-Host "Fatal error. Project path not found: $projectPath" -foregroundcolor "Red"
	exit
}

if ($changeVersion.Trim().ToLower().Equals("y")) {
	#Reading BIOSBuddy project file
	$xml = [xml](Get-Content $projectFilePath)
	[string]$assemblyVersion = $xml.Project.PropertyGroup.AssemblyVersion

	#Split the Version Numbers
	$avMajor, $avMinor, $avPatch = $assemblyVersion.Split('.')

	Write-Host "Current assembly version is: $assemblyVersion" -foregroundcolor "Green"
	Write-Host "Current Minor version is: $avMinor" -foregroundcolor "Green"

	#Sets new version into Project 
	#Warning: for this to work, since the PropertyGroup is indexed, AssemblyVersion must be in the FIRST Propertygroup (or else, change the index).
	Write-Host "What kind of release is this? If not minor then patch version will be incremented." -foregroundcolor "Green"
	$isMinorRelease = Read-Host "Minor release? Y/N"

	if ($isMinorRelease.Trim().ToLower().Equals("y")) {
		[int]$avMinor = [int]$avMinor + 1
		[int]$avPatch = 0
	}
	else {
		[int]$avPatch = [int]$avPatch + 1
	}

	$xml.Project.PropertyGroup.AssemblyVersion = "$avMajor.$avMinor.$avPatch".Trim()
	[string]$assemblyVersion = $xml.Project.PropertyGroup.AssemblyVersion
	Write-Host "New assembly version is $assemblyVersion" -foregroundcolor "Green"
	#Saving project file
	$xml.Save($projectFilePath)
	Write-Host "Project file updated" -foregroundcolor "Green"
	Write-Host "Finished release version management" -foregroundcolor "Green"
}

#---------------------------------
# Tests execution For BIOSBuddy
#---------------------------------
<#Write-Host "Starting tests execution for BIOSBuddy" -foregroundcolor "Green"
$testPath = $scriptPath + "\Tests"
Set-Location -Path $testPath
dotnet test
$testsLastExitCode = $LastExitCode
Write-Host "Tests LastExitCode: $testsLastExitCode" -foregroundcolor "Green"
if ( 0 -ne $testsLastExitCode ) {
	Write-Host "Fatal error. Some unit tests failed." -foregroundcolor "Red"
	exit
}
Write-Host "Finished tests execution for BIOSBuddy" -foregroundcolor "Green"
#>

#---------------------------------
# Publish-Build & Zip
#---------------------------------
#Cleaning previous publish
Write-Host "Removing old files public folder" -foregroundcolor "Green"
Remove-Item $publishPath\*.*

Write-Host "Starting cleaning previous build" -foregroundcolor "Green"
Set-Location -Path $scriptPath
dotnet clean BIOSBuddy\BIOSBuddy.csproj -o $publishPath

Write-Host "Starting Publish" -foregroundcolor "Green"
Set-Location -Path $scriptPath

Write-Host "Starting Publish BIOSBuddy" -foregroundcolor "Green"
dotnet publish BIOSBuddy\BIOSBuddy.csproj --self-contained false -f net8.0-windows -r win-x64 -c Release -o $publishPath /p:DebugType=None /p:DebugSymbols=false

$buildLastExitCode = $LastExitCode

Write-Host "Build ControlRef LastExitCode: $buildLastExitCode" -foregroundcolor "Green"

if ( 0 -ne $buildLastExitCode ) {
	Write-Host "Fatal error. Build seems to have failed on BIOSBuddy. No Zip & copy will be done." -foregroundcolor "Red"
	exit
}

#remove 'EmptyFiles' Folder
Write-Host "Removing EmptyFiles folder" -foregroundcolor "Green"
Remove-Item $publishPath\EmptyFiles -Force  -Recurse -ErrorAction SilentlyContinue

#Getting file info & remove revision from file_version
Write-Host "Getting file info" -foregroundcolor "Green"
$file_version = (Get-Command $publishPath\BIOSBuddy.exe).FileVersionInfo.FileVersion
Write-Host "File version is $file_version" -foregroundcolor "Green"

#Compressing release folder to destination
Write-Host "Destination for zip file:" $env:buddyReleaseDestinationFolderPath"\BIOSBuddy_x64_$file_version.zip" -foregroundcolor "Green"
Compress-Archive -Force -Path $publishPath\* -DestinationPath $env:buddyReleaseDestinationFolderPath"\BIOSBuddy_x64_$file_version.zip"

Write-Host "Finished publishing release version" -foregroundcolor "Green"

Write-Host "Script end" -foregroundcolor "Green"
