using System;
using System.Collections.Generic;
using System.Text;

namespace Raahro.Models
{
    public class ClientPolicy
    {
        public string MacAddress { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public BandwidthLimit Limit { get; set; } = new();
    }
}
