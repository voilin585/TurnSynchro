using System;
using EventBus;

namespace EventBus
{
    public enum EventTokenDef
    {
        Connector,
        TurnWindow,
        TurnSynchr,
        GameTurnwork,
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

        public static int et_Turnwindow
        {
            get
            {
                return EventDefine[(int)EventTokenDef.TurnWindow];
            }
        }

        public static int et_Turnsynchr
        {
            get
            {
                return EventDefine[(int)EventTokenDef.TurnSynchr];
            }
        }

        public static int et_game_Turnwork
        {
            get
            {
                return EventDefine[(int)EventTokenDef.GameTurnwork];
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