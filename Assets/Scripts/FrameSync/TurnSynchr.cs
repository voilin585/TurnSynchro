using System;
using System.Collections.Generic;
using UnityEngine;
using EventBus;

namespace TurnSyncModule
{
    public class TurnSynchr : IDisposable
	{
		public const byte MIN_Turn_SPEED = 1;
		public const byte MAX_Turn_SPEED = 8;

        /// <summary>
        /// 
        /// </summary>
		private const int TurnDelay_Limit = 200;
		private const float JitterCoverage = 0.85f;
		private const int StatDelayCnt = 30;

		private bool _bActive;
		public bool bRunning;
		public bool bEscape;

        /// <summary>
        /// 关键帧时间间隔
        /// </summary>
		public uint TurnDelta = 50u;

        /// <summary>
        /// 阻塞的帧的数量
        /// </summary>
		private uint EndBlockWaitNum;
		public uint PreActTurns = 5u;

		public int nDriftFactor = 16;

        /// <summary>
        /// 服务器的延迟帧数？
        /// </summary>
		public uint SvrTurnLater;

		public uint SvrTurnDelta = 50u;
		private uint SvrTurnIndex;
		private uint KeyTurnRate = 1u;

		private uint ServerSeed = 12345u;

        /// <summary>
        /// 起始帧时间
        /// </summary>
		public float startTurnTime;

		private uint backstepTurnCounter;
		private uint uCommandId;
		private Queue<ITurnCommand> commandQueue = new Queue<ITurnCommand>();
		private byte m_turnSpeed = 1;

        /// <summary>
        /// 当前包延迟
        /// </summary>
		private int _CurPkgDelay;

        /// <summary>
        /// 平均延迟
        /// </summary>
		private int AvgTurnDelay;
        private float fLocalRunTime = 0.0f;

        public bool bShowJitterChart;

		public int tryCount;

		public uint CurTurnNum
		{
			get;
			private set;
		}

        /// <summary>
        /// 最后的逻辑帧，该数据由帧窗口模块负责修改，是逐一递增的，不会跳跃式推进
        /// </summary>
		public uint EndTurnNum
		{
			get;
			private set;
		}

        //断线重连使用？
        public uint BlockTurnWaitNum
        {
            get { return EndBlockWaitNum; }
        }

		public ulong LogicTurnTick
		{
			get;
			private set;
		}

		public byte TurnSpeed
		{
			get
			{
				return m_turnSpeed;
			}
			set
			{
                m_turnSpeed = (byte)Mathf.Clamp((int)value, 1, 8);
				if (_bActive)
				{
					ResetStartTime();
				}
			}
		}

        public bool bActive
		{
			get
			{
				return _bActive;
			}
			set
			{
				if (_bActive != value)
				{

                    MEObjDeliver e = ObjectCachePool.instance.Fetch<MEObjDeliver>();
                    e.opcode = (int)EObjDeliverOPCode.E_OP_UPDATE_TAILS_OF_GAME_LOGIC;
                    Mercury.instance.Broadcast(EventTokenTable.et_Turnsynchr, this, e);
                    _bActive = value;
				}
			}
		}

        public bool HasExecutableCommands
        {
            get { return commandQueue.Count > 0; }
        }

		public uint NewCommandId
		{
			get
			{
				++uCommandId;
				return uCommandId;
			}
			set
			{
				uCommandId = value;
			}
		}

		public int CalculateJitterDelay(long nDelayMs)
		{
			_CurPkgDelay = ((nDelayMs <= 0L) ? 0 : ((int)nDelayMs));
			if (AvgTurnDelay < 0)
			{
				AvgTurnDelay = _CurPkgDelay;
			}
			else
			{
				AvgTurnDelay = (29 * AvgTurnDelay + _CurPkgDelay) / 30;
			}
			return AvgTurnDelay;
		}

        virtual public void Dispose()
        {
            commandQueue.Clear();
        }

        //这里的帧号是有序的，完整的，逐步递增的，只是帧的间隔时间由于延迟的存在而不确定
		public bool SetKeyTurnIndex(uint svrNum, bool noIndexChk = false)
		{
            if (noIndexChk || svrNum > SvrTurnIndex)
            {
                SvrTurnIndex = svrNum;
                EndTurnNum = (svrNum + SvrTurnLater) * KeyTurnRate;
                CalcBackstepTimeSinceStart(svrNum);
                return true;
            }
            return false;
		}

		public void ResetSynchr()
		{
			bActive = false;
			SetSynchrRunning(true);
			TurnDelta = 50u;
			CurTurnNum = 0u;
			EndTurnNum = 0u;
			LogicTurnTick = 0uL;
			EndBlockWaitNum = 0u;
			PreActTurns = 50u;
			SvrTurnDelta = TurnDelta;
			SvrTurnLater = 0u;
			SvrTurnIndex = 0u;
			//CacheSetLater = 0u;
			KeyTurnRate = 1u;
			TurnSpeed = 1;
		    fLocalRunTime = 0;
			_CurPkgDelay = 0;
			AvgTurnDelay = 0;
			NewCommandId = 0u;
			startTurnTime = Time.realtimeSinceStartup;
			backstepTurnCounter = 0u;
			commandQueue.Clear();
		}

		public void SetSynchrConfig(uint svrDelta, uint TurnLater, uint preActNum, uint randSeed, int driftFactor)
		{
			SvrTurnDelta = svrDelta;
			SvrTurnLater = 0u;
			//CacheSetLater = SvrTurnLater;
			KeyTurnRate = 1u;
			PreActTurns = preActNum;
			ServerSeed = randSeed;
            nDriftFactor = Mathf.Max(driftFactor, 4);
		}

		public void SwitchSynchrLocal()
		{
			if (bActive)
			{
				bActive = false;
				ResetStartTime();
			}
		}

		public void ResetStartTime()
		{
			if (bActive)
			{
				startTurnTime = (Time.realtimeSinceStartup * (float)TurnSpeed - LogicTurnTick * 0.001f) / (float)TurnSpeed;
			}
			else
			{
                startTurnTime = Time.time - LogicTurnTick * 0.001f + Time.smoothDeltaTime;
			}
		}

		public void SetSynchrRunning(bool bRun)
		{
			bRunning = bRun;
            TurnRandom.ResetSeed(ServerSeed);
		}

		public void StartSynchr(bool bAutoRun)
		{
			bActive = true;
			SetSynchrRunning(bAutoRun);
			SvrTurnIndex = 0u;
			TurnDelta = SvrTurnDelta / KeyTurnRate;
			CurTurnNum = 0u;
			EndTurnNum = 0u;
			LogicTurnTick = 0uL;
			EndBlockWaitNum = 0u;
			TurnSpeed = 1;
			_CurPkgDelay = 0;
			AvgTurnDelay = 0;
            fLocalRunTime = 0.0f;
			commandQueue.Clear();
			NewCommandId = 0u;
			startTurnTime = Time.realtimeSinceStartup;
			backstepTurnCounter = 0u;
		}

        //计算启动后的后退时间？

		public void CalcBackstepTimeSinceStart(uint inSvrNum)
		{
			if (backstepTurnCounter == inSvrNum)
			{
				return;
			}

            //战斗开始后的服务器时间
			ulong serverTime = (ulong)inSvrNum * (ulong)SvrTurnDelta;
            float delta = Time.realtimeSinceStartup - serverTime * 0.001f;
            float serverTimeGap = delta - startTurnTime;
            //网络正常的情况下，serverTimeGap 是小于 0 的
            if (serverTimeGap < 0f)
			{
                startTurnTime = delta;
			}
			backstepTurnCounter = inSvrNum;
		}

        private void UpdateMultiTurnByLocalTime()
        {
            fLocalRunTime += Time.smoothDeltaTime;
            SetKeyTurnIndex((uint)(fLocalRunTime * 1000f / TurnDelta), true);
            UpdateMultiTurn(true);
        }

        /// <summary>
        /// 该方法每个渲染帧会执行一次，但是会以逻辑帧间隔，经过平均延迟计算，出发逻辑帧的执行
        /// </summary>
        /// <param name="bLocalTimeDriver"></param>
        private void UpdateMultiTurn(bool bLocalTimeDriver = false)
		{			
            {
                MEObjDeliver e = ObjectCachePool.instance.Fetch<MEObjDeliver>();
                e.opcode = (int)EObjDeliverOPCode.E_OP_UPDATE_TAILS_OF_GAME_LOGIC;
                Mercury.instance.Broadcast(EventTokenTable.et_Turnsynchr, this, e);
            }
			
            int drift = (int)((EndTurnNum - CurTurnNum) / (uint)nDriftFactor);
            tryCount = Mathf.Clamp(drift, 1, 100);
			int i = tryCount;


			float rt = Time.realtimeSinceStartup;
            if (bLocalTimeDriver)
            {
                rt = fLocalRunTime + startTurnTime;
            }

            //帧同步开始后经过了多少时间。nowtime 是渲染帧间隔，小于一个逻辑帧
            long nowTime = (long)((rt - startTurnTime) * 1000f);
            //某一个逻辑帧延迟时间。举例，第一个逻辑帧到来之前，nDelayMs是小于零的，小于零的数学意义是超越延迟，实际并不存在，此时只能在逻辑帧范围内，
            //说明还没有延迟。随着时间的推进，nowTime 会越来越大，直到超过逻辑帧的时长，延迟在30ms内，则被认为是没有时延的
            long nDelayMs = nowTime - (long)((SvrTurnIndex + 1u) * SvrTurnDelta);
            //平均延迟（以30帧计算）
            int smoothDelay = CalculateJitterDelay(nDelayMs);            
            nowTime *= (long)TurnSpeed;
			while (i > 0)
			{
				long lastTime = (long)(CurTurnNum * TurnDelta);
                long deltaTime = nowTime - lastTime;
                deltaTime -=  (long)smoothDelay;
                //这里是处理核心，如果 deltaTime 随着渲染帧的增长而增长，当他小于一个逻辑帧时，则不做任何处理，当他大于一个逻辑帧时，则触发一次逻辑帧处理
                //
                if (deltaTime >= (long)TurnDelta)
				{
                    //假设一直没有收到新的逻辑帧，则每次都会执行到这里（十有八九是网络出了问题）
					if (CurTurnNum >= EndTurnNum)
					{
						EndBlockWaitNum += 1u;
						i = 0;
					}
					else
					{
						EndBlockWaitNum = 0u;
						CurTurnNum += 1u;
                        LogicTurnTick += (ulong)TurnDelta;

                        //这里和传奇的貌似一致，都是先执行命令，在刷新游戏逻辑
						while (commandQueue.Count > 0)
						{
							ITurnCommand TurnCommand = commandQueue.Peek();
                            uint commandTurn = (TurnCommand.TurnNum + SvrTurnLater) * KeyTurnRate;
                            if (commandTurn > CurTurnNum)
                            {
                                break;
                            }

                            TurnCommand = commandQueue.Dequeue();
                            TurnCommand.TurnNum = commandTurn;
                            TurnCommand.ExecCommand();

                            AbstractSmartObj obj = (AbstractSmartObj)TurnCommand;
                            if (obj != null)
                                obj.Release();					
						}
                        if (!bEscape)
						{							
                            //kaede: logic update
                            MEObjDeliver e = ObjectCachePool.instance.Fetch<MEObjDeliver>();

                            e.args[0] = (int)TurnDelta;
                            e.args[1] = i == 1 && deltaTime < (long)(2u * TurnDelta); //false;                            
                            e.opcode = (int)EObjDeliverOPCode.E_OP_UPDATE_GAME_LOGIC;

                            Mercury.instance.Broadcast(EventTokenTable.et_Turnsynchr, this, e);                           
                        }
                        i--;
					}
				}
				else
				{
					i = 0;
				}
			}
		}
              
		public void UpdateTurn()
		{
			if (bActive)
			{
				if (bRunning)
				{
					UpdateMultiTurn();
				}
			}
			else
			{
                // run under single play mode.
                UpdateMultiTurnByLocalTime();
            }
		}

		public void PushTurnCommand(ITurnCommand command)
		{
			command.cmdId = NewCommandId;
            if (_bActive)
			{
				command.OnReceive();
			}
			else
			{
				command.TurnNum = CurTurnNum;
			}

            commandQueue.Enqueue(command);
		}
	}
}
