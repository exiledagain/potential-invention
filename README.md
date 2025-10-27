# Install

 - Run [MelonLoader](https://github.com/LavaGang/MelonLoader) installer on Last Epoch to generate assemblies
 - Set LE_INSTALL System/User variable to the folder containing your Last Epoch install
 - Compile pi-melon-mod using [Visual Studio](https://visualstudio.microsoft.com/) with C# support (Unity games use [dotnet6.0 sdk](https://dotnet.microsoft.com/en-us/download/dotnet/6.0))
 - successful compilation will copy pi-melon-mod.dll to your Last Epoch mods

# Usage (local)

 - Save a loot filter to query.txt
 - Change `maxRollJson` in `testigc.js` to your imprint json
 - Run [node testigc.js](https://nodejs.org/en)
