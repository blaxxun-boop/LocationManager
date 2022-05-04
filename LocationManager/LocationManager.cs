using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using JetBrains.Annotations;
using UnityEngine;
using Object = UnityEngine.Object;

namespace LocationManager;

public enum ShowIcon
{
	Always,
	Never,
	Explored
}

public enum Rotation
{
	Fixed,
	Random,
	Slope
}

[PublicAPI]
public struct Range
{
	public float min;
	public float max;

	public Range(float min, float max)
	{
		this.min = min;
		this.max = max;
	}
}

[PublicAPI]
public class Location
{
	public bool CanSpawn = true;
	public Heightmap.Biome Biome = Heightmap.Biome.Meadows;
	/// <summary>
	/// If the location should spawn more towards the edge of the biome or towards the center.
	/// <para>Use "Edge" to make it spawn towards the edge.</para>
	/// <para>Use "Median" to make it spawn towards the center.</para>
	/// <para>Use "Everything" if it doesn't matter.</para>
	/// </summary>
	public Heightmap.BiomeArea SpawnArea = Heightmap.BiomeArea.Everything;
	/// <summary>
	/// Maximum number of locations to spawn in.
	/// <para>Does not mean that this many locations will spawn. But Valheim will try its best to spawn this many, if there is space.</para>
	/// </summary>
	public int Count = 1;
	/// <summary>
	/// If set to true, this location will be prioritized over other locations, if they would spawn in the same area.
	/// </summary>
	public bool Prioritize = false;
	/// <summary>
	/// If set to true, Valheim will try to spawn your location as close to the center of the map as possible.
	/// </summary>
	public bool PreferCenter = false;
	/// <summary>
	/// If set to true, all other locations will be deleted, once the first one has been discovered by a player.
	/// </summary>
	public bool Unique = false;
	/// <summary>
	/// The name of the group of the location, used by the minimum distance from group setting.
	/// </summary>
	public string GroupName;
	/// <summary>
	/// Locations in the same group will keep at least this much distance between each other.
	/// </summary>
	public float MinimumDistanceFromGroup = 0f;
	/// <summary>
	/// When to show the map icon of the location. Requires an icon to be set.
	/// <para>Use "Never" to not show a map icon for the location.</para>
	/// <para>Use "Always" to always show a map icon for the location.</para>
	/// <para>Use "Explored" to start showing a map icon for the location as soon as a player has explored the area.</para>
	/// </summary>
	public ShowIcon ShowMapIcon = ShowIcon.Never;

	/// <summary>
	/// Sets the map icon for the location.
	/// </summary>
	public string? MapIcon
	{
		get => mapIconName;
		set
		{
			mapIconName = value;
			MapIconSprite = mapIconName is null ? null : loadSprite(mapIconName);
		}
	}

	private string? mapIconName = null;

	/// <summary>
	/// Sets the map icon for the location.
	/// </summary>
	public Sprite? MapIconSprite = null;
	/// <summary>
	/// How to rotate the location.
	/// <para>Use "Fixed" to use the rotation of the prefab.</para>
	/// <para>Use "Random" to randomize the rotation.</para>
	/// <para>Use "Slope" to rotate the location along a possible slope.</para>
	/// </summary>
	public Rotation Rotation = Rotation.Random;
	/// <summary>
	/// The minimum and maximum height difference of the terrain below the location.
	/// </summary>
	public Range HeightDelta = new(0, 2);
	/// <summary>
	/// If the location should spawn near water.
	/// </summary>
	public bool SnapToWater = false;
	/// <summary>
	/// If the location should spawn in a forest.
	/// <para>Everything above 1.15 is considered a forest by Valheim.</para>
	/// <para>2.19 is considered a thick forest by Valheim.</para>
	/// </summary>
	public Range ForestThreshold = new(0, 2.19f);
	/// <summary>
	/// Minimum and maximum range from the center of the map for the location.
	/// </summary>
	public Range SpawnDistance = new(0, 10000);
	/// <summary>
	/// Minimum and maximum altitude for the location.
	/// </summary>
	public Range SpawnAltitude = new(-1000f, 1000f);

	private readonly global::Location location;
	private string folderName = "";
	private AssetBundle? assetBundle;
	private static readonly List<Location> registeredLocations = new();

	public Location(string assetBundleFileName, string prefabName, string folderName = "assets") : this(PrefabManager.RegisterAssetBundle(assetBundleFileName, folderName), prefabName)
	{
		this.folderName = folderName;
	}

	public Location(AssetBundle bundle, string prefabName) : this(bundle.LoadAsset<GameObject>(prefabName))
	{
		assetBundle = bundle;
	}

	public Location(GameObject location) : this(location.GetComponent<global::Location>())
	{
		if (this.location == null)
		{
			throw new ArgumentNullException(nameof(location), "The GameObject does not have a location component.");
		}
	}

	public Location(global::Location location)
	{
		this.location = location;
		GroupName = location.name;
		registeredLocations.Add(this);
	}

	private byte[]? ReadEmbeddedFileBytes(string name)
	{
		using MemoryStream stream = new();
		if (Assembly.GetExecutingAssembly().GetManifestResourceStream(Assembly.GetExecutingAssembly().GetName().Name + $"{(folderName == "" ? "" : ".") + folderName}." + name) is not {} assemblyStream)
		{
			return null;
		}

		assemblyStream.CopyTo(stream);
		return stream.ToArray();
	}

	private Texture2D? loadTexture(string name)
	{
		if (ReadEmbeddedFileBytes(name) is { } textureData)
		{
			Texture2D texture = new(0, 0);
			texture.LoadImage(textureData);
			return texture;
		}
		return null;
	}

	private Sprite loadSprite(string name)
	{
		if (loadTexture(name) is { } texture)
		{
			return Sprite.Create(texture, new Rect(0, 0, 64, 64), Vector2.zero);
		}
		if (assetBundle?.LoadAsset<Sprite>(name) is { } sprite)
		{
			return sprite;
		}

		throw new FileNotFoundException($"Could not find a file named {name} for the map icon");
	}

	private static void AddLocationToZoneSystem(ZoneSystem __instance)
	{
		foreach (GameObject gameObject in Resources.FindObjectsOfTypeAll<GameObject>())
		{
			if (gameObject.name == "_Locations" && gameObject.transform.Find("Misc") is { } locationMisc)
			{
				foreach (Location location in registeredLocations)
				{
					GameObject locationInstance = Object.Instantiate(location.location.gameObject, locationMisc, true);
					locationInstance.name = location.location.name;
					__instance.m_locations.Add(new ZoneSystem.ZoneLocation
					{
						m_prefabName = locationInstance.name,
						m_enable = location.CanSpawn,
						m_biome = location.Biome,
						m_biomeArea = location.SpawnArea,
						m_quantity = location.Count,
						m_prioritized = location.Prioritize,
						m_centerFirst = location.PreferCenter,
						m_unique = location.Unique,
						m_group = location.GroupName,
						m_minDistanceFromSimilar = location.MinimumDistanceFromGroup,
						m_iconAlways = location.ShowMapIcon == ShowIcon.Always,
						m_iconPlaced = location.ShowMapIcon == ShowIcon.Explored,
						m_randomRotation = location.Rotation == Rotation.Random,
						m_slopeRotation = location.Rotation == Rotation.Slope,
						m_snapToWater = location.SnapToWater,
						m_minTerrainDelta = location.HeightDelta.min,
						m_maxTerrainDelta = location.HeightDelta.max,
						m_inForest = true,
						m_forestTresholdMin = location.ForestThreshold.min,
						m_forestTresholdMax = location.ForestThreshold.max,
						m_minDistance = location.SpawnDistance.min,
						m_maxDistance = location.SpawnDistance.max,
						m_minAltitude = location.SpawnAltitude.min,
						m_maxAltitude = location.SpawnAltitude.max
					});
				}
			}
		}
	}

	private static void AddLocationZNetViewsToZNetScene(ZNetScene __instance)
	{
		if (__instance.m_prefabs.Count <= 0)
		{
			return;
		}

		foreach (ZNetView netView in registeredLocations.SelectMany(l => l.location.GetComponentsInChildren<ZNetView>()))
		{
			__instance.m_prefabs.Add(netView.gameObject);
		}
	}

	private static void AddMinimapIcons(Minimap __instance)
	{
		foreach (Location location in registeredLocations)
		{
			if (location.MapIconSprite is { } icon)
			{
				__instance.m_locationIcons.Add(new Minimap.LocationSpriteData { m_icon = icon, m_name = location.location.name });
			}
		}
	}

	static Location()
	{
		Harmony harmony = new("org.bepinex.helpers.LocationManager");
		harmony.Patch(AccessTools.DeclaredMethod(typeof(ZNetScene), nameof(ZNetScene.Awake)), postfix: new HarmonyMethod(AccessTools.DeclaredMethod(typeof(Location), nameof(AddLocationZNetViewsToZNetScene)), Priority.VeryHigh));
		harmony.Patch(AccessTools.DeclaredMethod(typeof(ZoneSystem), nameof(ZoneSystem.SetupLocations)), new HarmonyMethod(AccessTools.DeclaredMethod(typeof(Location), nameof(AddLocationToZoneSystem))));
		harmony.Patch(AccessTools.DeclaredMethod(typeof(Minimap), nameof(Minimap.Awake)), postfix: new HarmonyMethod(AccessTools.DeclaredMethod(typeof(Location), nameof(AddMinimapIcons))));
	}

	private static class PrefabManager
	{
		private struct BundleId
		{
			[UsedImplicitly]
			public string assetBundleFileName;
			[UsedImplicitly]
			public string folderName;
		}

		private static readonly Dictionary<BundleId, AssetBundle> bundleCache = new();

		public static AssetBundle RegisterAssetBundle(string assetBundleFileName, string folderName = "assets")
		{
			BundleId id = new() { assetBundleFileName = assetBundleFileName, folderName = folderName };
			if (!bundleCache.TryGetValue(id, out AssetBundle assets))
			{
				assets = bundleCache[id] = Resources.FindObjectsOfTypeAll<AssetBundle>().FirstOrDefault(a => a.name == assetBundleFileName) ?? AssetBundle.LoadFromStream(Assembly.GetExecutingAssembly().GetManifestResourceStream(Assembly.GetExecutingAssembly().GetName().Name + $"{(folderName == "" ? "" : ".") + folderName}." + assetBundleFileName));
			}
			return assets;
		}
	}
}
