# EA Hide Connector Labels

A small Enterprise Architect add-in. Every time you draw a new relationship
(connector) between two elements on a diagram, its labels (name, role names, stereotype such as «FK») are hidden automatically.
The multiplicities at each end (1, 0..*, *) stay visible.

Built for EA 15.1 (32-bit). Installs for the current Windows user only, no admin rights needed.

## Install

```powershell
powershell -ExecutionPolicy Bypass -File C:\dev\oxyum\EAHideConnectorLabels\install.ps1
```

Then close and reopen Enterprise Architect. Check it under
the **Specialize** ribbon > **Manage-Add-Ins**: `EAHideConnectorLabels` should be listed as *Enabled*.

## Use

Just draw connectors. The labels disappear as soon as the connector is created.

Menu **Specialize** ribbon > **Add-Ins** > **Hide Connector Labels** (also in the diagram right-click menu under *Specialize*):

- **Auto-hide labels on new connectors**: on/off toggle (on by default, remembered).
- **Hide labels of all connectors on current diagram**: one-shot cleanup for existing diagrams.

To show a connector's labels again: right-click the connector > **Label Visibility** / **Show All Labels**
(wording varies a little by EA version).

## Uninstall

```powershell
powershell -ExecutionPolicy Bypass -File C:\dev\oxyum\EAHideConnectorLabels\uninstall.ps1
```

Then restart EA. You can also just disable it from **Specialize > Manage-Add-Ins**.

## How it works

- Handles EA's `EA_OnPostNewConnector` broadcast event.
- Saves the diagram (so none of your pending diagram edits are lost), marks every label except
  the two multiplicities as hidden (`HDN=1` in `DiagramLink.Geometry`),
  then reloads the diagram so the change shows.
- Note: because the diagram is saved when a connector is drawn, EA's diagram *undo* history
  does not go back past that point.

## Rebuild

```powershell
powershell -ExecutionPolicy Bypass -File build.ps1
```

Uses the .NET Framework 4 `csc.exe` that ships with Windows and EA's own `Interop.EA.dll`.
If you move this folder, run `install.ps1` again (the registration points at the DLL's path).
Close EA before rebuilding, since it keeps the DLL locked.


