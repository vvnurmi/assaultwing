﻿using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Xna.Framework;

namespace AW2.Sound
{
    public abstract class SoundInstance
    {
        public abstract void Play();
        public abstract void Stop();
        public abstract void SetGob(AW2.Game.Gob gob);
        public abstract void Dispose();
        public abstract void EnsureIsPlaying();
    };

}