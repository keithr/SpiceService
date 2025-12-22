@echo off
REM SpiceService Tray - Batch Installer
REM Installs to user's LocalAppData without admin rights

setlocal

echo ========================================
echo SpiceService Tray Installer
echo ========================================
echo.

REM Get installation directory
set "INSTALL_DIR=%LocalAppData%\SpiceService\Tray"
set "START_MENU=%AppData%\Microsoft\Windows\Start Menu\Programs\SpiceService"
set "DESKTOP=%UserProfile%\Desktop"
set "STARTUP=%AppData%\Microsoft\Windows\Start Menu\Programs\Startup"

echo Installation directory: %INSTALL_DIR%
echo.

REM Check if running from installer directory
if not exist "SpiceServiceTray.exe" (
    echo ERROR: SpiceServiceTray.exe not found in current directory.
    echo Please run this script from the installer directory.
    pause
    exit /b 1
)

REM Create installation directory
echo Creating installation directory...
if not exist "%INSTALL_DIR%" mkdir "%INSTALL_DIR%"
if not exist "%INSTALL_DIR%\libraries" mkdir "%INSTALL_DIR%\libraries"

REM Copy files
echo Copying application files...
xcopy /E /I /Y "*.exe" "%INSTALL_DIR%\"
xcopy /E /I /Y "*.dll" "%INSTALL_DIR%\"
xcopy /E /I /Y "*.config" "%INSTALL_DIR%\"
xcopy /E /I /Y "runtimes" "%INSTALL_DIR%\runtimes\" 2>nul
if exist "libraries" xcopy /E /I /Y "libraries\*" "%INSTALL_DIR%\libraries\"

REM Create Start Menu folder
echo Creating Start Menu shortcuts...
if not exist "%START_MENU%" mkdir "%START_MENU%"

REM Create Start Menu shortcut
echo Set oWS = WScript.CreateObject("WScript.Shell") > "%TEMP%\createshortcut.vbs"
echo sLinkFile = "%START_MENU%\SpiceService Tray.lnk" >> "%TEMP%\createshortcut.vbs"
echo Set oLink = oWS.CreateShortcut(sLinkFile) >> "%TEMP%\createshortcut.vbs"
echo oLink.TargetPath = "%INSTALL_DIR%\SpiceServiceTray.exe" >> "%TEMP%\createshortcut.vbs"
echo oLink.WorkingDirectory = "%INSTALL_DIR%" >> "%TEMP%\createshortcut.vbs"
echo oLink.Description = "SpiceService Circuit Simulation Tray Application" >> "%TEMP%\createshortcut.vbs"
echo oLink.Save >> "%TEMP%\createshortcut.vbs"
cscript //nologo "%TEMP%\createshortcut.vbs"
del "%TEMP%\createshortcut.vbs"

REM Create Desktop shortcut (optional)
set /p CREATE_DESKTOP="Create Desktop shortcut? (Y/N): "
if /i "%CREATE_DESKTOP%"=="Y" (
    echo Set oWS = WScript.CreateObject("WScript.Shell") > "%TEMP%\createshortcut.vbs"
    echo sLinkFile = "%DESKTOP%\SpiceService Tray.lnk" >> "%TEMP%\createshortcut.vbs"
    echo Set oLink = oWS.CreateShortcut(sLinkFile) >> "%TEMP%\createshortcut.vbs"
    echo oLink.TargetPath = "%INSTALL_DIR%\SpiceServiceTray.exe" >> "%TEMP%\createshortcut.vbs"
    echo oLink.WorkingDirectory = "%INSTALL_DIR%" >> "%TEMP%\createshortcut.vbs"
    echo oLink.Description = "SpiceService Circuit Simulation Tray Application" >> "%TEMP%\createshortcut.vbs"
    echo oLink.Save >> "%TEMP%\createshortcut.vbs"
    cscript //nologo "%TEMP%\createshortcut.vbs"
    del "%TEMP%\createshortcut.vbs"
    echo Desktop shortcut created.
)

REM Create Startup shortcut (optional)
set /p CREATE_STARTUP="Start automatically on login? (Y/N): "
if /i "%CREATE_STARTUP%"=="Y" (
    echo Set oWS = WScript.CreateObject("WScript.Shell") > "%TEMP%\createshortcut.vbs"
    echo sLinkFile = "%STARTUP%\SpiceService Tray.lnk" >> "%TEMP%\createshortcut.vbs"
    echo Set oLink = oWS.CreateShortcut(sLinkFile) >> "%TEMP%\createshortcut.vbs"
    echo oLink.TargetPath = "%INSTALL_DIR%\SpiceServiceTray.exe" >> "%TEMP%\createshortcut.vbs"
    echo oLink.WorkingDirectory = "%INSTALL_DIR%" >> "%TEMP%\createshortcut.vbs"
    echo oLink.Description = "SpiceService Circuit Simulation Tray Application" >> "%TEMP%\createshortcut.vbs"
    echo oLink.Save >> "%TEMP%\createshortcut.vbs"
    cscript //nologo "%TEMP%\createshortcut.vbs"
    del "%TEMP%\createshortcut.vbs"
    echo Startup shortcut created.
)

echo.
echo ========================================
echo Installation complete!
echo ========================================
echo.
echo Application installed to: %INSTALL_DIR%
echo.
set /p LAUNCH="Launch SpiceService Tray now? (Y/N): "
if /i "%LAUNCH%"=="Y" (
    start "" "%INSTALL_DIR%\SpiceServiceTray.exe"
)

echo.
pause

