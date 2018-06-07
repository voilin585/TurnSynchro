using System.Collections.Generic;
using EventBus;
using FrameSyncModule;

namespace Network
{    
    public class Reconnection : Singleton<Reconnection>
    {
        public static List<IUpdatableExtension> s_extensions = new List<IUpdatableExtension>();

        private enum EState
        {
            E_STATE_NONE = 0,
            E_STATE_WAIT_HANDLING_FRAMES,
            E_STATE_RECEIVING_FRAMES,
            E_STATE_EXECUTING_FRAMES,
        }

        private const int kFramesDevourPerTick = 300;//每次最多可以请求300帧
        private const int kFrameBlockThreshold = 1;
        private const int kFrameReqStuckTimesThreshold = 150;

        private EState _state = EState.E_STATE_NONE;
        private LinkedList<FrapWrap> _laterFrames = new LinkedList<FrapWrap>();

        private int _maxFrameCount = 0;

        /// <summary>
        /// 最新追帧目标
        /// </summary>
        private int _lastFrameNeedToTrace = 0;
        private int _currentFrameTraced = 0;//当前追到哪一帧
        private int _tracedFrame = 0;
        private int _lastTracedFrame = 0;
        private int _lastReportPercent = 0;
        //当请求消息发送以后，记录等待响应的帧数，上限是150帧。超过上限，则重新请求
        private int _stuckTimes = 0;
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
                return _state != EState.E_STATE_NONE;
            }
        }

        public int CurrentFrameTraced
        {
            get { return _currentFrameTraced; }
        }

        //
        private void PushFrame2LaterFrameList (MercuryEventBase e)
        {
            if (e != null)
            {
                MEObjDeliver evt = (MEObjDeliver)e;
                FrapWrap frap = ObjectCachePool.instance.Fetch<FrapWrap>();
                frap.frameID = (uint)evt.args[0];
                frap.data = evt.args[1];
                e.Release();

                if (Utility.LinkedListInsert(_laterFrames, frap, FrapWrap.FrapWrapInsertComparsionFunc))
                {
                    if (_lastFrameNeedToTrace < frap.frameID)
                    {
                        _lastFrameNeedToTrace = (int)frap.frameID; // record last frame while tracing the newest frame.
                    }
                }
                else
                {
                    // release duplicated data
                    AbstractSmartObj obj = (AbstractSmartObj)frap.data;
                    if (obj != null)
                        obj.Release();
                    frap.Release();
                }
            }
        }

        /// <summary>
        /// 如果该帧是断线重连时请求的延迟帧，则优先处理，并上报进度。如果是最新的服务器帧，则稍后处理
        /// </summary>
        /// <param name="e"></param>
        /// <param name="isLaterFrame">1 表示断线重连状态中，发送过来的最新服务器帧， 2 表示重连时请求的延迟帧</param>
        public void HandleFrames(MercuryEventBase e, bool isLaterFrame = false)
        {
            if (!isLaterFrame)
            {
                uint frapNo = (uint)(((MEObjDeliver)e).args[0]);
                if (_currentFrameTraced < (int)frapNo)
                    _currentFrameTraced = (int)frapNo;
                Mercury.instance.Broadcast(EventTokenTable.et_framewindow, this, e);
                ReportProgress();
            }
            else
            {
                PushFrame2LaterFrameList(e);
            }
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

        public void ResidentUpdate()
        {
            for (int ii = 0; ii < s_extensions.Count; ++ii)
            {
                if (s_extensions[ii] != null)
                    s_extensions[ii].Update();
            }
        }

        private void ProcessLaterFrames ()
        {
            if (_laterFrames.Count > 0)
            {
                LinkedListNode<FrapWrap> node = _laterFrames.First;
                while (node != null && node.Value != null)
                {
                    FrapWrap frap = node.Value;
                    MEObjDeliver e = ObjectCachePool.instance.Fetch<MEObjDeliver>();
                    e.args[0] = frap.frameID;
                    e.args[1] = frap.data;
                    e.opcode = (int)EObjDeliverOPCode.E_OP_HANDLE_FRAMEPACK;
                    Mercury.instance.Broadcast(EventTokenTable.et_framewindow, this, e);                   

                    ReportProgress();

                    frap.Release();
                    node = node.Next;
                }

                _laterFrames.Clear();
            }
        }

        public void Update()
        {            
            if (_state == EState.E_STATE_EXECUTING_FRAMES)
            {
                ProcessLaterFrames();

                if (FrameSyncService.instance.BlockFrameWaitNum > kFrameBlockThreshold
                    && FrameSyncService.instance.GetFrameSyncChr().CurFrameNum < _lastFrameNeedToTrace)
                {
                    _currentFrameTraced = (int)FrameSyncService.instance.GetFrameSyncChr().CurFrameNum + 1;
                    _state = EState.E_STATE_RECEIVING_FRAMES;
                    _canPerformCheck = false;
                    return;
                }
                
                // tolerent by maxium repair count
                bool fin = FrameSyncService.instance.GetFrameSyncChr().CurFrameNum >= _lastFrameNeedToTrace;
                if (fin)
                {
                    ReportProgress(100);

                    MEObjDeliver e = ObjectCachePool.instance.Fetch<MEObjDeliver>();
                    e.opcode = (int)EObjDeliverOPCode.E_OP_END_RECONNECTION;
                    Mercury.instance.Broadcast(EventTokenTable.et_game_framework, this, e);
                                            
                    _canPerformCheck = true;
                    _state = EState.E_STATE_NONE;
                }
            }
            else if (_state == EState.E_STATE_RECEIVING_FRAMES)
            {
                if (_adjustGap)
                {
                    if (_laterFrames.Count > 0)
                    {
                        // adjust gap, the gap is generated by loading stuck
                        int gap = (int)_laterFrames.First.Value.frameID - _lastFrameNeedToTrace - 1;
                        if (gap > 0)
                        {
                            _lastFrameNeedToTrace = (int)_laterFrames.First.Value.frameID - 1;
                        }                        
                        _adjustGap = false;
                    }
                }
                int frameEnd = UnityEngine.Mathf.Min(_currentFrameTraced + kFramesDevourPerTick, _lastFrameNeedToTrace);
                if (frameEnd > _currentFrameTraced)
                {
                    MEObjDeliver e = ObjectCachePool.instance.Fetch<MEObjDeliver>();
                    e.opcode = (int)EObjDeliverOPCode.E_OP_FETCH_RECONNECTION_FRAMES;
                    e.args[0] = _currentFrameTraced;
                    e.args[1] = frameEnd;

                    Mercury.instance.Broadcast(EventTokenTable.et_game_framework, this, e);

                    _lastTracedFrame = _currentFrameTraced;
                    _tracedFrame = frameEnd;
                    _stuckTimes = 0;
                    _state = EState.E_STATE_WAIT_HANDLING_FRAMES;
                }
                else
                {
                    // force processing later frames
                    ProcessLaterFrames();                    
                    _state = EState.E_STATE_EXECUTING_FRAMES;
                }
            }
            else if (_state == EState.E_STATE_WAIT_HANDLING_FRAMES)
            {
                if (_currentFrameTraced == _lastTracedFrame)
                    ++_stuckTimes;
                else
                {
                    _lastTracedFrame = _currentFrameTraced;
                    _stuckTimes = 0;
                }

                // request again while stucking
                if (_stuckTimes >= kFrameReqStuckTimesThreshold)
                {
                    _state = EState.E_STATE_RECEIVING_FRAMES;
                    return;
                }
                if (_currentFrameTraced < _tracedFrame)
                    return;
                if (_tracedFrame < _lastFrameNeedToTrace)
				{
					_state = EState.E_STATE_RECEIVING_FRAMES;
				}
                //追上之后，处理后来帧
				else
				{
                    ProcessLaterFrames();
                    _canPerformCheck = true;
                    _state = EState.E_STATE_EXECUTING_FRAMES;
                }
            }
        }
        
        private void ReportProgress (int progress = -1)
        {
            // reconnection progress
            int percent = progress < 0 ? UnityEngine.Mathf.Clamp((int)(FrameSyncService.instance.GetFrameSyncChr().CurFrameNum * 1f / _lastFrameNeedToTrace * 100), 0, 99) : progress;
            if (percent > _lastReportPercent)
            {
                _lastReportPercent = percent;
                MEObjDeliver report = ObjectCachePool.instance.Fetch<MEObjDeliver>();
                report.opcode = (int)EObjDeliverOPCode.E_OP_RECONNECTION_PROGRESS;
                report.args[0] = percent;//progress < 0 ? UnityEngine.Mathf.Clamp(percent, 0, 99) : progress;
                Mercury.instance.Broadcast(EventTokenTable.et_loading_ui, this, report);
            }
        }

        // extension RequireReconnection to implement message pack and send logic
        // NOTE: do not use this internal function indepentently.
        public bool _RequireReconnection(int TotalFramesPassed)
        {
            if (_state == EState.E_STATE_NONE && TotalFramesPassed > 0)
            {
                _state = EState.E_STATE_RECEIVING_FRAMES;
                _lastFrameNeedToTrace = TotalFramesPassed;
                _currentFrameTraced = 0;
                _lastReportPercent = 0;
                _tracedFrame = 0;
                _canPerformCheck = false;
                _adjustGap = true;

                MEObjDeliver e = ObjectCachePool.instance.Fetch<MEObjDeliver> ();
				e.opcode = (int)EObjDeliverOPCode.E_OP_START_RECONNECTION;
				Mercury.instance.Broadcast (EventTokenTable.et_game_framework, this, e);

                return true;
            }
            else if (_state == EState.E_STATE_NONE && TotalFramesPassed == 0)
            {
                ReportProgress(100);
            }
            return false;
        }

        public void _CancelReconnection ()
        {
            _state = EState.E_STATE_NONE;
        }

        public void Reset ()
        {
            _CancelReconnection();
            for (int ii = 0; ii < s_extensions.Count; ++ii)
            {
                if (s_extensions[ii] != null)
                    s_extensions[ii].Reset();
            }
        }
    }
}