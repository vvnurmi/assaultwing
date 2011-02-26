﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework;

namespace AW2.Sound
{
    public class SoundInstanceXNA : SoundInstance
    {
        public SoundInstanceXNA(SoundEffectInstance effect)
        {
            _instance = effect;
        }  
        
        public SoundInstanceXNA(SoundEffectInstance effect, AW2.Game.Gob gob, float baseVolume, float distanceScale)
        {
            _instance = effect;
            _gob = gob;
            _emitter = new AudioEmitter();
            _baseVolume = baseVolume;
            _instance.Volume = _baseVolume;

            _distanceScale = distanceScale;
        }

        public override void SetVolume(float vol)
        {
            _instance.Volume = _baseVolume * vol;
        }

        public override void Play()
        {
            if (_emitter != null)
            {
                _instance.Apply3D(new AudioListener(), _emitter);
            }
            _instance.Play();
            
        }
        public override void Stop()
        {
            _instance.Stop();
        }

        public AW2.Game.Gob Gob { get; set; }

        public override void Dispose()
        {
            _instance.Dispose();
            _gob = null;
            _emitter = null;
        }
        public bool IsFinished()
        {
            return _instance.State == SoundState.Stopped;
        }

        //int DSCALE = 200;
        public void UpdateSpatial(AudioListener listener)
        {
            if (_gob != null)
            {
                _emitter.Position = new Vector3(_gob.Pos.X, _gob.Pos.Y, 0);
                _emitter.Velocity = new Vector3(_gob.Move.X, _gob.Move.Y, 0);

                SoundEffect.DistanceScale = _distanceScale;
                SoundEffect.DopplerScale = 0.05f;
                _instance.Apply3D(listener, _emitter);//listener, _emitter);

            }
        }
                    
        public override void EnsureIsPlaying()
        {
            if (_instance.State != SoundState.Playing)
            {
                Console.WriteLine("Re-Started " + _instance.ToString());
                _instance.Play();
            }
        }

        private SoundEffectInstance _instance;   
        private AW2.Game.Gob _gob;
        private AudioEmitter _emitter;
        private float _baseVolume;
        private float _distanceScale;
        
    }
}
