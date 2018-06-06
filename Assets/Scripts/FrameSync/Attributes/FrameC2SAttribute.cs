
namespace MobaGo.TurnSyncModule
{
    public class TurnC2SAttribute : TurnClassAttribute
    {
        public override int CreatorID
        {
            get
            {
                return (int)ID;
            }
        }

        public ConstEnum.TurnCMD_C2S ID;

        public TurnC2SAttribute(ConstEnum.TurnCMD_C2S id)
        {
            ID = id;
        }
    }
}
