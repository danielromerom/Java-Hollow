@echo off
if not exist "%~dp0Builds\VRCheck\JavaHollowVRCheck.exe" (
  echo The standalone VR check build has not finished successfully yet.
  pause
  exit /b 1
)
start "Java Hollow standalone VR check" "%~dp0Builds\VRCheck\JavaHollowVRCheck.exe" -force-d3d11
