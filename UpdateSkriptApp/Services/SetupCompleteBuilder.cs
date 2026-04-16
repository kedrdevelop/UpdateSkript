using System;
using System.IO;

namespace UpdateSkriptApp.Services;

public class SetupCompleteBuilder : ISetupCompleteBuilder
{
    private readonly IFileSystem _fileSystem;

    public SetupCompleteBuilder(IFileSystem fileSystem)
    {
        _fileSystem = fileSystem;
    }

    public void InjectSetupCompleteCmd()
    {
        string scriptsDir = @"C:\Windows\Setup\Scripts";
        if (!_fileSystem.DirectoryExists(scriptsDir))
        {
            _fileSystem.CreateDirectory(scriptsDir);
        }

        string cmdPath = Path.Combine(scriptsDir, "SetupComplete.cmd");

        string content = @"@echo off
set LOG=C:\SetupComplete_Debug.log
echo [%DATE% %TIME%] Starting SetupComplete cleanup... > %LOG%

:: 1. Force Registry Overrides for ReserveManager
echo [%DATE% %TIME%] Killing ReserveManager scenarios... >> %LOG%
reg add ""HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\ReserveManager"" /v ""ActiveScenario"" /t REG_DWORD /d 0 /f >> %LOG% 2>&1
reg add ""HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\ReserveManager"" /v ""ShippedWithReserves"" /t REG_DWORD /d 0 /f >> %LOG% 2>&1

:: 2. Wait Loop for Reserved Storage
echo [%DATE% %TIME%] Attempting to disable Reserved Storage via DISM... >> %LOG%
for /L %%i in (1,1,10) do (
    echo [%DATE% %TIME%] Attempt %%i of 10... >> %LOG%
    DISM.exe /Online /Set-ReservedStorageState /State:Disabled >> %LOG% 2>&1
    if not errorlevel 1 (
        echo [%DATE% %TIME%] SUCCESS: Reserved Storage disabled. >> %LOG%
        goto DISM_SUCCESS
    )
    echo [%DATE% %TIME%] FAILED: Still in use. Waiting 20s... >> %LOG%
    timeout /t 20 >nul
)
echo [%DATE% %TIME%] CRITICAL: DISM failed after 10 attempts. System might be unstable. >> %LOG%

:DISM_SUCCESS
:: 3. Cleanup defaultuser0 and deployment flags
echo [%DATE% %TIME%] Cleaning up defaultuser0 and flags... >> %LOG%
net user defaultuser0 /delete >> %LOG% 2>&1
rd /s /q ""C:\Users\defaultuser0"" >> %LOG% 2>&1
reg delete ""HKLM\SYSTEM\Setup\Upgrade"" /f >> %LOG% 2>&1

del /f /q ""%PUBLIC%\UpdateSkript_*.flag"" >> %LOG% 2>&1

:: 4. Final Sysprep (Generalize to OOBE)
echo [%DATE% %TIME%] Triggering Sysprep... >> %LOG%
%WINDIR%\system32\sysprep\sysprep.exe /oobe /generalize /shutdown >> %LOG% 2>&1
";
        _fileSystem.WriteAllText(cmdPath, content);
    }
}
