using System;
using System.IO;
using System.Text;
using System.Reflection;
using System.Collections.Generic;
using System.Data;
using System.Text.RegularExpressions;
using System.Threading;
using System.Timers;
using System.Net;

using PRoCon.Core;
using PRoCon.Core.Plugin;
using PRoCon.Core.Plugin.Commands;
using PRoCon.Core.Players;
using PRoCon.Core.Players.Items;
using PRoCon.Core.Battlemap;
using PRoCon.Core.Maps;
using PRoCon.Core.HttpServer;
using System.Linq;
using System.Collections;

namespace PRoConEvents
{
    public class MapTools : PRoConPluginAPI, IPRoConPluginInterface
    {
        // Procon Variables
        bool isPluginEnabled;
        private List<string> modeSettingsVar = new List<string>()
        {
            "ConquestLarge0,true,100,100",
            "ConquestSmall0,true,100,100",
            "CarrierAssaultLarge0,true,100,100",
            "RushLarge0,true,100,100",
            "Obliteration,true,100,100",
            "CaptureTheFlag0,true,100,100",
            "Chainlink0,true,100,100",
        };
        private List<string> overrideSettingsVar = new List<string>()
        {
            "RushLarge0,MP_Siege,false,100,100",
            "ConquestLarge0,MP_Flooded,false,100,100",
        };
        private bool presetsEnabled = false;
        private bool fastForwardEnable = false;
        private int fastForwardDelay = 15;
        // Gamemode settings
        private Dictionary<string, GameSettings> modeSettings = new Dictionary<string, GameSettings>();
        private Dictionary<string, Dictionary<string, GameSettings>> overrideSettings = new Dictionary<string, Dictionary<string, GameSettings>>();
        // Timers
        private System.Timers.Timer applySettingsTimer;
        private System.Timers.Timer refreshIndicesTimer;
        private System.Timers.Timer fastFowardTimer;
        // Map Info
        private List<MaplistEntry> currMapList;
        private bool isLastRound;
        private bool roundEnded;
        private string currMap;
        private string currMode;
        private string nextMap;
        private string nextMode;

        private struct GameSettings
        {
            public bool VehicleSpawns;
            public int GameTimeLimit;
            public int GameModeCounter;
        }

        public MapTools()
        {
            isPluginEnabled = false;
        }
        #region Plugin Info
        public string GetPluginName()
        {
            return "MapTools";
        }

        public string GetPluginVersion()
        {
            return "1.1.0";
        }

        public string GetPluginAuthor()
        {
            return "BadPylot";
        }

        public string GetPluginWebsite()
        {
            return "https://github.com/BadPylot/MapTools";
        }

        public string GetPluginDescription()
        {
            return "See the <a href='https://github.com/BadPylot/MapTools'>MapTools Github Repository</a> for documentation.<br>Copy-Pastable Link:<br>https://github.com/BadPylot/MapTools";
        }
        #endregion
        #region Procon Events
        public void OnPluginLoaded(string strHostName, string strPort, string strPRoConVersion)
        {
            string latestVersion = getLatestVersion();
            if (!GetPluginVersion().Equals(latestVersion))
            {
                sayConsole($"A new version of MapTools is available on GitHub: v{latestVersion}.");
                sayConsole($"Download the latest version of MapTools at https://github.com/BadPylot/MapTools/releases/tag/{latestVersion} for the latest features and bugfixes.");
            }
            RegisterEvents(GetType().Name, "OnRoundOver", "OnMaplistGetMapIndices", "OnMaplistList", "OnLevelLoaded");
            updateMapData();
        }

        public void OnPluginEnable()
        {
            sayConsole("Plugin Enabled.");
            isPluginEnabled = true;
        }

        public void OnPluginDisable()
        {
            sayConsole("Plugin Disabled.");
            applySettingsTimer.Enabled = false;
            fastFowardTimer.Enabled = false;
            isPluginEnabled = false;
        }
        #endregion
        #region Variables
        public List<CPluginVariable> GetDisplayPluginVariables()
        {
            List<CPluginVariable> lstReturn = new List<CPluginVariable>();
            lstReturn.Add(new CPluginVariable("Map/Mode Presets|Presets Enabled", typeof(bool), presetsEnabled));
            lstReturn.Add(new CPluginVariable("Map/Mode Presets|Default Mode Settings", typeof(String[]), modeSettingsVar.ToArray()));
            lstReturn.Add(new CPluginVariable("Map/Mode Presets|Map Overrides", typeof(String[]), overrideSettingsVar.ToArray()));
            lstReturn.Add(new CPluginVariable("Fast Forward|Fast Forward Enabled", typeof(bool), fastForwardEnable));
            lstReturn.Add(new CPluginVariable("Fast Forward|Fast Forward Delay", typeof(int), fastForwardDelay));
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
                            return;
                        }
                        presetsEnabled = tPresetsEnabled;
                        if (!presetsEnabled && (applySettingsTimer != null))
                        {
                            applySettingsTimer.Enabled = false;
                        }
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
                            // Make the mode name lowercase. Capitalization errors can REALLY mess up your day.
                            inputLineA[0] = inputLineA[0].ToLower();
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
                            sayConsole("Default Mode Settings: Invalid Input.");
                            return;
                        }
                        modeSettings = tmodeSettings;
                        modeSettingsVar = tmodeSettingsVar;
                        return;
                    }
                case "Map Overrides":
                    {
                        // Make a temporary, empty list to modify
                        Dictionary<string, Dictionary<string, GameSettings>> tOverrideSettings = new Dictionary<string, Dictionary<string, GameSettings>>();
                        // Make a variable we can use to determine whether all settings were parsed successfully
                        bool success = true;
                        // Decode the String input into a list.
                        List<string> tOverrideSettingsVar = new List<string>(CPluginVariable.DecodeStringArray(strValue));
                        if (tOverrideSettingsVar[0] != "")
                        {
                            foreach (string singleLine in tOverrideSettingsVar)
                            {
                                GameSettings parsedGameSettings = new GameSettings();
                                String[] inputLineA = singleLine.Split(',');
                                if (inputLineA.Length < 4)
                                {
                                    success = false;
                                    break;
                                }
                                // Lowercase BS again
                                inputLineA[0] = inputLineA[0].ToLower();
                                inputLineA[1] = inputLineA[1].ToLower();
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
                                sayConsole("Map Overrides: Invalid Input.");
                                return;
                            }
                        }
                        overrideSettings = tOverrideSettings;
                        overrideSettingsVar = tOverrideSettingsVar;
                        return;
                    }
                case "Fast Forward Enabled":
                    {
                        fastForwardEnable = Boolean.Parse(CPluginVariable.Decode(strValue));
                        return;
                    }
                case "Fast Forward Delay":
                    {
                        int tempFFDelay = 0;
                        bool success = true;
                        if (!int.TryParse(CPluginVariable.Decode(strValue), out tempFFDelay)) success = false;
                        if (!((14 < tempFFDelay) && (tempFFDelay < 60))) success = false;
                        if (!success)
                        {
                            sayConsole("Fast Forward Delay: Invalid Input. Delay must be an integer between 15 and 60.");
                            return;
                        }
                        fastForwardDelay = tempFFDelay;
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
            return true;
        }
        #endregion
        #region Events
        public override void OnRoundOver(int winningTeamId)
        {
            try
            {
                roundEnded = true;
                if (!isPluginEnabled) return;
                int secondsToRestart = 60;
                if (fastForwardEnable)
                {
                    secondsToRestart = fastForwardDelay + 1;
                    fastFowardTimer = new System.Timers.Timer(secondsToRestart * 1000);
                    fastFowardTimer.Elapsed += skipRoundTimerWrapper;
                    fastFowardTimer.Enabled = true;
                }
                if (!presetsEnabled || !isLastRound) return;
                // Refresh indices so that we know for sure what the next mode is.
                // Votemap likes to wait until the last minute to set the next map.
                refreshIndicesTimer = new System.Timers.Timer((secondsToRestart - 6) * 1000);
                refreshIndicesTimer.Elapsed += updateMapDataTimerWrapper;
                refreshIndicesTimer.Enabled = true;
                // Gives indices time to refresh before actually applying settings.
                applySettingsTimer = new System.Timers.Timer((secondsToRestart - 3) * 1000);
                applySettingsTimer.Elapsed += runCommandsTimerWrapper;
                applySettingsTimer.Enabled = true;
            } 
            catch (Exception e)
            {
                sayConsole(e.GetType().ToString());
                sayConsole(e.Message);
                sayConsole(e.StackTrace);
            }
        }

        public override void OnMaplistList(List<MaplistEntry> lstMaplist)
        {
            currMapList = new List<MaplistEntry>(lstMaplist);
        }

        public override void OnMaplistGetMapIndices(int mapIndex, int nextIndex)
        {
            nextMap = currMapList[nextIndex].MapFileName;
            nextMode = currMapList[nextIndex].Gamemode;

        }
        public override void OnLevelLoaded(String strMapFileName, String strMapMode, Int32 roundsPlayed, Int32 roundsTotal)
        {
            isLastRound = (roundsPlayed + 1 == roundsTotal || roundsTotal == 0);
            string lastMap = currMap;
            string lastMode = currMode;
            currMap = strMapFileName;
            currMode = strMapMode;
            if ((!((lastMap == currMap) && (lastMode == currMode))) && !roundEnded && isPluginEnabled && presetsEnabled)
            {
                sayConsole("Manual map change detected. Running commands and restarting round.");
                runCommands(currMap, currMode);
                rconCommand("mapList.restartRound");
            }
            roundEnded = false;
        }
        #endregion
        #region Timers
        private void runCommandsTimerWrapper(Object source, System.Timers.ElapsedEventArgs e)
        {
            applySettingsTimer.Enabled = false;
            runCommands(nextMap, nextMode);
        }
        private void updateMapDataTimerWrapper(Object source, System.Timers.ElapsedEventArgs e)
        {
            refreshIndicesTimer.Enabled = false;
            updateMapData();
        }
        private void skipRoundTimerWrapper(Object source, System.Timers.ElapsedEventArgs e)
        {
            fastFowardTimer.Enabled = false;
            sayConsole("Running Next Round...");
            rconCommand("mapList.runNextRound");
        }
        #endregion
        #region Main Functions
        private void runCommands(string cmdMap, string cmdMode)
        {
            string cmdMapLower = cmdMap.ToLower();
            string cmdModeLower = cmdMode.ToLower();
            if ((cmdMap == null) || (cmdMode == null))
            {
                sayConsole("Current Map/Mode not found.");
                return;
            }
            try
            {
                if (overrideSettings.ContainsKey(cmdModeLower) && overrideSettings[cmdModeLower].ContainsKey(cmdMapLower))
                {
                    sayConsole($"Executing override commands for Mode {cmdMode}, Map {cmdMap}.");
                    rconCommand("vars.vehicleSpawnAllowed", overrideSettings[cmdModeLower][cmdMapLower].VehicleSpawns.ToString().ToLower());
                    rconCommand("vars.gamemodeCounter", overrideSettings[cmdModeLower][cmdMapLower].GameModeCounter.ToString());
                    rconCommand("vars.roundTimeLimit", overrideSettings[cmdModeLower][cmdMapLower].GameTimeLimit.ToString());
                }
                else
                {
                    sayConsole($"Executing default commands for Mode {cmdMode}.");
                    rconCommand("vars.vehicleSpawnAllowed", modeSettings[cmdModeLower].VehicleSpawns.ToString().ToLower());
                    rconCommand("vars.gamemodeCounter", modeSettings[cmdModeLower].GameModeCounter.ToString());
                    rconCommand("vars.roundTimeLimit", modeSettings[cmdModeLower].GameTimeLimit.ToString());
                }
            }
            catch (Exception error)
            {
                sayConsole($"Default commands not found for mode {cmdMode}. Settings will not be changed!");
                sayConsole(error.GetType().Name);
                sayConsole(error.Message);
            }
        }
        #endregion
        #region Helpers
        private void rconCommand(params String[] arguments)
        {
            string[] execArgs = { "procon.protected.send" };
            ExecuteCommand(execArgs.Concat(arguments).ToArray());
        }
        private void updateMapData()
        {
            rconCommand("mapList.list");
            rconCommand("mapList.getMapIndices");
        }
        private void sayConsole(String arguments)
        {
            ExecuteCommand("procon.protected.pluginconsole.write", "[MapTools] " + arguments);
        }
        private string getLatestVersion()
        {
            using (WebClient client = new WebClient())
            {
                client.Headers.Add($"User-Agent: MapTools/{GetPluginVersion()}");
                string requestURL = "https://api.github.com/repos/BadPylot/MapTools/releases";
                Regex semverRegex = new Regex(@"^(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)(?:-((?:0|[1-9]\d*|\d*[a-zA-Z-][0-9a-zA-Z-]*)(?:\.(?:0|[1-9]\d*|\d*[a-zA-Z-][0-9a-zA-Z-]*))*))?(?:\+([0-9a-zA-Z-]+(?:\.[0-9a-zA-Z-]+)*))?$");
                try
                {
                    // Casting Hell
                    Hashtable response = (Hashtable)((ArrayList)JSON.JsonDecode(client.DownloadString(requestURL)))[0];
                    string currentVersion = (string)response["tag_name"];
                    if (!semverRegex.IsMatch(currentVersion))
                    {
                        throw new Exception("Release Tag is not valid SemVer syntax.");
                    }
                    return currentVersion;
                }
                catch (Exception e)
                {
                    sayConsole("Error fetching latest version. Please manually check the plugin repository for updates.");
                    sayConsole(e.Message);
                    return GetPluginVersion();
                }
            }
        }
        #endregion
    }
}
