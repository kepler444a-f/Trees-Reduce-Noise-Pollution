using Colossal.Logging;
using Colossal.IO.AssetDatabase;
using Game;
using Game.Modding;
using Game.SceneFlow;

namespace TreesReduceNoisePollution
{
    public class Mod : IMod
    {
        public static ILog log = LogManager.GetLogger($"{nameof(TreesReduceNoisePollution)}").SetShowsErrorsInUI(false);
        public static Setting m_Setting;

        public void OnLoad(UpdateSystem updateSystem)
        {
            log.Info(nameof(OnLoad));

            if (m_Setting == null)
            {
                m_Setting = new Setting(this);
            }

            m_Setting.RegisterInOptionsUI();
            GameManager.instance.localizationManager.AddSource("en-US", new LocaleEN(m_Setting));

// Fixed: AssetDatabase now resolves with the added using statement
            AssetDatabase.global.LoadSettings(nameof(TreesReduceNoisePollution), m_Setting, new Setting(this));

            updateSystem.UpdateAt<TreeNoiseAbsorptionSystem>(SystemUpdatePhase.GameSimulation);
        }

        public void OnDispose()
        {
            log.Info(nameof(OnDispose));
            if (m_Setting != null)
            {
                m_Setting.UnregisterInOptionsUI();
                m_Setting = null;
            }
        }
    }
}