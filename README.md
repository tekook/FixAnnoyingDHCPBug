# FixAnnoyingDHCPBug

Fix for DHCP gateway bug introduced in March Windows 11 Pro upgrade (still unfixed).

## About
Windows 11 DHCP sometimes assigns IP but no default gateway, breaking connectivity.
This bug was introduced in March 2026 and offizially has been patched.
Still multiple of my Windows PCs have a hard time getting a default gateway after boot.
For me it happens on multiple networks (does not matter if wired or wireless) and for multiple DHCP-Servers.
Since some of my collegues had the same error, I decided to create this little service to ensure the Gateway is set after boot.

## Features
- Configurable interfaces
- Auto-bounce on failure
- Runs as service to ensure admin privilegies and running at boot
- Logs to Event Log

## Manual installation
Build the app and run as Administrator:
```
FixAnnoyingDHCPBug.exe /Install
```
This will install the service and register the app.

To uninstall:
```
FixAnnoyingDHCPBug.exe /Uninstall
```

## MSI Installation
Use the provided installer for automatic installed.
Update the appsettings.json to your need in your installation folder.

## Configuration (appsettings.json)
```json
{
  "ServiceSettings": {
    "InterfaceNames": ["Ethernet", "Ethernet2", "WLAN"],
    "BounceDelaySeconds": 15,
    "MaxRetries": 5,
    "PeriodDelay": 10
  }
}
```
- `InterfaceNames`: Check with `netsh interface show interface`
- `PeriodDelay`: This is the delay between retries of the service. This is also the initial delay after the service starts.
- `BounceDelaySeconds`: How much seconds to wait between disabling and re-enabling the interface.

## Build
Open `FixAnnoyingDHCPBug.slnx` in Visual Studio or `dotnet build`.

## Word about AI-Usage
I used an AI to help me create this ReadMe.md and for research on the bug. Not for coding.
I created an Powershell Script back in April for the same solution, but was tired of manually running it, since TaskScheduler sometimes stumblen on System-Boot-Tasks.

## License
MIT