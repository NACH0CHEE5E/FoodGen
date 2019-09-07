using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Pipliz;
using Pipliz.JSON;
using Recipes;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Chatting;
using Shared;
using NetworkUI;
using NetworkUI.Items;

namespace FoodGen
{
    [ModLoader.ModManager]
    class Class1
    {
        public const string MOD_VERSION = "0.1.0";

        public const string NAME = "NACH0";
        public const string MODNAME = "FoodGen";
        public const string MODNAMESPACE = NAME + "." + MODNAME + ".";

        public static string GAMEDATA_FOLDER = @"";
        public static string GAME_SAVES = @"";
        public static string GAME_SAVEFILE = @"";
        public static string GAME_ROOT = @"";
        public static string MOD_FOLDER = @"gamedata/mods/NACH0/Decor";
        public static string MOD_MESH_PATH = "./meshes";
        public static string MOD_ICON_PATH = "./textures/icons";
        public static string MOD_CUSTOM_TEXTURE_PATH = "./textures/custom";

        public static string FILE_NAME = "FoodGen.json";
        public static string FILE_PATH = @"";

        public static Dictionary<string, Dictionary<string, int>> FoodValues = new Dictionary<string, Dictionary<string, int>>();
        //public static Dictionary<string, int> Settings = new Dictionary<string, int>();
        static bool WasDay = false;
        static int day = 0;

        [ModLoader.ModCallback(ModLoader.EModCallbackType.OnAssemblyLoaded, MODNAMESPACE + "OnAssemblyLoaded")]
        public static void OnAssemblyLoaded(string path)
        {
            MOD_FOLDER = Path.GetDirectoryName(path) + "/";

            GAME_ROOT = path.Substring(0, path.IndexOf("gamedata")).Replace("\\", "/") + "/";
            GAMEDATA_FOLDER = path.Substring(0, path.IndexOf("gamedata") + "gamedata".Length).Replace("\\", "/") + "/";
            GAME_SAVES = GAMEDATA_FOLDER + "savegames/";
        }

        [ModLoader.ModCallback(ModLoader.EModCallbackType.AfterSelectedWorld, MODNAMESPACE + "AfterSelectedWorld")]
        public static void AfterSelectedWorld()
        {
            GAME_SAVEFILE = GAME_SAVES + ServerManager.WorldName + "/";
            FILE_PATH = GAME_SAVEFILE + FILE_NAME;

        }

        [ModLoader.ModCallback(ModLoader.EModCallbackType.AfterWorldLoad, MODNAMESPACE + "AfterWorldLoad")]
        public static void AfterWorldLoad()
        {
            day = Pipliz.Math.RoundToInt(System.Math.Floor(TimeCycle.TotalHours / 24));
            if (TimeCycle.IsDay)
            {
                WasDay = true;
            }
            if (!File.Exists(FILE_PATH))
            {
                File.Create(FILE_PATH);
                //FoodValues.Add("default", "0", 0)

            }
            else
            {
                var FILE_CONTENTS = File.ReadAllText(FILE_PATH);
                if (FILE_CONTENTS != "")
                {
                    FoodValues = JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, int>>>(FILE_CONTENTS);
                }
            }
        }
        [ModLoader.ModCallback(ModLoader.EModCallbackType.OnUpdate, MODNAMESPACE + "OnUpdate")]
        public static void OnUpdate()
        {
            if (!WasDay && TimeCycle.IsDay)
            {
                WasDay = true;
                day = Pipliz.Math.RoundToInt(System.Math.Floor(TimeCycle.TotalHours / 24));
                return;
            }
            else if (WasDay && !TimeCycle.IsDay)
            {
                day = Pipliz.Math.RoundToInt(System.Math.Floor(TimeCycle.TotalHours / 24));
                foreach (Colony colony in ServerManager.ColonyTracker.ColoniesByID.Values)
                {
                    if (!FoodValues.ContainsKey(colony.ColonyID.ToString()))
                    {
                        FoodValues[colony.ColonyID.ToString()] = new Dictionary<string, int>();
                    }
                    if (!FoodValues[colony.ColonyID.ToString()].ContainsKey(day.ToString()))
                    {
                        FoodValues[colony.ColonyID.ToString()].Add(day.ToString(), Pipliz.Math.RoundToInt(colony.Stockpile.TotalFood));
                    }
                    List<string> keysToRemove = new List<string>();
                    foreach (var dict in FoodValues[colony.ColonyID.ToString()])
                    {
                        if (Int32.Parse(dict.Key) < day - 10)
                        {
                            keysToRemove.Add(dict.Key);
                        }
                    }
                    foreach (var key in keysToRemove)
                    {
                        FoodValues[colony.ColonyID.ToString()].Remove(key);
                    }
                }
                WasDay = false;
            }
        }
        [ModLoader.ModCallback(ModLoader.EModCallbackType.OnAutoSaveWorld, MODNAMESPACE + "OnAutoSaveWorld")]
        public static void OnAutoSaveWorld()
        {
            var food = JsonConvert.SerializeObject(FoodValues, Formatting.Indented);
            File.WriteAllText(FILE_PATH, food);
            return;
        }
        [ModLoader.ModCallback(ModLoader.EModCallbackType.OnSaveWorldMisc, MODNAMESPACE + "OnSaveWorldMisc")]
        public static void OnSaveWorldMisc(JObject j)
        {
            var food = JsonConvert.SerializeObject(FoodValues, Formatting.Indented);
            File.WriteAllText(FILE_PATH, food);
            return;
        }
        [ModLoader.ModCallback(ModLoader.EModCallbackType.OnPlayerClicked, MODNAMESPACE + "OnPlayerClick")]
        public static void PlaceItem(Players.Player player, PlayerClickedData data)
        {
            if (data.TypeSelected == ItemTypes.GetType("NACH0.Types.FoodGen").ItemIndex)
            {
                if (data.ClickType == PlayerClickedData.EClickType.Left)
                {
                    SendUI(player);
                }
            }
        }
        public static void SendUI(Players.Player player)
        {
            NetworkMenu FoodUI = new NetworkMenu();
            FoodUI.Identifier = "FoodGenUI";
            FoodUI.LocalStorage.SetAs("header", "Food Generation Stats");
            FoodUI.Width = 600;
            FoodUI.Height = 200;

            Label emptyLabel = new Label("Change:");
            Label fromYesterdayLabel = new Label("from yesterday");
            Label from5DaysLabel = new Label("average per day over 5 days");
            List<(IItem, int)> LabelRow1 = new List<(IItem, int)>();

            LabelRow1.Add((emptyLabel, 100));
            LabelRow1.Add((fromYesterdayLabel, 200));
            LabelRow1.Add((from5DaysLabel, 200));

            HorizontalRow HorizontalLabelRow1 = new HorizontalRow(LabelRow1);

            FoodUI.Items.Add(HorizontalLabelRow1);

            string changeFromYesterdayAmount = "No data";
            string changeFrom5DaysAmount = "No data";
            if (FoodValues.ContainsKey(player.ActiveColony.ColonyID.ToString()))
            {
                int todayAmount = Pipliz.Math.RoundToInt(player.ActiveColony.Stockpile.TotalFood);
                int yesterdayAmount = -1;
                int days5AgoAmount = -1;

                if (FoodValues[player.ActiveColony.ColonyID.ToString()].ContainsKey((day - 1).ToString()))
                {
                    yesterdayAmount = FoodValues[player.ActiveColony.ColonyID.ToString()][(day - 1).ToString()];
                }
                if (FoodValues[player.ActiveColony.ColonyID.ToString()].ContainsKey((day - 5).ToString()))
                {
                    days5AgoAmount = FoodValues[player.ActiveColony.ColonyID.ToString()][(day - 5).ToString()];
                }
                if (yesterdayAmount != -1)
                {
                    int amountYesterday = (todayAmount - yesterdayAmount) * 2000;
                    if (amountYesterday >= 10000 || amountYesterday <= -10000)
                    {
                        amountYesterday = amountYesterday / 1000;
                        changeFromYesterdayAmount = String.Format("{0:n0}", amountYesterday) + "K cals";
                    }
                    else
                    {
                        changeFromYesterdayAmount = String.Format("{0:n0}", amountYesterday) + " cals";
                    }
                }
                if (days5AgoAmount != -1)
                {
                    int amount5days = (todayAmount - days5AgoAmount) * 2000 / 5;
                    if (amount5days >= 10000 || amount5days <= -10000)
                    {
                        amount5days = amount5days / 1000;
                        changeFrom5DaysAmount = String.Format("{0:n0}", amount5days) + "K cals";
                    }
                    else
                    {
                        changeFrom5DaysAmount = String.Format("{0:n0}", amount5days) + " cals";
                    }
                }


            }


            Label changeLabel = new Label("Amount:");
            Label fromYesterdayAmountLabel = new Label(changeFromYesterdayAmount);
            Label from5DaysAmountLabel = new Label(changeFrom5DaysAmount);
            List<(IItem, int)> LabelRow2 = new List<(IItem, int)>();

            LabelRow2.Add((changeLabel, 100));
            LabelRow2.Add((fromYesterdayAmountLabel, 200));
            LabelRow2.Add((from5DaysAmountLabel, 200));

            HorizontalRow HorizontalLabelRow2 = new HorizontalRow(LabelRow2);
            FoodUI.Items.Add(HorizontalLabelRow2);

            NetworkMenuManager.SendServerPopup(player, FoodUI);
        }
    }
}
