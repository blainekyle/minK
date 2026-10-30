param(
    [string]$Version = "1.0.0",
    [switch]$SkipTests
)

$ErrorActionPreference = "Stop"

$RepoRoot = Split-Path -Parent $PSScriptRoot
$ProjectFile = Join-Path $RepoRoot "minK\minK.csproj"
$SolutionFile = Join-Path $RepoRoot "minK.sln"
$StagingDir = Join-Path $PSScriptRoot "staging\win-x64"
$DistDir = Join-Path $RepoRoot "dist"
$IssFile = Join-Path $PSScriptRoot "minK.iss"

Write-Host "==> [1/4] Locating Inno Setup 6 compiler (ISCC.exe)..." -ForegroundColor Cyan
$IsccCandidates = @(
    @(
        (Get-Command iscc -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Source -ErrorAction SilentlyContinue),
        "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe",
        "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
        "$env:ProgramFiles\Inno Setup 6\ISCC.exe"
    ) | Where-Object { $_ -and (Test-Path $_) }
)

if (-not $IsccCandidates -or $IsccCandidates.Count -eq 0) {
    throw "Inno Setup 6 (ISCC.exe) not found. Install via: winget install --id JRSoftware.InnoSetup -e --scope user"
}
$IsccExe = $IsccCandidates[0]
Write-Host "    Found ISCC: $IsccExe" -ForegroundColor Gray

if (-not $SkipTests) {
    Write-Host "==> [2/4] Running unit test suite..." -ForegroundColor Cyan
    dotnet test $SolutionFile -c Release --nologo
    if ($LASTEXITCODE -ne 0) { throw "Unit tests failed." }
} else {
    Write-Host "==> [2/4] Skipping unit tests (-SkipTests)." -ForegroundColor Yellow
}

Write-Host "==> [3/4] Publishing self-contained win-x64 payload..." -ForegroundColor Cyan
if (Test-Path $StagingDir) {
    Remove-Item $StagingDir -Recurse -Force
}
New-Item -ItemType Directory -Path $StagingDir -Force | Out-Null
New-Item -ItemType Directory -Path $DistDir -Force | Out-Null

dotnet publish $ProjectFile `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:Version=$Version `
    -p:DebugType=None `
    -p:DebugSymbols=false `
    -o $StagingDir

if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed." }

# Ensure POSIX LF line endings on Git Bash CLI shim
$ShShim = Join-Path $PSScriptRoot "bin\mink"
$ShContent = [System.IO.File]::ReadAllText($ShShim).Replace("`r`n", "`n")
[System.IO.File]::WriteAllText($ShShim, $ShContent, (New-Object System.Text.UTF8Encoding($false)))

Write-Host "==> [4/4] Compiling Windows installer package..." -ForegroundColor Cyan
& $IsccExe "/DMyAppVersion=$Version" "/DPublishDir=$StagingDir" "/DOutputDirectory=$DistDir" $IssFile
if ($LASTEXITCODE -ne 0) { throw "Inno Setup compilation failed." }

$InstallerPath = Join-Path $DistDir "minK-Setup-$Version-win-x64.exe"
$SizeMb = [Math]::Round((Get-Item $InstallerPath).Length / 1MB, 2)
Write-Host ""
Write-Host "SUCCESS: Built installer -> $InstallerPath ($SizeMb MB)" -ForegroundColor Green
