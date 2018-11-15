
using Android.OS;
using System;
using Capstone;
using Android.App;
using Android.Gms.Maps;
using Android.Gms.Maps.Model;

namespace GooglePlayServicesMap
{
    public class MapActivity : Android.Support.V4.App.FragmentActivity, IOnMapReadyCallback
    {
        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            SetContentView(Resource.Layout.activity_main);

            //Create map
            var mapFrag = ((SupportMapFragment)SupportFragmentManager.FindFragmentById(Resource.Id.map));
            mapFrag.GetMapAsync(this);
        }
        public void OnMapReady(GoogleMap map)
        {
            map.AddMarker(new MarkerOptions().SetPosition(new LatLng(0, 0)).SetTitle("Marker"));
        }
    }
}