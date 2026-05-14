using Colossal.IO.AssetDatabase;
using Game.Modding;
using Game.Settings;
using Game.UI.Widgets;
using System.Collections.Generic;

namespace TreesReduceNoisePollution
{
    [FileLocation("TreesReduceNoisePollution")]
    public class Setting : ModSetting
    {
        public const string kMainSection = "Main";
        public const string kGeneralGroup = "General";

        public Setting(IMod mod) : base(mod) => SetDefaults();

        [SettingsUISection(kMainSection, kGeneralGroup)]
        public bool ModEnabled { get; set; } = true;

        [SettingsUISlider(min = 0, max = 500, step = 1)]
        [SettingsUISection(kMainSection, kGeneralGroup)]
        public int TreeNoiseStrength { get; set; } = 25;

        [SettingsUISection(kMainSection, kGeneralGroup)]
        [SettingsUIDropdown(typeof(Setting), nameof(GetMode))]
        public int ReductionMode { get; set; } = 0;

        [SettingsUISlider(min = 16, max = 512, step = 16)]
        [SettingsUISection(kMainSection, kGeneralGroup)]
        public int UpdateInterval { get; set; } = 128;

        [SettingsUISection(kMainSection, kGeneralGroup)]
        public bool VerboseLogging { get; set; } = false;

        public DropdownItem<int>[] GetMode() => new[]
        {
            new DropdownItem<int>{ value = 0, displayName = "Linear" },
            new DropdownItem<int>{ value = 1, displayName = "Realistic (Logarithmic)" }
        };

        public override void SetDefaults()
        {
            ModEnabled = true;
            TreeNoiseStrength = 25;
            ReductionMode = 0;
            UpdateInterval = 128;
            VerboseLogging = false;
        }
    }
}