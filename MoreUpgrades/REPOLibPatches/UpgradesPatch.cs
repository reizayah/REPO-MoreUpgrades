using HarmonyLib;
using MoreUpgrades.Classes;
using REPOLib.Modules;
using System.Collections.Generic;
using System.Linq;

namespace MoreUpgrades.REPOLibPatches
{
    [HarmonyPatch(typeof(Upgrades))]
    internal class UpgradesPatch
    {
        [HarmonyPatch("RegisterUpgrades")]
        [HarmonyPostfix]
        static void RegisterUpgrades(Dictionary<string, PlayerUpgrade> ____playerUpgrades)
        {
            if (Plugin.instance.assetBundle == null || StatsManager.instance == null)
                return;
            foreach (KeyValuePair<string, PlayerUpgrade> pair in ____playerUpgrades)
            {
                UpgradeItem upgradeItem = Plugin.instance.upgradeItems.FirstOrDefault(x => x.playerUpgrade.Item == pair.Value.Item);
                if (upgradeItem == null)
                    continue;
                StatsManager.instance.upgradesInfo.Add("playerUpgrade" + pair.Key, new StatsManager.UpgradeInfo
                {
                    displayName = upgradeItem.upgradeBase.name
                });
                string key = "appliedPlayerUpgrade" + pair.Key;
                Dictionary<string, int> appliedPlayerDictionary = upgradeItem.appliedPlayerDictionary;
                SortedDictionary<string, Dictionary<string, int>> dictionaryOfDictionaries = 
                    (SortedDictionary<string, Dictionary<string, int>>)AccessTools.Field(typeof(StatsManager),
                    "dictionaryOfDictionaries").GetValue(StatsManager.instance);
                if (dictionaryOfDictionaries.TryGetValue(key, out Dictionary<string, int> dictionary))
                    appliedPlayerDictionary = dictionary;
                else
                {
                    appliedPlayerDictionary.Clear();
                    dictionaryOfDictionaries.Add(key, appliedPlayerDictionary);
                }
            }
        }

        [HarmonyPatch("RegisterUpgrade")]
        [HarmonyPostfix]
        static void RegisterUpgrade(ref PlayerUpgrade __result)
        {
            if (Plugin.instance.assetBundle == null || __result == null)
                return;
            if (Plugin.instance.importUpgrades.Value && !Plugin.instance.excludeUpgradeIds.Value.Split(',').Select(x => x.Trim())
                .Where(x => !string.IsNullOrEmpty(x)).Contains(__result.UpgradeId))
                Plugin.instance.upgradeItems.Add(new UpgradeItem(__result));
        }
    }
}