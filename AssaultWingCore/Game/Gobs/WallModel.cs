using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using AW2.Helpers;
using AW2.Helpers.Serialization;

namespace AW2.Game.Gobs
{
    /// <summary>
    /// A piece of wall initialised from a 3D model.
    /// </summary>
    /// Note that a wall has no position or movement like other gobs have. 
    /// Instead, a wall acts like a polygon. For visual purposes, walls have 
    /// also a third dimension.
    /// <see cref="AW2.Game.Gobs.Wall"/>
    public class WallModel : Wall
    {
        #region WallModel Fields

        /// <summary>
        /// The name of the 3D model to draw the wall with.
        /// </summary>
        /// Note: This field overrides the type parameter Gob.modelName.
        [RuntimeState]
        private CanonicalString wallModelName;

        #endregion // WallModel Fields

        #region WallModel Properties

        /// <summary>
        /// Names of all models that this gob type will ever use.
        /// </summary>
        public override IEnumerable<CanonicalString> ModelNames
        {
            get { return base.ModelNames.Union(new CanonicalString[] { wallModelName }); }
        }

        #endregion // WallModel Properties

        /// <summary>
        /// Creates an uninitialised piece of wall.
        /// </summary>
        /// This constructor is only for serialisation.
        public WallModel()
        {
            wallModelName = (CanonicalString)"dummymodel";
        }

        /// <summary>
        /// Creates a piece of wall.
        /// </summary>
        /// <param name="typeName">The type of the wall.</param>
        public WallModel(CanonicalString typeName)
            : base(typeName)
        {
            wallModelName = (CanonicalString)"dummymodel";
        }

        #region Methods related to gobs' functionality in the game world

        /// <summary>
        /// Called when graphics resources need to be loaded.
        /// </summary>
        public override void LoadContent()
        {
            base.LoadContent();
            // Replace defaults set by Wall.
            Set3DModel();
        }

        /// <summary>
        /// Activates the gob, i.e. performs an initialisation rite.
        /// </summary>
        /// DataEngine will call this method to make the gob do necessary 
        /// initialisations to make it fully functional on addition to 
        /// an ongoing play of the game.
        public override void Activate()
        {
            if (Arena.IsForPlaying)
                Set3DModel();
            else
                ModelName = wallModelName;
            base.Activate();
        }

        #endregion Methods related to gobs' functionality in the game world

        #region Methods related to serialisation

        /// <summary>
        /// Serialises the gob for to a binary writer.
        /// </summary>
        /// <param name="writer">The writer where to write the serialised data.</param>
        /// <param name="mode">Which parts of the gob to serialise.</param>
        public override void Serialize(NetworkBinaryWriter writer, SerializationModeFlags mode)
        {
            base.Serialize(writer, mode);
            if ((mode & SerializationModeFlags.ConstantData) != 0)
            {
                writer.Write((CanonicalString)wallModelName);
            }
        }

        /// <summary>
        /// Deserialises the gob from a binary writer.
        /// </summary>
        /// <param name="reader">The reader where to read the serialised data.</param>
        /// <param name="mode">Which parts of the gob to deserialise.</param>
        public override void Deserialize(NetworkBinaryReader reader, SerializationModeFlags mode, int framesAgo)
        {
            base.Deserialize(reader, mode, framesAgo);
            if ((mode & SerializationModeFlags.ConstantData) != 0)
            {
                wallModelName = new CanonicalString(reader.ReadInt32());
                var model = Game.Content.Load<Model>(wallModelName);
                Effect = GetEffect(model);
                Texture = GetTexture(model);
            }
        }

        public override void Cloned()
        {
            wallModelName = ModelName;
            base.Cloned();
        }

        #endregion Methods related to serialisation

        /// <summary>
        /// Sets the wall's 3D model based on 'wallModelName'.
        /// </summary>
        private void Set3DModel()
        {
            // Recover wall data from its 3D model.
            var model = Game.Content.Load<Model>(wallModelName);
            VertexPositionNormalTexture[] vertexData;
            short[] indexData;
            Graphics3D.GetModelData(model, out vertexData, out indexData);
            var worldMatrix = AWMathHelper.CreateWorldMatrix(Scale, Rotation, Pos);
            for (int i = 0; i < vertexData.Length; ++i)
            {
                vertexData[i].Position = Vector3.Transform(vertexData[i].Position, worldMatrix);
                vertexData[i].Normal = Vector3.TransformNormal(vertexData[i].Normal, worldMatrix);
            }
            var effect = GetEffect(model);
            Set3DModel(vertexData, indexData, effect.Texture, effect);
        }

        private static BasicEffect GetEffect(Model model)
        {
            if (model.Meshes.Count > 1)
                throw new ArgumentOutOfRangeException("Model has more than one mesh");
            if (model.Meshes[0].Effects.Count > 1)
                throw new ArgumentOutOfRangeException("Model mesh has more than one effect");
            var effect = model.Meshes[0].Effects[0] as BasicEffect;
            if (effect == null)
                throw new ArgumentException("Model mesh's effect isn't a BasicEffect");
            return effect;
        }

        private static Texture2D GetTexture(Model model)
        {
            return GetEffect(model).Texture;
        }

        #region IConsistencyCheckable Members

        /// <summary>
        /// Makes the instance consistent in respect of fields marked with a
        /// limitation attribute.
        /// </summary>
        /// <param name="limitationAttribute">Check only fields marked with 
        /// this limitation attribute.</param>
        /// <see cref="Serialization"/>
        public override void MakeConsistent(Type limitationAttribute)
        {
            base.MakeConsistent(limitationAttribute);
            if (limitationAttribute == typeof(TypeParameterAttribute))
            {
                // Make sure there's no null references.

                // 'wallModelName' is actually part of our runtime state,
                // but its value is passed onwards by 'ModelNames' even
                // if we were only a gob template. The real problem is
                // that we don't make a difference between gob templates
                // and actual gob instances (that have a proper runtime state).
                if (wallModelName == null)
                    wallModelName = (CanonicalString)"dummymodel";
            }
            if (limitationAttribute == typeof(RuntimeStateAttribute))
            {
                // Make sure there's no null references.
                if (wallModelName == null)
                    wallModelName = (CanonicalString)"dummymodel";
            }
        }

        #endregion IConsistencyCheckable Members
    }
}