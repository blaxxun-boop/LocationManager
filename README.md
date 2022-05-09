# Location Manager

Makes your custom location spawn during world generation in Valheim.

## How to add locations

Copy the asset bundle into your project and make sure to set it as an EmbeddedResource in the properties of the asset bundle.
Default path for the asset bundle is an `assets` directory, but you can override this.
This way, you don't have to distribute your assets with your mod. They will be embedded into your mods DLL.

### Merging the precompiled DLL into your mod

Add the following three lines to the bottom of the first PropertyGroup in your .csproj file, to enable C# V10.0 features and to allow the use of publicized DLLs.

```xml
<LangVersion>10</LangVersion>
<Nullable>enable</Nullable>
<AllowUnsafeBlocks>true</AllowUnsafeBlocks>
```

Download the LocationManager.dll from the release section to the right.
Including the dll is best done via ILRepack (https://github.com/ravibpatel/ILRepack.Lib.MSBuild.Task). You can load this package (ILRepack.Lib.MSBuild.Task) from NuGet.

If you have installed ILRepack via NuGet, simply create a file named `ILRepack.targets` in your project and copy the following content into the file

```xml
<?xml version="1.0" encoding="utf-8"?>
<Project xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
    <Target Name="ILRepacker" AfterTargets="Build">
        <ItemGroup>
            <InputAssemblies Include="$(TargetPath)" />
            <InputAssemblies Include="$(OutputPath)\LocationManager.dll" />
        </ItemGroup>
        <ILRepack Parallel="true" DebugInfo="true" Internalize="true" InputAssemblies="@(InputAssemblies)" OutputFile="$(TargetPath)" TargetKind="SameAsPrimaryAssembly" LibraryPath="$(OutputPath)" />
    </Target>
</Project>
```

Make sure to set the LocationManager.dll in your project to "Copy to output directory" in the properties of the DLL and to add a reference to it.
After that, simply add `using LocationManager;` to your mod and use the `Location` class, to add your locations.

## Example project

This adds two different locations, one of the locations has creature spawners.

```csharp
using BepInEx;
using LocationManager;

namespace CustomLocation;

[BepInPlugin(ModGUID, ModName, ModVersion)]
public class CustomLocation : BaseUnityPlugin
{
	private const string ModName = "CustomLocation";
	private const string ModVersion = "1.0.0";
	private const string ModGUID = "org.bepinex.plugins.customlocation";

	public void Awake()
	{
		_ = new LocationManager.Location("guildfabs", "GuildAltarSceneFab")
		{
			MapIcon = "portalicon.png",
			ShowMapIcon = ShowIcon.Explored,
			Biome = Heightmap.Biome.Meadows,
			SpawnDistance = new Range(500, 1500),
			SpawnAltitude = new Range(10, 100),
			MinimumDistanceFromGroup = 100,
			Count = 15,
			Unique = true
		};
		
		LocationManager.Location location = new("krumpaclocations", "WaterPit1")
		{
			MapIcon = "K_Church_Ruin01.png",
			ShowMapIcon = ShowIcon.Always,
			Biome = Heightmap.Biome.Meadows,
			SpawnDistance = new Range(100, 1500),
			SpawnAltitude = new Range(5, 150),
			MinimumDistanceFromGroup = 100,
			Count = 15
		};
		
		// If your location has creature spawners, you can configure the creature they spawn like this.
		location.CreatureSpawner.Add("Spawner_1", "Neck");
		location.CreatureSpawner.Add("Spawner_2", "Troll");
		location.CreatureSpawner.Add("Spawner_3", "Greydwarf");
		location.CreatureSpawner.Add("Spawner_4", "Neck");
		location.CreatureSpawner.Add("Spawner_5", "Troll");
		location.CreatureSpawner.Add("Spawner_6", "Greydwarf");
	}
}
```
