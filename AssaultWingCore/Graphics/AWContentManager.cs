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
        private IDictionary<string, object> _loadedContent = new Dictionary<string, object>();

        public Dictionary<string, Tuple<VertexPositionNormalTexture[], short[]>> ModelCache { get; private set; }

        public AWContentManager(IServiceProvider serviceProvider)
            : base(serviceProvider, ".\\")
        { }

        public bool Exists<T>(string assetName)
        {
            return File.Exists(GetAssetFullName<T>(assetName) + ".xnb");
        }

        /// <summary>
        /// Loads an asset that has been processed by the Content Pipeline.
        /// </summary>
        /// Repeated calls to load the same asset will return the same object instance.
        public override T Load<T>(string assetName)
        {
            if (assetName == null) throw new ArgumentNullException("assetName");
            var assetFullName = GetAssetFullName<T>(assetName);
            object item;
            if (_loadedContent.TryGetValue(assetFullName, out item)) return (T)item;
            item = ReadAsset<T>(assetFullName, null);
            _loadedContent.Add(assetFullName, item);
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
                // Skip texture names that are part of 3D models.
                if (!IsModelTextureFilename(filename)) yield return Path.GetFileNameWithoutExtension(filename);
            }
        }

        public void LoadAllGraphicsContent()
        {
            ModelCache = new Dictionary<string, Tuple<VertexPositionNormalTexture[], short[]>>();
            foreach (var filename in Directory.GetFiles(Paths.MODELS, "*.xnb"))
                if (!IsModelTextureFilename(filename))
                {
                    var model = Load<Model>(Path.GetFileNameWithoutExtension(filename));
                    CacheModelData(Path.GetFileNameWithoutExtension(filename), model);
                }
            foreach (var filename in Directory.GetFiles(Paths.TEXTURES, "*.xnb"))
                Load<Texture2D>(Path.GetFileNameWithoutExtension(filename));
            foreach (var filename in Directory.GetFiles(Paths.FONTS, "*.xnb"))
                Load<SpriteFont>(Path.GetFileNameWithoutExtension(filename));
        }
        
        private void CacheModelData(string modelName, Model model)
        {
            // HACK: Cache 3D model data. During arena startup, the game server uses the wall
            // model data to create collision areas for the wall. If the GraphicsDevice is in a bad
            // state at that moment, the model data is crippled and that results in crippled gameplay
            // where most walls are not collidable.
            VertexPositionNormalTexture[] vertexData;
            short[] indexData;
            Graphics3D.GetModelData(model, out vertexData, out indexData);
            ModelCache[modelName] = Tuple.Create(vertexData, indexData);
        }

        private bool IsModelTextureFilename(string filename)
        {
            // Note: This works only if texture asset names never end in "_0".
            // The foolproof way to do the skipping is to skip the asset names
            // mentioned inside other asset files.
            var match = Regex.Match(filename, @"^.*[\\/][^\\/]*?(_0)?\.xnb$", RegexOptions.IgnoreCase);
            return match.Groups[1].Length != 0;
        }

        private static string GetAssetFullName<T>(string assetName)
        {
            return assetName.Contains(@"\")
                ? assetName
                : Path.Combine(GetAssetPath(assetName, typeof(T)), assetName);
        }

        private static string GetAssetPath(string assetName, Type type)
        {
            if (typeof(Texture).IsAssignableFrom(type)) return Paths.TEXTURES;
            else if (type == typeof(Model)) return Paths.MODELS;
            else if (type == typeof(SpriteFont)) return Paths.FONTS;
            else if (type == typeof(Effect)) return Paths.SHADERS;
            else if (type == typeof(Song) || type == typeof(SoundEffect))
            {
                // Hack!
                // Everything which ends with 2 digits + extension is a sound
                var chars = assetName.ToCharArray(assetName.Length - 2, 2);
                if (Char.IsNumber(chars[0]) && Char.IsNumber(chars[1]))
                    return Paths.SOUNDS;
                return Paths.MUSIC;
            }
            else if (type == typeof(Video)) return Paths.VIDEO;
            throw new ArgumentException("Cannot load content of unexpected type " + type.Name);
        }
    }
}
