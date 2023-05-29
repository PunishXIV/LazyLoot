using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata.Ecma335;
using System.Text;
using System.Threading.Tasks;

namespace LazyLoot
{
    internal class Utils
    {
        public unsafe static int GetPlayerIlevel()
        {
            var atkArrayDataHolder = FFXIVClientStructs.FFXIV.Client.System.Framework.Framework.Instance()->GetUiModule()->GetRaptureAtkModule()->AtkModule.AtkArrayDataHolder;
            return atkArrayDataHolder.NumberArrays[62]->IntArray[21];
        }
    }
}
