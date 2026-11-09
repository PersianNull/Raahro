using System.IO;
using System.Text.Json;
using Raahro.Models;

namespace Raahro.Services;

public class BandwidthPolicyService : IBandwidthPolicyService
{
    private readonly string _filePath;
    private readonly Dictionary<string, ClientPolicy> _policies = new(StringComparer.OrdinalIgnoreCase);
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public BandwidthPolicyService()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Raahro");
        Directory.CreateDirectory(dir);
        _filePath = Path.Combine(dir, "client-policies.json");
    }

    public BandwidthLimit GetLimit(string macAddress)
    {
        macAddress = NormalizeMac(macAddress);
        return _policies.TryGetValue(macAddress, out var p)
            ? CloneLimit(p.Limit)
            : new BandwidthLimit();
    }

    public void SetLimit(string macAddress, BandwidthLimit limit, string? displayName = null)
    {
        macAddress = NormalizeMac(macAddress);
        if (!_policies.TryGetValue(macAddress, out var policy))
        {
            policy = new ClientPolicy { MacAddress = macAddress };
            _policies[macAddress] = policy;
        }

        policy.Limit = CloneLimit(limit);
        if (!string.IsNullOrWhiteSpace(displayName))
            policy.DisplayName = displayName;
    }

    public void Remove(string macAddress)
    {
        _policies.Remove(NormalizeMac(macAddress));
    }

    public IReadOnlyList<ClientPolicy> GetAll() => _policies.Values.ToList();

    public async Task LoadAsync()
    {
        try
        {
            if (!File.Exists(_filePath)) return;
            var json = await File.ReadAllTextAsync(_filePath);
            var list = JsonSerializer.Deserialize<List<ClientPolicy>>(json, JsonOptions);
            _policies.Clear();
            if (list == null) return;
            foreach (var p in list)
            {
                if (string.IsNullOrWhiteSpace(p.MacAddress)) continue;
                p.MacAddress = NormalizeMac(p.MacAddress);
                _policies[p.MacAddress] = p;
            }
        }
        catch
        {
            
        }
    }

    public async Task SaveAsync()
    {
        try
        {
            var json = JsonSerializer.Serialize(_policies.Values.ToList(), JsonOptions);
            await File.WriteAllTextAsync(_filePath, json);
        }
        catch
        {
            
        }
    }

    private static string NormalizeMac(string mac) =>
        mac.Trim().Replace(':', '-').ToUpperInvariant();

    private static BandwidthLimit CloneLimit(BandwidthLimit l) => new()
    {
        DownloadKbps = l.DownloadKbps,
        UploadKbps = l.UploadKbps,
        IsBlocked = l.IsBlocked
    };
}