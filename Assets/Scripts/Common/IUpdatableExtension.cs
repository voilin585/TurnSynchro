
//可作为断线重连的插件式扩展
public interface IUpdatableExtension
{
    void Init();
    void UnInit();
    void Update();
    void Reset();
}