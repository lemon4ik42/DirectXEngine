
namespace DirectXEngine
{
    internal static class Program
    {
        private static void Main()
        {
            EngineCore core = EngineCore.Current;
            core.Initialize();
            new Scene();
            Camera.Main.AddComponent<Initializer>();
            core.Run();
        }
    }
}
