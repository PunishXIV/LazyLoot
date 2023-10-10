namespace LazyLoot
{
    internal class Utils
    {
        public unsafe static int GetPlayerIlevel()
        {
            var atkArrayDataHolder = FFXIVClientStructs.FFXIV.Client.System.Framework.Framework.Instance()->GetUiModule()->GetRaptureAtkModule()->AtkModule.AtkArrayDataHolder;
            return atkArrayDataHolder.NumberArrays[63]->IntArray[21];
        }
    }
}
