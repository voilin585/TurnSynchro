using System;

namespace TurnSyncModule
{
	public struct TurnCommand<T> : ITurnCommand where T : struct, ICommand
    {
		private uint _playerID;
		private uint _TurnNum;
		private uint _cmdId;
        private byte _syncType;

		private byte _cmdType;

		public T cmdData;

		public uint cmdId
		{
			get
			{
				return _cmdId;
			}
			set
			{
				_cmdId = value;
			}
		}

		public byte cmdType
		{
			get
			{
				return _cmdType;
			}
			set
			{
				_cmdType = value;
			}
		}

		public uint TurnNum
		{
			get
			{
				return _TurnNum;
			}
			set
			{
				_TurnNum = value;
			}
		}

		public uint playerID
		{
			get
			{
				return _playerID;
			}
			set
			{
				_playerID = value;
			}
		}

        public byte syncType
        {
            get
            {
                return _syncType;
            }
            set
            {
                _syncType = value;
            }
        }

        public int Serialize(ref byte[] data)
        {            
            return cmdData.Serialize(_playerID, ref data);
        }

        public bool Deserialize(object msg)
        {
            return cmdData.Deserialize(msg);
        }

		public void OnReceive()
		{
			cmdData.OnReceive(this);
		}

		public void Preprocess()
		{
			cmdData.Preprocess(this);
		}

        public void ExecCommand()
		{
			cmdData.ExecCommand(this);
#if DEBUG_LOGOUT
            if (MobaGo.Game.Data.DebugMask.HasMask(MobaGo.Game.Data.DebugMask.E_DBG_MASK.MASK_MOVEDATA))
            {
                DebugHelper.LogOut(string.Format("{0} Player:{5} cmd {1} {2} {3} {4}",
                    TurnSyncService.instance.GetTurnSyncChr().CurTurnNum, this._cmdId, this._playerID, this._cmdType,
                    this._TurnNum, this.cmdData.GetType()));
            }
#endif
		}
	}
}
