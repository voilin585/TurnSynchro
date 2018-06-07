using System;

namespace FrameSyncModule
{
	public struct FrameCommand<T> : IFrameCommand where T : struct, ICommand
    {
		private uint _playerID;
		private uint _frameNum;
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

		public uint frameNum
		{
			get
			{
				return _frameNum;
			}
			set
			{
				_frameNum = value;
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
		}
	}
}
