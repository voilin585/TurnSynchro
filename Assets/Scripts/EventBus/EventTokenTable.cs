using System;
using EventBus;

namespace EventBus
{
    public enum EventTokenDef
    {
        Connector,
        FrameWindow,
        FrameSynchr,
        GameFramework,
        LoadingUI,
        ErrorMonitor,
        Globally,

        TokenDef_Max,
    }

    public static class EventTokenTable
    {
        public static int et_connector
        {
            get
            {
                return EventDefine[(int)EventTokenDef.Connector];
            }
        }

        public static int et_framewindow
        {
            get
            {
                return EventDefine[(int)EventTokenDef.FrameWindow];
            }
        }

        public static int et_framesynchr
        {
            get
            {
                return EventDefine[(int)EventTokenDef.FrameSynchr];
            }
        }

        public static int et_game_framework
        {
            get
            {
                return EventDefine[(int)EventTokenDef.GameFramework];
            }
        }

        public static int et_loading_ui
        {
            get
            {
                return EventDefine[(int)EventTokenDef.LoadingUI];
            }
        }

        public static int et_error_monitor
        {
            get
            {
                return EventDefine[(int)EventTokenDef.ErrorMonitor];
            }
        }

        public static int et_globally
        {
            get
            {
                return EventDefine[(int)EventTokenDef.Globally];
            }
        }

        public static int[] EventDefine = new int[(int)EventTokenDef.TokenDef_Max];
        
        public static void PrepareTokenTable ()
        {
            for (int i = 0; i < EventDefine.Length; i++)
            {
                EventDefine[i] = Mercury.instance.AccquireSession();
            }
        }
    }
}