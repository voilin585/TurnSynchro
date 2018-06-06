using System;
using System.Collections.Generic;
using UnityEngine;
using EventBus;
using Network;

namespace TurnSyncModule
{
    public class FrapWrap : AbstractSmartObj
    {
        public uint TurnID = 0u;
        public object data = null;

        public override void OnRelease()
        {
            data = null;
        }

        public static int FrapWrapInsertComparsionFunc(FrapWrap atom, FrapWrap curr, FrapWrap prev)
        {
            if (atom == null)
                return 0;

            if (curr != null && curr.TurnID == atom.TurnID)
                return 2;

            if (prev == null && curr != null)
            {
                if (atom.TurnID < curr.TurnID)
                    return -1;
            }
            else
            {
                if (atom.TurnID > prev.TurnID && atom.TurnID < curr.TurnID)
                    return 1;
            }

            return 0;
        }
    }

    /// <summary>
    /// 帧缓冲，确保帧序，实现丢帧，延迟等修复机制，不断向同步模块输出正常帧，该模块不处理网络波动
    /// </summary>

    public class TurnWindow : IDisposable
	{
        public const uint FRQ_WIN_LEN = 900u;        
        public const int MAX_REPAIR_TURNCOUNT = 20;//1秒

        private object[] _receiveWindow = null; 

		private uint _basFrqNo;//页号（900帧为一页）
		private uint _begFrqNo;//记录实际执行到的帧号
		private uint _maxFrqNo;//记录最大帧号

		private int _repairCounter;
		private uint _repairBegNo;
		private int _repairTimes;
		private int _timeoutTurnStep;

        public bool IsRepairing
        {
            get { return _maxFrqNo > _begFrqNo; }
        }

        private LinkedList<FrapWrap> _laterTurns = new LinkedList<FrapWrap>();

		private bool _hasPoolCreated = false;

        private bool _laterTurnTracing = false;
        public bool IsLaterTurnTracing
        {
            get { return _laterTurnTracing && _laterTurns.Count == 0; }
            set { _laterTurnTracing = value; }
        }

		public TurnWindow()
		{
			if (!_hasPoolCreated) {
				ObjectCachePool.instance.CreatePool<FrapWrap> (128, 16);
				_hasPoolCreated = true;
			}
			Reset();
            Mercury.instance.AddListener(EventTokenTable.et_Turnwindow, OnMercuryEvent);   
		}

        private void OnMercuryEvent(object sender, MercuryEventBase e)
        {
            if (e.eventId == (int)EMercuryEvent.E_ME_OBJ_DELIVER)
            {
                MEObjDeliver evt = (MEObjDeliver)e;
                if (evt.opcode == (int)EObjDeliverOPCode.E_OP_HANDLE_TurnPACK)
                {
                    object[] args = (object[])evt.obj;
                    if (!HandleTurnCommandPackage((uint)args[0], args[1]))
                    {
                        uint TurnID = (uint)args[0];
                        if (TurnID >= _begFrqNo)
                        {
                            FrapWrap wrap = ObjectCachePool.instance.Fetch<FrapWrap>();
                            wrap.TurnID = TurnID;
                            wrap.data = args[1];

                            if (!Utility.LinkedListInsert(_laterTurns, wrap, FrapWrap.FrapWrapInsertComparsionFunc))
                            {
                                ProcessTurnDropInternal(TurnID, wrap.data, false);
                                wrap.Release();
                            }
                        }
                    }
                }
            }
        }

        private uint _TurnNo2WindowIdx(uint theFrqNo)
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
			_timeoutTurnStep = 6;
		}

        public void ClearRepair ()
        {
            _repairBegNo = 0u;
            _repairCounter = 0;
            _repairTimes = 0;
        }

		public void UpdateTurn()
		{
            if (Reconnection.instance.IsReconnection 
                || TurnSyncService.instance.ServiceMode == TurnSyncService.EServiceMode.E_SM_LOCALLY
                || TurnSyncService.instance.ServiceMode == TurnSyncService.EServiceMode.E_SM_OB 
                || TurnSyncService.instance.ServiceMode == TurnSyncService.EServiceMode.E_SM_PLAYBACK)                
                return;

            while (_laterTurns.Count > 0)
            {
                FrapWrap wrap = _laterTurns.First.Value;
                if (HandleTurnCommandPackage(wrap.TurnID, wrap.data))
                {
                    _laterTurns.RemoveFirst();
                    wrap.Release();

                    if (!_laterTurnTracing)
                        _laterTurnTracing = true;
                }
                else
                {
                    break;
                }
            }

            if (_maxFrqNo > _begFrqNo)
            {
                if (_receiveWindow[(int)_TurnNo2WindowIdx(_begFrqNo)] == null)
                {
                    if (_repairBegNo != _begFrqNo)
                    {
                        _repairBegNo = _begFrqNo;
                        RequestRepairLackTurns();
                        _repairTimes = 0;
                        _repairCounter = 0;
                    }
                    else if (++_repairCounter > (2 ^ _repairTimes) * _timeoutTurnStep)
                    {
                        RequestRepairLackTurns();
                        _repairCounter = 0;
                        _repairTimes++;
                    }
                }
            }
		}

        private void ProcessTurnCommandInternal(object msg)
        {           
            MEObjDeliver e = ObjectCachePool.instance.Fetch<MEObjDeliver>();
            e.args[0] = msg;
            e.opcode = (int)EObjDeliverOPCode.E_OP_PROCESS_TurnPACK;

            Mercury.instance.Broadcast(EventTokenTable.et_game_Turnwork, this, e);
        }

        private void ProcessTurnDropInternal (uint TurnID, object msg, bool releaseSharedBuf = true)
        {
            MEObjDeliver e = ObjectCachePool.instance.Fetch<MEObjDeliver>();
            e.args[0] = TurnID;
            e.args[1] = msg;
            e.args[2] = releaseSharedBuf;
            e.opcode = (int)EObjDeliverOPCode.E_OP_PROCESS_TurnDROP;

            Mercury.instance.Broadcast(EventTokenTable.et_game_Turnwork, this, e);
        }

 
        public bool HandleTurnCommandPackage(uint pkgFrapNo, object msg)
        {
            bool result = false;
            if (pkgFrapNo > _maxFrqNo)
            {
                _maxFrqNo = pkgFrapNo;
            }
            //收到的帧过期，做丢弃处理。
            if (pkgFrapNo < _begFrqNo)
            {
                ProcessTurnDropInternal(pkgFrapNo, msg);
                return true;             
            }

            if (_begFrqNo <= pkgFrapNo && pkgFrapNo < _basFrqNo + FRQ_WIN_LEN)
            {
                _receiveWindow[(int)_TurnNo2WindowIdx(pkgFrapNo)] = msg;               
                if (TurnSyncService.instance.isActive)
                {
                    object rawCmd = null;
                    while ((rawCmd = _FetchRawCommand(_begFrqNo)) != null)
                    {
                        if ((_begFrqNo += 1u) % FRQ_WIN_LEN == 0u)
                        {
                            _basFrqNo = _begFrqNo;
                        }

                        ProcessTurnCommandInternal(rawCmd);                        
                    }
                }

                result = true;
            }

            return result;
        }

        private object _FetchRawCommand(uint frqNo)
		{
			uint id = _TurnNo2WindowIdx(frqNo);
            object result = _receiveWindow[(int)id];
            _receiveWindow[(int)id] = null;

            return result;
		}

        virtual public void Dispose()
        {
            _receiveWindow = null;
            Mercury.instance.RemoveListener(EventTokenTable.et_Turnwindow, OnMercuryEvent);
        }

		private void RequestRepairLackTurns()
		{
            if (_maxFrqNo <= _begFrqNo)
            {
                return;
            }

            List<int> Turns = new List<int>();

            //多请求两帧
            int len = Mathf.Min ((int)(_maxFrqNo - _begFrqNo + 2u), (int)MAX_REPAIR_TURNCOUNT);
            for (uint ii = _begFrqNo; ii < _begFrqNo + len; ++ii)
            {
                //再做一次检测，防止由于帧乱序到达而重复请求
                int pos = (int)_TurnNo2WindowIdx(ii);
                if (_receiveWindow[pos] == null)
                {
                    Turns.Add((int)ii);
                }
            }

            if ( Turns.Count > 0 )
            {
                MEObjDeliver e = ObjectCachePool.instance.Fetch<MEObjDeliver>();
                int[] tmp = Turns.ToArray();
                e.args[0] = (object)tmp;
                e.opcode = (int)EObjDeliverOPCode.E_OP_LACK_TurnS;
                Mercury.instance.Broadcast(EventTokenTable.et_game_Turnwork, this, e);
            }
		}
	}
}
