using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ConstEnum
{
    public enum Turn_SYNC_TYPE : sbyte
    {
        Turn_SYNC_T_C2C = 1,
        Turn_SYNC_T_C2S = 2,
    };

    public enum TurnCMD_C2C : sbyte
    {
        Turn_CMD_INVALID = 0,
        Turn_CMD_PLAYERMOVE = 1,
        Turn_CMD_PLAYERMOVEDIRECTION = 2,
        Turn_CMD_PLAYERSTOPMOVE = 3,
        Turn_CMD_ATTACKPOSITION = 4,
        Turn_CMD_ATTACKACTOR = 5,
        Turn_CMD_LEARNSKILL = 6,
        Turn_CMD_USECURVETRACKSKILL = 7,
        Turn_CMD_USECOMMONATTACK = 8,
        Turn_CMD_SWITCHAOUTAI = 9,
        Turn_CMD_SWITCHCAPTAIN = 10,
        Turn_CMD_SWITCHSUPERKILLER = 11,
        Turn_CMD_SWITCHGODMODE = 12,
        Turn_CMD_LEARNTALENT = 13,
        Turn_CMD_TESTCOMMANDDELAY = 14,
        Turn_CMD_PLAYERRUNAWAY = 15,
        Turn_CMD_PLAYERDISCONNECT = 16,
        Turn_CMD_PLAYERRECONNECT = 17,
        Turn_CMD_PLAYATTACKTARGETMODE = 18,
        Turn_CMD_SVRNTFCHGKTurnLATER = 19,
        Turn_CMD_ASSISTSTATECHG = 20,
        Turn_CMD_CHGAUTOAI = 21,
        Turn_CMD_PLAYER_BUY_EQUIP = 22,
        Turn_CMD_PLAYER_SELL_EQUIP = 23,
        Turn_CMD_PLAYER_ADD_GOLD_COIN_IN_BATTLE = 24,
        Turn_CMD_SET_SKILL_LEVEL = 25,
        Turn_CMD_PLAYCOMMONATTACKTMODE = 26,
        Turn_CMD_LOCKATTACKTARGET = 27,
        Turn_CMD_Signal_Btn_Position = 28,
        Turn_CMD_Signal_MiniMap_Position = 29,
        Turn_CMD_Signal_MiniMap_Target = 30,
        Turn_CMD_CHANGETARGETINGMODE = 31,
        Turn_CMD_USEEQUIPSKILL = 32,
        Turn_CMD_SURRENDER_RESULT = 33,
        Turn_CMD_NOTIFY_VERSION_TOKEN = 34,
        Turn_CMD_EXECUTION_GAP = 35,
        _count = 36,
    };

    public enum TurnCMD_C2S : sbyte
    {
        CSSYNC_CMD_USEOBJECTIVESKILL = 0,
        CSSYNC_CMD_USEDIRECTIONALSKILL = 1,
        CSSYNC_CMD_USEPOSITIONSKILL = 2,
        _count = 3,
    };
}
