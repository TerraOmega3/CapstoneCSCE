
using Android.OS;
using Android.Views;
using System;
using Android.Support.V4.View;
using Android.Support.V7.Widget;
using Android.Support.V4.App;
using Android.Support.Design.Widget;
using Capstone;
using Android.Support.V4.Widget;
using TB = Android.Support.V7.Widget.Toolbar;
using Android.Support.V7.App;

namespace Localized.Fragments
{
    public class LocalizationActivity : Fragment
    {
        public override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);

        }

        public static LocalizationActivity NewInstance()
        {
            var frag1 = new LocalizationActivity { Arguments = new Bundle() };
            return frag1;
        }


        public override View OnCreateView(LayoutInflater inflater, ViewGroup container, Bundle savedInstanceState)
        {
            // var ignored = base.OnCreateView(inflater, container, savedInstanceState);
            //return inflater.Inflate(Resource.Layout.activity_local , null);
            
            base.OnCreateView(inflater, container, savedInstanceState);
            
            View view = inflater.Inflate(Resource.Layout.content_local, container, false);
            
            //DrawerLayout drawerLocal = view.FindViewById<DrawerLayout>(Resource.Id.drawer_layout);
            //NavigationView navigationView = view.FindViewById<NavigationView>(Resource.Id.nav_view);
            //navigationView.SetNavigationItemSelectedListener(this);
            /*
            TB toolbarLocal = view.FindViewById<TB>(Resource.Id.toolbarLocal);
            AppCompatActivity activity = (AppCompatActivity)getActivity();
            activity.setSupportActionBar(toolbarLocal);
            activity.getSupportActionBar().setDisplayHomeAsUpEnabled(true);
            SetSupportActionBar(toolbarLocal);
            ActionBarDrawerToggle toggle = new ActionBarDrawerToggle(this, drawerLocal, toolbarLocal, Resource.String.navigation_drawer_open, Resource.String.navigation_drawer_close);
            drawerLocal.AddDrawerListener(toggle);
            toggle.SyncState();
            */
            return view;
        }
    }
}