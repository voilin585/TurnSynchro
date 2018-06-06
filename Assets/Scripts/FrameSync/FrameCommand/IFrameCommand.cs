using System;

namespace TurnSyncModule
{
	public interface ITurnCommand
	{
        // C2C or C2S
        byte syncType
        {
            get;
            set;
        }

		byte cmdType
		{
			get;
			set;
		}

		uint cmdId
		{
			get;
			set;
		}

		uint TurnNum
		{
			get;
			set;
		}

		uint playerID
		{
			get;
			set;
		}

        int Serialize(ref byte[] data);
        bool Deserialize(object msg);

		void OnReceive();
        void Preprocess();
        void ExecCommand();
	}
}
