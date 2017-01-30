@echo off
cls
pushd "%~dp0"
call paket restore
if errorlevel 1 (
  exit /b %errorlevel%
)

"packages\build\FAKE\tools\Fake.exe" build.fsx %*
popd