@echo off
setlocal
cd /d "%~dp0"

set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" set "CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"

if not exist "%CSC%" (
    echo ERROR: csc.exe not found.
    echo.
    pause
    exit /b 1
)

echo Building SendToBackClick.exe...
echo.

"%CSC%" /nologo /target:winexe /platform:anycpu /optimize+ /out:"SendToBackClick.exe" /r:System.Windows.Forms.dll /r:System.Drawing.dll "SendToBackClick.cs"

if errorlevel 1 (
    echo.
    echo ========================================
    echo BUILD FAILED.
    echo ========================================
    echo.
    pause
    exit /b 1
)

echo.
echo ========================================
echo OK: SendToBackClick.exe created.
echo ========================================
echo.
echo Launch the EXE by double-clicking it.
echo Left-click on - : normal behavior
echo Right-click on -: send the window to the back
echo.
pause
