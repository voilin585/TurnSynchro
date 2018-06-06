
namespace MobaGo.TurnSyncModule
{
    public class TurnC2CAttribute : TurnClassAttribute
    {
        public override int CreatorID
        {
            get
            {
                return (int)ID;
            }
        }

        public ConstEnum.TurnCMD_C2C ID;

        public TurnC2CAttribute(ConstEnum.TurnCMD_C2C id)
        {
            ID = id;
        }
    }
}