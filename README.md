# Ariadne Mod Manager 🧶

An opinionated mod manager for games on both Windows and Linux (WIP), Ariadne brings all the benefits of a traditional list-based mod manager while stripping away the complexity. All while keeping mod files isolated with VFS for easy installs, uninstalls, and profile switching. 

>[!WARNING]
> Ariadne is in an early incomplete Alpha state. Though the basic functions are available, there will be features missing that you would otherwise expect, and probably bugs. Use at your own risk.


## Features


### Virtual File System
Ariadne virtualizes your game and mods onto a mounted path that doesn't take any extra disk space and looks like a plain old folder to games and other applications, for as long as your modding session needs to last. Your real game install remains untouched.

>[!NOTE]
> If the mod manager somehow crashes, there are no leftover files or anything to repair. Deployment in this case means deployment of the VFS, it does **not** mean hardlink deployment.

Any changes made to game files (modification, creation, deletion) appear as a copy in a buffer called **Overwrite**, and take precedence over your real game files. They can be deleted anytime to revert the changes made, ensuring strong resilience for bugs, slips, and mistakes. 

If you accidentally deleted all your save files in-game; recover them, if a tool changed some game meshes or textures without your consent; the changes are in fact, reversible.

For mod authors, this also means they get a non-destructive workflow!

Programmers will be familiar with copy-on-write (COW) semantics and whiteouts, which is applied here.

>[!TIP]
> Unlike MO2, Ariadne's virtualization is not process-local. This means you can browse the virtual directory using your file manager of choice, point any external tool at it, and best of all, you only need to deploy the VFS once before running as many applications in it as you like, in any way you want.

> [!IMPORTANT]  
> The virtual filesystem on Windows works slightly differently to how it does on Linux. This is because of platform limitations. In short, on Windows, the real game install folder is overlaid into a virtual folder that exists in Ariadne's own Staging directory. It's similar to Wabbajack's "Stock Game Method", but at runtime (and without the copying or extra disk space).

---

### Clean Profile Separation
Profiles no longer require all mods to be present in the GUI, and can "forget" mods that remain present in other profiles, leading to a less cluttered modlist view overall. After all, there is no reason for mods that are only used in one profile to clutter up the view of other profiles. 

Moreover, on top of profiles having their own load order, they also have their own overwrite, which keeps the deployed game from being affected with loose files from other profiles. 

---

### Multi-Target Integration
It's very easy for mods to install to more than just one target directory. For example, Skyrim Special Edition has support for targeting mods to either Data or Root. Functionally this is identical to a plugin like Root Builder, except it's integrated into the mod manager and VFS itself. 

>[!TIP]
> You can retarget mods post install using a right click menu option!

### Graphical User Interface
The manager uses a heavily opinionated graphical user interface (GUI) with the cross-platform AvaloniaUI. 

If one were to make comparisons, the most apt description would be a GUI of a complexity between MO2 and Vortex, with a layout inspired from the former.

For example, whereas in a traditional mod manager, files and folders under a mod would open under a separate window (or even the default file explorer), Ariadne keeps them as part of the modlist view, underneath their respective mod.

In addition, Ariadne has the concept of mod groups rather than separators. Mod groups are a first-class concept of rigid, atomic units containing multiple mods; for the user this means dragging a group effectively drags all its children along as well. Mods that are loose without a group will always be



The driving UX philosophy is simple (ish); the interface should be comfortable to use, transparent about its current state, and yield control to the user without being overwhelming. 

To this end, the program remains legible, widely accessible, and its semantics free of jargon. The depth of its function is presented to the user in a way that is attractive rather than off-putting. Therefore, the interface is calm, and minimal, but still capable.

Obviously this is all highly subjective and not everyone can agree on which elements support or detract from the above philosophy, or on whether the current state of the program even supports this design at all, hence why it is **opinionated**. 

The best way to test out the interface's effectiveness is to try it out and see. 

>[!TIP]
> There is custom color theming support in the way of configuration files. Bundled by default are Catpuccin, Rose Pine, and Tokyo Night (default).

---


### Extensible Game Support
The majority of support for different games comes from bundled `.json` configuration files, which can be opened with any text editor and redistributed freely. 

Ariadne was designed from the ground-up for easy, open extensibility, so adding support for a specific game is, in most cases, trivial to perform. 

That being said, if you intend on contributing game support files, please test to make sure your configuration works locally first; contributions without testing will not be accepted.

Regarding local game install detection,  a wide variety of vendors are supported, including Steam, GOG, and Epic. 

---

### Web Protocol Support
Ariadne contains integration for downloading from popular modding websites like Nexus Mods, supported with SSO. Premium account benefits are fully supported as well. Also supports Mod.pub!

---

### Other Quality of ~~Life~~ Mod Features
- One mod list = one pane = one file view. 
- Drag and drop files and folders between folders, mods, or flatten if needed. Fix badly packaged mods in one motion.
- Visual conflict highlights
- Load order support.
- Preview mod files before deployment.
- BSA and other archive preview.
- Dynamic app launcher tray


## Quickstart

### Windows

#### Requirements
- [WinFSP 2026 beta4 or later](https://github.com/winfsp/winfsp/releases/tag/v2.2B4)
- [.NET Runtime 10](https://dotnet.microsoft.com/en-us/download/dotnet/10.0)

#### Getting Started

1. Extract and launch the application.
2. Select a game to manage, and create a new instance.
3. Set up Ariadne to handle NXMs (Nexus Mods downloads) with **`Sources > Nexus Mods > Handle nxm:// links`**
4. Optionally set up Ariadne to handle other protocols.
5. You're ready to mod!

When ready to run your modded game, click the **Deploy** button on the top right of the profile bar. Keep in mind that any changes to your modlist is not supported while your mods are deployed.

>[!TIP]
>You can link and unlink NXM handling at any time through the system menu.

##### Personal Nexus API Key Usage

To use Ariadne with a personal Nexus API key (for testing/dev purposes **only**) you must build this project in the Debug configuration, and make sure your `ARIADNE_NEXUS_API_KEY` environment variable is set prior to launching the application.

Release builds and user-facing builds can **only** use SSO.


### Linux

WIP
  






