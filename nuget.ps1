$ErrorActionPreference = 'Stop';

if ($isWindows) {
  Write-Host Copy nuget packages
  mkdir "nuget"
  xcopy /Y /I "Amiga.FileFormats.ADF\bin\Release\*.nupkg" "nuget\*"
  xcopy /Y /I "Amiga.FileFormats.LHA\bin\Release\*.nupkg" "nuget\*"
}
