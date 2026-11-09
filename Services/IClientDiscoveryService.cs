using Raahro.Models;
using System;
using System.Collections.Generic;
using System.Text;


namespace Raahro.Services;

public interface IClientDiscoveryService
{
    Task<IReadOnlyList<ConnectedClient>> GetConnectedClientsAsync();
}