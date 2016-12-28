using System.Threading.Tasks;

namespace DataSpreads.SignalW {

    public static class TaskCache {
        public static Task CompletedTask { get; } = Task.FromResult(0);
    }
}
