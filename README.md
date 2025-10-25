# schedule-il2cpp-example
Example template and instructions for modding Schedule I IL2CPP version

## Requirements
- The game requires your mod to target the net6 framework, this is defined in the ExampleMod.csproj file TargetFramework property and net6 must be installed before building the library. Visual Studio will prompt you to install it if not present.
- Then install the 2 required packages, Visual Studio toolbar -> Project -> Manage NuGet Packages
  - Search for LavaGang.MelonLoader and install it
  - Search for Lib.Harmony and install it

- Now you must additionally have MelonLoader installed for the game, if it is not installed do it now.
- Start your game once and let MelonLoader build the il2cpp assemblies. After this is done the game will start and then close the game.
  - Then you must navigate to the following directory: C:\Program Files (x86)\Steam\steamapps\common\Schedule I\MelonLoader\Il2CppAssemblies
  - From here you will need two files: Assembly-CSharp.dll and UnityEngine.CoreModule.dll, move these two files into the libs folder. (Also specified in the .csproj file)
      - NOTE: Now if you inspect that that Assembly-CSharp.dll file with dnSpy, you will find that the namespace for ScheduleOne has become Il2CppScheduleOne
      - NOTE: You will need to use that specific namespace when referring to the game namespace related things by: using Il2CppScheduleOne.Player;

- After these steps are done, you are ready to code your own logics. See the Template MainMod.cs file for the basic requirements for Harmony and MelonLoader.
