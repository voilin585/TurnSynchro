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
        /// �ؼ�֡ʱ����
        /// </summary>
		public uint TurnDelta = 50u;

        /// <summary>
        /// ������֡������
        /// </summary>
		private uint EndBlockWaitNum;
		public uint PreActTurns = 5u;

		public int nDriftFactor = 16;

        /// <summary>
        /// ���������ӳ�֡����
        /// </summary>
		public uint SvrTurnLater;

		public uint SvrTurnDelta = 50u;
		private uint SvrTurnIndex;
		private uint KeyTurnRate = 1u;

		private uint ServerSeed = 12345u;

        /// <summary>
        /// ��ʼ֡ʱ��
        /// </summary>
		public float startTurnTime;

		private uint backstepTurnCounter;
		private uint uCommandId;
		private Queue<ITurnCommand> commandQueue = new Queue<ITurnCommand>();
		private byte m_turnSpeed = 1;

        /// <summary>
        /// ��ǰ���ӳ�
        /// </summary>
		private int _CurPkgDelay;

        /// <summary>
        /// ƽ���ӳ�
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
        /// �����߼�֡����������֡����ģ�鸺���޸ģ�����һ�����ģ�������Ծʽ�ƽ�
        /// </summary>
		public uint EndTurnNum
		{
			get;
			private set;
		}

        //��������ʹ�ã�
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

        //�����֡��������ģ������ģ��𲽵����ģ�ֻ��֡�ļ��ʱ�������ӳٵĴ��ڶ���ȷ��
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

        //����������ĺ���ʱ�䣿

		public void CalcBackstepTimeSinceStart(uint inSvrNum)
		{
			if (backstepTurnCounter == inSvrNum)
			{
				return;
			}

            //ս����ʼ��ķ�����ʱ��
			ulong serverTime = (ulong)inSvrNum * (ulong)SvrTurnDelta;
            float delta = Time.realtimeSinceStartup - serverTime * 0.001f;
            float serverTimeGap = delta - startTurnTime;
            //��������������£�serverTimeGap ��С�� 0 ��
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
        /// �÷���ÿ����Ⱦ֡��ִ��һ�Σ����ǻ����߼�֡���������ƽ���ӳټ��㣬�����߼�֡��ִ��
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

            //֡ͬ����ʼ�󾭹��˶���ʱ�䡣nowtime ����Ⱦ֡�����С��һ���߼�֡
            long nowTime = (long)((rt - startTurnTime) * 1000f);
            //ĳһ���߼�֡�ӳ�ʱ�䡣��������һ���߼�֡����֮ǰ��nDelayMs��С����ģ�С�������ѧ�����ǳ�Խ�ӳ٣�ʵ�ʲ������ڣ���ʱֻ�����߼�֡��Χ�ڣ�
            //˵����û���ӳ١�����ʱ����ƽ���nowTime ��Խ��Խ��ֱ�������߼�֡��ʱ�����ӳ���30ms�ڣ�����Ϊ��û��ʱ�ӵ�
            long nDelayMs = nowTime - (long)((SvrTurnIndex + 1u) * SvrTurnDelta);
            //ƽ���ӳ٣���30֡���㣩
            int smoothDelay = CalculateJitterDelay(nDelayMs);            
            nowTime *= (long)TurnSpeed;
			while (i > 0)
			{
				long lastTime = (long)(CurTurnNum * TurnDelta);
                long deltaTime = nowTime - lastTime;
                deltaTime -=  (long)smoothDelay;
                //�����Ǵ�����ģ���� deltaTime ������Ⱦ֡������������������С��һ���߼�֡ʱ�������κδ�����������һ���߼�֡ʱ���򴥷�һ���߼�֡����
                //
                if (deltaTime >= (long)TurnDelta)
				{
                    //����һֱû���յ��µ��߼�֡����ÿ�ζ���ִ�е����ʮ�а˾�������������⣩
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

                        //����ʹ����ò��һ�£�������ִ�������ˢ����Ϸ�߼�
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
