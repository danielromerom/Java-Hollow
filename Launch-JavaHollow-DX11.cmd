@echo off
rem Save and close this project's current Unity Editor before launching.
rem Session-only graphics API test; does not change Player or quality settings.
start "Java Hollow DX11 test" "C:\Unity\6000.2.9f1\Editor\Unity.exe" -projectPath "%~dp0." -force-d3d11
