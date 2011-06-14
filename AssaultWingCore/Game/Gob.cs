using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using AW2.Core;
using AW2.Game.Arenas;
using AW2.Game.GobUtils;
using AW2.Graphics;
using AW2.Helpers;
using AW2.Helpers.Serialization;

namespace AW2.Game
{
    /// <summary>
    /// Game object, i.e., a gob.
    /// </summary>
    /// By default, a gob is a single material object in the game world
    /// that follows the laws of physics and displays itself as a 3D model.
    /// A gob's additional functionality is implemented in a subclass.
    ///
    /// There can be several special instances of each subclass of Gob. Each of
    /// these 'template instances' defines a gob type by specifying values for the
    /// type parameters of that Gob subclass. Newly created gob instances automatically
    /// initialise their type parameter fields by copying them from a template instance.
    /// Template instances are referred to by human-readable names such as "rocket pod".
    /// 
    /// Class Gob manages exhaust engines, i.e. particle engines that produce
    /// engine exhaust fumes or similar. Exhaust engines are created automatically,
    /// provided that the gob's 3D model has bones whose name begins with Thruster
    /// and <b>exhaustEngineNames</b> contains at least one valid particle engine name.
    /// All exhaust engines are set on loop and their position and direction
    /// are set each frame. The subclass should manage other parameters of the engines.
    /// 
    /// Class Gob also provides methods required by certain Gob subclasses 
    /// such as those that can be damaged. This serves to keep general code
    /// in one place only.
    /// 
    /// Class Gob and its subclasses use limited (de)serialisation for
    /// for saving and loading gob types. Therefore those fields
    /// that describe the gob type should be marked as 'type parameters' by 
    /// <b>TypeParameterAttribute</b>, and those fields that describe the gob's 
    /// state during gameplay should be marked by <b>RuntimeStateAttribute</b>.
    /// The remaining fields, precalculated data and references to objects that
    /// are not part of the game state should not be marked with either attribute.
    /// 
    /// Each Gob subclass must provide a parameterless constructor that initialises all
    /// of its type parameters to descriptive and exemplary default values. Always create
    /// subclass instances by calling <see cref="AW2.Game.Gob.CreateGob"/>.
    /// <see cref="AW2.Helpers.TypeParameterAttribute"/>
    /// <see cref="AW2.Helpers.RuntimeStateAttribute"/>
    [LimitedSerialization]
    [System.Diagnostics.DebuggerDisplay("ID:{ID} TypeName:{TypeName} Pos:{Pos} Move:{Move}")]
    public class Gob : Clonable, IConsistencyCheckable, INetworkSerializable, ICustomTypeDescriptor
    {
        /// <summary>
        /// Type of a gob's preferred placement to arena layers.
        /// </summary>
        public enum LayerPreferenceType
        {
            /// <summary>
            /// Place gob to the gameplay layer.
            /// </summary>
            Front,

            /// <summary>
            /// Place gob to the gameplay backlayer.
            /// </summary>
            Back,
        }

        #region Fields for all gobs

        /// <summary>
        /// A gob ID that will not be used by any actual gob.
        /// </summary>
        /// <seealso cref="ID"/>
        public const int INVALID_ID = 0;

        /// <summary>
        /// Default rotation of gobs. Points up in the game world.
        /// </summary>
        public const float DEFAULT_ROTATION = MathHelper.PiOver2;

        /// <summary>
        /// Time, in seconds, for a gob to stop being cold.
        /// </summary>
        /// <see cref="Gob.Cold"/>
        private const float WARM_UP_TIME = 0.2f;

        /// <summary>
        /// Maximum gob displacement that is not interpreted by the draw position
        /// smoothing algorithm as a repositioning. Measured in meters.
        /// </summary>
        private const int POS_SMOOTHING_CUTOFF = 50;

        /// <summary>
        /// Maximum gob rotation change that is not interpreted by the draw rotation
        /// smoothing algorithm as a discrete rotation. Measured in radians.
        /// </summary>
        public const float ROTATION_SMOOTHING_CUTOFF = MathHelper.PiOver2;

        private const float HIDING_ALPHA_LIMIT = 0.01f;
        private const float MIN_GOB_COORDINATE = -Arena.ARENA_OUTER_BOUNDARY_THICKNESS;
        private const float MAX_GOB_COORDINATE = 16000 + Arena.ARENA_OUTER_BOUNDARY_THICKNESS;
        private const float MIN_GOB_DELTA_COORDINATE = byte.MinValue / 8f;
        private const float MAX_GOB_DELTA_COORDINATE = byte.MaxValue / 8f;
        private static readonly TimeSpan FULL_NETWORK_UPDATE_INTERVAL = TimeSpan.FromSeconds(1);

        private static Queue<int> g_unusedRelevantIDs;
        private static Queue<int> g_unusedIrrelevantIDs;

        /// <summary>
        /// Drawing depth of 2D graphics of the gob, between 0 and 1.
        /// 0 is front, 1 is back.
        /// </summary>
        [TypeParameter]
        private float _depthLayer2D;

        /// <summary>
        /// Drawing mode of 2D graphics of the gob.
        /// </summary>
        [TypeParameter]
        private DrawMode2D _drawMode2D;

        /// <summary>
        /// Preferred placement of gob to arena layers.
        /// </summary>
        [TypeParameter]
        private LayerPreferenceType _layerPreference;

        [RuntimeState, Browsable(false)]
        private int _staticID;

        /// <summary>
        /// Position of the gob in the game world.
        /// </summary>
        [RuntimeState]
        private Vector2 _pos;

        /// <summary>
        /// Gob rotation around the Z-axis in radians.
        /// </summary>
        [RuntimeState]
        private float _rotation;

        /// <summary>
        /// Mass of the gob, measured in kilograms.
        /// </summary>
        /// Larger mass needs more force to be put in motion. 
        [TypeParameter]
        private float _mass;

        /// <summary>
        /// Name of the 3D model of the gob. The name indexes the model database in GraphicsEngine.
        /// </summary>
        [TypeParameter]
        private CanonicalString _modelName;

        /// <summary>
        /// Scaling factor of the 3D model.
        /// </summary>
        [TypeParameter]
        private float _scale;

        /// <summary>
        /// Types of gobs to create on birth.
        /// </summary>
        [TypeParameter, ShallowCopy]
        private CanonicalString[] _birthGobTypes;

        /// <summary>
        /// Types of gobs to create on death.
        /// </summary>
        /// You might want to put some gob types of subclass Explosion here.
        [TypeParameter, ShallowCopy]
        private CanonicalString[] _deathGobTypes;

        /// <summary>
        /// Time of birth of the gob, in game time.
        /// </summary>
        private TimeSpan _birthTime;

        /// <summary>
        /// True iff the Die() has been called for this gob.
        /// </summary>
        private bool _dead;

        private int _disabledCount;

        /// <summary>
        /// True iff the gob moves around by the laws of physics.
        /// </summary>
        /// Subclasses should set this according to their needs.
        [TypeParameter]
        private bool _movable;

        /// <summary>
        /// Preferred maximum time between the gob's state updates
        /// from the game server to game clients, in real time,
        /// or zero if there are no periodic network updates.
        /// </summary>
        [TypeParameter]
        private TimeSpan _networkUpdatePeriod;
        private TimeSpan _nextFullNetworkUdpateTime; // in game time
        private Vector2 _lastNetworkUpdatePos;

        /// <summary>
        /// Access only through <see cref="ModelPartTransforms"/>.
        /// </summary>
        private Matrix[] _modelPartTransforms;

        /// <summary>
        /// Last time of update of <see cref="modelPartTransforms"/>, in game time.
        /// </summary>
        private TimeSpan _modelPartTransformsUpdated;

        /// <summary>
        /// Bounding volume of the visuals of the gob, in gob coordinates.
        /// </summary>
        protected BoundingSphere _drawBounds;

        #endregion Fields for all gobs

        #region Fields for gobs with thrusters

        /// <summary>
        /// Names of exhaust engine types.
        /// </summary>
        [TypeParameter, ShallowCopy]
        private CanonicalString[] _exhaustEngineNames;

        /// <summary>
        /// Particle engines that manage exhaust fumes.
        /// </summary>
        private Gob[] _exhaustEngines;

        private bool _exhaustEffectsEnabled = true;

        #endregion Fields for gobs with thrusters

        #region Fields for collisions

        /// <summary>
        /// Collision primitives, translated according to the gob's location.
        /// </summary>
        [TypeParameter]
        protected CollisionArea[] _collisionAreas;

        #endregion Fields for collisions

        #region Fields for damage

        /// <summary>
        /// Maximum amount of sustainable damage.
        /// </summary>
        [TypeParameter]
        private float _maxDamage;

        #endregion Fields for damage

        #region Fields for bleach

        /// <summary>
        /// Function that maps bleach damage to bleach, i.e. degree of whiteness.
        /// </summary>
        private static Curve g_bleachCurve;

        /// <summary>
        /// Amount of accumulated damage that determines the amount of bleach.
        /// </summary>
        private float _bleachDamage;

        /// <summary>
        /// Previously returned value of <see cref="GetBleach"/>.
        /// </summary>
        private float _previousBleach;

        /// <summary>
        /// Time when bleach will be reset, in game time.
        /// </summary>
        /// When bleach is set to nonzero, this time is set to denote how
        /// long the bleach is supposed to stay on.
        private TimeSpan _bleachResetTime;

        #endregion Fields for bleach

        #region Gob properties

        /// <summary>
        /// The gob's unique runtime identifier.
        /// </summary>
        public int ID { get; set; }

        /// <summary>
        /// The gob's unique identifier while serialized as part of an arena, or zero.
        /// Not to be confused with <see cref="ID"/>.
        /// </summary>
        public int StaticID { get { return _staticID; } set { _staticID = value; } }

        public AssaultWingCore Game { get; set; }

        public Arena Arena { get; set; }

        /// <summary>
        /// Is the gob relevant to gameplay. Irrelevant gobs won't receive state updates
        /// from the server when playing over network and they can therefore be created
        /// independently on a client.
        /// </summary>
        public virtual bool IsRelevant { get { return true; } }

        public bool IsDisposed { get; private set; }
        public bool IsDamageable
        {
            get
            {
                return CollisionAreas.Any(area => (area.Type & CollisionAreaType.PhysicalDamageable) != 0);
            }
        }
        public bool IsVisible { get; set; }
        public Player VisibilityLimitedTo { get; set; }
        public float CollisionDamageToOthersMultiplier { get; set; }

        /// <summary>
        /// Gob drawing bleach override, between 0 and 1. If null, normal bleach behaviour is used.
        /// </summary>
        public float? BleachValue { get; set; }

        /// <summary>
        /// Drawing depth of 2D graphics of the gob, between 0 and 1.
        /// 0 is front, 1 is back.
        /// </summary>
        public float DepthLayer2D { get { return _depthLayer2D; } set { _depthLayer2D = value; } }

        /// <summary>
        /// The last player to damage this gob
        /// </summary>
        public Player LastDamager { get; private set; }

        /// <summary>
        /// Last time when <see cref="LastDamager"/> is relevant, in game time.
        /// </summary>
        public TimeSpan LastDamagerTimeout { get; private set; }

        /// <summary>
        /// Drawing mode of 2D graphics of the gob.
        /// </summary>
        public DrawMode2D DrawMode2D { get { return _drawMode2D; } set { _drawMode2D = value; } }

        /// <summary>
        /// Preferred placement of gob to arena layers.
        /// </summary>
        public LayerPreferenceType LayerPreference { get { return _layerPreference; } }

        /// <summary>
        /// Bounding volume of the visuals of the gob, in world coordinates.
        /// </summary>
        public virtual BoundingSphere DrawBounds
        {
            get
            {
                return new BoundingSphere(_drawBounds.Center.RotateZ(DrawRotation + DrawRotationOffset) + new Vector3(Pos + DrawPosOffset, 0), _drawBounds.Radius);
            }
        }

        /// <summary>
        /// The 3D model of the gob.
        /// </summary>
        protected Model Model { get; private set; }

        /// <summary>
        /// Names of all 3D models that this gob type will ever use.
        /// </summary>
        public virtual IEnumerable<CanonicalString> ModelNames
        {
            get { return new List<CanonicalString> { _modelName }; }
        }

        /// <summary>
        /// Names of all textures that this gob type will ever use.
        /// </summary>
        public virtual IEnumerable<CanonicalString> TextureNames
        {
            get { return new List<CanonicalString>(); }
        }

        /// <summary>
        /// Position of the gob in the game world.
        /// </summary>
        public virtual Vector2 Pos { get { return _pos; } set { _pos = value; } }

        /// <summary>
        /// Drawing position delta of the gob in the game world, relative to <see cref="Pos"/>.
        /// This is mostly zero except on game clients who use this to smooth out erratic gob
        /// movement caused by inconsistency between local updates and game server updates.
        /// </summary>
        public Vector2 DrawPosOffset { get; set; }

        /// <summary>
        /// Sets <see cref="Pos"/>, <see cref="Move"/> and <see cref="Rotation"/>
        /// as if the gob appeared there instantaneously
        /// as opposed to moving there in a continuous fashion.
        /// </summary>
        public virtual void ResetPos(Vector2 pos, Vector2 move, float rotation)
        {
            Pos = pos;
            Move = move;
            Rotation = rotation;
        }

        /// <summary>
        /// Get or set the gob's movement vector.
        /// </summary>
        public virtual Vector2 Move { get; set; }

        /// <summary>
        /// Mass of the gob, measured in kilograms.
        /// </summary>
        public float Mass { get { return _mass; } }

        /// <summary>
        /// Get or set the gob's rotation around the Z-axis.
        /// </summary>
        public virtual float Rotation
        {
            get { return _rotation; }
            set { _rotation = value % MathHelper.TwoPi; }
        }

        public virtual float DrawRotation { get { return Rotation; } }

        /// <summary>
        /// Drawing rotation delta of the gob, relative to <see cref="Rotation"/>.
        /// This is mostly zero except on game clients who use this to smooth out
        /// erratic gob rotation caused by inconsistency between local updates and
        /// game server updates.
        /// </summary>
        public float DrawRotationOffset { get; set; }

        /// <summary>
        /// The player who owns the gob. Can be null for impartial gobs.
        /// </summary>
        public Player Owner { get { return OwnerProxy != null ? OwnerProxy.GetValue() : null; } set { OwnerProxy = value; } }
        public LazyProxy<int, Player> OwnerProxy { get; set; }

        /// <summary>
        /// Arena layer of the gob, or <c>null</c> if uninitialised. Set by <see cref="Arena"/>.
        /// </summary>
        /// Note that if somebody who is not <see cref="Arena"/> sets this value,
        /// it leads to confusion.
        public ArenaLayer Layer { get; set; }

        /// <summary>
        /// Returns the name of the 3D model of the gob.
        /// </summary>
        public CanonicalString ModelName { get { return _modelName; } set { _modelName = value; } }

        /// <summary>
        /// Get and set the scaling factor of the 3D model.
        /// </summary>
        public float Scale { get { return _scale; } set { _scale = value; } }

        /// <summary>
        /// Amount of alpha to use when drawing the gob's 3D model, between 0 and 1.
        /// </summary>
        public float Alpha { get; set; }

        /// <summary>
        /// Is the gob hidden from opposing players.
        /// </summary>
        public bool IsHidden { get { return IsHiding && Alpha < HIDING_ALPHA_LIMIT; } }

        /// <summary>
        /// Is the gob trying to hide from opposing players.
        /// </summary>
        public bool IsHiding { get; set; }

        public float AgeInGameSeconds { get { return _birthTime.SecondsAgoGameTime(); } }

        /// <summary>
        /// Returns the world matrix of the gob, i.e., the translation from
        /// game object coordinates to game world coordinates.
        /// </summary>
        public virtual Matrix WorldMatrix { get { return AWMathHelper.CreateWorldMatrix(_scale, DrawRotation + DrawRotationOffset, _pos + DrawPosOffset); } }

        /// <summary>
        /// The transform matrices of the gob's 3D model parts.
        /// </summary>
        private Matrix[] ModelPartTransforms
        {
            get
            {
                if (_modelPartTransforms == null || _modelPartTransforms.Length != Model.Bones.Count)
                {
                    _modelPartTransforms = new Matrix[Model.Bones.Count];
                    _modelPartTransformsUpdated = new TimeSpan(-1);
                }
                if (_modelPartTransformsUpdated < Arena.TotalTime)
                {
                    _modelPartTransformsUpdated = Arena.TotalTime;
                    CopyAbsoluteBoneTransformsTo(Model, _modelPartTransforms);
                }
                return _modelPartTransforms;
            }
        }

        /// <summary>
        /// The collision areas of the gob. Note: To remove some collision areas
        /// during gameplay, call <see cref="RemoveCollisionAreas"/>.
        /// </summary>
        /// <remarks>
        /// Collision areas are set to null by Wall. It is faster than to remove elements from a large array.
        /// </remarks>
        public IEnumerable<CollisionArea> CollisionAreas { get { return _collisionAreas.Where(area => area != null); } }

        /// <summary>
        /// Is the gob cold.
        /// </summary>
        /// All gobs are born <b>cold</b>. If a gob is cold, it won't
        /// collide with other gobs that have the same owner. This works around
        /// the problem of bullets hitting the firing ship immediately at birth.
        public virtual bool Cold { get { return _birthTime.SecondsAgoGameTime() < WARM_UP_TIME; } }

        /// <summary>
        /// Is the gob dead, i.e. has Die been called for this gob.
        /// </summary>
        /// If you hold references to gobs, do check every now and then if the gobs
        /// are dead and remove the references if they are.
        public bool Dead { get { return _dead; } }

        /// <summary>
        /// Is the gob disabled. A disabled gob is not regarded in movement and collisions.
        /// There can be multiple overlapping requests to disable a gob. The gob stays
        /// disabled until all such requests have been removed. For every disabling
        /// there must be a corresponding enabling.
        /// </summary>
        /// <seealso cref="Enable()"/>
        /// <seealso cref="Disable()"/>
        public bool Disabled
        {
            get { return _disabledCount > 0; }
        }

        /// <summary>
        /// True iff the gob moves around by the laws of physics.
        /// </summary>
        public bool Movable { get { return _movable; } protected set { _movable = value; } }

        /// <summary>
        /// True iff the gob's movement is affected by gravity and other forces.
        /// Subclasses should set this according to their needs.
        /// </summary>
        public bool Gravitating { get; protected set; }

        #endregion Gob Properties

        #region Network properties

        /// <summary>
        /// Preferred maximum time between the gob's state updates
        /// from the game server to game clients, in real time.
        /// </summary>
        public TimeSpan NetworkUpdatePeriod { get { return _networkUpdatePeriod; } set { _networkUpdatePeriod = value; } }

        public bool ForcedNetworkUpdate { get; set; }

        /// <summary>
        /// Time of last network update, in real time. Used only on the game server.
        /// </summary>
        public TimeSpan LastNetworkUpdate { get; set; }

        #endregion Network properties

        #region Gob static and instance constructors, and static constructor-like methods

        static Gob()
        {
            g_unusedRelevantIDs = new Queue<int>(Enumerable.Range(1, short.MaxValue - 1));
            g_unusedIrrelevantIDs = new Queue<int>(Enumerable.Range(short.MinValue, -1 - short.MinValue));
            g_bleachCurve = new Curve();
            g_bleachCurve.PreLoop = CurveLoopType.Constant;
            g_bleachCurve.PostLoop = CurveLoopType.Constant;
            g_bleachCurve.Keys.Add(new CurveKey(0, 0));
            g_bleachCurve.Keys.Add(new CurveKey(5, 0.1f));
            g_bleachCurve.Keys.Add(new CurveKey(30, 0.3f));
            g_bleachCurve.Keys.Add(new CurveKey(100, 0.5f));
            g_bleachCurve.Keys.Add(new CurveKey(200, 0.65f));
            g_bleachCurve.Keys.Add(new CurveKey(500, 0.8f));
            g_bleachCurve.Keys.Add(new CurveKey(1000, 0.9f));
            g_bleachCurve.Keys.Add(new CurveKey(5000, 1));
            g_bleachCurve.ComputeTangents(CurveTangent.Linear);
        }

        /// <summary>
        /// For serialization only.
        /// </summary>
        public Gob()
        {
            _depthLayer2D = 0.5f;
            _drawMode2D = new DrawMode2D(DrawModeType2D.None);
            _layerPreference = LayerPreferenceType.Front;
            _pos = Vector2.Zero;
            Move = Vector2.Zero;
            _rotation = 0;
            _mass = 1;
            _modelName = (CanonicalString)"dummymodel";
            _scale = 1f;
            _birthGobTypes = new CanonicalString[0];
            _deathGobTypes = new CanonicalString[0];
            _collisionAreas = new CollisionArea[0];
            DamageLevel = 0;
            _maxDamage = 100;
            _birthTime = new TimeSpan(23, 59, 59);
            _dead = false;
            _movable = true;
            _exhaustEngineNames = new CanonicalString[0];
        }

        /// <summary>
        /// Creates a gob of the specified gob type.
        /// </summary>
        protected Gob(CanonicalString typeName)
            : base(typeName)
        {
            Gravitating = true;
            ResetPos(new Vector2(float.NaN), Vector2.Zero, float.NaN); // resets Pos and Rotation smoothing on game clients
            _exhaustEngines = new Gob[0];
            Alpha = 1;
            _previousBleach = -1;
            CollisionDamageToOthersMultiplier = 1;
        }

        /// <summary>
        /// Creates a gob of a given type, ensures the gob has a given base class
        /// and performs a given initialisation on the gob.
        /// This method is for game logic; gob init is skipped appropriately on clients.
        /// </summary>
        /// Note that you cannot call new Gob(typeName) because then the created object
        /// won't have the fields of the subclass that 'typeName' requires. This static method
        /// takes care of finding the correct subclass.
        /// 
        /// In order for a call to this method to be meaningful, <c>init</c>
        /// should contain a call to <c>DataEngine.AddGob</c> or similar method.
        /// <typeparam name="T">Required base class of the gob</typeparam>
        /// <param name="typeName">Template type name of the gob</param>
        /// <param name="init">Initialisation to perform on the gob</param>
        /// <seealso cref="CreateGob(CanonicalString)"/>
        public static void CreateGob<T>(AssaultWingCore game, CanonicalString typeName, Action<T> init) where T : Gob
        {
            T gob = Clonable.Instantiate(typeName) as T;
            if (gob == null) throw new ApplicationException("Gob type template " + typeName + " wasn't of expected type " + typeof(T).Name);
            gob.Game = game;
            if (game.NetworkMode != NetworkMode.Client || !gob.IsRelevant)
                init(gob);
        }

        /// <summary>
        /// Creates unconditionally a new gob from the given runtime state.
        /// </summary>
        /// Use this method to revive gobs whose runtime state you have deserialised.
        /// This method will create the gob properly, initialising all fields and then
        /// copying the runtime state fields to the new instance.
        /// <param name="runtimeState">The runtime state from where to initialise the new gob.</param>
        /// <returns>The newly created gob.</returns>
        /// <seealso cref="CreateGob(Gob, Action&lt;Gob&gt;)"/>
        private static Gob CreateGob(Gob runtimeState)
        {
            Gob gob = (Gob)Clonable.Instantiate(runtimeState.TypeName);
            if (runtimeState.GetType() != gob.GetType())
                throw new ArgumentException("Runtime gob of class " + runtimeState.GetType().Name +
                    " has type name \"" + runtimeState.TypeName + "\" which is for class " + gob.GetType().Name);
            gob.SetRuntimeState(runtimeState);
            return gob;
        }

        /// <summary>
        /// Creates a new gob from the given runtime state and performs a given initialisation on it.
        /// This method is for game logic; gob init is skipped appropriately on clients.
        /// </summary>
        /// Use this method to revive gobs whose runtime state you have deserialised.
        /// This method will create the gob properly, initialising all fields and then
        /// copying the runtime state fields to the new instance.
        /// 
        /// In order for a call to this method to be meaningful, <c>init</c>
        /// should contain a call to <c>DataEngine.AddGob</c> or similar method.
        /// <param name="runtimeState">The runtime state from where to initialise the new gob.</param>
        /// <returns>The newly created gob.</returns>
        /// <param name="init">Initialisation to perform on the gob.</param>
        /// <seealso cref="CreateGob(Gob)"/>
        public static void CreateGob(AssaultWingCore game, Gob runtimeState, Action<Gob> init)
        {
            var gob = CreateGob(runtimeState);
            gob.Game = game;
            if (game.NetworkMode != NetworkMode.Client || !gob.IsRelevant)
                init(gob);
        }

        #endregion Gob constructors and static constructor-like methods

        #region Methods related to gobs' functionality in the game world

        /// <summary>
        /// Called when graphics resources need to be loaded.
        /// </summary>
        public virtual void LoadContent()
        {
            Model = Game.Content.Load<Model>(ModelName);
        }

        /// <summary>
        /// Called when graphics resources need to be unloaded.
        /// </summary>
        public virtual void UnloadContent()
        {
        }

        /// <summary>
        /// Activates the gob, i.e. performs an initialisation rite.
        /// </summary>
        /// DataEngine will call this method to make the gob do necessary 
        /// initialisations to make it fully functional on addition to 
        /// an ongoing play of the game.
        public virtual void Activate()
        {
            EnsureHasID();
            _birthTime = Arena.TotalTime;
            LastNetworkUpdate = Arena.TotalTime;
            IsVisible = true;
            LoadContent();
            if (Arena.IsForPlaying)
            {
                TransformUnmovableCollisionAreas(_collisionAreas);
                CreateBirthGobs();
                CreateModelBirthGobs();
                CreateExhaustEngines();
            }

            foreach (var mesh in Model.Meshes)
                foreach (BasicEffect be in mesh.Effects)
                    Arena.PrepareEffect(be);

            // Create draw bounding volume
            VertexPositionNormalTexture[] vertexData;
            short[] indexData;
            Graphics3D.GetModelData(Model, out vertexData, out indexData);
            _drawBounds = BoundingSphere.CreateFromPoints(vertexData.Select(v => v.Position * _scale));
        }

        /// <summary>
        /// Updates the gob according to physical laws.
        /// </summary>
        /// Overriden Update methods should explicitly call this method in order to have 
        /// physical laws apply to the gob and the gob's exhaust engines updated.
        public virtual void Update()
        {
            Arena.Move(this, Game.TargetElapsedTime, allowIrreversibleSideEffects: Game.NetworkMode != NetworkMode.Client);
            DrawPosOffset *= 0.95f; // reduces the offset to less than 5 % in 60 updates
            DrawRotationOffset = DampDrawRotationOffset(DrawRotationOffset);
        }

        public static float DampDrawRotationOffset(float drawRotationOffset)
        {
            // Reduce large rotation offsets in somewhat constant steps but small offsets
            // in smaller and smaller steps.
            // If x_n are within [0,1], the formula is x_{n+1} = ((x_n + 3)^2 - 9) / 8
            float sign = Math.Sign(drawRotationOffset);
            float temp = sign * drawRotationOffset / ROTATION_SMOOTHING_CUTOFF + 3;
            return sign * ROTATION_SMOOTHING_CUTOFF * (temp * temp - 9) / 8;
        }

        /// <summary>
        /// Kills the gob, i.e. performs a death ritual and removes the gob from the game world.
        /// </summary>
        /// Call this method to make the gob die like it would during normal gameplay.
        /// Alternatively, if you want to just make the gob disappear, you can simply
        /// remove it from the game data. But this might be a bad idea later on if gobs
        /// refer to each other.
        /// Overriding methods should not do anything if the property <b>Dead</b> is true.
        /// <param name="cause">The cause of death.</param>
        public void Die(BoundDamageInfo info)
        {
            if (Dead) return;
            if (Game.NetworkMode == NetworkMode.Client && IsRelevant) return;
            DieImpl(new Coroner(info), false);
        }

        public void Die()
        {
            Die(DamageInfo.Unspecified.Bind(this, Arena.TotalTime));
        }

        /// <summary>
        /// Kills the gob, i.e. performs a death ritual and removes the gob from the game world.
        /// Compared to <see cref="Die(BoundDamageInfo)"/>, this method forces death on game clients.
        /// Therefore this method is to be called only when interpreting the game server's kill
        /// messages.
        /// </summary>
        /// <seealso cref="Die(BoundDamageInfo)"/>
        public void DieOnClient()
        {
            var coroner = new Coroner(DamageInfo.Unspecified.Bind(this, Arena.TotalTime));
            DieImpl(coroner, true);
        }

        /// <summary>
        /// Releases all resources allocated by the gob.
        /// </summary>
        public virtual void Dispose()
        {
            UnloadContent();
            IsDisposed = true;
            if (ID > 0)
                g_unusedRelevantIDs.Enqueue(ID);
            else if (ID < 0)
                g_unusedIrrelevantIDs.Enqueue(ID);
        }

        public virtual void Draw(Matrix view, Matrix projection)
        {
            float bleachFactor = GetBleach();
            if (bleachFactor > 0.01f)
                ModelRenderer.DrawBleached(Model, WorldMatrix, view, projection, ModelPartTransforms, bleachFactor);
            else if (Alpha < 1)
                ModelRenderer.DrawTransparent(Model, WorldMatrix, view, projection, ModelPartTransforms, Alpha);
            else
                ModelRenderer.Draw(Model, WorldMatrix, view, projection, ModelPartTransforms);
            if (IsHiding && Owner != null && !Owner.IsRemote)
            {
                var outlineAlpha = Alpha < HIDING_ALPHA_LIMIT ? 1 : Math.Max(0, 0.3f - 2 * Alpha);
                ModelRenderer.DrawOutlineTransparent(Model, WorldMatrix, view, projection, ModelPartTransforms, outlineAlpha);
            }
        }

        /// <summary>
        /// Draws the gob's 2D graphics.
        /// </summary>
        /// Assumes that the sprite batch has been Begun already and will be
        /// Ended later by someone else.
        /// <param name="gameToScreen">Transformation from game coordinates 
        /// to screen coordinates (pixels).</param>
        /// <param name="spriteBatch">The sprite batch to draw sprites with.</param>
        /// <param name="scale">Scale of graphics.</param>
        public virtual void Draw2D(Matrix gameToScreen, SpriteBatch spriteBatch, float scale)
        {
            // No 2D graphics by default.
        }

        #endregion Methods related to gobs' functionality in the game world

        #region Methods related to serialisation

        /// <summary>
        /// Copies the gob's runtime state from another gob.
        /// </summary>
        /// All value type fields (also those declared in Gob subclasses) that are 
        /// marked with RuntimeStateAttribute are automatically copied.
        /// Subclasses overriding this method must also call their base class' method.
        /// <param name="runtimeState">The gob whose runtime state to imitate.</param>
        protected virtual void SetRuntimeState(Gob runtimeState)
        {
            var fields = Serialization.GetFields(GetType(), typeof(RuntimeStateAttribute), null);
            foreach (var field in fields)
            {
                var value = field.GetValue(runtimeState);
                var cloneableValue = value as ICloneable;
                if (cloneableValue != null)
                    field.SetValue(this, cloneableValue.Clone());
                else
                    field.SetValue(this, value);
            }
        }

        /// <summary>
        /// Serialises the gob to a binary writer.
        /// </summary>
        /// Subclasses should call the base implementation
        /// before performing their own serialisation.
        /// <param name="writer">The writer where to write the serialised data.</param>
        /// <param name="mode">Which parts of the gob to serialise.</param>
        public virtual void Serialize(NetworkBinaryWriter writer, SerializationModeFlags mode)
        {
#if NETWORK_PROFILING
            using (new NetworkProfilingScope("GobBase"))
#endif
            {
                checked
                {
                    if ((mode & SerializationModeFlags.ConstantData) != 0)
                    {
                        writer.Write((short)ID);
                        byte flags = 0;
                        if (StaticID != 0) flags |= (byte)0x01;
                        writer.Write((byte)flags);
                        if (StaticID != 0) writer.Write((short)StaticID);
                        if (Owner != null)
                            writer.Write((sbyte)Owner.ID);
                        else
                            writer.Write((sbyte)Spectator.UNINITIALIZED_ID);
                    }
                    if ((mode & SerializationModeFlags.VaryingData) != 0)
                    {
                        var fullUpdate = true; // UNDONE because of bugs !!! _nextFullNetworkUdpateTime <= Game.GameTime.TotalGameTime;
                        byte rotationAndFlags = unchecked((byte)(((int)Math.Round(_rotation / MathHelper.TwoPi * 128)) & 0x7f));
                        if (fullUpdate)
                        {
                            rotationAndFlags |= 0x80;
                            _nextFullNetworkUdpateTime = Game.GameTime.TotalGameTime + FULL_NETWORK_UPDATE_INTERVAL;
                            writer.Write((byte)rotationAndFlags);
                            writer.WriteNormalized16((Vector2)_pos, MIN_GOB_COORDINATE, MAX_GOB_COORDINATE);
                        }
                        else
                        {
                            writer.Write((byte)rotationAndFlags);
                            writer.WriteNormalized8((Vector2)(_pos - _lastNetworkUpdatePos), MIN_GOB_DELTA_COORDINATE, MAX_GOB_DELTA_COORDINATE);
                            _lastNetworkUpdatePos = _pos;
                        }
                        writer.WriteHalf((Vector2)Move);
                        if (IsDamageable) writer.Write((byte)(byte.MaxValue * DamageLevel / MaxDamageLevel));
                    }
                }
            }
        }

        /// <summary>
        /// Deserialises the gob from a binary writer.
        /// </summary>
        /// Subclasses should call the base implementation
        /// before performing their own deserialisation.
        /// <param name="reader">The reader where to read the serialised data.</param>
        /// <param name="mode">Which parts of the gob to deserialise.</param>
        public virtual void Deserialize(NetworkBinaryReader reader, SerializationModeFlags mode, int framesAgo)
        {
            if ((mode & SerializationModeFlags.ConstantData) != 0)
            {
                ID = reader.ReadInt16();
                byte flags = reader.ReadByte();
                if ((flags & 0x01) != 0) StaticID = reader.ReadInt16();
                int ownerID = reader.ReadSByte();
                OwnerProxy = new LazyProxy<int, Player>(FindPlayer);
                OwnerProxy.SetData(ownerID);
            }
            if ((mode & SerializationModeFlags.VaryingData) != 0)
            {
                var oldRotation = _rotation;
                byte rotationAndFlags = reader.ReadByte();
                var fullUpdate = (rotationAndFlags & 0x80) != 0;
                _rotation = (rotationAndFlags & 0x7f) * MathHelper.TwoPi / 128;
                DrawRotationOffset = AWMathHelper.GetAbsoluteMinimalEqualAngle(DrawRotationOffset + oldRotation - _rotation);
                if (float.IsNaN(DrawRotationOffset) || Math.Abs(DrawRotationOffset) > ROTATION_SMOOTHING_CUTOFF)
                    DrawRotationOffset = 0;

                var oldPos = _pos;
                var newPos = fullUpdate
                    ? reader.ReadVector2Normalized16(MIN_GOB_COORDINATE, MAX_GOB_COORDINATE)
                    : _lastNetworkUpdatePos + reader.ReadVector2Normalized8(MIN_GOB_DELTA_COORDINATE, MAX_GOB_DELTA_COORDINATE);
                _lastNetworkUpdatePos = newPos;
                var newMove = reader.ReadHalfVector2();
                ExtrapolatePosAndMove(newPos, newMove, framesAgo);
                DrawPosOffset += oldPos - _pos;
                if (float.IsNaN(DrawPosOffset.X) || DrawPosOffset.LengthSquared() > POS_SMOOTHING_CUTOFF * POS_SMOOTHING_CUTOFF)
                    DrawPosOffset = Vector2.Zero;

                if (IsDamageable) DamageLevel = reader.ReadByte() / (float)byte.MaxValue * MaxDamageLevel;
            }
        }

        /// <summary>
        /// Sets the gob's position and movement by computing it from a known position
        /// and movement some time ago.
        /// </summary>
        public void ExtrapolatePosAndMove(Vector2 oldPos, Vector2 oldMove, int frameCount)
        {
            _pos = oldPos;
            Move = oldMove;
            if (Arena != null) Arena.Move(this, frameCount, allowIrreversibleSideEffects: false);
        }

        #endregion Methods related to serialisation

        #region Gob public methods

        /// <summary>
        /// Returns the game world location of a named position on the gob's 3D model.
        /// </summary>
        /// Named positions are defined in the gob's 3D model by specially named
        /// ModelBones (a.k.a. Frames in the X file). An external object that is
        /// positioned at a named place on the ship -- such as a weapon -- is 
        /// given a bone index at its creation, and passing that index to this method
        /// the external object can find out where in the game world it is located.
        /// <see cref="GetNamedPositions(string)"/>
        /// <param name="boneIndex">The bone index of the named position.</param>
        /// <returns>The game world location of the named position.</returns>
        /// <seealso cref="GetBoneRotation(int)"/>
        public Vector2 GetNamedPosition(int boneIndex)
        {
            return Vector2.Transform(Vector2.Zero, ModelPartTransforms[boneIndex] * WorldMatrix);
        }

        /// <summary>
        /// Returns the game world rotation of a named position on the gob's 3D model.
        /// </summary>
        /// <seealso cref="GetNamedPosition(int)"/>
        public float GetBoneRotation(int boneIndex)
        {
            var transformed = Vector2.TransformNormal(Vector2.UnitX, ModelPartTransforms[boneIndex]);
            float boneRotation = transformed.Angle();
            return Rotation + DrawRotationOffset + boneRotation;
        }

        /// <summary>
        /// Returns a list of named positions in the gob's 3D model with bone indices
        /// for later calls to <b>GetNamedPosition(int)</b>.
        /// </summary>
        /// <see cref="GetNamedPosition(int)"/>
        /// <param name="namePrefix">Prefix for names of positions to return.</param>
        /// <returns>A list of position names and bone indices in the gob's 3D model.</returns>
        public IEnumerable<Tuple<string, int>> GetNamedPositions(string namePrefix)
        {
            return
                from bone in Model.Bones
                let name = bone.Name
                where name != null && name.StartsWith(namePrefix)
                select Tuple.Create(name, bone.Index);
        }

        public int GetCollisionAreaID(CollisionArea area)
        {
            return Array.IndexOf(_collisionAreas, area);
        }

        public CollisionArea GetCollisionArea(int areaID)
        {
            if (areaID < 0 || areaID >= _collisionAreas.Length) return null;
            return _collisionAreas[areaID];
        }

        /// <summary>
        /// Makes the gob forget its collision areas. Doesn't unregister the collision
        /// areas from <c>PhysicsEngine</c>.
        /// </summary>
        /// This method is to be called only before the gob has been registered
        /// to <c>PhysicsEngine</c>. This method is only a hack to allow putting
        /// any gob in a background arena layer.
        public void ClearCollisionAreas()
        {
            _collisionAreas = new CollisionArea[0];
        }

        public void Enable()
        {
            if (_disabledCount == 0) throw new InvalidOperationException("Cannot enable a gob that is already enabled");
            --_disabledCount;
        }

        public void Disable()
        {
            ++_disabledCount;
        }

        public override string ToString()
        {
            return string.Format("{0} {1}", TypeName, ID);
        }

        #endregion Gob public methods

        #region Gob methods related to thrusters

        /// <summary>
        /// Creates exhaust engines for the gob in all thrusters named in its 3D model.
        /// </summary>
        /// A subclass should call this method on <b>Activate</b> 
        /// if it wants to have visible thrusters,
        /// and then do final adjustments on the newly created <b>exhaustEngines</b>.
        /// The subclass should regularly <b>Update</b> its <b>exhaustEngines</b>.
        private void CreateExhaustEngines()
        {
            var boneIndices = GetNamedPositions("Thruster");
            int templates = _exhaustEngineNames.Length;
            var exhaustBoneIList = new List<int>();
            var exhaustEngineList = new List<Gob>();
            foreach (var boneIndex in boneIndices)
                for (int tempI = 0; tempI < templates; ++tempI)
                    Gob.CreateGob<Gobs.Peng>(Game, _exhaustEngineNames[tempI], gob =>
                    {
                        gob.Leader = this;
                        gob.LeaderBone = boneIndex.Item2;
                        Arena.Gobs.Add(gob);
                        exhaustBoneIList.Add(boneIndex.Item2);
                        exhaustEngineList.Add(gob);
                    });
            _exhaustEngines = exhaustEngineList.ToArray();
        }

        #endregion Gob methods related to thrusters

        #region Gob miscellaneous protected methods

        protected Tuple<bool, Gob> FindGob(int id)
        {
            var gob = id == Gob.INVALID_ID || Arena == null
                ? null
                : Arena.Gobs.FirstOrDefault(g => g.ID == id);
            return Tuple.Create(gob != null, gob);
        }

        protected virtual void SetExhaustEffectsEnabled(bool active)
        {
            if (active == _exhaustEffectsEnabled) return;
            _exhaustEffectsEnabled = active;
            foreach (var exhaustEngine in _exhaustEngines.OfType<Gobs.Peng>())
                if (active)
                    exhaustEngine.Emitter.Resume();
                else
                    exhaustEngine.Emitter.Pause();
        }

        /// <summary>
        /// Copies a transform of each bone in a model relative to all parent bones of the bone into a given array.
        /// </summary>
        protected virtual void CopyAbsoluteBoneTransformsTo(Model model, Matrix[] transforms)
        {
            model.CopyAbsoluteBoneTransformsTo(transforms);
        }

        #endregion Gob miscellaneous protected methods

        #region Collision methods

        /// <summary>
        /// The primary physical collision area of the gob or <b>null</b> if it doesn't have one.
        /// </summary>
        /// The primary physical collision area is used mostly for movable gobs as an
        /// optimisation to avoid looping through all collision areas in order to find
        /// physical ones.
        public CollisionArea PhysicalArea
        {
            get
            {
                if (_collisionAreas.Length == 0) return null;
                if ((_collisionAreas[0].Type & CollisionAreaType.Physical) == 0) return null;
                return _collisionAreas[0];
            }
        }

        /// <summary>
        /// Performs collision operations for the case when one of this gob's collision areas
        /// is overlapping one of another gob's collision areas. Returns the kinds of side
        /// effects that were performed.
        /// </summary>
        /// Called only when <b>theirArea.Type</b> matches either <b>myArea.CollidesAgainst</b> or
        /// <b>myArea.CannotOverlap</b>.
        /// <param name="myArea">The collision area of this gob.</param>
        /// <param name="theirArea">The collision area of the other gob.</param>
        /// <param name="stuck">If <b>true</b> then the gob is stuck, i.e.
        /// <b>theirArea.Type</b> matches <b>myArea.CannotOverlap</b> and it's not possible
        /// to backtrack out of the overlap. It is then up to this gob and the other gob 
        /// to resolve the overlap.</param>
        public virtual Arena.CollisionSideEffectType Collide(CollisionArea myArea, CollisionArea theirArea, bool stuck, Arena.CollisionSideEffectType sideEffectTypes)
        {
            return Arena.CollisionSideEffectType.None;
        }

        #endregion Collision methods

        #region Damage methods

        public event Action<Coroner> Death;

        /// <summary>
        /// The amount of damage, between 0 and <b>MaxDamageLevel</b>.
        /// 0 means the entity is in perfect condition;
        /// <b>MaxDamageLevel</b> means the entity is totally destroyed.
        /// </summary>
        public float DamageLevel { get; set; }

        /// <summary>
        /// The maximum amount of damage the entity can sustain.
        /// </summary>
        public float MaxDamageLevel { get { return _maxDamage; } set { _maxDamage = value; } }

        /// <summary>
        /// Optional filter for processing damage from physical collisions (not explosions
        /// and other active weapon damage). Parameter is damage about to be inflicted.
        /// Return value is the remaining filtered damage.
        /// </summary>
        public Func<float, float> CollisionDamageFilter { get; set; }

        /// <summary>
        /// Called when the gob experiences a physical collision into another gob.
        /// </summary>
        public virtual void PhysicalCollisionInto(Gob other, Vector2 moveDelta, float damageMultiplier)
        {
            float collisionDamage = 0.0003f * damageMultiplier * CollisionDamageToOthersMultiplier * moveDelta.Length() * other.Mass;
            other.InflictCollisionDamage(collisionDamage, new DamageInfo(this));
        }

        public virtual void InflictCollisionDamage(float damageAmount, DamageInfo info)
        {
            if (damageAmount < 0) throw new ArgumentOutOfRangeException("damageAmount");
            if (CollisionDamageFilter != null)
                damageAmount = CollisionDamageFilter(damageAmount);
            if (damageAmount > 0)
                InflictDamage(damageAmount, info);
        }

        public virtual void InflictDamage(float damageAmount, DamageInfo info)
        {
            if (damageAmount < 0) throw new ArgumentOutOfRangeException("damageAmount");
            if (damageAmount == 0) return;
            var boundInfo = info.Bind(this, Arena.TotalTime);
            if (boundInfo.SourceType == BoundDamageInfo.SourceTypeType.EnemyPlayer)
            {
                LastDamager = boundInfo.PlayerCause;
                LastDamagerTimeout = Arena.TotalTime + TimeSpan.FromSeconds(6);
            }
            DamageLevel = Math.Min(_maxDamage, DamageLevel + damageAmount);
            _bleachDamage += damageAmount;
            if (DamageLevel == _maxDamage) Die(boundInfo);
        }

        public void RepairDamage(float repairAmount)
        {
            if (repairAmount < 0) throw new ArgumentOutOfRangeException("repairAmount");
            DamageLevel = Math.Max(0, DamageLevel - repairAmount);
        }

        #endregion Damage methods

        #region Private methods

        private Tuple<bool, Player> FindPlayer(int id)
        {
            if (id == Spectator.UNINITIALIZED_ID) return Tuple.Create(false, (Player)null);
            var player = Game.DataEngine.Players.FirstOrDefault(p => p.ID == id);
            return Tuple.Create(player != null, player);
        }

        /// <summary>
        /// Creates birth gobs for the gob.
        /// </summary>
        private void CreateBirthGobs()
        {
            foreach (var gobType in _birthGobTypes)
            {
                CreateGob<Gob>(Game, gobType, gob =>
                {
                    gob.ResetPos(Pos, Vector2.Zero, Rotation);
                    var peng = gob as Gobs.Peng;
                    if (peng != null)
                    {
                        peng.Owner = Owner;
                        peng.Leader = this;
                    }
                    Arena.Gobs.Add(gob);
                });
            }
        }

        /// <summary>
        /// Creates birth gobs for the gob from specially named meshes in the gob's 3D model.
        /// </summary>
        private void CreateModelBirthGobs()
        {
            var poses = GetNamedPositions("Peng_");
            foreach (var pos in poses)
            {
                // We expect 3D model bones named like "Peng_blinker_1", where
                // "Peng" is a special marker,
                // "blinker" is the typename of the Peng to create,
                // "1" is an optional number used only to make such bone names unique.
                string[] tokens = pos.Item1.Split('_');
                if (tokens.Length < 2 || tokens.Length > 3) throw new ApplicationException("Invalid birth gob definition " + pos.Item1 + " in 3D model " + _modelName);
                Gob.CreateGob<Gobs.Peng>(Game, (CanonicalString)tokens[1], gob =>
                {
                    gob.Leader = this;
                    gob.LeaderBone = pos.Item2;
                    Arena.Gobs.Add(gob);
                });
            }
        }

        /// <summary>
        /// Pretransforms the gob's collision areas if the gob is unmovable.
        /// </summary>
        public void TransformUnmovableCollisionAreas(IEnumerable<CollisionArea> collisionAreas)
        {
            if (Movable) return;
            foreach (var area in collisionAreas)
            {
                if (area.Owner != this) throw new ApplicationException("Trying to transform area to a non-owner");
                area.AreaGob = area.AreaGob.Transform(WorldMatrix);
            }
        }

        public void SelfDestruct(CanonicalString[] deathGobTypes)
        {
            this._deathGobTypes = deathGobTypes;
        }

        /// <param name="forceRemove">Force removal of the dead gob. Useful for clients.</param>
        private void DieImpl(Coroner coroner, bool forceRemove)
        {
            if (Dead) throw new InvalidOperationException("Gob is already dead");
            _dead = true;
            if (Death != null) Death(coroner);
            Arena.Gobs.Remove(this, forceRemove);
            foreach (var gobType in _deathGobTypes)
                CreateGob<Gob>(Game, gobType, gob =>
                {
                    gob.ResetPos(Pos, Vector2.Zero, Rotation);
                    gob.Owner = Owner;
                    Arena.Gobs.Add(gob);
                });
        }

        /// <summary>
        /// Returns the amount of bleach to use when drawing the gob's 3D model, between 0 and 1.
        /// </summary>
        /// A bleach of 0 means the 3D model looks normal. 
        /// A bleach of 1 means the 3D model is drawn totally white.
        /// Anything in between states the amount of blend from the
        /// unbleached 3D model towards the totally white 3D model.
        private float GetBleach()
        {
            if (BleachValue.HasValue) return BleachValue.Value;

            // Reset bleach if it's getting old.
            if (Arena.TotalTime >= _bleachResetTime)
                _previousBleach = 0;

            // Set new bleach based on accumulated damage during this frame.
            if (_bleachDamage > 0)
            {
                float newBleach = g_bleachCurve.Evaluate(_bleachDamage);
                if (newBleach > _previousBleach)
                {
                    _previousBleach = newBleach;
                    _bleachResetTime = Arena.TotalTime + TimeSpan.FromSeconds(0.055);
                }
                _bleachDamage = 0;
            }

            return _previousBleach;
        }

        private void EnsureHasID()
        {
            if (ID != INVALID_ID) return;
            ID = Game.NetworkMode == NetworkMode.Client
                ? g_unusedIrrelevantIDs.Dequeue()
                : g_unusedRelevantIDs.Dequeue();
        }

        #endregion

        #region IConsistencyCheckable and Clonable Members

        /// <summary>
        /// Called on a cloned object after the cloning.
        /// </summary>
        public override void Cloned()
        {
            foreach (var area in _collisionAreas) area.Owner = this;
        }

        /// <summary>
        /// Makes the instance consistent in respect of fields marked with a
        /// limitation attribute.
        /// </summary>
        /// <param name="limitationAttribute">Check only fields marked with 
        /// this limitation attribute.</param>
        /// <see cref="Serialization"/>
        public virtual void MakeConsistent(Type limitationAttribute)
        {
            if (limitationAttribute == typeof(TypeParameterAttribute))
            {
                // Rearrange our collision areas to have a physical area be first, if there is such,
                // and all other collision areas to be sorted in increasing order by name.
                // Order is important because game server and game clients communicate in indices.
                _collisionAreas = _collisionAreas.OrderBy(area => (area.Type & CollisionAreaType.Physical) != 0 ? "" : area.Name).ToArray();

                // Make physical attributes sensible.
                _mass = Math.Max(0.001f, _mass); // strictly positive mass
            }
        }

        #endregion

        #region ICustomTypeDescriptor Members

        public AttributeCollection GetAttributes()
        {
            return TypeDescriptor.GetAttributes(this);
        }

        public string GetClassName()
        {
            return TypeDescriptor.GetClassName(this);
        }

        public string GetComponentName()
        {
            return TypeDescriptor.GetComponentName(this);
        }

        public TypeConverter GetConverter()
        {
            return TypeDescriptor.GetConverter(this);
        }

        public EventDescriptor GetDefaultEvent()
        {
            return TypeDescriptor.GetDefaultEvent(this);
        }

        public PropertyDescriptor GetDefaultProperty()
        {
            return TypeDescriptor.GetDefaultProperty(this);
        }

        public object GetEditor(Type editorBaseType)
        {
            return TypeDescriptor.GetEditor(this, editorBaseType);
        }

        public EventDescriptorCollection GetEvents(Attribute[] attributes)
        {
            return TypeDescriptor.GetEvents(this, attributes);
        }

        public EventDescriptorCollection GetEvents()
        {
            return TypeDescriptor.GetEvents(this);
        }

        public PropertyDescriptorCollection GetProperties(Attribute[] attributes)
        {
            throw new NotImplementedException();
        }

        public PropertyDescriptorCollection GetProperties()
        {
            var props = Serialization.GetFields(GetType(), typeof(RuntimeStateAttribute), null)
                .Select(m => new GobPropertyDescriptor(m));
            return new PropertyDescriptorCollection(props.ToArray());
        }

        public object GetPropertyOwner(PropertyDescriptor pd)
        {
            return this;
        }

        #endregion ICustomTypeDescriptor Members
    }
}
