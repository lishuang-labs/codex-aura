@echo off
cd /d "%~dp0"
if exist "DragonCodexBoot.exe" (
  start "" "DragonCodexBoot.exe" --preview
) else (
  start "" "%~dp0build\DragonCodexBoot\DragonCodexBoot.exe" --preview
)
