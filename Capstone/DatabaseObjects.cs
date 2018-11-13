using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using Newtonsoft.Json;
using RestSharp;

namespace Capstone
{
    public class ApRssiPair
    {
        public string _id { get; set; }
        public string _parent_id { get; set; }
        public int ap_rssi_id { get; set; }
        public int rssi { get; set; }
        public string ap_mac_addr { get; set; }
    }

    public class Fingerprint
    {
        public string _id { get; set; }
        public int fp_id { get; set; }
        public double fp_latitude { get; set; }
        public double fp_longitude { get; set; }
        public List<ApRssiPair> ap_rssi { get; set; }
    }
}