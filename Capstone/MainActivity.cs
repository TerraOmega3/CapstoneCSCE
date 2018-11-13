﻿using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using Android.App;
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
using Plugin.Geolocator;
using RestSharp;
using Newtonsoft.Json;

namespace Capstone
{
    [Activity(Label = "EyeFi", Theme = "@style/AppTheme.NoActionBar", MainLauncher = true)]
    public class MainActivity : AppCompatActivity, NavigationView.IOnNavigationItemSelectedListener
    {
        //Change this to your own network ID name or in the schools case "tamulink-wpa"
        const string networkSSID = "\"" + "tamulink-wpa" + "\"";
       
        TextView wifiText;
        Button wifiButton;
        WifiManager wifiManager;
        public IList<ScanResult> scanResults;
        RestClient client;

        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            //Creates main page layout by referencing activity_main.axml
            SetContentView(Resource.Layout.activity_main);

            //Set up database connection
            var baseUrl = "https://testdb-05fa.restdb.io/rest/";
            client = new RestClient(baseUrl);

            var locator = CrossGeolocator.Current;
            locator.DesiredAccuracy = 10;
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
            
            //Gather WifiInfo data
            WifiInfo wifiInfo = wifiManager.ConnectionInfo;

            //Connect to the main_text on Content_main layer
            wifiText = (TextView)FindViewById(Resource.Id.main_text);

            //Scan all nearby AccessPoints
            wifiManager.StartScan();
            scanResults = wifiManager.ScanResults;
            
            //Display to main_content the first 3 Accesspoints
            wifiText.Text = wifiInfo.SSID + "";
            for (int i = 0; i < 3; i++)
            {
                ScanResult AccessPoint = scanResults[i];
                wifiText.Append("\n AP SSID: " + AccessPoint.Bssid + "\n RSSI: " + AccessPoint.Level);
            }
            //Chain text to include wifi info items (Note: rssi, BSSID, and linkspeed can all be called after getting wifiInfo)
            // BSSID: AP address, LinkSpeed is the internet speed in Mbps, Rssi is in decibals so from -80 to -10 where -10 is a better connection
            
            //Create and connect to button from main_content
            wifiButton = FindViewById<Button>(Resource.Id.button1);

            //Calls the geolocator function on press (The function also recalls the AP scan)
            wifiButton.Click += async (object sender, EventArgs args) => { await findPosition(sender, args); };

            //Calls the polling function every 3 seconds
            System.Timers.Timer pollTimer = new System.Timers.Timer();
            pollTimer.Interval = 3000; // in miliseconds
            pollTimer.Elapsed += pollWiFi;
            pollTimer.Start();

            //Displays a three dot vertical button widget which displays a list of actions (in this case none at the moment)
            Android.Support.V7.Widget.Toolbar toolbar = FindViewById<Android.Support.V7.Widget.Toolbar>(Resource.Id.toolbar);
            SetSupportActionBar(toolbar);

            //Displays the mail button
            FloatingActionButton fab = FindViewById<FloatingActionButton>(Resource.Id.fab);
            fab.Click += FabOnClick;

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
            RunOnUiThread(async() => { await findPosition(sender, e); });
        }

        //Function to find current GPS coordinates for footprinting use
        async Task findPosition(object sender, EventArgs e)
        {
            var locator = CrossGeolocator.Current;
            //How accurate to the meter. IE 500 would result in coordinates being 500 meters within your vicinity
            locator.DesiredAccuracy = 1;
            //Get Position
            var position = await locator.GetPositionAsync(TimeSpan.FromMilliseconds(500));
            //Edit the Text to include Lat/Long
            WifiInfo wifiInfo = wifiManager.ConnectionInfo;
            wifiText.Text = wifiInfo.SSID + "";
            wifiText.Append("\nLat: " + position.Latitude + "\nLong: " + position.Longitude);

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
            for (int i = 0; i < 3; i++)
            //foreach(ScanResult AccessPoint in scanResults)
            {
                ScanResult AccessPoint = scanResults[i];
                wifiText.Append("\n AP SSID: " + AccessPoint.Bssid + "\n RSSI: " + AccessPoint.Level);
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

        //Create a three dot upper tab to go to settings on the upper right of the main page
        public override bool OnCreateOptionsMenu(IMenu menu)
        {
            MenuInflater.Inflate(Resource.Menu.menu_main, menu);
            return true;
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

        //This is the mail button's function call
        private void FabOnClick(object sender, EventArgs eventArgs)
        {
            View view = (View)sender;
            //Causes a small bar to appear on the bottom that calls either a function or in this case a "replace" statement
            Snackbar.Make(view, "Replace with your own action", Snackbar.LengthLong)
                .SetAction("Action", (Android.Views.View.IOnClickListener)null).Show();
        }
        //This is the side bar's function calls
        public bool OnNavigationItemSelected(IMenuItem item)
        {
            int id = item.ItemId;

            //Creates logo and id name by referencing the file activity_main_drawer.xml which lists each one
            if (id == Resource.Id.nav_map)
            {
                // Handles the map functions
                Android.Widget.RelativeLayout mainLayout = (Android.Widget.RelativeLayout)FindViewById(Resource.Id.all_container);
                Android.Widget.RelativeLayout parentLayout = (Android.Widget.RelativeLayout)FindViewById(Resource.Layout.app_bar_main);
                LayoutInflater inflater = (LayoutInflater)GetSystemService(Context.LayoutInflaterService);
                View layout = inflater.Inflate(Resource.Layout.content_main, parentLayout);
                mainLayout.RemoveAllViews();
                mainLayout.AddView(layout);
                
            }
            else if (id == Resource.Id.nav_loc)
            {
                Android.Widget.RelativeLayout mainLayout = (Android.Widget.RelativeLayout)FindViewById(Resource.Id.all_container);
                LayoutInflater inflater = (LayoutInflater)GetSystemService(Context.LayoutInflaterService);
                View layout = inflater.Inflate(Resource.Layout.content_local, null);
                mainLayout.RemoveAllViews();
                mainLayout.AddView(layout);
               
            }
            else if (id == Resource.Id.nav_set)
            {

            }
            else if (id == Resource.Id.nav_share)
            {

            }
            else if (id == Resource.Id.nav_send)
            {

            }

            DrawerLayout drawer = FindViewById<DrawerLayout>(Resource.Id.drawer_layout);
            drawer.CloseDrawer(GravityCompat.Start);
            return true;
        }
    }
}