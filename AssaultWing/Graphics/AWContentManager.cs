﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Media;
using AW2.Helpers;

namespace AW2.Graphics
{
    /// <summary>
    /// Content manager specifically for Assault Wing.
    /// </summary>
    public class AWContentManager : ContentManager
    {
        IDictionary<string, object> loadedContent = new Dictionary<string, object>();
        List<IDisposable> disposableContent = new List<IDisposable>();

        /// <summary>
        /// Creates a new content manager for Assault Wing.
        /// </summary>
        public AWContentManager(IServiceProvider serviceProvider)
            : base(serviceProvider)
        { }

        /// <summary>
        /// Loads an asset that has been processed by the Content Pipeline.
        /// </summary>
        /// Repeated calls to load the same asset will return the same object instance.
        public override T Load<T>(string assetName)
        {
            string assetFullName;
            if (assetName.Contains('\\'))
                assetFullName = assetName;
            else
            {
                string assetPath = null;
                var type = typeof(T);
                if (typeof(Texture).IsAssignableFrom(type)) assetPath = Paths.Textures;
                else if (type == typeof(Model)) assetPath = Paths.Models;
                else if (type == typeof(SpriteFont)) assetPath = Paths.Fonts;
                else if (type == typeof(Effect)) assetPath = Paths.Shaders;
                else if (type == typeof(Song) || type == typeof(SoundEffect)) assetPath = Paths.Music;
                else throw new ArgumentException("Cannot load content of unexpected type " + type.Name);
                assetFullName = Path.Combine(assetPath, assetName);
            }
            object item;
            loadedContent.TryGetValue(assetFullName, out item);
            if (item != null) return (T)item;
            item = ReadAsset<T>(assetFullName, disposableItem => disposableContent.Add(disposableItem));
            loadedContent.Add(assetFullName, item);
            return (T)item;
        }

        /// <summary>
        /// Returns the names of all assets. Only XNB files are considered
        /// as containing assets. Unprocessed XML files are ignored.
        /// </summary>
        public IEnumerable<string> GetAssetNames()
        {
            foreach (var filename in Directory.GetFiles(RootDirectory, "*.xnb", SearchOption.AllDirectories))
            {
                // We skip texture names that are part of 3D models.
                // Note: This works only if texture asset names never end in "_0".
                // The foolproof way to do the skipping is to skip the asset names
                // mentioned inside other asset files.
                var match = Regex.Match(filename, @"^.*[\\/]([^\\/]*?)(_0)?\.xnb$", RegexOptions.IgnoreCase);
                if (match.Groups[2].Length == 0) yield return match.Groups[1].Value;
            }
        }

        public override void Unload()
        {
            base.Unload();
            foreach (var value in disposableContent) value.Dispose();
            disposableContent.Clear();
            loadedContent.Clear();
        }
    }
}
