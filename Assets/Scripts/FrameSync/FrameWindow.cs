using System;
using System.Collections.Generic;
using UnityEngine;
using EventBus;
using Network;

namespace FrameSyncModule
{
    
    public class FrapWrap : AbstractSmartObj
    {
        public uint frameID = 0u;
        public object data = null;

        public override void OnRelease()
        {
            data = null;
        }

        public static int FrapWrapInsertComparsionFunc(FrapWrap atom, FrapWrap curr, FrapWrap prev)
        {
            if (atom == null)
                return 0;

            if (curr != null && curr.frameID == atom.frameID)
                return 2;

            if (prev == null && curr != null)
            {
                if (atom.frameID < curr.frameID)
                    return -1;
            }
            else
            {
                if (atom.frameID > prev.frameID && atom.frameID < curr.frameID)
                    return 1;
            }

            return 0;
        }
    }

    /// <summary>
    /// 帧缓冲区，输入网络帧（无序，缺失，延迟），经过处理后，输出同步帧（有序，完整，延迟）
    /// </summary>
    public class FrameWindow : IDisposable
	{
        public const uint FRQ_WIN_LEN = 900u;        
        public const int MAX_REPAIR_FRAMECOUNT = 20;//1秒

        private object[] _receiveWindow = null; //window for handling incoming frame commands

		private uint _basFrqNo;//页号（900帧为一页）
		private uint _begFrqNo;//记录实际执行到的帧号
		private uint _maxFrqNo;//记录最近帧号

		private int _repairCounter;
		private uint _repairBegNo;
		private int _repairTimes;
		private int _timeoutFrameStep;

        public bool IsRepairing
        {
            get { return _maxFrqNo > _begFrqNo; }
        }

        private LinkedList<FrapWrap> _laterFrames = new LinkedList<FrapWrap>();

		private bool _hasPoolCreated = false;

        private bool _laterFrameTracing = false;
        public bool IsLaterFrameTracing
        {
            get { return _laterFrameTracing && _laterFrames.Count == 0; }
            set { _laterFrameTracing = value; }
        }

		public FrameWindow()
		{
			if (!_hasPoolCreated) {
				ObjectCachePool.instance.CreatePool<FrapWrap> (128, 16);
				_hasPoolCreated = true;
			}
			Reset();
            Mercury.instance.AddListener(EventTokenTable.et_framewindow, OnMercuryEvent);   
		}

        private void OnMercuryEvent(object sender, MercuryEventBase e)
        {
            if (e.eventId == (int)EMercuryEvent.E_ME_OBJ_DELIVER)
            {
                MEObjDeliver evt = (MEObjDeliver)e;
                if (evt.opcode == (int)EObjDeliverOPCode.E_OP_HANDLE_FRAMEPACK)
                {
                    object[] args = (object[])evt.obj;
                    if (!HandleFrameCommandPackage((uint)args[0], args[1]))
                    {
                        uint frameID = (uint)args[0];
                        if (frameID >= _begFrqNo)
                        {
                            FrapWrap wrap = ObjectCachePool.instance.Fetch<FrapWrap>();
                            wrap.frameID = frameID;
                            wrap.data = args[1];

                            //如果该帧已存在，则丢弃
                            if (!Utility.LinkedListInsert(_laterFrames, wrap, FrapWrap.FrapWrapInsertComparsionFunc))
                            {
                                ProcessFrameDropInternal(frameID, wrap.data, false);
                                wrap.Release();
                            }
                        }
                    }
                }
            }
        }

        private uint _FrameNo2WindowIdx(uint theFrqNo)
		{
            return theFrqNo % FRQ_WIN_LEN;
		}

		public void Reset()
		{
            _receiveWindow = new object[FRQ_WIN_LEN];
			_basFrqNo = 0u;
			_begFrqNo = 0u;
			_maxFrqNo = 0u;
			_repairCounter = 0;
			_repairBegNo = 0u;
			_repairTimes = 0;
			_timeoutFrameStep = 6;
		}

        public void ClearRepair ()
        {
            _repairBegNo = 0u;
            _repairCounter = 0;
            _repairTimes = 0;
        }

		public void UpdateFrame()
		{
            if (Reconnection.instance.IsReconnection 
                || FrameSyncService.instance.ServiceMode == FrameSyncService.EServiceMode.E_SM_LOCALLY
                || FrameSyncService.instance.ServiceMode == FrameSyncService.EServiceMode.E_SM_OB 
                || FrameSyncService.instance.ServiceMode == FrameSyncService.EServiceMode.E_SM_PLAYBACK)                
                return;

            // Process later frames
            //网络太弱，延迟太高，则启动修复。
            while (_laterFrames.Count > 0)
            {
                FrapWrap wrap = _laterFrames.First.Value;
                if (HandleFrameCommandPackage(wrap.frameID, wrap.data))
                {
                    _laterFrames.RemoveFirst();
                    wrap.Release();

                    if (!_laterFrameTracing)
                        _laterFrameTracing = true;
                }
                else
                {
                    break;
                }
            }

            if (_maxFrqNo > _begFrqNo)
            {
                if (_receiveWindow[(int)_FrameNo2WindowIdx(_begFrqNo)] == null)
                {
                    if (_repairBegNo != _begFrqNo)
                    {
                        _repairBegNo = _begFrqNo;
                        RequestRepairLackFrames();
                        _repairTimes = 0;
                        _repairCounter = 0;
                    }
                    else if (++_repairCounter > (2 ^ _repairTimes) * _timeoutFrameStep)
                    {
                        RequestRepairLackFrames();
                        _repairCounter = 0;
                        _repairTimes++;
                    }
                }
            }
		}

        private void ProcessFrameCommandInternal(object msg)
        {           
            MEObjDeliver e = ObjectCachePool.instance.Fetch<MEObjDeliver>();
            e.args[0] = msg;
            e.opcode = (int)EObjDeliverOPCode.E_OP_PROCESS_FRAMEPACK;

            Mercury.instance.Broadcast(EventTokenTable.et_game_framework, this, e);
        }

        private void ProcessFrameDropInternal (uint frameID, object msg, bool releaseSharedBuf = true)
        {
            MEObjDeliver e = ObjectCachePool.instance.Fetch<MEObjDeliver>();
            e.args[0] = frameID;
            e.args[1] = msg;
            e.args[2] = releaseSharedBuf;
            e.opcode = (int)EObjDeliverOPCode.E_OP_PROCESS_FRAMEDROP;

            Mercury.instance.Broadcast(EventTokenTable.et_game_framework, this, e);
        }

 

        public bool HandleFrameCommandPackage(uint pkgFrapNo, object msg)
        {
            bool result = false;
            if (pkgFrapNo > _maxFrqNo)
            {
                _maxFrqNo = pkgFrapNo;
            }

            if (pkgFrapNo < _begFrqNo)
            {
                ProcessFrameDropInternal(pkgFrapNo, msg);
                return true;             
            }

            if (_begFrqNo <= pkgFrapNo && pkgFrapNo < _basFrqNo + FRQ_WIN_LEN)
            {
                _receiveWindow[(int)_FrameNo2WindowIdx(pkgFrapNo)] = msg;               
                if (FrameSyncService.instance.isActive)
                {
                    object rawCmd = null;
                    //逐步处理每一帧
                    while ((rawCmd = _FetchRawCommand(_begFrqNo)) != null)
                    {
                        if ((_begFrqNo += 1u) % FRQ_WIN_LEN == 0u)
                        {
                            _basFrqNo = _begFrqNo;
                        }

                        ProcessFrameCommandInternal(rawCmd);                        
                    }
                }

                result = true;
            }

            return result;
        }

        private object _FetchRawCommand(uint frqNo)
		{
			uint id = _FrameNo2WindowIdx(frqNo);
            object result = _receiveWindow[(int)id];
            _receiveWindow[(int)id] = null;

            return result;
		}

        virtual public void Dispose()
        {
            _receiveWindow = null;
            Mercury.instance.RemoveListener(EventTokenTable.et_framewindow, OnMercuryEvent);
        }

		private void RequestRepairLackFrames()
		{
            if (_maxFrqNo <= _begFrqNo)
            {
                return;
            }

            //voilin: request small repair
            List<int> frames = new List<int>();

            //会多请求两帧
            int len = Mathf.Min ((int)(_maxFrqNo - _begFrqNo + 2u), (int)MAX_REPAIR_FRAMECOUNT);
            for (uint ii = _begFrqNo; ii < _begFrqNo + len; ++ii)
            {
                //再做一次检测，防止由于帧乱序到达而重复请求
                int pos = (int)_FrameNo2WindowIdx(ii);
                if (_receiveWindow[pos] == null)
                {
                    frames.Add((int)ii);
                }
            }

            if ( frames.Count > 0 )
            {
                MEObjDeliver e = ObjectCachePool.instance.Fetch<MEObjDeliver>();
                int[] tmp = frames.ToArray();
                e.args[0] = (object)tmp;
                e.opcode = (int)EObjDeliverOPCode.E_OP_LACK_FRAMES;
                Mercury.instance.Broadcast(EventTokenTable.et_game_framework, this, e);
            }
		}
	}
}
