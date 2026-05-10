using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using KeybindLib.Classes;
using MoreUpgrades.Classes;
using MoreUpgrades.Patches;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MoreUpgrades
{
    [BepInDependency(Compatibility.KeybindLib.modGUID, BepInDependency.DependencyFlags.HardDependency)]
    [BepInDependency(Compatibility.REPOLib.modGUID, BepInDependency.DependencyFlags.HardDependency)]
    [BepInPlugin(modGUID, modName, modVer)]
    internal class Plugin : BaseUnityPlugin
    {
        private const string modGUID = "bulletbot.moreupgrades";
        private const string modName = "MoreUpgrades";
        private const string modVer = "1.6.8";

        internal static Plugin instance;
        public ManualLogSource logger;
        private readonly Harmony harmony = new Harmony(modGUID);

        public ConfigEntry<bool> importUpgrades;
        public ConfigEntry<string> excludeUpgradeIds;

        public AssetBundle assetBundle;
        public List<UpgradeItem> upgradeItems;

        public bool updateTracker;

        internal void PatchAll(string name) =>
            Assembly.GetExecutingAssembly().GetTypes().Where(x =>
                x.Namespace == $"{typeof(Plugin).Namespace}.{name}").ToList().ForEach(x => harmony.PatchAll(x));

        private GameObject GetVisualsFromComponent(Component component)
        {
            GameObject visuals = null;
            if (component is EnemyParent)
            {
                EnemyParent enemyParent = component as EnemyParent;
                Enemy enemy = (Enemy)AccessTools.Field(typeof(EnemyParent), "Enemy").GetValue(component);
                if (enemyParent.enemyName != "Bella")
                {
                    try
                    {
                        visuals = enemyParent.GetComponentInChildren<EnemyVision>().VisionTransform.gameObject;
                    }
                    catch { }
                    if (visuals == null)
                    {
                        try
                        {
                            visuals = enemyParent.EnableObject.GetComponentInChildren<Animator>().gameObject;
                        }
                        catch { }
                    }
                }
                else
                    visuals = enemy.GetComponentInChildren<EnemyTricycle>().followTargetTransform.gameObject;
                if (visuals == null)
                    visuals = enemy.gameObject;
            }
            else if (component is PlayerAvatar)
            {
                PlayerAvatar playerAvatar = component as PlayerAvatar;
                visuals = playerAvatar.playerAvatarVisuals.gameObject;
            }
            return visuals;
        }

        internal void RegisterToMap(Component component)
        {
            if (Map.Instance == null)
                return;
            GameObject visuals = GetVisualsFromComponent(component);
            if (visuals == null)
                return;
            string upgradeItemName = null;
            if (component is EnemyParent)
                upgradeItemName = "Map Enemy Tracker";
            else if (component is PlayerAvatar)
                upgradeItemName = "Map Player Tracker";
            else
                return;
            UpgradeItem upgradeItem = upgradeItems.FirstOrDefault(x => x.upgradeBase.name == upgradeItemName);
            if (upgradeItem == null)
                return;
            List<MapInfo> mapInfos = upgradeItem.GetVariable<List<MapInfo>>("Map Infos");
            if (mapInfos.Any(x => x.component == component))
                return;
            GameObject mapObject = Instantiate(Map.Instance.CustomObject, Map.Instance.OverLayerParent);
            mapObject.name = visuals.name;
            MapCustomEntity mapCustomEntity = mapObject.GetComponent<MapCustomEntity>();
            mapCustomEntity.Parent = visuals.transform;
            mapInfos.Add(new MapInfo()
            {
                component = component,
                mapCustomEntity = mapCustomEntity
            });
            updateTracker = true;
        }

        internal void ShowToMap(Component component)
        {
            UpgradeItem mapEnemyTracker = upgradeItems.FirstOrDefault(x => x.upgradeBase.name == "Map Enemy Tracker");
            UpgradeItem mapPlayerTracker = upgradeItems.FirstOrDefault(x => x.upgradeBase.name == "Map Player Tracker");
            if (mapEnemyTracker == null && mapPlayerTracker == null)
                return;
            List<MapInfo> enemyMapInfos = mapEnemyTracker?.GetVariable<List<MapInfo>>("Map Infos");
            List<MapInfo> playerMapInfos = mapPlayerTracker?.GetVariable<List<MapInfo>>("Map Infos");
            if (enemyMapInfos == null && playerMapInfos == null)
                return;
            MapInfo mapInfo = enemyMapInfos?.FirstOrDefault(x => x.component == component) ??
                playerMapInfos?.FirstOrDefault(x => x.component == component);
            if (mapInfo == null)
                return;
            bool isEnemy = component is EnemyParent;
            bool isPlayer = component is PlayerAvatar;
            if (!isEnemy && !isPlayer)
                return;
            if (isEnemy)
            {
                EnemyParent enemyParent = component as EnemyParent;
                if (mapEnemyTracker.GetConfig<string>("Exclude Enemies").Split(',').Select(x => x.Trim())
                    .Where(x => !string.IsNullOrEmpty(x)).Contains(enemyParent.enemyName))
                    return;
            }
            mapInfo.visible = true;
            updateTracker = true;
        }

        internal void HideFromMap(Component component)
        {
            UpgradeItem mapEnemyTracker = upgradeItems.FirstOrDefault(x => x.upgradeBase.name == "Map Enemy Tracker");
            UpgradeItem mapPlayerTracker = upgradeItems.FirstOrDefault(x => x.upgradeBase.name == "Map Player Tracker");
            if (mapEnemyTracker == null && mapPlayerTracker == null)
                return;
            List<MapInfo> enemyMapInfos = mapEnemyTracker?.GetVariable<List<MapInfo>>("Map Infos");
            List<MapInfo> playerMapInfos = mapPlayerTracker?.GetVariable<List<MapInfo>>("Map Infos");
            if (enemyMapInfos == null && playerMapInfos == null)
                return;
            MapInfo mapInfo = enemyMapInfos?.FirstOrDefault(x => x.component == component) ??
                playerMapInfos?.FirstOrDefault(x => x.component == component);
            if (mapInfo == null)
                return;
            bool isEnemy = component is EnemyParent;
            bool isPlayer = component is PlayerAvatar;
            if (!isEnemy && !isPlayer)
                return;
            if (isEnemy)
            {
                EnemyParent enemyParent = component as EnemyParent;
                if (mapEnemyTracker.GetConfig<string>("Exclude Enemies").Split(',').Select(x => x.Trim())
                    .Where(x => !string.IsNullOrEmpty(x)).Contains(enemyParent.enemyName))
                    return;
            }
            mapInfo.visible = false;
            updateTracker = true;
        }

        internal void SwapOnMap(Component component, Component fromComponent)
        {
            UpgradeItem mapEnemyTracker = upgradeItems.FirstOrDefault(x => x.upgradeBase.name == "Map Enemy Tracker");
            UpgradeItem mapPlayerTracker = upgradeItems.FirstOrDefault(x => x.upgradeBase.name == "Map Player Tracker");
            if (mapEnemyTracker == null || mapPlayerTracker == null)
                return;
            List<MapInfo> enemyMapInfos = mapEnemyTracker.GetVariable<List<MapInfo>>("Map Infos");
            List<MapInfo> playerMapInfos = mapPlayerTracker.GetVariable<List<MapInfo>>("Map Infos");
            if (enemyMapInfos == null || playerMapInfos == null)
                return;
            MapInfo mapInfo = enemyMapInfos.FirstOrDefault(x => x.component == component) ??
                playerMapInfos.FirstOrDefault(x => x.component == component);
            MapInfo fromMapInfo = enemyMapInfos.FirstOrDefault(x => x.component == fromComponent) ??
                playerMapInfos.FirstOrDefault(x => x.component == fromComponent);
            if (mapInfo == null || fromMapInfo == null)
                return;
            bool componentValid = component is PlayerAvatar || component is EnemyParent;
            bool fromComponentValid = fromComponent is PlayerAvatar || fromComponent is EnemyParent;
            if (!componentValid || !fromComponentValid)
                return;
            if (component is EnemyParent || fromComponent is EnemyParent)
            {
                List<string> enemyNames = new List<string>();
                if (component is EnemyParent enemyParent)
                    enemyNames.Add(enemyParent.enemyName);
                if (fromComponent is EnemyParent fromEnemyParent)
                    enemyNames.Add(fromEnemyParent.enemyName);
                if (enemyNames.Any(x => 
                    mapEnemyTracker.GetConfig<string>("Exclude Enemies").Split(',').Select(y => y.Trim())
                    .Where(y => !string.IsNullOrEmpty(y)).Contains(x)))
                    return;
            }
            if (enemyMapInfos.Contains(mapInfo) && playerMapInfos.Contains(fromMapInfo))
            {
                enemyMapInfos.Remove(mapInfo);
                enemyMapInfos.Add(fromMapInfo);
                playerMapInfos.Remove(fromMapInfo);
                playerMapInfos.Add(mapInfo);
            }
            else if (enemyMapInfos.Contains(fromMapInfo) && playerMapInfos.Contains(mapInfo))
            {
                enemyMapInfos.Remove(fromMapInfo);
                enemyMapInfos.Add(mapInfo);
                playerMapInfos.Remove(mapInfo);
                playerMapInfos.Add(fromMapInfo);
            }
            updateTracker = true;
        }
        
        void Awake()
        {
            instance = this;
            logger = BepInEx.Logging.Logger.CreateLogSource(modName);
            assetBundle = AssetBundle.LoadFromMemory(Properties.Resources.moreupgrades);
            if (assetBundle == null)
            {
                logger.LogError("Something went wrong when loading the asset bundle.");
                return;
            }
            importUpgrades = Config.Bind("! REPOLib Configuration !", "Import Upgrades", false, 
                "Whether to import the upgrades from REPOLib.");
            excludeUpgradeIds = Config.Bind("! REPOLib Configuration !", "Exclude Upgrade IDs", "",
                "Exclude specific REPOLib upgrades by listing their IDs, seperated by commas." +
                "\nThis setting only has an effect if 'Import Upgrades' is enabled.");
            upgradeItems = new List<UpgradeItem>();
            UpgradeItem.Base sprintUsageBase = new UpgradeItem.Base
            {
                name = "Sprint Usage",
                maxAmount = 10,
                maxAmountInShop = 2,
                minPrice = 9000,
                maxPrice = 14000
            };
            UpgradeItem sprintUsage = null;
            void UpdateSprintUsage(PlayerAvatar playerAvatar, int level)
            {
                if (PlayerController.instance.playerAvatarScript != playerAvatar)
                    return;
                string key = "Energy Sprint Drain";
                if (!sprintUsage.HasVariable(key))
                    sprintUsage.AddVariable(key, PlayerController.instance.EnergySprintDrain);
                PlayerController.instance.EnergySprintDrain =
                    sprintUsage.GetVariable<float>(key) * Mathf.Pow(sprintUsage.GetConfig<float>("Scaling Factor"), level);
            }
            sprintUsageBase.onStart += UpdateSprintUsage;
            sprintUsageBase.onUpgrade += UpdateSprintUsage;
            sprintUsage = new UpgradeItem(sprintUsageBase);
            sprintUsage.AddConfig("Scaling Factor", 0.9f,
                "Formula: energySprintDrain * (scalingFactor ^ upgradeLevel))");
            upgradeItems.Add(sprintUsage);
            UpgradeItem.Base valuableCountBase = new UpgradeItem.Base
            {
                name = "Valuable Count",
                minPrice = 30000,
                maxPrice = 40000,
                maxPurchaseAmount = 1,
                priceIncreaseScaling = 0
            };
            UpgradeItem valuableCount = null;
            valuableCountBase.onVariablesStart += delegate
            {
                valuableCount.AddVariable("Current Valuables", new List<ValuableObject>());
                valuableCount.AddVariable("Changed", false);
                valuableCount.AddVariable("Previous Count", 0);
                valuableCount.AddVariable("Previous Value", 0);
                valuableCount.AddVariable("Text Length", 0);
            };
            valuableCountBase.onUpdate += delegate
            {
                if (SemiFunc.RunIsLobby() || SemiFunc.RunIsShop())
                    return;
                PlayerAvatar playerAvatar = PlayerController.instance.playerAvatarScript;
                if (MissionUI.instance != null && playerAvatar != null && 
                    valuableCount.playerUpgrade.GetLevel(playerAvatar) > 0)
                {
                    TextMeshProUGUI missionText = 
                        (TextMeshProUGUI)AccessTools.Field(typeof(MissionUI), "Text").GetValue(MissionUI.instance);
                    string messagePrev = 
                        (string)AccessTools.Field(typeof(MissionUI), "messagePrev").GetValue(MissionUI.instance);
                    List<ValuableObject> currentValuables = 
                        valuableCount.GetVariable<List<ValuableObject>>("Current Valuables");
                    bool changed = valuableCount.GetVariable<bool>("Changed");
                    int previousCount = valuableCount.GetVariable<int>("Previous Count");
                    int previousValue = valuableCount.GetVariable<int>("Previous Value");
                    int textLength = valuableCount.GetVariable<int>("Text Length");
                    int count = currentValuables.Count;
                    bool displayTotalValue = valuableCount.GetConfig<bool>("Display Total Value");
                    int value = displayTotalValue ? currentValuables.Select(x =>
                    {
                        return (int)((float)AccessTools.Field(typeof(ValuableObject), "dollarValueCurrent").GetValue(x));
                    }).Sum() : 0;
                    if (!string.IsNullOrEmpty(missionText.text) && 
                        (changed || previousCount != count || previousValue != value))
                    {
                        string text = missionText.text;
                        if (!changed && (previousCount != count || previousValue != value))
                            text = text.Substring(0, text.Length - textLength);
                        string valuableText = $"\nValuables: <b>{count}</b>" +
                            (displayTotalValue ? 
                                $" (<color=#558B2F>$</color><b>{SemiFunc.DollarGetString(value)}</b>)" : "");
                        text += valuableText;
                        valuableCount.SetVariable("Previous Count", count);
                        valuableCount.SetVariable("Previous Value", value);
                        valuableCount.SetVariable("Text Length", valuableText.Length);
                        missionText.text = text;
                        AccessTools.Field(typeof(MissionUI), "messagePrev").SetValue(MissionUI.instance, text);
                        if (changed)
                            valuableCount.SetVariable("Changed", false);
                    }
                }
            };
            valuableCount = new UpgradeItem(valuableCountBase);
            valuableCount.AddConfig("Display Total Value", true, 
                "Whether to display the total value next to the valuable counter.");
            valuableCount.AddConfig("Ignore Money Bags", false,
                "Whether to ignore the money bags from the extraction points.");
            upgradeItems.Add(valuableCount);
            Sprite mapTracker = assetBundle.LoadAsset<Sprite>("Map Tracker");
            float trackerDelay = 0.2f;
            void UpdateTracker(UpgradeItem upgradeItem)
            {
                PlayerAvatar playerAvatar = PlayerController.instance.playerAvatarScript;
                if (playerAvatar != null)
                {
                    bool hasUpgrade = upgradeItem.playerUpgrade.GetLevel(playerAvatar) > 0;
                    PlayerDeathHead playerDeathHead = (PlayerDeathHead)AccessTools.Field(typeof(PlayerAvatar), "playerDeathHead").GetValue(playerAvatar);
                    List<MapInfo> mapInfos = upgradeItem.GetVariable<List<MapInfo>>("Map Infos");
                    foreach (MapInfo mapInfo in mapInfos)
                    {
                        MapCustomEntity mapCustomEntity = mapInfo.mapCustomEntity;
                        SpriteRenderer spriteRenderer = mapCustomEntity.spriteRenderer;
                        if (hasUpgrade)
                        {
                            Transform parent = mapCustomEntity.Parent;
                            if (Map.Instance.Active)
                                Map.Instance.CustomPositionSet(mapCustomEntity.transform, parent);
                            spriteRenderer.sprite = upgradeItem.GetConfig<bool>("Arrow Icon") ? mapTracker :
                                playerDeathHead.mapCustom.sprite;
                            Color color = upgradeItem.GetConfig<Color>("Color");
                            if (upgradeItem.upgradeBase.name == "Map Player Tracker" &&
                                upgradeItem.GetConfig<bool>("Player Color"))
                                color =
                                    (Color)AccessTools.Field(typeof(PlayerAvatarVisuals),
                                        "color").GetValue(playerAvatar.playerAvatarVisuals);
                            MapLayer layerParent = Map.Instance.GetLayerParent(parent.position.y + 1f);
                            if (layerParent.layer == Map.Instance.PlayerLayer)
                                color.a = 1f;
                            else
                                color.a = 0.3f;
                            spriteRenderer.color = color;
                        }
                        spriteRenderer.enabled = hasUpgrade && mapInfo.visible;
                    }
                }
                updateTracker = false;
            };
            UpgradeItem.Base mapEnemyTrackerBase = new UpgradeItem.Base
            {
                name = "Map Enemy Tracker",
                minPrice = 50000,
                maxPrice = 60000,
                maxPurchaseAmount = 1,
                priceIncreaseScaling = 0
            };
            UpgradeItem mapEnemyTracker = null;
            mapEnemyTrackerBase.onVariablesStart += delegate
            {
                mapEnemyTracker.AddVariable("Map Infos", new List<MapInfo>());
            };
            mapEnemyTrackerBase.onUpdate += delegate
            {
                if (updateTracker || Time.time % trackerDelay < Time.deltaTime)
                    UpdateTracker(mapEnemyTracker);
            };
            mapEnemyTracker = new UpgradeItem(mapEnemyTrackerBase);
            mapEnemyTracker.AddConfig("Arrow Icon", true, 
                "Whether the icon should appear as an arrow showing direction instead of a dot.");
            mapEnemyTracker.AddConfig("Color", Color.red, "The color of the icon.");
            mapEnemyTracker.AddConfig("Exclude Enemies", "", 
                "Exclude specific enemies from displaying their icon by listing their names." +
                "\nExample: 'Gnome, Clown', seperated by commas.");
            upgradeItems.Add(mapEnemyTracker);
            UpgradeItem.Base mapPlayerTrackerBase = new UpgradeItem.Base
            {
                name = "Map Player Tracker",
                minPrice = 30000,
                maxPrice = 40000,
                maxPurchaseAmount = 1,
                priceIncreaseScaling = 0
            };
            UpgradeItem mapPlayerTracker = null;
            mapPlayerTrackerBase.onVariablesStart += delegate
            {
                mapPlayerTracker.AddVariable("Map Infos", new List<MapInfo>());
            };
            mapPlayerTrackerBase.onUpdate += delegate
            {
                if (updateTracker || Time.time % trackerDelay < Time.deltaTime)
                    UpdateTracker(mapPlayerTracker);
            };
            mapPlayerTracker = new UpgradeItem(mapPlayerTrackerBase);
            mapPlayerTracker.AddConfig("Arrow Icon", true, 
                "Whether the icon should appear as an arrow showing direction instead of a dot.");
            mapPlayerTracker.AddConfig("Player Color", false, "Whether the icon should be colored as the player.");
            mapPlayerTracker.AddConfig("Color", Color.blue, "The color of the icon.");
            upgradeItems.Add(mapPlayerTracker);
            UpgradeItem.Base itemResistBase = new UpgradeItem.Base
            {
                name = "Item Resist",
                maxAmount = 10,
                maxAmountInShop = 2,
                minPrice = 4000,
                maxPrice = 6000
            };
            UpgradeItem itemResist = null;
            itemResistBase.onVariablesStart += delegate
            {
                itemResist.AddVariable("Last Player Grabbed", new Dictionary<PhysGrabObject, PlayerAvatar>());
            };
            itemResist = new UpgradeItem(itemResistBase);
            itemResist.AddConfig("Scaling Factor", 0.9f,
                "Formula: valueLost * (scalingFactor ^ upgradeLevel)");
            itemResist.AddConfig("Print Valuables", false, "If enabled, the valuable name will be printed to the console that is being grabbed.");
            itemResist.AddConfig("Exclude Valuables", "", "Exclude specific valuables by listing their names, seperated by commas.");
            upgradeItems.Add(itemResist);
            UpgradeItem.Base mapZoomBase = new UpgradeItem.Base
            {
                name = "Map Zoom",
                maxAmount = 2,
                maxAmountInShop = 1,
                minPrice = 20000,
                maxPrice = 35000,
                maxPurchaseAmount = 2
            };
            UpgradeItem mapZoom = null;
            void UpdateMapZoom(PlayerAvatar playerAvatar, int level)
            {
                if (PlayerController.instance.playerAvatarScript != playerAvatar)
                    return;
                MapPatch.mapCamera.orthographicSize = MapPatch.defaultMapZoom + level * mapZoom.GetConfig<float>("Scaling Factor");
            }
            mapZoomBase.onStart += UpdateMapZoom;
            mapZoomBase.onUpgrade += UpdateMapZoom;
            mapZoom = new UpgradeItem(mapZoomBase);
            mapZoom.AddConfig("Scaling Factor", 0.75f,
                "Formula: defaultMapZoom + upgradeLevel * scalingFactor");
            upgradeItems.Add(mapZoom);
            UpgradeItem autoScan = new UpgradeItem(new UpgradeItem.Base
            {
                name = "Autoscan",
                maxAmount = 3,
                maxAmountInShop = 1,
                minPrice = 45000,
                maxPrice = 50000,
                maxPurchaseAmount = 3
            });
            autoScan.AddConfig("Silent Scanning", false,
                "Whether the scanned items should be silent or not.");
            autoScan.AddConfig("Scaling Factor", 5f,
                "Formula: upgradeLevel * scalingFactor");
            upgradeItems.Add(autoScan);
            UpgradeItem itemValue = new UpgradeItem(new UpgradeItem.Base
            {
                name = "Item Value",
                maxAmount = 10,
                maxAmountInShop = 2,
                minPrice = 75000,
                maxPrice = 82500
            });
            itemValue.AddConfig("Scaling Factor", 0.05f,
                "This variable is based on the host!\nFormula: itemValue * (1 + upgradeLevel * scalingFactor)");
            upgradeItems.Add(itemValue);
            UpgradeItem.Base extraLifeBase = new UpgradeItem.Base
            {
                name = "Extra Life",
                maxAmount = 10,
                maxAmountInShop = 2,
                minPrice = 150000,
                maxPrice = 225000
            };
            Keybind reviveKeybind = Keybinds.Bind("Revive", "<Keyboard>/r");
            extraLifeBase.onUpdate += delegate
            {
                PlayerAvatar playerAvatar = PlayerController.instance.playerAvatarScript;
                if (playerAvatar == null || !SemiFunc.NoTextInputsActive())
                    return;
                if (InputManager.instance.KeyUp(reviveKeybind.inputKey))
                    MoreUpgradesManager.instance.Revive(playerAvatar);
            };
            UpgradeItem extraLife = new UpgradeItem(extraLifeBase);
            extraLife.AddConfig("Singleplayer Invincibility Timer", 3f,
                "This variable is based on the host! After reviving, you will be given a short invincibility period.");
            extraLife.AddConfig("Multiplayer Invincibility Timer", 0f,
                "This variable is based on the host! After reviving, you will be given a short invincibility period.");
            upgradeItems.Add(extraLife);
            SceneManager.activeSceneChanged += delegate
            {
                if (RunManager.instance == null || RunManager.instance.levelCurrent == RunManager.instance.levelMainMenu 
                    || RunManager.instance.levelCurrent == RunManager.instance.levelLobbyMenu
                    || RunManager.instance.levelCurrent == RunManager.instance.levelSplashScreen)
                    return;
                GameObject manager = new GameObject("More Upgrades Manager");
                manager.AddComponent<MoreUpgradesManager>();
            };
            logger.LogMessage($"{modName} has started.");
            PatchAll("Patches");
            if (Compatibility.REPOLib.IsLoaded())
                Compatibility.REPOLib.OnAwake();
        }
    }
}