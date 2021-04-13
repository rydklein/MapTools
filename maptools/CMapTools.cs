using System;
using System.IO;
using System.Text;
using System.Reflection;
using System.Collections.Generic;
using System.Data;
using System.Text.RegularExpressions;
using System.Threading;
using System.Timers;

using PRoCon.Core;
using PRoCon.Core.Plugin;
using PRoCon.Core.Plugin.Commands;
using PRoCon.Core.Players;
using PRoCon.Core.Players.Items;
using PRoCon.Core.Battlemap;
using PRoCon.Core.Maps;
using PRoCon.Core.HttpServer;
using System.Linq;

namespace PRoConEvents
{
    public class CMapTools : PRoConPluginAPI, IPRoConPluginInterface
    {
        // Procon Variables
        bool m_isPluginEnabled;
        private List<string> modeSettingsVar = new List<string>();
        private List<string> overrideSettingsVar = new List<string>();
        private bool presetsEnabled;
        private bool descsEnabled;
        // Gamemode settings + descriptions
        private Dictionary<string, GameSettings> modeSettings = new Dictionary<string, GameSettings>();
        private Dictionary<string, Dictionary<string, GameSettings>> overrideSettings = new Dictionary<string, Dictionary<string, GameSettings>>();
        private Dictionary<string, string> modeDescriptions = new Dictionary<string, string>();
        // Timers
        private static System.Timers.Timer applySettingsTimer;
        private static System.Timers.Timer refreshIndicesTimer;
        private static System.Timers.Timer giveTutorialTimer;
        // Map Info
        private List<MaplistEntry> currMapList;
        private string currMap;
        private string currMode;
        private string nextMap;
        private string nextMode;
        private struct GameSettings
        {
            public bool VehicleSpawns;
            public int GameTimeLimit;
            public int GameModeCounter;
            public List<KeyValuePair<string, string>> CustomCommands;
        }

        public CMapTools()
        {

            this.m_isPluginEnabled = false;
        }

        public string GetPluginName()
        {
            return "MapTools";
        }

        public string GetPluginVersion()
        {
            return "1.0";
        }

        public string GetPluginAuthor()
        {
            return "BadPylot";
        }

        public string GetPluginWebsite()
        {
            return "discord.gg/hh5RKFHBeZ";
        }

        public string GetPluginDescription()
        {
            return @"";
        }

        public void OnPluginLoaded(string strHostName, string strPort, string strPRoConVersion)
        {

            this.RegisterEvents(this.GetType().Name, "OnRoundOver", "OnMaplistGetMapIndices", "OnMaplistList", "OnLevelLoaded");
        }

        public void OnPluginEnable()
        {
            this.sayConsole("^bMapTools ^2Enabled!");

            this.m_isPluginEnabled = true;
        }

        public void OnPluginDisable()
        {
            this.sayConsole("^bMapTools ^1Disabled!");
            applySettingsTimer.Enabled = false;
            this.m_isPluginEnabled = false;
        }

        public List<CPluginVariable> GetDisplayPluginVariables()
        {
            List<CPluginVariable> lstReturn = new List<CPluginVariable>();
            lstReturn.Add(new CPluginVariable("Map/Mode Presets|Presets Enabled", typeof(bool), this.presetsEnabled));
            lstReturn.Add(new CPluginVariable("Map/Mode Presets|Default Mode Settings", typeof(String[]), this.modeSettingsVar.ToArray()));
            lstReturn.Add(new CPluginVariable("Map/Mode Presets|Overrides", typeof(String[]), this.overrideSettingsVar.ToArray()));
            //lstReturn.Add(new CPluginVariable("Mode Descriptions|Enabled", typeof(bool), this.descsEnabled));
            return lstReturn;
        }

        public List<CPluginVariable> GetPluginVariables()
        {
            return GetDisplayPluginVariables();
        }

        public void SetPluginVariable(string strVariable, string strValue)
        {
            if (strVariable.Contains("|"))
            {
                strVariable = strVariable.Split('|')[1];
            }
            switch (strVariable)
            {
                case ("Presets Enabled"):
                    {
                        bool tPresetsEnabled;
                        if (!Boolean.TryParse(strValue, out tPresetsEnabled))
                        {
                            this.sayConsole("Invalid Input.");
                            return;
                        }
                        presetsEnabled = tPresetsEnabled;
                        if (!presetsEnabled && (applySettingsTimer != null))
                        {
                            applySettingsTimer.Enabled = false;
                        }
                        this.sayConsole("Settings updated.");
                        return;
                    }
                case "Default Mode Settings":
                    {
                        // Make a temporary, empty list to modify
                        Dictionary<string, GameSettings> tmodeSettings = new Dictionary<string, GameSettings>();
                        // Make a variable we can use to determine whether all settings were parsed successfully
                        bool success = true;
                        // Decode the String input into a list.
                        List<string> tmodeSettingsVar = new List<string>(CPluginVariable.DecodeStringArray(strValue));
                        foreach (string singleLine in tmodeSettingsVar)
                        {
                            GameSettings parsedGameSettings = new GameSettings();
                            String[] inputLineA = singleLine.Split(',');
                            // If parsing fails, or if the mode is already in the list, set success to false, and break the loop.
                            if (!parseGameSettingsString(inputLineA, 0, out parsedGameSettings) || tmodeSettings.ContainsKey(inputLineA[0]))
                            {
                                success = false;
                                break;
                            }
                            // Add the parsed game settings to the dictionary of modes + settings, with the key being the mode name
                            // And the value being the game settings.
                            tmodeSettings.Add(inputLineA[0], parsedGameSettings);
                        }
                        if (!success)
                        {
                            this.sayConsole("Invalid Input.");
                            return;
                        }
                        this.sayConsole("Settings updated.");
                        modeSettings = tmodeSettings;
                        modeSettingsVar = tmodeSettingsVar;
                        return;
                    }
                case "Overrides":
                    {
                        // Make a temporary, empty list to modify
                        Dictionary<string, Dictionary<string, GameSettings>> tOverrideSettings = new Dictionary<string, Dictionary<string, GameSettings>>();
                        // Make a variable we can use to determine whether all settings were parsed successfully
                        bool success = true;
                        // Decode the String input into a list.
                        List<string> tOverrideSettingsVar = new List<string>(CPluginVariable.DecodeStringArray(strValue));
                        foreach (string singleLine in tOverrideSettingsVar)
                        {
                            GameSettings parsedGameSettings = new GameSettings();
                            String[] inputLineA = singleLine.Split(',');
                            // If parsing fails, or if the mode is already in the list, set success to false, and break the loop.
                            if (!parseGameSettingsString(inputLineA, 1, out parsedGameSettings) || (tOverrideSettings.ContainsKey(inputLineA[0]) && tOverrideSettings.ContainsKey(inputLineA[1])))
                            {
                                success = false;
                                break;
                            }
                            // Add the parsed game settings to the dictionary of modes + settings, with the key being the mode name
                            // And the value being the game settings.
                            if (!tOverrideSettings.ContainsKey(inputLineA[0]))
                            {
                                tOverrideSettings.Add(inputLineA[0], new Dictionary<string, GameSettings>());
                            }
                            tOverrideSettings[inputLineA[0]].Add(inputLineA[1], parsedGameSettings);
                        }
                        if (!success)
                        {
                            this.sayConsole("Invalid Input.");
                            return;
                        }
                        this.sayConsole("Settings updated.");
                        overrideSettings = tOverrideSettings;
                        overrideSettingsVar = tOverrideSettingsVar;
                        return;
                    }
            }
        }
        private bool parseGameSettingsString(string[] gameSettingsA, int offset, out GameSettings userGameSettings)
        {
            // So that we can return before assigning to userGameSettings
            userGameSettings = default;
            // Game settings has to be at least 4.
            if (gameSettingsA.Length < offset + 3)
            {
                return false;
            }
            // If not a bool, return false..
            if (!bool.TryParse(gameSettingsA[offset + 1], out userGameSettings.VehicleSpawns))
            {
                return false;
            }
            // If not a number, return false.
            try
            {
                userGameSettings.GameModeCounter = short.Parse(gameSettingsA[offset + 2]);
                userGameSettings.GameTimeLimit = short.Parse(gameSettingsA[offset + 3]);
            }
            catch
            {
                return false;
            }
            // Iterate through all of the custom commands and assign them to the object.
            List<KeyValuePair<string, string>> tCustomCommands = new List<KeyValuePair<string, string>>();
                for (int i = gameSettingsA.Length; i > offset + 4; i = i - 2)
            {
                int index = i - 1;
                tCustomCommands.Add(new KeyValuePair<string, string>(gameSettingsA[i - 2], gameSettingsA[i - 1]));
            }
            userGameSettings.CustomCommands = tCustomCommands;
            return true;
        }

        private void rconCommand(params String[] arguments)
        {
            string[] execArgs = { "procon.protected.send" };
            this.ExecuteCommand(execArgs.Concat(arguments).ToArray());
        }
        private void sayConsole(String arguments)
        {
            this.ExecuteCommand("procon.protected.pluginconsole.write", arguments);
        }
        #region events
        public override void OnRoundOver(int winningTeamId)
        {
            if (!m_isPluginEnabled || !presetsEnabled) return;
            // Refresh indices so that we know for sure what the next mode is.
            // Votemap likes to wait until the last minute to set the next map.
            refreshIndicesTimer = new System.Timers.Timer(1000);
            refreshIndicesTimer.Elapsed += refreshIndices;
            refreshIndicesTimer.Enabled = true;
            // Gives indices time to refresh before actually applying settings.
            applySettingsTimer = new System.Timers.Timer(3000);
            applySettingsTimer.Elapsed += runCommands;
            applySettingsTimer.Enabled = true;
        }

        public override void OnMaplistList(List<MaplistEntry> lstMaplist)
        {
            this.currMapList = new List<MaplistEntry>(lstMaplist);
        }

        public override void OnMaplistGetMapIndices(int mapIndex, int nextIndex)
        {
            this.nextMap = currMapList[nextIndex].MapFileName;
            this.nextMode = currMapList[nextIndex].Gamemode;
        }
        public override void OnLevelLoaded(String strMapFileName, String strMapMode, Int32 roundsPlayed, Int32 roundsTotal)
        {
            this.currMap = strMapFileName;
            this.currMode = strMapMode;
        }
        #endregion
        private void runCommands(Object source, System.Timers.ElapsedEventArgs e)
        {
            applySettingsTimer.Enabled = false;
            // Reset Vehicle Spawn Delay
            sayConsole($"Executing default commands for Mode {nextMode}.");
            this.rconCommand("vars.vehicleSpawnDelay", "100");
            this.rconCommand("vars.vehicleSpawnAllowed", modeSettings[nextMode].VehicleSpawns.ToString().ToLower());
            this.rconCommand("vars.gamemodeCounter", modeSettings[nextMode].GameModeCounter.ToString());
            this.rconCommand("vars.roundTimeLimit", modeSettings[nextMode].GameTimeLimit.ToString());
            if (modeSettings[nextMode].CustomCommands != null)
            {
                foreach (KeyValuePair<string, string> customCommand in modeSettings[nextMode].CustomCommands)
                {
                    this.rconCommand(customCommand.Key, customCommand.Value);
                }
            }
            if (overrideSettings.ContainsKey(nextMode) && overrideSettings[nextMode].ContainsKey(nextMap))
            {
                sayConsole($"Executing overrides for Mode {nextMode}, Map {nextMap}.");
                this.rconCommand("vars.vehicleSpawnAllowed", overrideSettings[nextMode][nextMap].VehicleSpawns.ToString().ToLower());
                this.rconCommand("vars.gamemodeCounter", overrideSettings[nextMode][nextMap].GameModeCounter.ToString());
                this.rconCommand("vars.roundTimeLimit", overrideSettings[nextMode][nextMap].GameTimeLimit.ToString());
                if (overrideSettings[nextMode][nextMap].CustomCommands != null)
                {
                    foreach (KeyValuePair<string, string> customCommand in overrideSettings[nextMode][nextMap].CustomCommands)
                    {
                        this.rconCommand(customCommand.Key, customCommand.Value);
                    }
                }
            }
        }
        private void refreshIndices(Object source, System.Timers.ElapsedEventArgs e)
        {
            refreshIndicesTimer.Enabled = false;
            rconCommand("mapList.list");
            rconCommand("mapList.getMapIndices");
        }
    }
}
