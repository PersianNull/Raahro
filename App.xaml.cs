using Raahro.Services;
using Raahro.ViewModels;
using System.Windows;

namespace Raahro;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var adapterService = new AdapterService();
        var singBoxService = new SingBoxService();
        var hotspotService = new HotspotService();
        var icsService = new IcsService();
        var clientDiscovery = new ClientDiscoveryService();
        var policyService = new BandwidthPolicyService();
        var trafficMonitor = new TrafficMonitorService();
        var packetCapture = new PacketCaptureService();
        var clientBlock = new ClientBlockService();



        _ = policyService.LoadAsync();

        var viewModel = new MainViewModel(
            singBoxService,
            hotspotService,
            icsService,
            adapterService,
            clientDiscovery,
            policyService,
            trafficMonitor,
            packetCapture,
            clientBlock);

        var window = new MainWindow
        {
            DataContext = viewModel
        };

        window.Show();
    }
}