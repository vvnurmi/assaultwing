﻿using System;
using System.Collections.Generic;
using System.Xml;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using AW2.Core;
using AW2.Game;
using AW2.Graphics;
using AW2.Helpers;
using AW2.Game.Gobs;
using System.Globalization;

namespace AW2.Sound
{
    /// <summary>
    /// Sound engine. Works as an extra abstraction for XACT audio engine.
    /// </summary>
    /// 

    public class SoundEngineXNA : SoundEngine
    {
        public class SoundCue
        {
            public SoundCue(SoundEffect[] effects, float volume, float distanceScale, bool loop)
            {
                _effects = effects;
                _volume = volume;
                _loop = loop;
                _distanceScale = distanceScale;
            }

            public SoundEffect GetEffect()
            {
                int index = RandomHelper.GetRandomInt(_effects.Length);
                return _effects[index];
            }

            public bool _loop;
            public float _volume;
            public float _distanceScale;
            public SoundEffect[] _effects;
        }

        Dictionary<string, SoundCue> _soundCues = new Dictionary<string, SoundCue>();
        
        List<SoundInstanceXNA> _playingInstances = new List<SoundInstanceXNA>(); // One-off sounds
        List<WeakReference> _createdInstances = new List<WeakReference>(); // Sound instances with owner


        AudioListener _listener = new AudioListener();

        #region Private fields
        AWMusic _music;
        Action _volumeFadeAction;

        /*
        AudioEngine _audioEngine;
        WaveBank _waveBank;
        SoundBank _soundBank;
        AudioCategory _soundEffectCategory;
        */

        #endregion

        public SoundEngineXNA(AssaultWingCore game, int updateOrder)
            : base(game, updateOrder)
        {
            
        }

        #region Overridden GameComponent methods

        public override void Initialize()
        {
            try
            {
                Log.Write("Sound engine initialized.");
                string filePath = Game.Content.RootDirectory + "\\corecontent\\sounds\\sounddefs.xml";

                System.Globalization.CultureInfo ci = System.Globalization.CultureInfo.InstalledUICulture;
                NumberFormatInfo ni = (System.Globalization.NumberFormatInfo)ci.NumberFormat.Clone();
                ni.NumberDecimalSeparator = ".";

                var allSounds = new List<string>();

                var document = new XmlDocument();
                 document.Load(filePath);
                var soundNodes = document.SelectNodes("group/sound");
                foreach (XmlNode sound in soundNodes)
                {
                    string baseName = sound.Attributes["name"].Value.ToLower();

                    var loopAttribute = sound.Attributes["loop"];
                    bool loop = (loopAttribute != null ? Boolean.Parse(loopAttribute.Value) : false);

                    var spatialAttribute = sound.Attributes["spatial"];
                    bool spatial = (spatialAttribute != null ? Boolean.Parse(spatialAttribute.Value) : true);

                    var volumeAttribute = sound.Attributes["volume"];
                    float volume = (volumeAttribute != null ? (float)Double.Parse(volumeAttribute.Value, ni) : 1.0f);
                    
                    var distAttribute = sound.Attributes["distancescale"];
                    float dist = (distAttribute != null ? (float)Double.Parse(distAttribute.Value, ni) : 200.0f);


                    // Find all variations for a sound
                    var effects = new List<SoundEffect>();
                    var manager = Game.Content;

                    for (int i = 1; i <= 99; i++)
                    {
                        string name = string.Format("{0}{1:00}", baseName, i);
                        if (!manager.Exists<SoundEffect>(name)) break;

                        var effect = manager.Load<SoundEffect>(name);
                        effects.Add(effect);
                    }
                    if (effects.Count == 0)
                    {
                        Console.WriteLine("Error loading sound " + baseName + " (missing from project?)");
                    }
                    var cue = new SoundCue(effects.ToArray(), volume, dist, loop);
                    _soundCues.Add(baseName, cue);
                }
            }
            catch (InvalidOperationException e)
            {
                Log.Write("ERROR: There will be no sound. Sound engine initialization failed. Exception details: " + e.ToString());
                Enabled = false;
            }
        }

        public override void Update()
        {
            if (_volumeFadeAction != null) _volumeFadeAction();
            if (_music != null) _music.Volume = ActualMusicVolume;

            _playingInstances.RemoveAll(instance => instance.IsFinished());
            _createdInstances.RemoveAll(instance => instance.Target == null);
            
            List<Ship> ships = new List<Ship>();
            Vector2 pos = new Vector2();
            Vector2 move = new Vector2();
            int playerCount = 0;
            foreach(Player p in AssaultWingCore.Instance.DataEngine.Players)
            {
                if (p.Ship != null)
                {
                    pos = pos + p.Ship.Pos;
                    move = move + p.Ship.Move;
                    playerCount++;
                    break;
                }
            }
            
            _listener.Position = new Vector3(pos.X / playerCount, pos.Y / playerCount, 0);
            _listener.Velocity = new Vector3(move.X / playerCount, move.Y / playerCount, 0);
            foreach (SoundInstanceXNA instance in _playingInstances)
            {
                 instance.UpdateSpatial(_listener);   
            }

            foreach (WeakReference instance in _createdInstances.ToArray())
            {
                ((SoundInstanceXNA)instance.Target).UpdateSpatial(_listener);
            }
        }

        #endregion

        #region Public interface

        /// <summary>
        /// Starts playing a random track from a tracklist.
        /// </summary>
        public override void PlayMusic(IList<BackgroundMusic> musics)
        {
            if (!Enabled) return;
            if (musics.Count > 0)
            {
                BackgroundMusic track = musics[RandomHelper.GetRandomInt(musics.Count)];
                PlayMusic(track.FileName, track.Volume);
            }
        }

        /// <summary>
        /// Starts playing set track from game music playlist
        /// </summary>
        public override void PlayMusic(string trackName, float trackVolume)
        {
            if (!Enabled) return;
            StopMusic();
            RelativeMusicVolume = trackVolume;
            InternalMusicVolume = 1;
            _music = new AWMusic(Game.Content, trackName) { Volume = ActualMusicVolume };
            _music.EnsureIsPlaying();
        }

        /// <summary>
        /// Stops music playback immediately.
        /// </summary>
        public override void StopMusic()
        {
            if (!Enabled) return;
            if (_music == null) return;
            _music.EnsureIsStopped();
            _volumeFadeAction = null;
        }

        /// <summary>
        /// Stops music playback with a fadeout.
        /// </summary>
        public override void StopMusic(TimeSpan fadeoutTime)
        {
            if (!Enabled) return;
            if (_music == null || !_music.IsPlaying) return;
            var now = Game.GameTime.TotalRealTime;
            float fadeoutSeconds = (float)fadeoutTime.TotalSeconds;
            _volumeFadeAction = () =>
            {
                float volume = MathHelper.Clamp(1 - now.SecondsAgoRealTime() / fadeoutSeconds, 0, 1);
                InternalMusicVolume = volume;
                if (volume == 0) StopMusic();
            };
        }

        /// <summary>
        /// Returns the named cue or <code>null</code> if sounds are disabled.
        /// </summary>
        private SoundInstance CreateSoundInternal(string soundName, Gob parentGob)
        {
            if (!Enabled) return null;

            soundName = soundName.ToLower();

            if (!_soundCues.ContainsKey(soundName))
            {
                throw new ArgumentException("Sound " + soundName + " does not exist!");
            }

            SoundCue cue = _soundCues[soundName.ToLower()];

            SoundEffect soundEffect = cue.GetEffect();
            

            SoundEffectInstance instance = soundEffect.CreateInstance();

            instance.IsLooped = cue._loop;
            //Console.WriteLine("Setting vol " + cue._volume + " for " + soundName);
            
            return new SoundInstanceXNA(instance, parentGob, cue._volume, cue._distanceScale);
        }

        public override SoundInstance CreateSound(string soundName, Gob parentGob)
        {
            SoundInstanceXNA instance = (SoundInstanceXNA)CreateSoundInternal(soundName, parentGob);
            _createdInstances.Add(new WeakReference(instance));
            return instance;
        }

        public override SoundInstance PlaySound(string soundName, Gob parentGob)
        {
            SoundInstanceXNA instance = (SoundInstanceXNA)CreateSoundInternal(soundName, parentGob);

            if (instance != null)
            {
                instance.Play();                
            }
            _playingInstances.Add(instance);
            return instance;
        }


        #endregion
    }
}
