using System.Threading.Tasks;

namespace Spreads.SignalW {

    public static class TaskCache {
        public static Task CompletedTask { get; } = Task.FromResult(0);
    }
}
