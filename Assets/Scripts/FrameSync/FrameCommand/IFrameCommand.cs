using System;

namespace FrameSyncModule
{
	public interface IFrameCommand
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

		uint frameNum
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
