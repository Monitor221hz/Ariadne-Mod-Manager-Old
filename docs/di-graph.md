# Daedalus DI Injections

```
services.AddModManager(gameModules)
│
├─ AddMods()
│  └─ IModInfoSerializer ──► ModInfoSerializer                                    [singleton]
│
├─ AddGames(gameModules)
│  ├─ IGameCatalog ──► GameCatalog                                                [singleton, lazy]
│  │  ├─ gameModules : Assembly[]
│  │  └─ looseConfigDirectory : DirectoryInfo?
│  ├─ IGameLocator ──► GameLocator                                                [singleton]
│  └─ IInstalledGameSerializer ──► InstalledGameSerializer                        [singleton]
│
├─ ConfigureVFS()
│  ├─ IVirtualFileSystem ──► WinFspVirtualFileSystem                              [transient, per mount]
│  ├─ Func<IVirtualFileSystem> ──► sp.GetRequiredService<IVirtualFileSystem>      [singleton]
│  └─ IVirtualFileSystemFactory ──► ConstVirtualFileSystemFactory<IVirtualFileSystem> [singleton]
│     └─ Func<IVirtualFileSystem>
│
├─ IModManagerPaths ──► ModManagerPaths                                           [singleton]
│  └─ AppContext.BaseDirectory
│
├─ IDeploymentPathsFactory ──► DeploymentPathsFactory                             [singleton]
│  └─ IModManagerPaths
│
├─ IModProfileSerializer ──► ModProfileSerializer                                 [singleton]
│  ├─ IModInfoSerializer
│  └─ IModManagerPaths
│
├─ Func<IModProfile, IModDeploymentMethod>                                        [singleton]
│  ├─ IVirtualFileSystemFactory
│  └─ IDeploymentPathsFactory ──► Create(profile)
│
└─ IModDeploymentMethodFactory ──► ModDeploymentMethodFactory                     [singleton]
   └─ Func<IModProfile, IModDeploymentMethod>

Runtime products (not registered in the container)
│
├─ IDeploymentPaths ──► DeploymentPathsFactory.Create(profile)
│  ├─ OverwriteDirectory = profile.OverwriteFolder
│  └─ StagingDirectory   = IModManagerPaths.StagingFolder
│
└─ IModDeploymentMethod ──► ModDeploymentMethodFactory.Create(profile)            [fresh per profile]
   ├─ IVirtualFileSystemFactory
   └─ IDeploymentPaths (of that profile)
```
