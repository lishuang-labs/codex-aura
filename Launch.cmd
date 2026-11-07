@echo off
cd /d "%~dp0"
if exist "DragonCodexBoot.exe" (
  start "" "DragonCodexBoot.exe" --launch
) else (
  start "" "%~dp0build\DragonCodexBoot\DragonCodexBoot.exe" --launch
)
