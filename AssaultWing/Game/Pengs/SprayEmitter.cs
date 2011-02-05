using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using AW2.Game.Gobs;
using AW2.Helpers;
using AW2.Helpers.Serialization;

namespace AW2.Game.Pengs
{
    /// <summary>
    /// Particle emitter spilling stuff radially outwards from a circle sector 
    /// (or a full circle) in a radius from its center.
    /// The center is located at the origin of the peng's coordinate system.
    /// </summary>
    [LimitedSerialization]
    public class SprayEmitter : IConsistencyCheckable
    {
        /// <summary>
        /// Type of initial particle facing.
        /// </summary>
        private enum FacingType
        {
            /// <summary>
            /// Particles face the average emission direction.
            /// </summary>
            Directed,

            /// <summary>
            /// Particles face the direction where they start moving,
            /// that is, away from the emission center.
            /// </summary>
            Forward,

            /// <summary>
            /// Particles face in random directions.
            /// </summary>
            Random,
        }

        #region SprayEmitter fields

        /// <summary>
        /// Names of textures of particles to emit.
        /// </summary>
        [TypeParameter]
        private CanonicalString[] _textureNames;

        /// <summary>
        /// Names of types of gobs to emit.
        /// </summary>
        [TypeParameter]
        private CanonicalString[] _gobTypeNames;

        [RuntimeState]
        private bool _paused;

        [ExcludeFromDeepCopy]
        private Peng _peng;

        [ExcludeFromDeepCopy]
        private Texture2D[] _textures;

        /// <summary>
        /// Radius of emission circle.
        /// </summary>
        [TypeParameter]
        private float _radius;

        /// <summary>
        /// Half width of emission sector, in radians.
        /// </summary>
        /// Setting spray angle to pi (3.14159...) will spray particles
        /// in a full circle.
        [TypeParameter]
        private float _sprayAngle;

        /// <summary>
        /// Type of particle facing at emission.
        /// </summary>
        [TypeParameter]
        private FacingType _facingType;

        /// <summary>
        /// Initial magnitude of particle velocity, in meters per second.
        /// </summary>
        /// The 'age' argument of this peng parameter will always be set to zero.
        /// The direction of particle velocity will be away from the emission center.
        [TypeParameter, ShallowCopy]
        private PengParameter _initialVelocity;

        /// <summary>
        /// Emission frequency, in number of particles per second.
        /// </summary>
        [TypeParameter]
        private float _emissionFrequency;

        /// <summary>
        /// Number of particles to create, or negative for no limit.
        /// </summary>
        [TypeParameter, RuntimeState]
        private int _numberToCreate;

        /// <summary>
        /// Time of next particle birth, in game time.
        /// </summary>
        private TimeSpan _nextBirth;

        private int _numberCreated;

        #endregion SprayEmitter fields

        #region Properties

        /// <summary>
        /// Names of textures of particles to emit.
        /// </summary>
        public CanonicalString[] TextureNames { get { return _textureNames; } }

        /// <summary>
        /// Textures in the same order as in <see cref="_textureNames"/>.
        /// </summary>
        public Texture2D[] Textures { get { return _textures; } private set { _textures = value; } }

        /// <summary>
        /// Names of types of gobs to emit.
        /// </summary>
        public CanonicalString[] GobTypeNames { get { return _gobTypeNames; } }

        /// <summary>
        /// The peng this emitter belongs to.
        /// </summary>
        public Peng Peng { get { return _peng; } set { _peng = value; } }

        /// <summary>
        /// If <c>true</c>, no particles will be emitted.
        /// </summary>
        public bool Paused
        {
            get { return _paused; }
            set
            {
                if (_paused && !value)
                {
                    // Forget about creating particles whose creation was due 
                    // while we were paused.
                    if (_nextBirth < Peng.Arena.TotalTime)
                        _nextBirth = Peng.Arena.TotalTime;
                }
                _paused = value;
            }
        }

        /// <summary>
        /// <c>true</c> if emitting has finished for good, <c>false</c> otherwise.
        /// </summary>
        public bool Finished { get { return _numberToCreate > 0 && _numberCreated >= _numberToCreate; } }

        #endregion Properties

        /// <summary>
        /// This constructor only is for serialisation.
        /// </summary>
        public SprayEmitter()
        {
            _textureNames = new[] { (CanonicalString)"dummytexture" };
            _gobTypeNames = new[] { (CanonicalString)"dummygob" };
            _paused = false;
            _radius = 15;
            _sprayAngle = MathHelper.PiOver4;
            _facingType = FacingType.Random;
            _initialVelocity = new CurveLerp();
            _emissionFrequency = 10;
            _numberToCreate = -1;
            _nextBirth = new TimeSpan(-1);
        }

        public void LoadContent()
        {
            Textures = new Texture2D[TextureNames.Length];
            for (int i = 0; i < TextureNames.Length; ++i)
                Textures[i] = Peng.Game.Content.Load<Texture2D>(TextureNames[i]);
        }

        public void UnloadContent()
        {
        }

        /// <summary>
        /// Returns created particles, adds created gobs to <c>DataEngine</c>.
        /// Returns <c>null</c> if no particles were created.
        /// </summary>
        public IEnumerable<Particle> Emit()
        {
            if (_paused) return null;
            if (Finished) return null;
            List<Particle> particles = null;

            // Initialise 'nextBirth'.
            if (_nextBirth.Ticks < 0)
                _nextBirth = Peng.Arena.TotalTime;

            // Count how many to create.
            int createCount = Math.Max(0, (int)(1 + _emissionFrequency * (Peng.Arena.TotalTime - _nextBirth).TotalSeconds));
            if (_numberToCreate >= 0)
            {
                createCount = Math.Min(createCount, _numberToCreate);
                _numberCreated += createCount;
            }
            _nextBirth += TimeSpan.FromSeconds(createCount / _emissionFrequency);

            if (createCount > 0 && _textureNames.Length > 0)
                particles = new List<Particle>();

            // Create the particles. They are created 
            // with an even distribution over the circle sector
            // defined by 'radius', the origin and 'sprayAngle'.

            for (int i = 0; i < createCount; ++i)
            {
                // Find out type of emitted thing (which gob or particle) and create it.
                int emitType = RandomHelper.GetRandomInt(_textureNames.Length + _gobTypeNames.Length);

                // The emitted thing init routine must be an Action<Gob>
                // so that it can be passed to Gob.CreateGob. Particle init
                // is included in the same routine because of large similarities.
                Action<Gob> emittedThingInit = gob => GobCreation(gob, createCount, i, emitType, ref particles);
                if (emitType < _textureNames.Length)
                    emittedThingInit(null);
                else
                    Gob.CreateGob<Gob>(Peng.Game, _gobTypeNames[emitType - _textureNames.Length], emittedThingInit);
            }
            return particles;
        }

        public void Reset()
        {
            _numberCreated = 0;
        }

        #region IConsistencyCheckable Members

        /// <summary>
        /// Makes the instance consistent in respect of fields marked with a
        /// limitation attribute.
        /// </summary>
        /// <param name="limitationAttribute">Check only fields marked with 
        /// this limitation attribute.</param>
        /// <see cref="Serialization"/>
        public void MakeConsistent(Type limitationAttribute)
        {
            if (limitationAttribute == typeof(TypeParameterAttribute))
            {
                // Make sure there's no null references.
                if (_initialVelocity == null)
                    throw new Exception("Serialization error: SprayEmitter initialVelocity not defined");

                if (_emissionFrequency <= 0 || _emissionFrequency > 100000)
                {
                    Log.Write("Correcting insane emission frequency " + _emissionFrequency);
                    _emissionFrequency = MathHelper.Clamp(_emissionFrequency, 1, 100000);
                }
            }
            _nextBirth = new TimeSpan(-1);
        }

        #endregion

        private void GobCreation(Gob gob, int createCount, int i, int emitType, ref List<Particle> particles)
        {
            // Find out emission parameters.
            // We have to loop because some choices of parameters may not be wanted.
            int maxAttempts = 20;
            bool attemptOk = false;
            for (int attempt = 0; !attemptOk && attempt < maxAttempts; ++attempt)
            {
                bool lastAttempt = attempt == maxAttempts - 1;
                attemptOk = true;
                float pengInput = Peng.Input;
                int random = RandomHelper.GetRandomInt();
                float directionAngle, rotation;
                Vector2 directionUnit, pos, move;
                switch (Peng.ParticleCoordinates)
                {
                    case Peng.CoordinateSystem.Peng:
                        RandomHelper.GetRandomCirclePoint(_radius, -_sprayAngle, _sprayAngle,
                            out pos, out directionUnit, out directionAngle);
                        move = _initialVelocity.GetValue(0, pengInput, random) * directionUnit;
                        switch (_facingType)
                        {
                            case FacingType.Directed: rotation = 0; break;
                            case FacingType.Forward: rotation = directionAngle; break;
                            case FacingType.Random: rotation = RandomHelper.GetRandomFloat(0, MathHelper.TwoPi); break;
                            default: throw new Exception("SprayEmitter: Unhandled particle facing type " + _facingType);
                        }
                        break;
                    case Peng.CoordinateSystem.Game:
                        {
                            float posWeight = (i + 1) / (float)createCount;
                            var startPos = Peng.OldDrawPos;
                            var endPos = Peng.Pos + Peng.DrawPosOffset;
                            var iPos = Vector2.Lerp(startPos, endPos, posWeight);
                            var drawRotation = Peng.Rotation + Peng.DrawRotationOffset;
                            RandomHelper.GetRandomCirclePoint(_radius, drawRotation - _sprayAngle, drawRotation + _sprayAngle,
                                out pos, out directionUnit, out directionAngle);
                            pos += iPos;
                            move = Peng.Move + _initialVelocity.GetValue(0, pengInput, random) * directionUnit;

                            // HACK: 'move' will be added to 'pos' in PhysicalUpdater during this same frame
                            pos -= Peng.Game.PhysicsEngine.ApplyChange(move, Peng.Game.GameTime.ElapsedGameTime);

                            switch (_facingType)
                            {
                                case FacingType.Directed: rotation = Peng.Rotation; break;
                                case FacingType.Forward: rotation = directionAngle; break;
                                case FacingType.Random: rotation = RandomHelper.GetRandomFloat(0, MathHelper.TwoPi); break;
                                default: throw new Exception("SprayEmitter: Unhandled particle facing type " + _facingType);
                            }
                        }
                        break;
                    default:
                        throw new ApplicationException("SprayEmitter: Unhandled peng coordinate system " + Peng.ParticleCoordinates);
                }

                // Set the thing's parameters.
                if (emitType < _textureNames.Length)
                {
                    var particle = new Particle
                    {
                        Alpha = 1,
                        BirthTime = Peng.Arena.TotalTime,
                        Move = move,
                        PengInput = pengInput,
                        Pos = pos,
                        Random = random,
                        Direction = directionAngle,
                        DirectionVector = Vector2.UnitX.Rotate(directionAngle),
                        Rotation = rotation,
                        Scale = 1,
                        TextureIndex = emitType,
                        Timeout = Peng.Arena.TotalTime + TimeSpan.FromSeconds(Peng.ParticleUpdater.ParticleAge.GetValue(0, pengInput, random)),
                    };
                    particles.Add(particle);
                }
                else
                {
                    // Bail out if the position is not free for the gob.
                    if (!lastAttempt && !Peng.Arena.IsFreePosition(gob, pos))
                    {
                        attemptOk = false;
                        continue;
                    }
                    gob.Owner = Peng.Owner;
                    gob.ResetPos(pos, move, rotation);
                    Peng.Arena.Gobs.Add(gob);
                }
            }
        }
    }
}
