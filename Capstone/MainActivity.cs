using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using Android.App;
using Android.Gms.Maps;
using Android.Gms.Maps.Model;
using Android.Content;
using Android.Locations;
using Android.Net;
using Android.Net.Wifi;
using Android.OS;
using Android.Support.Design.Widget;
using Android.Support.V4.View;
using Android.Support.V4.Widget;
using Android.Support.V7.App;
using Android.Views;
using Android.Widget;
using Java.Util;
using Plugin.Geolocator;
using RestSharp;
using Newtonsoft.Json;
using Android.Support.V4.Content;
using Android;
using Android.Content.PM;
using Newtonsoft.Json.Linq;
using System.Linq;

namespace Capstone
{
    [Activity(Label = "EyeFi", Theme = "@style/AppTheme.NoActionBar", MainLauncher = true)]

    /*public class MapHandler : Android.Support.V4.App.FragmentActivity, IOnMapReadyCallback
    {
        public void OnMapReady(GoogleMap map)
        {
            map.AddMarker(new MarkerOptions().SetPosition(new LatLng(0, 0)).SetTitle("Marker"));
        }
    }*/

    public class MainActivity : AppCompatActivity, NavigationView.IOnNavigationItemSelectedListener, IOnMapReadyCallback
    {
        //Change this to your own network ID name or in the schools case "tamulink-wpa"
        const string networkSSID = "\"" + "tamulink-wpa" + "\"";
       
        TextView wifiText;
        WifiManager wifiManager;
        public int compare;
        public IList<ScanResult> scanResults;
        RestClient client;

        Button LocSwitch;
        bool polling = false;
        //Start as false for Footprint, True for Localizing
        bool PollSwitch = false;
        bool displayNavData = false;

        public void OnMapReady(GoogleMap map)
        {
            map.AddMarker(new MarkerOptions().SetPosition(new LatLng(0, 0)).SetTitle("Marker"));
        }

        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            //Creates main page layout by referencing activity_main.axml
            SetContentView(Resource.Layout.activity_main);

            //Create map
            if (ContextCompat.CheckSelfPermission(this, Manifest.Permission.AccessCoarseLocation) == (int)Permission.Granted && ContextCompat.CheckSelfPermission(this, Manifest.Permission.AccessFineLocation) == (int)Permission.Granted)
            {
                // We have permission, go ahead and use the location.
                Console.WriteLine("PERMISSIONS GRANTED");
                var mapFrag = ((SupportMapFragment)SupportFragmentManager.FindFragmentById(Resource.Id.map));
                mapFrag.GetMapAsync(this);
            }
            else
            {
                // Permission is not granted. If necessary display rationale & request.
                Console.WriteLine("NO PERMISSIONS GRANTED");
            }
            

            //Set up database connection
            var baseUrl = "https://testdb-05fa.restdb.io/rest/";
            client = new RestClient(baseUrl);

            //Everything below simply connects to wifi given the prewritten networkSSID. To test on your own wifi just change it to your wifiName
            //Do we really need to connect to the given SSID anymore since we are scanning access points and listing information?
            var conf = new WifiConfiguration();
            conf.Ssid = networkSSID;
            conf.AllowedKeyManagement.Set((int)KeyManagementType.None);
            var connectivityManager = (ConnectivityManager)GetSystemService(ConnectivityService);
            //The NetworkInfo line is technically obsolete so I disabled the portion of code which prevented its use then restored it. Just ignore for now.
#pragma warning disable CS0618 // Type or member is obsolete
            NetworkInfo.State mobileState = connectivityManager.GetNetworkInfo(ConnectivityType.Wifi).GetState();
#pragma warning restore CS0618 // Type or member is obsolete

            //Sees if it is connected to a network
            if (mobileState != NetworkInfo.State.Connected)
            {
                var mawifi = (WifiManager)GetSystemService(WifiService);
                mawifi.SetWifiEnabled(true);
            }

            wifiManager = (WifiManager)GetSystemService(WifiService);
            wifiManager.AddNetwork(conf);
            var list = wifiManager.ConfiguredNetworks;
            //Finds the network in your network list and connects to it.
            foreach (var i in list)
            {
                if (i.Ssid != null && i.Ssid.Equals(networkSSID))
                {
                    wifiManager.Disconnect();
                    wifiManager.EnableNetwork(i.NetworkId, true);
                    wifiManager.Reconnect();
                    break;
                }
            }

            //Calls the polling function every 4 seconds
            System.Timers.Timer pollTimer = new System.Timers.Timer();
            pollTimer.Interval = 4000; // in miliseconds
            pollTimer.Elapsed += pollWiFi;
            pollTimer.Start();

            //Displays a three dot vertical button widget which displays a list of actions (in this case none at the moment)
            Android.Support.V7.Widget.Toolbar toolbar = FindViewById<Android.Support.V7.Widget.Toolbar>(Resource.Id.toolbar);
            SetSupportActionBar(toolbar);

            //Creates the three horizontal bar on top left which brings up Navigation View on toggle
            DrawerLayout drawer = FindViewById<DrawerLayout>(Resource.Id.drawer_layout);

            //Opens and closes navigation tab on click
            ActionBarDrawerToggle toggle = new ActionBarDrawerToggle(this, drawer, toolbar, Resource.String.navigation_drawer_open, Resource.String.navigation_drawer_close);
            drawer.AddDrawerListener(toggle);
            toggle.SyncState();

            //Allows for the buttons in the navigation tab to work on button
            NavigationView navigationView = FindViewById<NavigationView>(Resource.Id.nav_view);
            navigationView.SetNavigationItemSelectedListener(this);
        }

        //Function to poll WiFi RSSID information, and display it
        private void pollWiFi(object sender, ElapsedEventArgs e)
        {
            if (!polling)
            {
                if (!PollSwitch)
                {
                    findPosition(sender, e);
                }
                else
                {
                    FindLocalization(sender, e);
                }
            }
        }
        static int partition(IList<ScanResult> arr, int low, int high)
        {
            ScanResult pivot = arr[high];

            // index of smaller element 
            int i = (low - 1);
            for (int j = low; j < high; j++)
            {
                // If current element is smaller  
                // than or equal to pivot 
                if (arr[j].Level >= pivot.Level)
                {
                    i++;

                    // swap arr[i] and arr[j] 
                    ScanResult temp = arr[i];
                    arr[i] = arr[j];
                    arr[j] = temp;
                }
            }

            // swap arr[i+1] and arr[high] (or pivot) 
            ScanResult temp1 = arr[i + 1];
            arr[i + 1] = arr[high];
            arr[high] = temp1;

            return i + 1;
        }
        static void quickSort(IList<ScanResult> arr, int low, int high)
        {
            if (low < high)
            {

                /* pi is partitioning index, arr[pi] is  
                now at right place */
                int pi = partition(arr, low, high);

                // Recursively sort elements before 
                // partition and after partition 
                quickSort(arr, low, pi - 1);
                quickSort(arr, pi + 1, high);
            }
        }

        //Function to find current GPS coordinates for footprinting use
        async Task findPosition(object sender, EventArgs e)
        {
            polling = true;
            //Connect to the navigation_text on content_navigation
            if (displayNavData)
            {
                wifiText = (TextView)FindViewById(Resource.Id.navigation_text);
            }

            var locator = CrossGeolocator.Current;
            //How accurate to the meter. IE 500 would result in coordinates being 500 meters within your vicinity
            locator.DesiredAccuracy = 1;
            //Get Position
            var position = await locator.GetPositionAsync(TimeSpan.FromMilliseconds(500));
            //Edit the Text to include Lat/Long
            WifiInfo wifiInfo = wifiManager.ConnectionInfo;
            if (displayNavData && wifiText != null)
            {
                RunOnUiThread(() =>
                {
                    wifiText.Text = wifiInfo.SSID + "";
                    wifiText.Append("\nLat: " + position.Latitude + "\nLong: " + position.Longitude);
                });
            }

            //insert new fingerprint to database
            var request = new RestRequest(Method.POST);
            request.Resource = "fingerprint-test";
            request.AddHeader("cache-control", "no-cache");
            request.AddHeader("x-apikey", "7118ad356550e03d458063ea0001e3009b7fc");
            request.AddHeader("content-type", "application/json");

            Fingerprint fingerprint = new Fingerprint();
            fingerprint.fp_latitude = position.Latitude;
            fingerprint.fp_longitude = position.Longitude;
            string insert_json = JsonConvert.SerializeObject(fingerprint);
            request.AddParameter("application/json", insert_json, ParameterType.RequestBody);

            IRestResponse response = client.Execute(request);
            Fingerprint fp_response = new Fingerprint();
            if (response.IsSuccessful)
            {
                string json_text = response.Content;
                fp_response = JsonConvert.DeserializeObject<Fingerprint>(json_text);
                Console.WriteLine("NEW FINGERPRINT " + fp_response.fp_latitude + " " + fp_response.fp_longitude);
            }
            else
            {
                Console.WriteLine(response.StatusDescription);
                Console.WriteLine(response.ErrorMessage);
                Console.WriteLine(response.ErrorException);
            }

            //Call the AP scan function
            wifiManager.StartScan();
            scanResults = wifiManager.ScanResults;
            quickSort(scanResults, 0, scanResults.Count - 1);
            for (int i = 0; i < 10; i++)
            {
                ScanResult AccessPoint = scanResults[i];
                if (displayNavData && wifiText != null)
                {
                    RunOnUiThread(() => { wifiText.Append("\n AP SSID: " + AccessPoint.Bssid + "\n RSSI: " + AccessPoint.Level); });
                }
                //insert ap-rssi-pair to fingerprint's child collection
                var request2 = new RestRequest(Method.POST);
                request2.Resource = "fingerprint-test/" + fp_response._id + "/ap_rssi";
                request2.AddHeader("cache-control", "no-cache");
                request2.AddHeader("x-apikey", "7118ad356550e03d458063ea0001e3009b7fc");
                request2.AddHeader("content-type", "application/json");

                ApRssiPair apRssiPair = new ApRssiPair();
                apRssiPair.rssi = AccessPoint.Level;
                apRssiPair.ap_mac_addr = AccessPoint.Bssid;
                string insert_json2 = JsonConvert.SerializeObject(apRssiPair);
                request2.AddParameter("application/json", insert_json2, ParameterType.RequestBody);

                IRestResponse response2 = client.Execute(request2);
                ApRssiPair arp_response = new ApRssiPair();
                if (response2.IsSuccessful)
                {
                    string json_text = response2.Content;
                    arp_response = JsonConvert.DeserializeObject<ApRssiPair>(json_text);
                }
                else
                {
                    Console.WriteLine(response2.StatusDescription);
                    Console.WriteLine(response2.ErrorMessage);
                    Console.WriteLine(response2.ErrorException);
                }
                
            }
            polling = false;
        }

        //What happens when someone presses the back button?
        public override void OnBackPressed()
        {
            DrawerLayout drawer = FindViewById<DrawerLayout>(Resource.Id.drawer_layout);
            //If the navigation side tab is open, close it
            if (drawer.IsDrawerOpen(GravityCompat.Start))
            {
                drawer.CloseDrawer(GravityCompat.Start);
            }
            else //Do nothing
            {
                base.OnBackPressed();
            }
        }

      
        async Task FindLocalization(object sender, EventArgs e)
        {
            polling = true;
            wifiManager.StartScan();
            scanResults = wifiManager.ScanResults;
            quickSort(scanResults, 0, scanResults.Count - 1);
            //A list of Parent ID's
            List<Parents> arpList = new List<Parents>();
            //Find all the parents(Coordinates Footprints) associated with the AccessPoints we just scanned
            for (int i = 0; i < 10; i++)
            {
                ScanResult AccessPoint = scanResults[i];
                //RunOnUiThread(() => { wifiText.Append("\n AP SSID: " + AccessPoint.Bssid + "\n RSSI: " + AccessPoint.Level); });
                var request = new RestRequest("ap-rssi-pair-test?q={\"ap_mac_addr\":\"" + AccessPoint.Bssid + "\"}", Method.GET);
                request.AddHeader("cache-control", "no-cache");
                request.RequestFormat = DataFormat.Json;
                request.AddHeader("x-apikey", "7118ad356550e03d458063ea0001e3009b7fc");
                request.AddHeader("content-type", "application/json");
                request.AddParameter("metafields", true);
                IRestResponse response = client.Execute(request);
                Console.WriteLine(response.Content);
                if (response.IsSuccessful)
                {
                    string json_text = response.Content;
                    //JObject JResponse = (JObject)JsonConvert.DeserializeObject(json_text);
                    if (arpList.Count == 0) arpList = (JsonConvert.DeserializeObject<List<Parents>>(json_text));
                    else
                    {
                        List<Parents> arpList2 = (JsonConvert.DeserializeObject<List<Parents>>(json_text));
                        arpList = arpList.Intersect(arpList2, new ParentsComp()).ToList();
                    }
                }
                else
                {
                    Console.WriteLine(response.StatusDescription);
                    Console.WriteLine(response.ErrorMessage);
                    Console.WriteLine(response.ErrorException);
                }
            }
            //Scan through the parent list and get their information from the footprint database
            List<Fingerprint> fpList = new List<Fingerprint>();
            for (int j = 0; j < arpList.Count; j++)
            {
                var request = new RestRequest("fingerprint-test?q={\"_id\":\"" + arpList[j]._parent_id + "\"}&fetchchildren=true", Method.GET);
                request.AddHeader("cache-control", "no-cache");
                request.RequestFormat = DataFormat.Json;
                request.AddHeader("x-apikey", "7118ad356550e03d458063ea0001e3009b7fc");
                request.AddHeader("content-type", "application/json");
                IRestResponse response = client.Execute(request);
                Console.WriteLine(response.Content);
                if (response.IsSuccessful)
                {
                    string json_text = response.Content;
                    //JObject JResponse = (JObject)JsonConvert.DeserializeObject(json_text);
                    if (fpList.Count == 0) fpList = (JsonConvert.DeserializeObject<List<Fingerprint>>(json_text));
                    else
                    {
                        List<Fingerprint> fpList2 = (JsonConvert.DeserializeObject<List<Fingerprint>>(json_text));
                        fpList.AddRange(fpList2);
                    }
                }
                else
                {
                    Console.WriteLine(response.StatusDescription);
                    Console.WriteLine(response.ErrorMessage);
                    Console.WriteLine(response.ErrorException);
                }
            }
            //The Value with the least difference overall
            int lowest = 99999999;
            //The spot we will mark to remember as the lowest and most likely location
            int place = 0;
            //Now we compare the current AP scan list with each AP seen in the fpList
            for (int k = 0; k < fpList.Count; k++)
            {
                int sum = 0;
                for (int l = 0; l < fpList[k].ap_rssi.Count; l++)
                {
                    
                    for(int m = 0; m < 10; m++)
                    {
                        if(scanResults[m].Bssid == fpList[k].ap_rssi[l].ap_mac_addr)
                        {
                            sum += Math.Abs(scanResults[m].Level - fpList[k].ap_rssi[l].rssi); 
                        }
                    }
                }
                //Check to see if we found a more likely location
                if (sum < lowest)
                {
                    place = k;
                    lowest = sum;
                }
            }
            double lat = fpList[place].fp_latitude;
            double lon = fpList[place].fp_longitude;
            if (displayNavData)
            {
                wifiText = (TextView)FindViewById(Resource.Id.navigation_text);
            }
            if (displayNavData && wifiText != null)
            {
                RunOnUiThread(() => { wifiText.Text = "\nLat: " + lat + "\nLong: " + lon; });
            }
            polling = false;
        }
        //Unsure of direct purpose
        public override bool OnOptionsItemSelected(IMenuItem item)
        {
            int id = item.ItemId;
            if (id == Resource.Id.action_settings)
            {
                return true;
            }

            return base.OnOptionsItemSelected(item);
        }

        //Toggle polling of wifi on button click in settings
        [Java.Interop.Export("toggle_polling")]
        public void toggle_polling(View b)
        {
            PollSwitch = !PollSwitch;

            Button button = (Button)b;

            if (PollSwitch)
            {
                button.Text = "Localize is Active.";
            }
            else
            {
                button.Text = "Footprint is Active.";
            }
        }

        //This is the side bar's function calls
        public bool OnNavigationItemSelected(IMenuItem item)
        {
            int id = item.ItemId;

            //Common code for all of the conditions below
            Android.Widget.RelativeLayout mainLayout = (Android.Widget.RelativeLayout)FindViewById(Resource.Id.all_container);
            LayoutInflater inflater = (LayoutInflater)GetSystemService(Context.LayoutInflaterService);
            View layout = null;

            //Creates logo and id name by referencing the file activity_main_drawer.xml which lists each one
            if (id == Resource.Id.nav_map)
            {
                displayNavData = false;
                layout = inflater.Inflate(Resource.Layout.content_map, null);
                
            }
            else if (id == Resource.Id.nav_loc)
            {
                displayNavData = true;
                layout = inflater.Inflate(Resource.Layout.content_navigation, null);
            }
            else if (id == Resource.Id.nav_set)
            {
                displayNavData = false;
                layout = inflater.Inflate(Resource.Layout.content_settings, null);
            }

            mainLayout.RemoveAllViews();
            mainLayout.AddView(layout);

            DrawerLayout drawer = FindViewById<DrawerLayout>(Resource.Id.drawer_layout);
            drawer.CloseDrawer(GravityCompat.Start);
            return true;
        }
    }
}