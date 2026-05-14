using Colossal;
using Colossal.Json;
using Colossal.Logging;
using Game;
using Game.Modding;
using Game.SceneFlow;
using Game.UI.Localization;
using System;
using System.Collections.Generic;
using System.IO;

namespace TreesReduceNoisePollution
{
    public class Mod : IMod
    {
        public static ILog Log = LogManager.GetLogger($"{nameof(TreesReduceNoisePollution)}").SetShowsErrorsInUI(false);
        public static Setting m_Setting;

        public void OnLoad(UpdateSystem updateSystem)
        {
            Log.Info("Loading Trees Reduce Noise Pollution");

            try
            {
                // 1. Initialize and register settings
                m_Setting = new Setting(this);
                m_Setting.RegisterInOptionsUI();

                // 2. Load localization files from the 'lang' folder
                LoadLocalization();

                // 3. Register the simulation system
                updateSystem.UpdateAt<TreeNoiseAbsorptionSystem>(SystemUpdatePhase.GameSimulation);

                Log.Info("Loaded successfully");
            }
            catch (Exception e)
            {
                Log.Error($"CRITICAL ERROR during OnLoad: {e.Message}\n{e.StackTrace}");
            }
        }

        public void OnDispose()
        {
            if (m_Setting != null)
            {
                m_Setting.UnregisterInOptionsUI();
                m_Setting = null;
            }
        }

        private void LoadLocalization()
        {
            try
            {
                // Get the directory where the DLL is located
                string assemblyDir = Path.GetDirectoryName(typeof(Mod).Assembly.Location);
                string langFolder = Path.Combine(assemblyDir, "lang");

                if (Directory.Exists(langFolder))
                {
                    // Register the LocaleAsset source to handle dynamic language switching
                    GameManager.instance.localizationManager.AddSource("en-US", new LocaleAsset(langFolder));
                    Log.Info($"Localization folder registered at: {langFolder}");
                }
                else
                {
                    Log.Warn($"Lang folder missing at {langFolder}. UI will show raw keys.");
                }
            }
            catch (Exception e)
            {
                Log.Error($"Failed to load localization: {e.Message}");
            }
        }
    }

    public class LocaleAsset : IDictionarySource
    {
        private readonly string m_Directory;
        public LocaleAsset(string directory) => m_Directory = directory;

        public IEnumerable<KeyValuePair<string, string>> ReadEntries(IList<IDictionaryEntryError> errors, Dictionary<string, int> indexer)
        {
            var result = new Dictionary<string, string>();
            var localeId = GameManager.instance.localizationManager.activeLocaleId;

            // Priority: Try active language (e.g., fr-FR), then fallback to en-US
            string filePath = Path.Combine(m_Directory, $"{localeId}.json");
            if (!File.Exists(filePath)) filePath = Path.Combine(m_Directory, "en-US.json");

            if (File.Exists(filePath))
            {
                try
                {
                    string json = File.ReadAllText(filePath);
                    var data = Decoder.Decode(json).Make<Dictionary<string, string>>();
                    if (data != null) return data;
                }
                catch (Exception e)
                {
                    Mod.Log.Error($"JSON Decode error in {filePath}: {e.Message}");
                }
            }
            return result;
        }

        public void Unload() { }
    }
}