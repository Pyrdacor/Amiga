$ErrorActionPreference = 'Stop';

if ($isWindows) {
} elseif ($isLinux) {
} else {
  Write-Host Publish Mac executables
  dotnet publish -c Release ./AmigaAdf/AmigaAdf.csproj -p:PublishSingleFile=true -p:IncludeAllContentForSelfExtract=true -r osx-x64 --no-restore --nologo --self-contained
  dotnet publish -c Release ./AmigaAdf/AmigaAdf.csproj -p:PublishSingleFile=true -p:IncludeAllContentForSelfExtract=true -r osx-arm64 --no-restore --nologo --self-contained
  dotnet publish -c Release ./AmigaLha/AmigaLha.csproj -p:PublishSingleFile=true -p:IncludeAllContentForSelfExtract=true -r osx-x64 --no-restore --nologo --self-contained
  dotnet publish -c Release ./AmigaLha/AmigaLha.csproj -p:PublishSingleFile=true -p:IncludeAllContentForSelfExtract=true -r osx-arm64 --no-restore --nologo --self-contained
  Write-Host Pack zips for Mac
  sudo xcode-select -s /Applications/Xcode-13.2.1.app
  Start-Process -FilePath codesign -Wait -WorkingDirectory . -ArgumentList '-s','-','--force','--verbose','--deep','--no-strict','"./AmigaAdf/bin/Any CPU/Release/net8.0/osx-x64/publish/AmigaAdf"'
  Start-Process -FilePath codesign -Wait -WorkingDirectory . -ArgumentList '-s','-','--force','--verbose','--deep','--no-strict','"./AmigaLha/bin/Any CPU/Release/net8.0/osx-x64/publish/AmigaLha"'
  7z a AmigaTools-Mac-x64.zip "./AmigaAdf/bin/Any CPU/Release/net8.0/osx-x64/publish/AmigaAdf" -mx9
  7z a AmigaTools-Mac-x64.zip "./AmigaLha/bin/Any CPU/Release/net8.0/osx-x64/publish/AmigaLha" -mx9
  Start-Process -FilePath codesign -Wait -WorkingDirectory . -ArgumentList '-s','-','--force','--verbose','--deep','--no-strict','"./AmigaAdf/bin/Any CPU/Release/net8.0/osx-arm64/publish/AmigaAdf"'
  Start-Process -FilePath codesign -Wait -WorkingDirectory . -ArgumentList '-s','-','--force','--verbose','--deep','--no-strict','"./AmigaLha/bin/Any CPU/Release/net8.0/osx-arm64/publish/AmigaLha"'
  7z a AmigaTools-Mac-Arm.zip "./AmigaAdf/bin/Any CPU/Release/net8.0/osx-arm64/publish/AmigaAdf" -mx9
  7z a AmigaTools-Mac-Arm.zip "./AmigaLha/bin/Any CPU/Release/net8.0/osx-arm64/publish/AmigaLha" -mx9
}
