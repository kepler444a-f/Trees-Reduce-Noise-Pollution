using Colossal;
using Colossal.IO.AssetDatabase;
using Game.Modding;
using Game.Settings;
using Game.UI;
using Game.UI.Widgets;
using System.Collections.Generic;
using Game.Input;

namespace TreesReduceNoisePollution
{
[FileLocation(nameof(TreesReduceNoisePollution))]
public class Setting : ModSetting
{
public const string kSection = "Main";

public Setting(IMod mod) : base(mod)
{
}

[SettingsUISection(kSection, "General")]
public bool ModEnabled { get; set; } = true;

[SettingsUISlider(min = 0, max = 500, step = 1, unit = Unit.kPercentage)]
[SettingsUISection(kSection, "General")]
public int TreeNoiseStrength { get; set; } = 25;

[SettingsUIDropdown(typeof(Setting), nameof(GetModeItems))]
[SettingsUISection(kSection, "General")]
public int ReductionMode { get; set; } = 0;

[SettingsUISlider(min = 16, max = 512, step = 16)]
[SettingsUISection(kSection, "General")]
public int UpdateInterval { get; set; } = 128;

[SettingsUISection(kSection, "General")]
public bool VerboseLogging { get; set; } = false;

public DropdownItem<int>[] GetModeItems()
{
return new[]
{
new DropdownItem<int> { value = 0, displayName = "Linear" },
new DropdownItem<int> { value = 1, displayName = "Realistic (Logarithmic)" }
};
}

public override void SetDefaults()
{
ModEnabled = true;
TreeNoiseStrength = 25;
ReductionMode = 0;
UpdateInterval = 128;
VerboseLogging = false;
}
}

// This replaces the JSON files. You can create similar classes for LocaleDE, LocaleFR, etc.
public class LocaleEN : IDictionarySource
{
private readonly Setting m_Setting;

public LocaleEN(Setting setting)
{
m_Setting = setting;
}

public IEnumerable<KeyValuePair<string, string>> ReadEntries(IList<IDictionaryEntryError> errors, Dictionary<string, int> indexCounts)
{
return new Dictionary<string, string>
{
{ m_Setting.GetSettingsLocaleID(), "Trees Reduce Noise Pollution" },
{ m_Setting.GetOptionTabLocaleID(Setting.kSection), "Main" },

{ m_Setting.GetOptionLabelLocaleID(nameof(Setting.ModEnabled)), "Mod Enabled" },
{ m_Setting.GetOptionDescLocaleID(nameof(Setting.ModEnabled)), "Enable or disable noise reduction logic for trees." },

{ m_Setting.GetOptionLabelLocaleID(nameof(Setting.TreeNoiseStrength)), "Noise Reduction Strength" },
{ m_Setting.GetOptionDescLocaleID(nameof(Setting.TreeNoiseStrength)), "How much noise each tree absorbs." },

{ m_Setting.GetOptionLabelLocaleID(nameof(Setting.ReductionMode)), "Reduction Calculation" },
{ m_Setting.GetOptionDescLocaleID(nameof(Setting.ReductionMode)), "Linear is flat per tree; Realistic uses logarithmic diminishing returns." },

{ m_Setting.GetOptionLabelLocaleID(nameof(Setting.UpdateInterval)), "Update Frequency" },
{ m_Setting.GetOptionDescLocaleID(nameof(Setting.UpdateInterval)), "How often (in frames) the noise map updates. Lower is more accurate but heavier on CPU." },

{ m_Setting.GetOptionLabelLocaleID(nameof(Setting.VerboseLogging)), "Verbose Logging" },
{ m_Setting.GetOptionDescLocaleID(nameof(Setting.VerboseLogging)), "Enables detailed info in the log file for debugging." }
};
}

public void Unload() { }
}
}