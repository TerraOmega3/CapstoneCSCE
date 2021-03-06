﻿using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using System.Net.Http;
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
using Android.Views.InputMethods;
using Plugin.Compass;
namespace Capstone
{
    [Activity(Label = "EyeFi", Theme = "@style/AppTheme.NoActionBar", MainLauncher = true, ScreenOrientation = ScreenOrientation.Portrait)]
    public class MainActivity : AppCompatActivity, NavigationView.IOnNavigationItemSelectedListener, IOnMapReadyCallback, GoogleMap.IOnMapClickListener
    {
        //Change this to your own network ID name or in the schools case "tamulink-wpa"
        const string networkSSID = "\"" + "tamulink-wpa" + "\"";

        TextView wifiText;
        TextView loadingText;
        WifiManager wifiManager;
        public int compare;
        public IList<ScanResult> scanResults;
        RestClient client;

        bool polling = false;
        //Start as true for wifi localization, false for gps
        bool PollSwitch = true;
        bool displayNavData = false;

        //Map vars
        private MapFragment mapFragment;
        Marker marker;
        Marker destination;
        GoogleMap map;
        List<LatLng> lines;
        //Direction vars
        HttpClient webclient = new HttpClient();
        Polyline polyline = null;

        //Compass vars
        double cHeading;

        public void OnMapReady(GoogleMap m)
        {
            map = m;
            map.UiSettings.CompassEnabled = true;
            map.SetOnMapClickListener(this);
            if (marker != null)
            {
                //Update map camera on first run only
                CameraPosition.Builder builder = CameraPosition.InvokeBuilder();
                builder.Target(new LatLng(marker.Position.Latitude, marker.Position.Longitude));
                builder.Zoom(18);
                builder.Bearing(155);
                builder.Tilt(65);

                CameraPosition cameraPosition = builder.Build();
                CameraUpdate cameraUpdate = CameraUpdateFactory.NewCameraPosition(cameraPosition);
                map.MoveCamera(cameraUpdate);
                marker = map.AddMarker(new MarkerOptions().SetPosition(new LatLng(marker.Position.Latitude, marker.Position.Longitude)).SetTitle("currentLoc"));
                if (destination != null)
                {
                    destination = map.AddMarker(new MarkerOptions().SetPosition(new LatLng(destination.Position.Latitude, destination.Position.Longitude)).SetTitle("dest"));
                    createRoute();
                }
            }

        }

        public void OnMapClick(LatLng point)
        {
            if (destination != null)
            {
                destination.Remove();
            }

            destination = map.AddMarker(new MarkerOptions().SetPosition(point).SetTitle("dest"));
            if (marker != null) {
                createRoute();
            }
        }

        public void CreateMap()
        {
            if (ContextCompat.CheckSelfPermission(this, Manifest.Permission.AccessCoarseLocation) == (int)Permission.Granted && ContextCompat.CheckSelfPermission(this, Manifest.Permission.AccessFineLocation) == (int)Permission.Granted)
            {
                // We have permission, go ahead and use the location.
                Console.WriteLine("PERMISSIONS GRANTED");
                mapFragment = MapFragment.NewInstance();
                mapFragment.GetMapAsync(this);
                this.FragmentManager.BeginTransaction().Add(Resource.Id.map, mapFragment, "map_fragment").Commit();
            }
            else
            {
                // Permission is not granted. If necessary display rationale & request.

            }
            //Click the search button to search for a destination.
            Button locationSearch = (Button)FindViewById(Resource.Id.search_button);
            locationSearch.Click += onMapSearch;

            //If you decide to press enter instead of the button then just click the button
            EditText DestSearch = (EditText)FindViewById(Resource.Id.Content_search);
            DestSearch.EditorAction += (sender, e) => {
                if (e.ActionId == ImeAction.Search)
                {
                    locationSearch.PerformClick();
                }
                else
                {
                    e.Handled = false;
                }
            };
           

            //Update marker to current loc
            
        }
        bool checkIntersect()
        {
            Console.WriteLine("CHECKING!!!!!!!!!");
            //Creat bearing vector (Bx, By) based on compass heading
            double rad = ((cHeading)) * (Math.PI / 180);
            double Bx = Math.Cos(rad);
            double By = Math.Sin(rad);
            //Create pin vector (pin point - current position)
            LatLng mPos = marker.Position;
            double Cx = mPos.Longitude;
            double Cy = mPos.Latitude;
            double Px = lines[1].Longitude;
            double Py = lines[1].Latitude;
            //normalize the vectors
            double mag = Math.Sqrt(Math.Pow(Bx, 2) + Math.Pow(By, 2));
            Bx /= mag;
            By /= mag;
            mag = Math.Sqrt(Math.Pow(Cx, 2) + Math.Pow(Cy, 2));
            Cx /= mag;
            Cy /= mag;
            mag = Math.Sqrt(Math.Pow(Px, 2) + Math.Pow(Py, 2));
            Px /= mag;
            Py /= mag;
            //Normalized pin vector
            double X2 = Px - Cx;
            double Y2 = Py - Cy;
            //Calculate intersection point of pin vector and bearing vector
            double slopeA = (Bx == 0) ? 0.0 : By / Bx;
            double slopeB = (X2 == 0) ? 0.0 : Y2 / X2;
            double Xi, Yi;
            if (slopeA == slopeB)
            {
                //Parallel
                return false;
            }
            else if (slopeA == 0 && slopeB != 0)
            {
                Xi = Cx;
                Yi = Bx * slopeB + Cy;
            }
            else if (slopeB == 0 && slopeA != 0)
            {
                Xi = Bx;
                Yi = Bx * slopeA + Cy;
            }
            else
            {
                Xi = (slopeA * Cx - slopeB * Bx + By - Cy) / (slopeA - slopeB);
                Yi = slopeB * (Xi - Bx) + By;
            }
            bool right = false;
            if (Px > Cx)
                right = true;
            if (right)
            {
                if (Xi > Cx)
                    return true;
            }
            else
            {
                if (Xi < Cx)
                    return true;
            }
            return false;
        }
        private Vibrator myVib;
        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            //Creates main page layout by referencing activity_main.axml
            SetContentView(Resource.Layout.activity_main);

            //Create map in initial tab
            CreateMap();
            myVib = (Vibrator)this.GetSystemService(VibratorService);
            //Create the compass
            CrossCompass.Current.CompassChanged += (s, e) =>
            {
                cHeading = e.Heading;
                //Console.WriteLine("*** Compass Heading = {0}", e.Heading);
                //if heading is pointing towards end of current line give feedback
                if (marker != null && lines != null && lines.Count > 0 && checkIntersect())
                {
                    myVib.Vibrate(100);
                    Console.WriteLine("INTERSECTION!!!!!!!!!!!!!!!!!!!!!!!!!");
                   // Console.WriteLine("Heading: " + cHeading);
                }
            };
            Task.Run(() => { CrossCompass.Current.Start(); });


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

            //Click the search button to search for a destination.
            Button locationSearch = (Button)FindViewById(Resource.Id.search_button);
            locationSearch.Click += onMapSearch;

            //If you decide to press enter instead of the button then just click the button
            EditText DestSearch = (EditText)FindViewById(Resource.Id.Content_search);
            DestSearch.EditorAction += (sender, e) => {
                if (e.ActionId == ImeAction.Search)
                {
                    locationSearch.PerformClick();
                }
                else
                {
                    e.Handled = false;
                }
            };

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
        private void DismissKeyboard()
        {
            var view = CurrentFocus;
            if (view != null)
            {
                var imm = (InputMethodManager)GetSystemService(InputMethodService);
                imm.HideSoftInputFromWindow(view.WindowToken, 0);
            }
        }
        public void onMapSearch(object sender, EventArgs e)
        {
            DismissKeyboard();
            EditText locationSearch = (EditText)FindViewById(Resource.Id.Content_search);
            String location = locationSearch.Text;
            IList<Address> addressList = null;

            if (location != null || !location.Equals(""))
            {
                if (destination != null)
                {
                    destination.Remove();
                }

                Geocoder geocoder = new Geocoder(this);
                addressList = geocoder.GetFromLocationName(location, 1);
                if (addressList.Count == 0)
                {
                    return;
                }

                Address address = addressList[0];
                LatLng latLng = new LatLng(address.Latitude, address.Longitude);
                if (destination != null)
                {
                    destination.Remove();
                }
                destination = map.AddMarker(new MarkerOptions().SetPosition(latLng).SetTitle("dest"));
                map.AnimateCamera(CameraUpdateFactory.NewLatLng(latLng));
                if (marker != null) { 
                    createRoute();
                }
            }
        }
        
        //Function to poll WiFi RSSID information, and display it
        private void pollWiFi(object sender, ElapsedEventArgs e)
        {
            if (!polling)
            {
                if (PollSwitch)
                {
                    FindLocalization(sender, e);
                }
                else
                {
                    findPosition(sender, e);
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

    

            RunOnUiThread(() =>
            {
                //Put marker down on current loc, remove previous marker
                if (marker != null)
                {
                    marker.Remove();
                }
                else
                {
                    //Update map camera on first run only
                    CameraPosition.Builder builder = CameraPosition.InvokeBuilder();
                    builder.Target(new LatLng(position.Latitude, position.Longitude));
                    builder.Zoom(18);
                    builder.Bearing(155);
                    builder.Tilt(65);

                    CameraPosition cameraPosition = builder.Build();
                    CameraUpdate cameraUpdate = CameraUpdateFactory.NewCameraPosition(cameraPosition);
                    map.MoveCamera(cameraUpdate);
                }

                //Update marker to current loc
                marker = map.AddMarker(new MarkerOptions().SetPosition(new LatLng(position.Latitude, position.Longitude)).SetTitle("currentLoc"));
            });

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
            if (displayNavData)
            {
                loadingText = (TextView)FindViewById(Resource.Id.loading_text);
            }
            if (loadingText != null)
            {
                RunOnUiThread(() => { loadingText.Text = "|......|"; });
            }
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
                if (i > 4)
                {
                    if (displayNavData && loadingText == null)
                    {
                        loadingText = (TextView)FindViewById(Resource.Id.loading_text);
                    }
                    if (loadingText != null)
                    {
                        RunOnUiThread(() => { loadingText.Text = "||.....|"; });
                    }
                }
                if (response.IsSuccessful)
                {
                    string json_text = response.Content;
                    //JObject JResponse = (JObject)JsonConvert.DeserializeObject(json_text);
                    if (arpList.Count == 0) arpList = (JsonConvert.DeserializeObject<List<Parents>>(json_text));
                    else
                    {
                        List<Parents> arpList2 = (JsonConvert.DeserializeObject<List<Parents>>(json_text));
                        //arpList = arpList.Intersect(arpList2, new ParentsComp()).ToList();
                        var tempList = arpList.Where(x => arpList2.Any(y => y._parent_id == x._parent_id)).ToList();
                        if (tempList.Count > 12)
                        {
                            arpList = tempList;
                        }
                        else
                        {
                            goto parentSearch;
                        }
                    }
                }
                else
                {
                    Console.WriteLine(response.StatusDescription);
                    Console.WriteLine(response.ErrorMessage);
                    Console.WriteLine(response.ErrorException);
                }
                
            }
            parentSearch:
            if (displayNavData && loadingText == null)
            {
                loadingText = (TextView)FindViewById(Resource.Id.loading_text);
            }
            if (loadingText != null)
            {
                RunOnUiThread(() => { loadingText.Text = "|||....|"; });
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
                if (j > arpList.Count / 2)
                {
                    if (displayNavData && loadingText == null)
                    {
                        loadingText = (TextView)FindViewById(Resource.Id.loading_text);
                    }
                    if (loadingText != null)
                    {
                        RunOnUiThread(() => { loadingText.Text = "||||...|"; });
                    }
                }
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
            if (displayNavData && loadingText == null)
            {
                loadingText = (TextView)FindViewById(Resource.Id.loading_text);
            }
            if (loadingText != null)
            {
                RunOnUiThread(() => { loadingText.Text = "|||||..|"; });
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
            if (displayNavData && loadingText == null)
            {
                loadingText = (TextView)FindViewById(Resource.Id.loading_text);
            }
            if (loadingText != null)
            {
                RunOnUiThread(() => { loadingText.Text = "||||||.|"; });
            }
            double lat = fpList[place].fp_latitude;
            double lon = fpList[place].fp_longitude;
            RunOnUiThread(() =>
            {
                //Put marker down on found position, remove previous marker
                if (marker != null)
                {
                    marker.Remove();
                }
                else
                {
                    //Update map camera on first run only
                    CameraPosition.Builder builder = CameraPosition.InvokeBuilder();
                    builder.Target(new LatLng(lat, lon));
                    builder.Zoom(18);
                    builder.Bearing(155);
                    builder.Tilt(65);

                    CameraPosition cameraPosition = builder.Build();
                    CameraUpdate cameraUpdate = CameraUpdateFactory.NewCameraPosition(cameraPosition);
                    map.MoveCamera(cameraUpdate);
                }

                //Update marker to current loc
                marker = map.AddMarker(new MarkerOptions().SetPosition(new LatLng(lat, lon)).SetTitle("currentLoc"));
            });

            if (displayNavData)
            {
                wifiText = (TextView)FindViewById(Resource.Id.navigation_text);
            }
            if (displayNavData && wifiText != null)
            {

                RunOnUiThread(() => {
                    wifiText.Text = "\nLat: " + lat + "\nLong: " + lon; });
            }
            if (displayNavData && loadingText == null)
            {
                loadingText = (TextView)FindViewById(Resource.Id.loading_text);
            }
            if (displayNavData && loadingText != null)
            {

                RunOnUiThread(() => {
                    loadingText.Text = "||||||||";
                });
            }
            
            if (destination != null)
            {
                RunOnUiThread(() =>
                {
                    createRoute();
                });
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
                mainLayout.RemoveAllViews();
                mainLayout.AddView(layout);
                CreateMap();
            }
            else if (id == Resource.Id.nav_loc)
            {
                displayNavData = true;
                layout = inflater.Inflate(Resource.Layout.content_navigation, null);
                mainLayout.RemoveAllViews();
                mainLayout.AddView(layout);
            }
            else if (id == Resource.Id.nav_set)
            {
                displayNavData = false;
                layout = inflater.Inflate(Resource.Layout.content_settings, null);
                mainLayout.RemoveAllViews();
                mainLayout.AddView(layout);
                Button toggle = (Button)FindViewById(Resource.Id.toggle_poll);
                if (PollSwitch)
                {
                    toggle.Text = "Localize is Active.";
                }
                else
                {
                    toggle.Text = "Footprint is Active.";
                }
            }

            

            DrawerLayout drawer = FindViewById<DrawerLayout>(Resource.Id.drawer_layout);
            drawer.CloseDrawer(GravityCompat.Start);
            return true;
        }

        //Creates the route from marker to dest and displays it on the map
        async Task createRoute()
        {
            LatLng start = marker.Position;
            LatLng end = destination.Position;

            string rawData = await GetRawRequest(start, end);
            //dynamic object since we don't know what the object names are yet
            dynamic routeObj = JsonConvert.DeserializeObject(rawData);
            //There is a lot more information in this, but legs is a good general place to start.
            //Details like addresses, names, and turn by turn diretions are available
            var legs = routeObj.routes[0].legs;

            lines = new List<LatLng>();

            //Go through the "legs" of the trip, should only be 1 for us always...but just in case.
            foreach(var leg in legs)
            {
                //Get the steps of the journey (aka each direction change has a latlng value)
                var steps = leg.steps;
                foreach (var step in steps)
                {
                    double slat = step.start_location.lat;
                    double slng = step.start_location.lng;
                    double elat = step.end_location.lat;
                    double elng = step.end_location.lng;

                    LatLng pt1 = new LatLng(slat, slng);
                    LatLng pt2 = new LatLng(elat, elng);

                    //Add the points to the polyline list
                    lines.Add(pt1);
                    lines.Add(pt2);
                }
            }

            //Set how the polyline will visually look, there are more options
            var polylineOptions = new PolylineOptions()
                            .InvokeColor(Android.Graphics.Color.Blue);

            //Fill polyline options with the points
            foreach (LatLng line in lines)
            {
                polylineOptions.Add(line);
            }

            //Run on UI thread so it shows up on map
            RunOnUiThread(() => {
                //Remove old polyline
                if (polyline != null)
                {
                    polyline.Remove();
                }
                //Add new polyline
                polyline = map.AddPolyline(polylineOptions);
            });
        }
        
        //Gets the raw json response from google directions api
        async Task<string> GetRawRequest(LatLng start, LatLng end)
        {
            //Yeah, it's gonna be deactivated asap
            string apikey = "AIzaSyBp5rR-4ubhga0gTeUKZHS9ToTMltGHObM";
            string apiUrl = "https://maps.googleapis.com/maps/api/directions/json?origin=" + start.Latitude + "," + start.Longitude + "&destination=" + end.Latitude + "," + end.Longitude + "&mode=walking&key=" + apikey;

            using (var response = await webclient.GetAsync(apiUrl))
            {
                if (response.IsSuccessStatusCode)
                {
                    var info = await response.Content.ReadAsStringAsync();
                    return info;
                }
            }

            return null;
        }
    }
}