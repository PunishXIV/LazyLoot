using Dalamud.Interface.FontIdentifier;
using ECommons.DalamudServices;

namespace LazyLoot
{
    internal class Utils
    {
        public unsafe static int GetPlayerIlevel()
        {
            var atkArrayDataHolder = FFXIVClientStructs.FFXIV.Client.System.Framework.Framework.Instance()->GetUIModule()->GetRaptureAtkModule()->AtkModule.AtkArrayDataHolder;
            return atkArrayDataHolder.NumberArrays[64]->IntArray[21];
        }
    }
}
