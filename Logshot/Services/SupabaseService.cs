using System;
using System.Threading.Tasks;
using Supabase;

namespace Logshot.Services;

public class SupabaseService
{
    private Client _client = null!;

    // We will replace these with your actual Supabase project keys later
    private const string SupabaseUrl = "YOUR_SUPABASE_URL_HERE";
    private const string SupabaseKey = "YOUR_SUPABASE_ANON_KEY_HERE";

    public async Task InitializeAsync()
    {
        // Prevent re-initialization if it's already running
        if (_client != null)
            return;

        var options = new SupabaseOptions
        {
            AutoConnectRealtime = true
        };

        _client = new Client(SupabaseUrl, SupabaseKey, options);
        await _client.InitializeAsync();
    }

    public Client GetClient()
    {
        return _client;
    }
}