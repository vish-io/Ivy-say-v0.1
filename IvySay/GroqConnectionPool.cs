using System.Net.Http;
using System.Net.NetworkInformation;
namespace IvySay;

// Refresh only for new requests. In-flight requests keep their client until finished.
internal sealed class GroqConnectionPool : IDisposable
{
    internal sealed class Generation(HttpClient client) { public readonly HttpClient Client = client; public int Active; public bool Retired; }
    private readonly object gate = new();
    private readonly Func<HttpMessageHandler> factory;
    private readonly bool refreshable, subscribed;
    private Generation? current;
    private bool refresh, disposed;
    public GroqConnectionPool(Func<HttpMessageHandler> factory, bool refreshable = true, bool observeNetwork = false)
    {
        this.factory = factory; this.refreshable = refreshable; subscribed = observeNetwork;
        if (subscribed) NetworkChange.NetworkAddressChanged += OnNetworkChanged;
    }
    private void OnNetworkChanged(object? sender, EventArgs e) => NotifyNetworkChanged();
    internal void NotifyNetworkChanged() { lock (gate) { if (!disposed && refreshable) refresh = true; } }
    private Generation NewGeneration() => new(new HttpClient(factory(), true)
    { BaseAddress = new Uri("https://api.groq.com/openai/v1/"), Timeout = TimeSpan.FromSeconds(60) });
    public Lease Acquire()
    {
        lock (gate)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (current == null || refresh)
            {
                // Create first, so an allocation failure does not strand the existing pool.
                var next = NewGeneration();
                if (current != null) Retire(current);
                current = next; refresh = false;
            }
            current.Active++;
            return new Lease(this, current);
        }
    }
    private void Retire(Generation generation)
    {
        generation.Retired = true;
        if (generation.Active == 0) generation.Client.Dispose();
    }
    internal void Failed(Lease lease)
    {
        lock (gate) { if (!disposed && refreshable && ReferenceEquals(current, lease.generation)) refresh = true; }
    }
    public void Dispose()
    {
        if (subscribed) NetworkChange.NetworkAddressChanged -= OnNetworkChanged;
        lock (gate) { if (disposed) return; disposed = true; if (current != null) { Retire(current); current = null; } }
    }
    internal sealed class Lease : IDisposable
    {
        private GroqConnectionPool? owner;
        internal readonly Generation generation;
        internal Lease(GroqConnectionPool owner, Generation generation) { this.owner = owner; this.generation = generation; }
        public HttpClient Client => generation.Client;
        public void Dispose()
        {
            var pool = Interlocked.Exchange(ref owner, null);
            if (pool == null) return;
            lock (pool.gate) { generation.Active--; if (generation.Retired && generation.Active == 0) generation.Client.Dispose(); }
        }
    }
}
