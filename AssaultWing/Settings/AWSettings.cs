﻿using System;
using System.Deployment.Application;
using System.IO;
using AW2.Helpers;
using AW2.Helpers.Serialization;

namespace AW2.Settings
{
    public class AWSettings
    {
        private SoundSettings _sound;
        private NetSettings _net;
        private GraphicsSettings _graphics;

        public SoundSettings Sound { get { return _sound; } private set { _sound = value; } }
        public NetSettings Net { get { return _net; } private set { _net = value; } }
        public GraphicsSettings Graphics { get { return _graphics; } private set { _graphics = value; } }

        private static string SettingsDirectory
        {
            get
            {
                if (ApplicationDeployment.IsNetworkDeployed)
                    return ApplicationDeployment.CurrentDeployment.DataDirectory;
                else
                {
                    var assemblyFilename = System.Reflection.Assembly.GetExecutingAssembly().Location;
                    return Path.GetDirectoryName(assemblyFilename);
                }
            }
        }

        private static string SettingsFilename
        {
            get
            {
                return Path.Combine(SettingsDirectory, "AssaultWing.config");
            }
        }

        public static AWSettings FromFile()
        {
            if (File.Exists(SettingsFilename))
                try
                {
                    return (AWSettings)TypeLoader.LoadTemplate(SettingsFilename, typeof(AWSettings), null, true);
                }
                catch (MemberSerializationException e)
                {
                    Log.Write("Error while reading settings from " + SettingsFilename + ": " + e);
                }

            // Create a new settings file
            Log.Write("Creating a new settings file " + SettingsFilename);
            var settings = new AWSettings();
            settings.ToFile();
            return settings;
        }

        public AWSettings()
        {
            Sound = new SoundSettings();
            Net = new NetSettings();
            Graphics = new GraphicsSettings();
        }

        public void ToFile()
        {
            TypeLoader.SaveTemplate(this, SettingsFilename, typeof(AWSettings), null);
        }
    }
}
