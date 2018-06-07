using System;
namespace FrameSyncModule
{
	public interface ICommand
	{
        int Serialize(uint id, ref byte[] data);
        bool Deserialize(object msg);

        void OnReceive(IFrameCommand cmd);
		void Preprocess(IFrameCommand cmd);
		void ExecCommand(IFrameCommand cmd);
	}
}
