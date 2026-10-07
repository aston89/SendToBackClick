# SendToBackClick

A tiny Windows utility that adds one simple function to the standard minimize button:

* **Left-click `—`** → normal minimize (default behaviour)
* **Right-click `—`** → send the window behind all other windows 

The window stays open and is **not minimized**.

## Why?

Windows has a built-in way to minimize a window, but no equally convenient way to simply put it behind the others.

SendToBackClick adds exactly that, without adding extra title-bar buttons, modifying the shell, or requiring AutoHotkey or a full window manager.

## Features

* Lightweight background utility
* No configuration required
* No tray UI
* No AutoHotkey
* Uses native Win32 window management
* Normal left-click behavior is unchanged

## Usage

Run:

```text
SendToBackClick.exe
```

Then right-click the **minimize (`—`) button** of any standard window.

To minimize normally, left-click it.

## Build

No .NET SDK is required.

Run:

```text
build-SendToBackClick.bat
```

The included build script uses the C# compiler already provided by the Windows .NET Framework.

## Startup

To launch automatically with Windows:

```text
Win + R → shell:startup
```

Create a shortcut to `SendToBackClick.exe` in that folder.

## Compatibility

Designed primarily for standard Windows title bars on **Windows 10 and Windows 11**.
Applications with completely custom title bars may not support the same behavior.
