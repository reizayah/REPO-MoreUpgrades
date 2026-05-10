using BepInEx.Configuration;
using HarmonyLib;
using REPOLib.Modules;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace MoreUpgrades.Classes
{
    internal class UpgradeItem
    {
        internal class Base
        {
            public string name = null;
            public int maxAmount = 1;
            public int maxAmountInShop = 1;
            public float minPrice = 1000;
            public float maxPrice = 1000;
            public int maxPurchaseAmount = 0;
            public float priceIncreaseScaling = -1f;
            public Action<PlayerAvatar, int> onStart;
            public Action<PlayerAvatar, int> onUpgrade;
            public Action onVariablesStart;
            public Action onUpdate;
            public Action onLateUpdate;
            public Action onFixedUpdate;
            public List<string> excludeConfigs = new List<string>();
        }

        private bool isRepoLibImported;
        private string sectionName;
        public Base upgradeBase;
        public PlayerUpgrade playerUpgrade;
        private Dictionary<string, ConfigEntryBase> configEntries;
        public Dictionary<string, int> appliedPlayerDictionary;
        public Dictionary<string, object> variables;

        public bool AddConfig<T>(string key, T defaultValue, string description = "")
        {
            if (configEntries.ContainsKey(key))
            {
                Plugin.instance.logger.LogWarning($"A config entry with the key '{key}' already exists. Duplicates are not allowed.");
                return false;
            }
            if (upgradeBase.excludeConfigs.Contains(key))
                return false;
            configEntries.Add(key, Plugin.instance.Config.Bind(sectionName, key, defaultValue, description));
            return true;
        }

        public bool SetConfig<T>(string key, T value)
        {
            if (!configEntries.TryGetValue(key, out ConfigEntryBase entry))
            {
                Plugin.instance.logger.LogWarning($"A config entry with the key '{key}' does not exist. Returning default value.");
                return false;
            }
            if (entry is ConfigEntry<T> convertedEntry)
            {
                convertedEntry.Value = value;
                return true;
            }
            Plugin.instance.logger.LogWarning($"Type mismatch for config entry '{key}'." +
                $" Expected: {entry.SettingType.FullName}, but got: {typeof(T).FullName}. Returning default value.");
            return false;
        }

        public T GetConfig<T>(string key)
        {
            if (!configEntries.TryGetValue(key, out ConfigEntryBase value))
            {
                Plugin.instance.logger.LogWarning($"A config entry with the key '{key}' does not exist. Returning default value.");
                return default;
            }
            if (value is ConfigEntry<T> convertedValue)
                return convertedValue.Value;
            Plugin.instance.logger.LogWarning($"Type mismatch for config entry '{key}'." +
                $" Expected: {value.SettingType.FullName}, but got: {typeof(T).FullName}. Returning default value.");
            return default;
        }

        public bool HasVariable(string key) => variables.TryGetValue(key, out object _);

        public bool AddVariable<T>(string key, T value)
        {
            if (HasVariable(key))
            {
                Plugin.instance.logger.LogWarning($"A variable with the key '{key}' already exists. Duplicates are not allowed.");
                return false;
            }
            variables.Add(key, value);
            return true;
        }

        public bool SetVariable<T>(string key, T value)
        {
            if (!variables.TryGetValue(key, out object obj))
            {
                Plugin.instance.logger.LogWarning($"A variable with the key '{key}' does not exist.");
                return false;
            }
            if (obj is T)
            {
                variables[key] = value;
                return true;
            }
            Plugin.instance.logger.LogWarning($"Type mismatch for variable '{key}'." +
                $" Expected: {obj.GetType().FullName}, but got: {typeof(T).FullName}.");
            return false;
        }

        public T GetVariable<T>(string key)
        {
            if (!variables.TryGetValue(key, out object value))
            {
                Plugin.instance.logger.LogWarning($"A variable with the key '{key}' does not exist. Returning default value.");
                return default;
            }
            if (value is T convertedValue)
                return convertedValue;
            Plugin.instance.logger.LogWarning($"Type mismatch for variable '{key}'." +
                $" Expected: {value.GetType().FullName}, but got: {typeof(T).FullName}. Returning default value.");
            return default;
        }

        private void SetupConfig()
        {
            configEntries = new Dictionary<string, ConfigEntryBase>();
            AddConfig("Enabled", true, "Whether the upgrade item can be spawned to the shop.");
            AddConfig("Max Amount", upgradeBase.maxAmount, 
                "The maximum number of times the upgrade item can appear in the truck.");
            AddConfig("Max Amount In Shop", upgradeBase.maxAmountInShop,
                "The maximum number of times the upgrade item can appear in the shop.");
            AddConfig("Minimum Price", upgradeBase.minPrice, "The minimum cost to purchase the upgrade item.");
            AddConfig("Maximum Price", upgradeBase.maxPrice, "The maximum cost to purchase the upgrade item.");
            AddConfig("Price Increase Scaling", upgradeBase.priceIncreaseScaling,
                "The scale of the price increase based on the total number of upgrade item purchased." +
                "\nSet this value under 0 to use the default scaling.");
            AddConfig("Price Multiplier", isRepoLibImported ? -1f : 1f,
               "The multiplier of the price." +
               "\nSet this value under 0 to use the default multiplier.");
            AddConfig("Max Purchase Amount", upgradeBase.maxPurchaseAmount,
                "The maximum number of times the upgrade item can be purchased before it is no longer available in the shop." +
                "\nSet to 0 to disable the limit.");
            AddConfig("Allow Team Upgrades", false,
                "Whether the upgrade item applies to the entire team instead of just one player.");
            AddConfig("Sync Host Upgrades", false, "Whether the host should sync the item upgrade for the entire team.");
            AddConfig("Starting Amount", 0, "The number of times the upgrade item is applied at the start of the game.");
        }

        internal UpgradeItem(Base upgradeBase)
        {
            sectionName = upgradeBase.name;
            this.upgradeBase = upgradeBase;
            appliedPlayerDictionary = new Dictionary<string, int>();
            variables = new Dictionary<string, object>();
            SetupConfig();
            Item item = ScriptableObject.CreateInstance<Item>();
            item.itemType = SemiFunc.itemType.item_upgrade;
            item.emojiIcon = SemiFunc.emojiIcon.orb_battery;
            item.itemVolume = SemiFunc.itemVolume.upgrade;
            string assetName = $"Modded Item Upgrade Player {upgradeBase.name}";
            item.name = assetName;
            item.itemName = $"{upgradeBase.name} Upgrade";
            item.maxAmount = GetConfig<int>("Max Amount");
            item.maxAmountInShop = GetConfig<int>("Max Amount In Shop");
            item.maxPurchaseAmount = GetConfig<int>("Max Purchase Amount");
            item.maxPurchase = item.maxPurchaseAmount > 0;
            Value value = ScriptableObject.CreateInstance<Value>();
            value.valueMin = GetConfig<float>("Minimum Price");
            value.valueMax = GetConfig<float>("Maximum Price");
            item.value = value;
            GameObject prefab = Plugin.instance.assetBundle.LoadAsset<GameObject>(upgradeBase.name);
            prefab.name = assetName;
            REPOLibItemUpgrade itemUpgrade = prefab.GetComponent<REPOLibItemUpgrade>();
            AccessTools.Field(typeof(REPOLibItemUpgrade), "_upgradeId").SetValue(itemUpgrade, upgradeBase.name);
            ItemAttributes itemAttributes = prefab.GetComponent<ItemAttributes>();
            itemAttributes.item = item;
            Items.RegisterItem(itemAttributes);
            playerUpgrade = Upgrades.RegisterUpgrade(upgradeBase.name, item, upgradeBase.onStart, upgradeBase.onUpgrade);
        }

        internal UpgradeItem(PlayerUpgrade playerUpgrade)
        {
            isRepoLibImported = true;
            Item item = playerUpgrade.Item;
            upgradeBase = new Base
            {
                name = playerUpgrade.UpgradeId,
                maxAmount = item.maxAmount,
                maxAmountInShop = item.maxAmountInShop,
                maxPurchaseAmount = item.maxPurchaseAmount,
                minPrice = item.value.valueMin,
                maxPrice = item.value.valueMax
            };
            sectionName = $"{upgradeBase.name} ({Compatibility.REPOLib.modGUID})";
            appliedPlayerDictionary = new Dictionary<string, int>();
            SetupConfig();
            item.maxAmount = GetConfig<int>("Max Amount");
            item.maxAmountInShop = GetConfig<int>("Max Amount In Shop");
            item.maxPurchaseAmount = GetConfig<int>("Max Purchase Amount");
            item.maxPurchase = item.maxPurchaseAmount > 0;
            item.value.valueMin = GetConfig<float>("Minimum Price");
            item.value.valueMax = GetConfig<float>("Maximum Price");
            this.playerUpgrade = playerUpgrade;
        }
    }
}