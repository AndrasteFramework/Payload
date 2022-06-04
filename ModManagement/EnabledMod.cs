using Andraste.Shared.ModManagement.Json;

namespace Andraste.Payload.ModManagement
{
    public class EnabledMod
    {
        public EnabledMod(ModInformation modInformation, ModSetting modSetting)
        {
            ModInformation = modInformation;
            ModSetting = modSetting;
        }

        public ModInformation ModInformation;
        public ModSetting ModSetting;
        public ModConfiguration ActiveConfiguration => ModInformation.Configurations[ModSetting.ActiveConfiguration];
    }
}