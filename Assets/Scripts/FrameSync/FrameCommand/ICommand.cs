using System;
namespace TurnSyncModule
{
	public interface ICommand
	{
        int Serialize(uint id, ref byte[] data);
        bool Deserialize(object msg);

        void OnReceive(ITurnCommand cmd);
		void Preprocess(ITurnCommand cmd);
		void ExecCommand(ITurnCommand cmd);
	}
}
