﻿using System;
using Windows.Devices.Geolocation;
using WPCordovaClassLib.Cordova;
using WPCordovaClassLib.Cordova.Commands;
using WPCordovaClassLib.Cordova.JSON;
using System.Diagnostics;

namespace Cordova.Extension.Commands
{
    public class BackgroundGeoLocation : BaseCommand, IBackgroundGeoLocation
    {
        private string ConfigureCallbackToken { get; set; }
        private BackgroundGeoLocationOptions BackgroundGeoLocationOptions { get; set; }

        public static IGeolocatorWrapper Geolocator { get; set; }

        /// <summary>
        /// RunningInBackground is a required property to run in background (also an active Geolocator instance is required)
        /// For more information read http://msdn.microsoft.com/library/windows/apps/jj662935(v=vs.105).aspx
        /// </summary> 
        public static bool RunningInBackground { get; set; }

        /// <summary>
        /// When start() is fired immediate after configure() in javascript, configure may not be finished yet, IsConfigured and IsConfiguring are used to keep track of this
        /// </summary>
        private bool IsConfigured { get; set; }
        private bool IsConfiguring { get; set; }

        private readonly IDebugNotifier _debugNotifier;

        public BackgroundGeoLocation()
        {
            IsConfiguring = false;
            IsConfigured = false;
            _debugNotifier = DebugNotifier.GetDebugNotifier();
        }

        public void configure(string args)
        {
            IsConfiguring = true;
            ConfigureCallbackToken = CurrentCommandCallbackId;
            RunningInBackground = false;

            BackgroundGeoLocationOptions = ParseBackgroundGeoLocationOptions(args);

            IsConfigured = BackgroundGeoLocationOptions.ParsingSucceeded;
            IsConfiguring = false;
        }

        private BackgroundGeoLocationOptions ParseBackgroundGeoLocationOptions(string configureArgs)
        {
            var parsingSucceeded = true;

            var options = JsonHelper.Deserialize<string[]>(configureArgs);

            double stationaryRadius, distanceFilter;
            UInt32 locationTimeout, desiredAccuracy;
            bool debug;

            if (!double.TryParse(options[3], out stationaryRadius))
            {
                DispatchCommandResult(new PluginResult(PluginResult.Status.JSON_EXCEPTION, string.Format("Invalid value for stationaryRadius:{0}", options[3])));
                parsingSucceeded = false;
            }
            if (!double.TryParse(options[4], out distanceFilter))
            {
                DispatchCommandResult(new PluginResult(PluginResult.Status.JSON_EXCEPTION, string.Format("Invalid value for distanceFilter:{0}", options[4])));
                parsingSucceeded = false;
            }
            if (!UInt32.TryParse(options[5], out locationTimeout))
            {
                DispatchCommandResult(new PluginResult(PluginResult.Status.JSON_EXCEPTION, string.Format("Invalid value for locationTimeout:{0}", options[5])));
                parsingSucceeded = false;
            }
            if (!UInt32.TryParse(options[6], out desiredAccuracy))
            {
                DispatchCommandResult(new PluginResult(PluginResult.Status.JSON_EXCEPTION, string.Format("Invalid value for desiredAccuracy:{0}", options[6])));
                parsingSucceeded = false;
            }
            if (!bool.TryParse(options[7], out debug))
            {
                DispatchCommandResult(new PluginResult(PluginResult.Status.JSON_EXCEPTION, string.Format("Invalid value for debug:{0}", options[7])));
                parsingSucceeded = false;
            }

            return new BackgroundGeoLocationOptions
            {
                Url = options[1],
                StationaryRadius = stationaryRadius,
                DistanceFilterInMeters = distanceFilter,
                LocationTimeoutInSeconds = locationTimeout,
                DesiredAccuracyInMeters = desiredAccuracy,
                Debug = debug,
                ParsingSucceeded = parsingSucceeded
            };
        }

        private readonly Object _startLock = new Object();

        public void start(string args)
        {
            lock (_startLock)
            {
                while (!IsConfigured && IsConfiguring)
                {
                    // Wait for configure() to complete...
                }

                if (!IsConfigured || !BackgroundGeoLocationOptions.ParsingSucceeded)
                {
                    DispatchCommandResult(new PluginResult(PluginResult.Status.INVALID_ACTION, "Cannot start: Run configure() with proper values!"));
                    stop(args);
                    return;
                }

                if (Geolocator != null && Geolocator.IsActive)
                {
                    DispatchCommandResult(new PluginResult(PluginResult.Status.INVALID_ACTION, "Already started!"));
                    return;
                }

                Geolocator = new GeolocatorWrapper(BackgroundGeoLocationOptions.DesiredAccuracyInMeters, BackgroundGeoLocationOptions.LocationTimeoutInSeconds * 1000, BackgroundGeoLocationOptions.DistanceFilterInMeters);
                Geolocator.PositionChanged += OnGeolocatorOnPositionChanged;
                Geolocator.Start();

                RunningInBackground = true;
            }
        }

        private void OnGeolocatorOnPositionChanged(GeolocatorWrapper sender, GeolocatorWrapperPositionChangedEventArgs eventArgs)
        {
            if (eventArgs.GeolocatorLocationStatus == PositionStatus.Disabled || eventArgs.GeolocatorLocationStatus == PositionStatus.NotAvailable)
            {
                DispatchMessage(PluginResult.Status.ERROR, string.Format("Cannot start: LocationStatus/PositionStatus: {0}! {1}", eventArgs.GeolocatorLocationStatus, IsConfigured), true, ConfigureCallbackToken);
                return;
            }

            if (BackgroundGeoLocationOptions.Debug)
            {
                Debug.WriteLine(eventArgs.DebugMessage);
                _debugNotifier.Notify(eventArgs.DebugMessage, new Tone(750, Frequency.High));
            }

            var callbackJsonResult = eventArgs.Position.Coordinate.ToJson();

            DispatchMessage(PluginResult.Status.OK, callbackJsonResult, true, ConfigureCallbackToken);
        }

        public void stop(string args)
        {
            RunningInBackground = false;
            Geolocator.Stop();
        }

        public void finish(string args)
        {
            DispatchMessage(PluginResult.Status.NO_RESULT, string.Empty, true, ConfigureCallbackToken);
        }

        public void onPaceChange(bool isMoving)
        {
            throw new NotImplementedException();
        }

        public void setConfig(string setConfigArgs)
        {
            if (Geolocator.IsActive)
            {
                Geolocator.PositionChanged -= OnGeolocatorOnPositionChanged;
                Geolocator.Stop();
            }

            if (string.IsNullOrWhiteSpace(setConfigArgs))
            {
                DispatchCommandResult(new PluginResult(PluginResult.Status.INVALID_ACTION, "Cannot set config because of an empty input"));
                return;
            }
            var parsingSucceeded = true;

            var options = JsonHelper.Deserialize<string[]>(setConfigArgs);

            double stationaryRadius, distanceFilter;
            UInt32 locationTimeout, desiredAccuracy;

            if (!double.TryParse(options[0], out stationaryRadius))
            {
                DispatchCommandResult(new PluginResult(PluginResult.Status.JSON_EXCEPTION, string.Format("Invalid value for stationaryRadius:{0}", options[2])));
                parsingSucceeded = false;
            }
            if (!double.TryParse(options[1], out distanceFilter))
            {
                DispatchCommandResult(new PluginResult(PluginResult.Status.JSON_EXCEPTION, string.Format("Invalid value for distanceFilter:{0}", options[3])));
                parsingSucceeded = false;
            }
            if (!UInt32.TryParse(options[2], out locationTimeout))
            {
                DispatchCommandResult(new PluginResult(PluginResult.Status.JSON_EXCEPTION, string.Format("Invalid value for locationTimeout:{0}", options[4])));
                parsingSucceeded = false;
            }
            if (!UInt32.TryParse(options[3], out desiredAccuracy))
            {
                DispatchCommandResult(new PluginResult(PluginResult.Status.JSON_EXCEPTION, string.Format("Invalid value for desiredAccuracy:{0}", options[5])));
                parsingSucceeded = false;
            }
            if (!parsingSucceeded) return;

            BackgroundGeoLocationOptions.StationaryRadius = stationaryRadius;
            BackgroundGeoLocationOptions.DistanceFilterInMeters = distanceFilter;
            BackgroundGeoLocationOptions.LocationTimeoutInSeconds = locationTimeout * 1000;
            BackgroundGeoLocationOptions.DesiredAccuracyInMeters = desiredAccuracy;

            Geolocator = new GeolocatorWrapper(desiredAccuracy, locationTimeout * 1000, distanceFilter);
            Geolocator.PositionChanged += OnGeolocatorOnPositionChanged;
            Geolocator.Start();

            DispatchCommandResult(new PluginResult(PluginResult.Status.OK));
        }

        public void getStationaryLocation(string args)
        {
            throw new NotImplementedException();
        }

        private void DispatchMessage(PluginResult.Status status, string message, bool keepCallback, string callBackId)
        {
            var pluginResult = new PluginResult(status, message) { KeepCallback = keepCallback };
            DispatchCommandResult(pluginResult, callBackId);
        }
    }
}
