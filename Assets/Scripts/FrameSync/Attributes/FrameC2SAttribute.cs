
namespace MobaGo.FrameSyncModule
{
    public class FrameC2SAttribute : FrameClassAttribute
    {
        public override int CreatorID
        {
            get
            {
                return (int)ID;
            }
        }

        public ConstEnum.FRAMECMD_C2S ID;

        public FrameC2SAttribute(ConstEnum.FRAMECMD_C2S id)
        {
            ID = id;
        }
    }
}
