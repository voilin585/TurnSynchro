
//断线重连的插件式扩展
public interface IReconnectionExtension
{
    void Init();
    void UnInit();
    void Update();
    void Reset();
}