
using System;
using System.Collections.Generic;
using UnityEngine;
using EventBus;

namespace FrameSyncModule
{
    public class FrameSynchr : IDisposable
	{
		public const byte MIN_FRAME_SPEED = 1;
		public const byte MAX_FRAME_SPEED = 8;

		private const int FrameDelay_Limit = 200;
		private const float JitterCoverage = 0.85f;
		private const int StatDelayCnt = 30;

		private bool _bActive;
		public bool bRunning;
		public bool bEscape;

        /// <summary>
        /// 关键帧时间间隔
        /// </summary>
		public uint FrameDelta = 50u;

        /// <summary>
        /// 阻塞的帧的数量
        /// </summary>
		private uint EndBlockWaitNum;
		public uint PreActFrames = 5u;

		public int nDriftFactor = 16;

		public uint SvrFrameLater;

		public uint SvrFrameDelta = 50u;
		private uint SvrFrameIndex;
		private uint KeyFrameRate = 1u;

		private uint ServerSeed = 12345u;

        /// <summary>
        /// 起始帧时间
        /// </summary>
		public float startFrameTime;

		private uint backstepFrameCounter;
		private uint uCommandId;
		private Queue<IFrameCommand> commandQueue = new Queue<IFrameCommand>();
		private byte frameSpeed = 1;

        /// <summary>
        /// 当前包延迟
        /// </summary>
		private int _CurPkgDelay;

        /// <summary>
        /// 平均延迟
        /// </summary>
		private int AvgFrameDelay;
        private float fLocalRunTime = 0.0f;

        public bool bShowJitterChart;

		public int tryCount;

		public uint CurFrameNum
		{
			get;
			private set;
		}

        /// <summary>
        /// 最后的逻辑帧，该数据由帧窗口模块负责修改，是逐一递增的，不会跳跃式推进
        /// </summary>
		public uint EndFrameNum
		{
			get;
			private set;
		}

        //断线重连使用
        public uint BlockFrameWaitNum
        {
            get { return EndBlockWaitNum; }
        }

		public ulong LogicFrameTick
		{
			get;
			private set;
		}

		public byte FrameSpeed
		{
			get
			{
				return frameSpeed;
			}
			set
			{
				frameSpeed = (byte)Mathf.Clamp((int)value, 1, 8);
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
                    Mercury.instance.Broadcast(EventTokenTable.et_framesynchr, this, e);
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
			if (AvgFrameDelay < 0)
			{
				AvgFrameDelay = _CurPkgDelay;
			}
			else
			{
				AvgFrameDelay = (29 * AvgFrameDelay + _CurPkgDelay) / 30;
			}
			return AvgFrameDelay;
		}

        virtual public void Dispose()
        {
            commandQueue.Clear();
        }

        //这里的帧号是有序的，完整的，逐步递增的，只是帧的间隔时间由于延迟的存在而不确定
		public bool SetKeyFrameIndex(uint svrNum, bool noIndexChk = false)
		{
            if (noIndexChk || svrNum > SvrFrameIndex)
            {
                SvrFrameIndex = svrNum;
                EndFrameNum = (svrNum + SvrFrameLater) * KeyFrameRate;
                CalcBackstepTimeSinceStart(svrNum);
                return true;
            }
            return false;
		}

		public void ResetSynchr()
		{
			bActive = false;
			SetSynchrRunning(true);
			FrameDelta = 50u;
			CurFrameNum = 0u;
			EndFrameNum = 0u;
			LogicFrameTick = 0uL;
			EndBlockWaitNum = 0u;
			PreActFrames = 50u;
			SvrFrameDelta = FrameDelta;
			SvrFrameLater = 0u;
			SvrFrameIndex = 0u;
			KeyFrameRate = 1u;
			frameSpeed = 1;
		    fLocalRunTime = 0;
			_CurPkgDelay = 0;
			AvgFrameDelay = 0;
			NewCommandId = 0u;
			startFrameTime = Time.realtimeSinceStartup;
			backstepFrameCounter = 0u;
			commandQueue.Clear();
		}

		public void SetSynchrConfig(uint svrDelta, uint frameLater, uint preActNum, uint randSeed, int driftFactor)
		{
			SvrFrameDelta = svrDelta;
			SvrFrameLater = 0u;
			KeyFrameRate = 1u;
			PreActFrames = preActNum;
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
				startFrameTime = (Time.realtimeSinceStartup * (float)frameSpeed - LogicFrameTick * 0.001f) / (float)frameSpeed;
			}
			else
			{
                startFrameTime = Time.time - LogicFrameTick * 0.001f + Time.smoothDeltaTime;
			}
		}

		public void SetSynchrRunning(bool bRun)
		{
			bRunning = bRun;
            FrameRandom.ResetSeed(ServerSeed);
		}

		public void StartSynchr(bool bAutoRun)
		{
			bActive = true;
			SetSynchrRunning(bAutoRun);
			SvrFrameIndex = 0u;
			FrameDelta = SvrFrameDelta / KeyFrameRate;
			CurFrameNum = 0u;
			EndFrameNum = 0u;
			LogicFrameTick = 0uL;
			EndBlockWaitNum = 0u;
			frameSpeed = 1;
			_CurPkgDelay = 0;
			AvgFrameDelay = 0;
            fLocalRunTime = 0.0f;
			commandQueue.Clear();
			NewCommandId = 0u;
			startFrameTime = Time.realtimeSinceStartup;
			backstepFrameCounter = 0u;
		}


		public void CalcBackstepTimeSinceStart(uint inSvrNum)
		{
			if (backstepFrameCounter == inSvrNum)
			{
				return;
			}

            //战斗开始后的服务器时间
			ulong serverTime = (ulong)inSvrNum * (ulong)SvrFrameDelta;
            float delta = Time.realtimeSinceStartup - serverTime * 0.001f;
            float serverTimeGap = delta - startFrameTime;
            //网络正常的情况下，serverTimeGap 是小于 0 的
            if (serverTimeGap < 0f)
			{
                startFrameTime = delta;
			}
			backstepFrameCounter = inSvrNum;
		}

        private void UpdateMultiFrameByLocalTime()
        {
            fLocalRunTime += Time.smoothDeltaTime;
            SetKeyFrameIndex((uint)(fLocalRunTime * 1000f / FrameDelta), true);
            UpdateMultiFrame(true);
        }

        /// <summary>
        /// 该方法每个渲染帧会执行一次，但是会以逻辑帧间隔，经过平均延迟计算，触发逻辑帧的执行
        /// </summary>
        /// <param name="bLocalTimeDriver"></param>
        private void UpdateMultiFrame(bool bLocalTimeDriver = false)
		{			
            {
                MEObjDeliver e = ObjectCachePool.instance.Fetch<MEObjDeliver>();
                e.opcode = (int)EObjDeliverOPCode.E_OP_UPDATE_TAILS_OF_GAME_LOGIC;
                Mercury.instance.Broadcast(EventTokenTable.et_framesynchr, this, e);
            }
			
            int drift = (int)((EndFrameNum - CurFrameNum) / (uint)nDriftFactor);
            tryCount = Mathf.Clamp(drift, 1, 100);
			int i = tryCount;
			float rt = Time.realtimeSinceStartup;
            if (bLocalTimeDriver)
            {
                rt = fLocalRunTime + startFrameTime;
            }

            //帧同步开始后经过了多少时间。nowtime 是渲染帧间隔，小于一个逻辑帧
            long nowTime = (long)((rt - startFrameTime) * 1000f);

            long nDelayMs = nowTime - (long)((SvrFrameIndex + 1u) * SvrFrameDelta);
            //平均延迟（以30帧计算）
            int smoothDelay = CalculateJitterDelay(nDelayMs);            
            nowTime *= (long)frameSpeed;
			while (i > 0)
			{
				long lastTime = (long)(CurFrameNum * FrameDelta);
                long deltaTime = nowTime - lastTime;
                deltaTime -=  (long)smoothDelay;
                //这里是处理核心，如果 deltaTime 随着渲染帧的增长而增长，当他小于一个逻辑帧时，则不做任何处理，当他大于一个逻辑帧时，则触发一次逻辑帧处理
                if (deltaTime >= (long)FrameDelta)
				{
                    //假设一直没有收到新的逻辑帧，则每次都会执行到这里
					if (CurFrameNum >= EndFrameNum)
					{
						EndBlockWaitNum += 1u;
						i = 0;
					}
					else
					{
						EndBlockWaitNum = 0u;
						CurFrameNum += 1u;
                        LogicFrameTick += (ulong)FrameDelta;

                        //先执行命令，再刷新游戏逻辑
						while (commandQueue.Count > 0)
						{
							IFrameCommand frameCommand = commandQueue.Peek();
                            uint commandFrame = (frameCommand.frameNum + SvrFrameLater) * KeyFrameRate;
                            if (commandFrame > CurFrameNum)
                            {
                                break;
                            }

                            frameCommand = commandQueue.Dequeue();
                            frameCommand.frameNum = commandFrame;
                            frameCommand.ExecCommand();

                            AbstractSmartObj obj = (AbstractSmartObj)frameCommand;
                            if (obj != null)
                                obj.Release();					
						}
                        if (!bEscape)
						{							
                            //通知刷新游戏逻辑层
                            MEObjDeliver e = ObjectCachePool.instance.Fetch<MEObjDeliver>();

                            e.args[0] = (int)FrameDelta;
                            e.args[1] = i == 1 && deltaTime < (long)(2u * FrameDelta); //false;                            
                            e.opcode = (int)EObjDeliverOPCode.E_OP_UPDATE_GAME_LOGIC;

                            Mercury.instance.Broadcast(EventTokenTable.et_framesynchr, this, e);                           
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
              
		public void UpdateFrame()
		{
			if (bActive)
			{
				if (bRunning)
				{
					UpdateMultiFrame();
				}
			}
			else
			{
                UpdateMultiFrameByLocalTime();
            }
		}

		public void PushFrameCommand(IFrameCommand command)
		{
			command.cmdId = NewCommandId;
            if (_bActive)
			{
				command.OnReceive();
			}
			else
			{
				command.frameNum = CurFrameNum;
			}

            commandQueue.Enqueue(command);
		}
	}
}
