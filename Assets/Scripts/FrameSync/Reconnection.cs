using System.Collections.Generic;
using EventBus;
using TurnSyncModule;

namespace Network
{
    /*
     1.断线重连的输入数据来自网络层，因此数据是不可靠的，可能会丢帧，延迟，延迟抖动。不过在断线重连模块不负责处理，该模块只负责将断线期间丢失的帧数据
       全部通过请求拿到
     2.输入数据有两类，一类是客户端主动请求的丢失帧，一类是服务端正常推进的帧。该机制会优先分批请求并处理丢失帧，全部处理完毕后，再处理正常推进
       的帧
     3.追帧期间，收到的正常帧也有可能会丢失，为解决该问题，追帧目标始终是最新的服务器帧，后期缓存里的重复帧会做丢弃处理
    */

    public class Reconnection : Singleton<Reconnection>
    {
        public static List<IReconnectionExtension> s_extensions = new List<IReconnectionExtension>();

        private enum ERectionState
        {
            Idle = 0,
            Waiting_Turns,
            Requesting_Turns,
            Executing_Turns,
        }

        /// <summary>
        /// 每次最多可以请求300帧
        /// </summary>
        private const int m_reqMaxTurnPerTick = 300;
        private const int kTurnBlockThreshold = 1;
        private const int kTurnReqStuckTimesThreshold = 150;

        private ERectionState m_CurState = ERectionState.Idle;
        private LinkedList<FrapWrap> m_LaterTurns = new LinkedList<FrapWrap>();

        /// <summary>
        /// 记录最新追帧目标。这个是变化的，因为在追帧过程中，游戏还在继续进行
        /// </summary>
        private int m_LastTurnNeedToTrace = 0;

        /// <summary>
        /// 当前已经追到哪一帧
        /// </summary>
        private int m_CurrTracedTurn = 0;

        /// <summary>
        /// 上一次已经追到哪一帧
        /// </summary>
        private int m_LastTracedTurn = 0;

        /// <summary>
        /// 记录本次请求的结束帧
        /// </summary>
        private int m_CurTracedTurnEnd = 0;

        /// <summary>
        /// 记录最新追帧进度
        /// </summary>
        private int m_LastReportPercent = 0;

        /// <summary>
        /// 当请求消息发送以后，记录等待响应的帧数，上限是150帧。超过上限，则重新请求
        /// </summary>

        private int m_StuckTimes = 0;
        private bool _adjustGap = true;
        
        private bool _canPerformCheck = false;
        public bool CanPerformCheck
        {
            get { return _canPerformCheck; }
        }

        public bool IsReconnection
        {
            get
            {
                return m_CurState != ERectionState.Idle;
            }
        }

        public int CurrentTurnTraced
        {
            get { return m_CurrTracedTurn; }
        }

        public override void Init()
        {
            for (int ii = 0; ii < s_extensions.Count; ++ii)
            {
                if (s_extensions[ii] != null)
                    s_extensions[ii].Init();
            }
        }

        public override void UnInit()
        {
            for (int ii = 0; ii < s_extensions.Count; ++ii)
            {
                if (s_extensions[ii] != null)
                    s_extensions[ii].UnInit();
            }
        }

        public void CancelReconnection()
        {
            m_CurState = ERectionState.Idle;
        }

        public void Reset()
        {
            CancelReconnection();
            for (int ii = 0; ii < s_extensions.Count; ++ii)
            {
                if (s_extensions[ii] != null)
                    s_extensions[ii].Reset();
            }
        }


        /// <summary>
        /// 在断线重连的扩展中调用
        /// </summary>
        /// <param name="TotalTurnsPassed"></param>
        /// <returns></returns>
        public bool StartReconnection(int TotalTurnsPassed)
        {
            if (m_CurState == ERectionState.Idle && TotalTurnsPassed > 0)
            {
                m_CurState = ERectionState.Requesting_Turns;
                m_LastTurnNeedToTrace = TotalTurnsPassed;
                m_CurrTracedTurn = 0;
                m_LastReportPercent = 0;
                m_CurTracedTurnEnd = 0;
                _canPerformCheck = false;
                _adjustGap = true;

                MEObjDeliver e = ObjectCachePool.instance.Fetch<MEObjDeliver>();
                e.opcode = (int)EObjDeliverOPCode.E_OP_START_RECONNECTION;
                Mercury.instance.Broadcast(EventTokenTable.et_game_Turnwork, this, e);

                return true;
            }
            else if (m_CurState == ERectionState.Idle && TotalTurnsPassed == 0)
            {
                ReportProgress(100);
            }
            return false;
        }


        /// <summary>
        /// 如果该帧是断线重连时请求的延迟帧，则优先处理，并上报进度。如果是最新的服务器帧，则稍后处理
        /// </summary>
        /// <param name="e"></param>
        /// <param name="isLatestTurn">1 表示断线重连状态中，发送过来的最新服务器帧， 2 表示重连时请求的延迟帧</param>
        public void HandleTurns(MercuryEventBase e, bool isLatestTurn = false)
        {
            if (!isLatestTurn)
            {
                uint frapNo = (uint)(((MEObjDeliver)e).args[0]);
                if (m_CurrTracedTurn < (int)frapNo)
                    m_CurrTracedTurn = (int)frapNo;
                Mercury.instance.Broadcast(EventTokenTable.et_Turnwindow, this, e);
                ReportProgress();
            }
            else
            {
                InsertToLaterTurns(e);
            }
        }

        public void ResidentUpdate()
        {
            for (int ii = 0; ii < s_extensions.Count; ++ii)
            {
                if (s_extensions[ii] != null)
                    s_extensions[ii].Update();
            }
        }

        /// <summary>
        /// 缓存正常帧
        /// </summary>
        /// <param name="e"></param>
        private void InsertToLaterTurns (MercuryEventBase e)
        {
            if (e != null)
            {
                MEObjDeliver evt = (MEObjDeliver)e;
                FrapWrap frap = ObjectCachePool.instance.Fetch<FrapWrap>();
                frap.TurnID = (uint)evt.args[0];
                frap.data = evt.args[1];
                e.Release();

                if (Utility.LinkedListInsert(m_LaterTurns, frap, FrapWrap.FrapWrapInsertComparsionFunc))
                {
                    if (m_LastTurnNeedToTrace < frap.TurnID)
                    {
                        m_LastTurnNeedToTrace = (int)frap.TurnID; // record last Turn while tracing the newest Turn.
                    }
                }
                else
                {
                    AbstractSmartObj obj = (AbstractSmartObj)frap.data;
                    if (obj != null)    obj.Release();
                    frap.Release();
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        private void ProcessLaterTurns ()
        {
            if (m_LaterTurns.Count > 0)
            {
                LinkedListNode<FrapWrap> node = m_LaterTurns.First;
                while (node != null && node.Value != null)
                {
                    FrapWrap frap = node.Value;
                    MEObjDeliver e = ObjectCachePool.instance.Fetch<MEObjDeliver>();
                    e.args[0] = frap.TurnID;
                    e.args[1] = frap.data;
                    e.opcode = (int)EObjDeliverOPCode.E_OP_HANDLE_TurnPACK;
                    Mercury.instance.Broadcast(EventTokenTable.et_Turnwindow, this, e);                   

                    ReportProgress();

                    frap.Release();
                    node = node.Next;
                }
                m_LaterTurns.Clear();
            }
        }

        public void Update()
        {            
            if (m_CurState == ERectionState.Executing_Turns)
            {
                ProcessLaterTurns();

                if (TurnSyncService.instance.BlockTurnWaitNum > kTurnBlockThreshold
                    && TurnSyncService.instance.GetTurnSyncChr().CurTurnNum < m_LastTurnNeedToTrace)
                {
                    m_CurrTracedTurn = (int)TurnSyncService.instance.GetTurnSyncChr().CurTurnNum + 1;
                    m_CurState = ERectionState.Requesting_Turns;
                    _canPerformCheck = false;
                    return;
                }             
                bool over = TurnSyncService.instance.GetTurnSyncChr().CurTurnNum >= m_LastTurnNeedToTrace;
                if (over)
                {
                    ReportProgress(100);

                    MEObjDeliver e = ObjectCachePool.instance.Fetch<MEObjDeliver>();
                    e.opcode = (int)EObjDeliverOPCode.E_OP_END_RECONNECTION;
                    Mercury.instance.Broadcast(EventTokenTable.et_game_Turnwork, this, e);
                                            
                    _canPerformCheck = true;
                    m_CurState = ERectionState.Idle;
                }
            }
            else if (m_CurState == ERectionState.Requesting_Turns)
            {
                if (_adjustGap)
                {
                    if (m_LaterTurns.Count > 0)
                    {
                        int gap = (int)m_LaterTurns.First.Value.TurnID - m_LastTurnNeedToTrace - 1;
                        if (gap > 0)
                        {
                            m_LastTurnNeedToTrace = (int)m_LaterTurns.First.Value.TurnID - 1;
                        }                        
                        _adjustGap = false;
                    }
                }
                int TurnEnd = UnityEngine.Mathf.Min(m_CurrTracedTurn + m_reqMaxTurnPerTick, m_LastTurnNeedToTrace);
                if (m_CurrTracedTurn < TurnEnd)
                {
                    MEObjDeliver e = ObjectCachePool.instance.Fetch<MEObjDeliver>();
                    e.opcode = (int)EObjDeliverOPCode.E_OP_FETCH_RECONNECTION_TurnS;
                    e.args[0] = m_CurrTracedTurn;
                    e.args[1] = TurnEnd;

                    Mercury.instance.Broadcast(EventTokenTable.et_game_Turnwork, this, e);

                    m_LastTracedTurn = m_CurrTracedTurn;
                    m_CurTracedTurnEnd = TurnEnd;
                    m_StuckTimes = 0;
                    m_CurState = ERectionState.Waiting_Turns;
                }
                //等待状态下，当超出等待上限准备重新请求时，上一次的数据恰好到来，并且恰好追帧完毕，则直接进入处理状态
                else
                {
                    ProcessLaterTurns();                    
                    m_CurState = ERectionState.Executing_Turns;
                }
            }
            else if (m_CurState == ERectionState.Waiting_Turns)
            {
                //未收到帧数据，则等待
                if (m_CurrTracedTurn == m_LastTracedTurn)
                {
                    m_StuckTimes++;
                }
                else
                {
                    m_LastTracedTurn = m_CurrTracedTurn;
                    m_StuckTimes = 0;
                }

                // 超过等待上限，则重新请求
                if (m_StuckTimes >= kTurnReqStuckTimesThreshold)
                {
                    m_CurState = ERectionState.Requesting_Turns;
                    return;
                }

                //表示本次请求的数据没有完全收到
                if (m_CurrTracedTurn < m_CurTracedTurnEnd)
                    return;

                //追帧未完成，继续请求
                if (m_CurTracedTurnEnd < m_LastTurnNeedToTrace)
				{
					m_CurState = ERectionState.Requesting_Turns;
				}
                //追帧结束，处理重连期间缓存的正常帧
				else
				{
                    ProcessLaterTurns();
                    _canPerformCheck = true;
                    m_CurState = ERectionState.Executing_Turns;
                }
            }
        }
        
        /// <summary>
        /// 向UI层上报重连进度
        /// </summary>
        /// <param name="progress"></param>
        private void ReportProgress (int progress = -1)
        {
            int percent = progress < 0 ? UnityEngine.Mathf.Clamp((int)(TurnSyncService.instance.GetTurnSyncChr().CurTurnNum * 1f / m_LastTurnNeedToTrace * 100), 0, 99) : progress;
            if (percent > m_LastReportPercent)
            {
                m_LastReportPercent = percent;
                MEObjDeliver report = ObjectCachePool.instance.Fetch<MEObjDeliver>();
                report.opcode = (int)EObjDeliverOPCode.E_OP_RECONNECTION_PROGRESS;
                report.args[0] = percent;
                Mercury.instance.Broadcast(EventTokenTable.et_loading_ui, this, report);
            }
        }
    }
}