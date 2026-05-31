# FixAnnoyingDHCPBug

Fix for DHCP gateway bug introduced in March Windows 11 Pro upgrade (still unfixed).

## About
Windows 11 DHCP sometimes assigns an IP but no default gateway, breaking connectivity.
This bug was introduced in March 2026 and officially has been patched.
Still, multiple of my Windows PCs have a hard time getting a default gateway after boot.
For me, it happens on multiple networks (does not matter if wired or wireless) and for multiple DHCP servers.
Since some of my colleagues had the same error, I decided to create this little service to ensure the gateway is set after boot.

## Features
* Configurable interfaces
* Auto-bounce on failure
* Runs as a service to ensure admin privileges and running at boot
* Logs to Event Log

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

Use the provided installer for automatic installation.
Update the appsettings.json to your needs in your installation folder.

## Configuration (appsettings.json)

```json
{
  "ServiceSettings": {
    "InterfaceNames": ["Ethernet", "Ethernet2", "WLAN"],
    "BounceDelay": 15,
    "MaxRetries": 5,
    "PeriodDelay": 10,
    "InitialDelay": 30
  }
}

```

* `InterfaceNames`: Check with `netsh interface show interface`

* `PeriodDelay`: This is the delay between retries of the service.


* `BounceDelay`: How many seconds to wait between disabling and re-enabling the interface.


* `InitialDelay`: Delay after service start or resume from PowerEvent



## Build

Open `FixAnnoyingDHCPBug.slnx` in Visual Studio or run `dotnet build`.

## Word about AI-Usage

I used an AI to help me create this README.md and for research on the bug, not for coding.
I created a PowerShell script back in April for the same solution, but was tired of manually running it, since Task Scheduler sometimes stumbled on System-Boot-Tasks.

## License

MIT