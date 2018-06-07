
namespace MobaGo.FrameSyncModule
{
    public class FrameC2CAttribute : FrameClassAttribute
    {
        public override int CreatorID
        {
            get
            {
                return (int)ID;
            }
        }

        public ConstEnum.FRAMECMD_C2C ID;

        public FrameC2CAttribute(ConstEnum.FRAMECMD_C2C id)
        {
            ID = id;
        }
    }
}