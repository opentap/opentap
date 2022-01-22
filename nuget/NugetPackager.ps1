# Neatly packages platform-specific files into runtime directories so we can keep all the common files in the same place

function CopyPreserveRelativePath{
    param(
        # Relative path to original file
        [Parameter()]
        [string]$FilePath,
        # Base path that the file should be mirrored to
        [Parameter()]
        [string]$TargetBase
    )    
    # Create target directory if it does not exist
    if ( ! $(Test-Path $TargetBase)){
        New-Item -Force $TargetBase -ItemType Directory | Out-Null
    }
    Push-Location $TargetBase
    # Force touch the file to recursively create directories
    $newItem = New-Item -Force $FilePath
    Pop-Location
    $filename = Resolve-Path $FilePath
    Write-Output "Copying $filename to $($newItem.FullName)"
    # Copy the original file to the new location
    Copy-Item $filename $newItem
}

# Copy linux specific files
Push-Location linux-x64

$runtimeDir = "../nuget/build/runtimes/linux-x64"
$packageXml = Get-ChildItem -File -Recurse package.xml | Resolve-Path -Relative | Select-Object -First 1
CopyPreserveRelativePath $packageXml $runtimeDir

CopyPreserveRelativePath ./tap $runtimeDir
CopyPreserveRelativePath ./tap.dll $runtimeDir
CopyPreserveRelativePath ./tap.runtimeconfig.json $runtimeDir
Get-ChildItem -File -Recurse libgit2-*.so* | Resolve-Path -Relative | ForEach-Object { CopyPreserveRelativePath $_ $runtimeDir }

Pop-Location

# Copy win-x86 specific files
Push-Location win-x86
$runtimeDir = "../nuget/build/runtimes/win-x86"
$packageXml = Get-ChildItem -File -Recurse package.xml | Resolve-Path -Relative | Select-Object -First 1
CopyPreserveRelativePath $packageXml $runtimeDir

CopyPreserveRelativePath ./tap.exe  $runtimeDir
CopyPreserveRelativePath ./tap.dll $runtimeDir
CopyPreserveRelativePath ./tap.runtimeconfig.json $runtimeDir

$git2dll = Get-ChildItem -File -Recurse *git2-*.dll.86 | Resolve-Path -Relative | Select-Object -First 1
CopyPreserveRelativePath $git2dll $runtimeDir

Pop-Location

# Copy win-x64 specific files
Push-Location win-x64
$runtimeDir = "../nuget/build/runtimes/win-x64"
$packageXml = Get-ChildItem -File -Recurse package.xml | Resolve-Path -Relative | Select-Object -First 1
CopyPreserveRelativePath $packageXml $runtimeDir

CopyPreserveRelativePath ./tap.exe  $runtimeDir
CopyPreserveRelativePath ./tap.dll $runtimeDir
CopyPreserveRelativePath ./tap.runtimeconfig.json $runtimeDir

$git2dll = Get-ChildItem -File -Recurse *git2-*.dll.dll.64 | Resolve-Path -Relative | Select-Object -First 1
CopyPreserveRelativePath $git2dll $runtimeDir

Pop-Location
