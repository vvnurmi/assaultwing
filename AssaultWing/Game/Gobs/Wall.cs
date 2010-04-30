using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Serialization;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using AW2.Helpers;
using AW2.Helpers.Geometric;
using AW2.Net.Messages;
using Rectangle = AW2.Helpers.Geometric.Rectangle;

namespace AW2.Game.Gobs
{
    /// <summary>
    /// A piece of wall.
    /// </summary>
    /// Note that a wall has no position or movement like other gobs have. 
    /// Instead, a wall acts like a polygon. For visual purposes, walls have 
    /// also a third dimension.
    public class Wall : Gob
    {
        #region Wall Fields

        /// <summary>
        /// The location of the wall's vertices in the game world.
        /// </summary>
        protected VertexPositionNormalTexture[] vertexData;

        /// <summary>
        /// The index data where every consequtive index triplet signifies
        /// one triangle. The indices index 'vertexData'.
        /// </summary>
        protected short[] indexData;

        /// <summary>
        /// Triangle index map of the wall's 3D model in the X-Y plane.
        /// </summary>
        /// If indexMap[y,x] == null then no triangle covers index map point (x,y).
        /// Otherwise indexMap[y,x] is an array of indices n such that 
        /// the triangle that is defined by the 3D model's index map elements 
        /// 3n, 3n+1 and 3n+2 covers the index map point (x,y).
        /// The index map has its own coordinate system that can be obtained from
        /// the 3D model's coordinate system by <b>indexMapTransform</b>.
        int[,][] indexMap;

        /// <summary>
        /// Transformation matrix from wall's 3D model's coordinates to index map coordinates.
        /// </summary>
        Matrix indexMapTransform;

        /// <summary>
        /// Triangle cover counts of the wall's 3D model.
        /// </summary>
        /// Index n corresponds to the triangle defined by the 3D model's
        /// index list members 3n, 3n+1 and 3n+2. A positive cover count signifies
        /// the number of index map points covered by the triangle that still 
        /// need to be deleted before the triangle is erased from the 3D model.
        /// A negative cover count marks a deleted triangle.
        int[] triangleCovers;

        /// <summary>
        /// The number of triangles in the wall's 3D model not yet removed.
        /// </summary>
        protected int TriangleCount { get; set; }

        /// <summary>
        /// The texture to draw the wall's 3D model with.
        /// </summary>
        protected Texture2D Texture { get; set; }

        /// <summary>
        /// The effect for drawing the wall.
        /// </summary>
        protected BasicEffect Effect { get; set; }

        /// <summary>
        /// The effect for drawing the wall as a silhouette.
        /// </summary>
        BasicEffect silhouetteEffect;

        /// <summary>
        /// The default effect for drawing the wall.
        /// </summary>
        static BasicEffect defaultEffect;

        /// <summary>
        /// The default effect for drawing the wall as a silhouette.
        /// </summary>
        static BasicEffect defaultSilhouetteEffect;

        /// <summary>
        /// Effect for drawing data for index maps.
        /// </summary>
        static BasicEffect maskEff;

        VertexDeclaration vertexDeclaration;

        #endregion // Wall Fields

        #region Properties

        /// <summary>
        /// Returns the world matrix of the gob, i.e., the translation from
        /// game object coordinates to game world coordinates.
        /// </summary>
        public override Matrix WorldMatrix
        {
            get
            {
                return Arena.IsForPlaying
                    ? Matrix.Identity
                    : base.WorldMatrix;
            }
        }

        /// <summary>
        /// Bounding volume of the visuals of the gob, in world coordinates.
        /// </summary>
        public override BoundingSphere DrawBounds
        {
            get
            {
                return Arena.IsForPlaying
                    ? drawBounds
                    : base.DrawBounds;
            }
        }

        #endregion Properties

        /// <summary>
        /// Creates an uninitialised piece of wall.
        /// </summary>
        /// This constructor is only for serialisation.
        public Wall()
            : base()
        {
            Set3DModel(new VertexPositionNormalTexture[] 
                {
                    new VertexPositionNormalTexture(new Vector3(0,0,0), -Vector3.UnitX, Vector2.Zero),
                    new VertexPositionNormalTexture(new Vector3(100,0,0), Vector3.UnitX, Vector2.UnitX),
                    new VertexPositionNormalTexture(new Vector3(0,100,0), Vector3.UnitY, Vector2.UnitY),
                },
                new short[] { 0, 1, 2 },
                null, null);
        }

        /// <summary>
        /// Creates a piece of wall.
        /// </summary>
        /// <param name="typeName">The type of the wall.</param>
        public Wall(CanonicalString typeName)
            : base(typeName)
        {
            movable = false;
        }

        #region Methods related to gobs' functionality in the game world

        /// <summary>
        /// Called when graphics resources need to be loaded.
        /// </summary>
        public override void LoadContent()
        {
            GraphicsDevice gfx = AssaultWing.Instance.GraphicsDevice;
            defaultEffect = defaultEffect ?? new BasicEffect(gfx, null);
            defaultSilhouetteEffect = defaultSilhouetteEffect ?? (BasicEffect)defaultEffect.Clone(gfx);
            maskEff = maskEff ?? (BasicEffect)defaultEffect.Clone(gfx);
            silhouetteEffect = defaultSilhouetteEffect;
            vertexDeclaration = vertexDeclaration ?? new VertexDeclaration(gfx, VertexPositionNormalTexture.VertexElements);
            base.LoadContent();
        }

        /// <summary>
        /// Called when graphics resources need to be unloaded.
        /// </summary>
        public override void UnloadContent()
        {
            // Must not dispose 'defaultSilhouetteEffect' because others may be using it.
            // Must not dispose 'silhouetteEffect' because it may refer to 'defaultSilhouetteEffect'.
            // 'texture' will be disposed by the graphics engine.
            // 'effect' is managed by other objects
            silhouetteEffect = null;
            if (defaultEffect != null)
            {
                defaultEffect.Dispose();
                defaultEffect = null;
            }
            if (maskEff != null)
            {
                maskEff.Dispose();
                maskEff = null;
            }
            if (vertexDeclaration != null)
            {
                vertexDeclaration.Dispose();
                vertexDeclaration = null;
            }
            base.UnloadContent();
        }

        /// <summary>
        /// Activates the gob, i.e. performs an initialisation rite.
        /// </summary>
        public override void Activate()
        {
            base.Activate();
            if (Arena.IsForPlaying)
            {
                if (AssaultWing.Instance.NetworkMode != NetworkMode.Client)
                    Prepare3DModel();
                InitializeIndexMap();
                drawBounds = BoundingSphere.CreateFromPoints(vertexData.Select(v => v.Position));
            }
            AssaultWing.Instance.DataEngine.ProgressBar.SubtaskCompleted();
        }

        /// <summary>
        /// Draws the gob's 3D graphics.
        /// </summary>
        /// <param name="view">The view matrix.</param>
        /// <param name="projection">The projection matrix.</param>
        public override void Draw(Matrix view, Matrix projection)
        {
            if (!Arena.IsForPlaying)
            {
                base.Draw(view, projection);
                return;
            }
            GraphicsDevice gfx = AssaultWing.Instance.GraphicsDevice;
            gfx.VertexDeclaration = vertexDeclaration;
            Effect.World = Matrix.Identity;
            Effect.Projection = projection;
            Effect.View = view;
            Effect.Texture = Texture;
            Effect.TextureEnabled = true;
            Arena.PrepareEffect(Effect);
            Effect.Begin();
            foreach (EffectPass pass in Effect.CurrentTechnique.Passes)
            {
                pass.Begin();
                gfx.DrawUserIndexedPrimitives<VertexPositionNormalTexture>(
                    PrimitiveType.TriangleList, vertexData, 0, vertexData.Length, indexData, 0, indexData.Length / 3);
                pass.End();
            }
            Effect.End();
        }

        /// <summary>
        /// Draws the wall as a silhouette.
        /// </summary>
        /// Assumes that the sprite batch has been Begun already and will be
        /// Ended later by someone else.
        /// <param name="view">The view matrix.</param>
        /// <param name="projection">The projection matrix.</param>
        /// <param name="spriteBatch">The sprite batch to draw sprites with.</param>
        public void DrawSilhouette(Matrix view, Matrix projection, SpriteBatch spriteBatch)
        {
            GraphicsDevice gfx = AssaultWing.Instance.GraphicsDevice;
            gfx.VertexDeclaration = vertexDeclaration;
            silhouetteEffect.World = Matrix.Identity;
            silhouetteEffect.Projection = projection;
            silhouetteEffect.View = view;
            silhouetteEffect.Texture = Texture;
            silhouetteEffect.VertexColorEnabled = false;
            silhouetteEffect.LightingEnabled = false;
            silhouetteEffect.TextureEnabled = false;
            silhouetteEffect.FogEnabled = false;
            silhouetteEffect.Begin();
            foreach (EffectPass pass in silhouetteEffect.CurrentTechnique.Passes)
            {
                pass.Begin();
                gfx.DrawUserIndexedPrimitives<VertexPositionNormalTexture>(
                    PrimitiveType.TriangleList, vertexData, 0, vertexData.Length, indexData, 0, indexData.Length / 3);
                pass.End();
            }
            silhouetteEffect.End();
        }

        #endregion Methods related to gobs' functionality in the game world

        /// <summary>
        /// Removes a round area from this wall, i.e. makes a hole.
        /// </summary>
        /// <param name="holePos">Center of the hole, in world coordinates.</param>
        /// <param name="holeRadius">Radius of the hole, in meters.</param>
        public void MakeHole(Vector2 holePos, float holeRadius)
        {
            if (holeRadius <= 0) return;
            if (AssaultWing.Instance.NetworkMode == NetworkMode.Client) return;

            // Eat a round hole.
            Vector2 posInIndexMap = Vector2.Transform(holePos, indexMapTransform);
            int indexMapWidth = indexMap.GetLength(1);
            int indexMapHeight = indexMap.GetLength(0);
            var removeIndices = new List<int>();
            AWMathHelper.FillCircle((int)Math.Round(posInIndexMap.X), (int)Math.Round(posInIndexMap.Y),
                (int)Math.Round(holeRadius), (x, y) =>
            {
                if (x < 0 || y < 0 || x >= indexMapWidth || y >= indexMapHeight) return;
                if (indexMap[y, x] == null) return;
                foreach (int index in indexMap[y, x])
                    if (--triangleCovers[index] == 0)
                        removeIndices.Add(index);
            });
            MakeHole(removeIndices);

            if (AssaultWing.Instance.NetworkMode == NetworkMode.Server && removeIndices.Any())
            {
                var message = new WallHoleMessage { GobId = Id, TriangleIndices = removeIndices };
                AssaultWing.Instance.NetworkEngine.GameClientConnections.Send(message);
            }

            // Remove the wall gob if all its triangles have been removed.
            if (TriangleCount == 0)
                Die(new DeathCause());
        }

        /// <summary>
        /// Removes some triangles from the wall's 3D model.
        /// </summary>
        public void MakeHole(IList<int> triangleIndices)
        {
            foreach (int index in triangleIndices)
            {
                // Replace the triangle in the 3D model with a trivial one.
                indexData[3 * index + 0] = 0;
                indexData[3 * index + 1] = 0;
                indexData[3 * index + 2] = 0;

                Arena.Unregister(collisionAreas[index]);
            }
            TriangleCount -= triangleIndices.Count();
        }

        #region Methods related to serialisation

        /// <summary>
        /// Serialises the gob for to a binary writer.
        /// </summary>
        public override void Serialize(Net.NetworkBinaryWriter writer, Net.SerializationModeFlags mode)
        {
            base.Serialize(writer, mode);
            if ((mode & AW2.Net.SerializationModeFlags.ConstantData) != 0)
            {
                writer.Write((int)vertexData.Length);
                foreach (var vertex in vertexData)
                    writer.WriteHalf((VertexPositionNormalTexture)vertex);
                writer.Write((int)indexData.Length);
                foreach (var index in indexData)
                    writer.Write((short)index);
            }
        }

        /// <summary>
        /// Deserialises the gob from a binary writer.
        /// </summary>
        public override void Deserialize(Net.NetworkBinaryReader reader, Net.SerializationModeFlags mode, TimeSpan messageAge)
        {
            base.Deserialize(reader, mode, messageAge);
            if ((mode & AW2.Net.SerializationModeFlags.ConstantData) != 0)
            {
                int vertexDataLength = reader.ReadInt32();
                vertexData = new VertexPositionNormalTexture[vertexDataLength];
                for (int i = 0; i < vertexDataLength; ++i)
                    vertexData[i] = reader.ReadHalfVertexPositionTextureNormal();
                int indexDataLength = reader.ReadInt32();
                indexData = new short[indexDataLength];
                for (int i = 0; i < indexDataLength; ++i)
                    indexData[i] = reader.ReadInt16();
                CreateCollisionAreas();
            }
        }

        #endregion Methods related to serialisation

        #region Protected methods

        /// <summary>
        /// Sets the wall's 3D model. To be called before the wall is Activate()d.
        /// </summary>
        /// <param name="vertexData">Vertex data of the 3D model.</param>
        /// <param name="indexData">Index data of the 3D model as triangle list.</param>
        /// <param name="texture">Texture of the 3D model.</param>
        /// <param name="effect">Effect of the 3D model.</param>
        protected void Set3DModel(VertexPositionNormalTexture[] vertexData, short[] indexData,
            Texture2D texture, BasicEffect effect)
        {
            this.vertexData = vertexData;
            this.indexData = indexData;
            this.Texture = texture;
            this.Effect = effect;
        }

        #endregion Protected methods

        #region Private methods

        /// <summary>
        /// Fines the wall's 3D model's triangles.
        /// </summary>
        private void FineTriangles()
        {
            VertexPositionNormalTexture[] fineVertexData;
            short[] fineIndexData;
            Graphics3D.FineTriangles(50, vertexData, indexData, out fineVertexData, out fineIndexData);
            indexData = fineIndexData;
            vertexData = fineVertexData;
        }

        /// <summary>
        /// Prepares the wall's 3D model for use in gameplay.
        /// </summary>
        private void Prepare3DModel()
        {
            GraphicsDevice gfx = AssaultWing.Instance.GraphicsDevice;
            silhouetteEffect = Effect == null ? null : (BasicEffect)Effect.Clone(gfx);
            FineTriangles();
            TriangleCount = this.indexData.Length / 3;
            CreateCollisionAreas();
        }

        private void CreateCollisionAreas()
        {
            // Create one collision area for each triangle in the wall's 3D model.
            collisionAreas = new CollisionArea[this.indexData.Length / 3 + 1];
            for (int i = 0; i + 2 < this.indexData.Length; i += 3)
            {
                // Create a physical collision area for this triangle.
                Vector3 v1 = this.vertexData[this.indexData[i + 0]].Position;
                Vector3 v2 = this.vertexData[this.indexData[i + 1]].Position;
                Vector3 v3 = this.vertexData[this.indexData[i + 2]].Position;
                IGeomPrimitive triangleArea = new Triangle(
                    new Vector2(v1.X, v1.Y),
                    new Vector2(v2.X, v2.Y),
                    new Vector2(v3.X, v3.Y));
                collisionAreas[i / 3] = new CollisionArea("General", triangleArea, this,
                    CollisionAreaType.PhysicalWall, CollisionAreaType.None, CollisionAreaType.None, CollisionMaterialType.Rough);
            }

            // Create a collision bounding volume for the whole wall.
            var positions = vertexData.Select(vertex => new Vector2(vertex.Position.X, vertex.Position.Y));
            var min = positions.Aggregate((v1, v2) => Vector2.Min(v1, v2));
            var max = positions.Aggregate((v1, v2) => Vector2.Max(v1, v2));
            var boundingArea = new Rectangle(min, max);
            collisionAreas[collisionAreas.Length - 1] = new CollisionArea("Bounding", boundingArea, this,
                CollisionAreaType.WallBounds, CollisionAreaType.None, CollisionAreaType.None, CollisionMaterialType.Rough);
        }

        /// <summary>
        /// Initialises the wall's index map from the wall's 3D model.
        /// </summary>
        private void InitializeIndexMap()
        {
            var boundingArea = collisionAreas.First(area => area.Name == "Bounding").Area.BoundingBox;
            var modelMin = boundingArea.Min;
            var modelDim = boundingArea.Dimensions;

            // Create an index map for the model.
            // The mask is initialised by a render of the 3D model by the graphics card.
            indexMap = new int[(int)Math.Ceiling(modelDim.Y) + 1, (int)Math.Ceiling(modelDim.X) + 1][];
            indexMapTransform = Matrix.CreateTranslation(-modelMin.X, -modelMin.Y, 0);

            // Create colour-coded vertices for each triangle.
            VertexPositionColor[] colouredVertexData = new VertexPositionColor[indexData.Length];
            for (int indexI = 0; indexI < indexData.Length; ++indexI)
            {
                VertexPositionNormalTexture originalVertex = vertexData[indexData[indexI]];
                Color color = new Color((byte)((indexI / 3) % 256), (byte)((indexI / 3 / 256) % 256), (byte)((indexI / 3 / 256 / 256) % 256));
                colouredVertexData[indexI] = new VertexPositionColor(originalVertex.Position, color);
            }

            // This method is run usually in a background thread -- during arena initialisation.
            // Therefore we have to tell the main draw routines to let us use the device in peace.
            // We break out of the lock regularly to allow others use the device, too.
            GraphicsDevice gfx = AssaultWing.Instance.GraphicsDevice;
            RenderTarget2D maskTarget = null;
            int targetSize = -1;
            lock (gfx)
            {

                // Draw the colour-coded triangles on our own render target for
                // index map initialisation. Render target will be a square with
                // size ('targetSize') a power of two to meet the demands of some
                // graphics devices. If the model dimensions are larger than 
                // 'targetSize', we will have to render the coloured triangles in pieces.
                GraphicsDeviceCapabilities gfxCaps = gfx.GraphicsDeviceCapabilities;
                GraphicsAdapter gfxAdapter = gfx.CreationParameters.Adapter;
                if (!gfxAdapter.CheckDeviceFormat(DeviceType.Hardware, gfx.DisplayMode.Format,
                    TextureUsage.None, QueryUsages.None, ResourceType.RenderTarget, SurfaceFormat.Color))
                    throw new Exception("Cannot create render target of type SurfaceFormat.Color");
                targetSize = Math.Min(
                    AWMathHelper.FloorPowerTwo(Math.Min(gfxCaps.MaxTextureHeight, gfxCaps.MaxTextureWidth)),
                    AWMathHelper.CeilingPowerTwo(Math.Max(indexMap.GetLength(1), indexMap.GetLength(0))));
                while (maskTarget == null)
                    try
                    {
                        maskTarget = new RenderTarget2D(gfx, targetSize, targetSize, 1, SurfaceFormat.Color);
                    }
                    catch (OutOfVideoMemoryException)
                    {
                        targetSize /= 2;
                    }
                    catch (Exception e)
                    {
                        throw new ApplicationException("Cannot create render target for index map creation", e);
                    }
            }

            // Set up the effect.
            maskEff.VertexColorEnabled = true;
            maskEff.LightingEnabled = false;
            maskEff.TextureEnabled = false;
            maskEff.View = Matrix.CreateLookAt(new Vector3(0, 0, 1000), Vector3.Zero, Vector3.Up);
            maskEff.Projection = Matrix.CreateOrthographicOffCenter(0, targetSize - 1,
                0, targetSize - 1, 10, 1000);
            maskEff.World = indexMapTransform;

            // Draw the coloured triangles in as many parts as necessary to cover 
            // the whole model with one unit in world coordinates corresponding to
            // one pixel width in the render target.
            for (int startY = 0; startY < indexMap.GetLength(0); startY += targetSize)
                for (int startX = 0; startX < indexMap.GetLength(1); )
                    try
                    {
                        lock (gfx)
                            ComputeIndexMapFragment(colouredVertexData, maskTarget, targetSize, startY, startX);
                        startX += targetSize;
                        System.Threading.Thread.Sleep(0);
                    }
                    // Some exceptions may be thrown if the graphics card is reset e.g.
                    // by a window resize. Just retry.
                    catch (NullReferenceException) { }
                    catch (InvalidOperationException) { }

            // Initialise triangle cover counts.
            triangleCovers = new int[indexData.Length / 3];
            foreach (int[] indices in indexMap)
                if (indices != null)
                    foreach (int index in indices)
                        ++triangleCovers[index];

            // If some triangle isn't mentioned in the index map, force it there.
            for (int i = 0; i < triangleCovers.Length; ++i)
                if (triangleCovers[i] == 0)
                {
                    Vector3 vert0 = vertexData[indexData[3 * i + 0]].Position;
                    Vector3 vert1 = vertexData[indexData[3 * i + 1]].Position;
                    Vector3 vert2 = vertexData[indexData[3 * i + 2]].Position;
                    Vector3 triangleCenter = (vert0 + vert1 + vert2) / 3;
                    Vector3 centerInIndexMap = Vector3.Transform(triangleCenter, indexMapTransform);
                    int centerInIndexMapX = (int)(Math.Round(centerInIndexMap.X) + 0.1);
                    int centerInIndexMapY = (int)(Math.Round(centerInIndexMap.Y) + 0.1);
                    int[] oldIndices = indexMap[centerInIndexMapY, centerInIndexMapX];
                    int[] newIndices = null;
                    if (oldIndices != null)
                    {
                        newIndices = new int[oldIndices.Length + 1];
                        Array.Copy(oldIndices, newIndices, oldIndices.Length);
                        newIndices[oldIndices.Length] = i;
                    }
                    else
                        newIndices = new int[] { i };
                    indexMap[centerInIndexMapY, centerInIndexMapX] = newIndices;
                    ++triangleCovers[i];
                }
        }

        private void ComputeIndexMapFragment(VertexPositionColor[] colouredVertexData,
            RenderTarget2D maskTarget, int targetSize, int startY, int startX)
        {
            GraphicsDevice gfx = AssaultWing.Instance.GraphicsDevice;

            // Set up graphics device.
            VertexDeclaration oldVertexDeclaration = gfx.VertexDeclaration;
            DepthStencilBuffer oldDepthStencilBuffer = gfx.DepthStencilBuffer;
            gfx.VertexDeclaration = new VertexDeclaration(gfx, VertexPositionColor.VertexElements);
            gfx.DepthStencilBuffer = null;

            // Move view to current start coordinates.
            maskEff.View = Matrix.CreateLookAt(new Vector3(startX, startY, 1000), new Vector3(startX, startY, 0), Vector3.Up);

            // Set and clear our own render target.
            gfx.SetRenderTarget(0, maskTarget);
            gfx.Clear(ClearOptions.Target, Color.White, 0, 0);

            // Draw the coloured triangles.
            maskEff.Begin();
            foreach (EffectPass pass in maskEff.CurrentTechnique.Passes)
            {
                pass.Begin();
                gfx.DrawUserPrimitives<VertexPositionColor>(PrimitiveType.TriangleList,
                    colouredVertexData, 0, colouredVertexData.Length / 3);
                pass.End();
            }
            maskEff.End();

            // Restore render target so what we can extract drawn pixels.
            gfx.SetRenderTarget(0, null);

            // Figure out mask data from the render target.
            Texture2D maskTexture = maskTarget.GetTexture();
            Color[] maskData = new Color[targetSize * targetSize];
            maskTexture.GetData<Color>(maskData);
            for (int y = 0; y < targetSize; ++y)
                for (int x = 0; x < targetSize; ++x)
                {
                    Color color = maskData[x + y * maskTexture.Width];
                    if (color == Color.White) continue;
                    int indexMapY = startY + targetSize - 1 - y;
                    int indexMapX = startX + x;
                    if (indexMapY >= indexMap.GetLength(0) || indexMapX >= indexMap.GetLength(1))
                        throw new IndexOutOfRangeException(string.Format("Index map overflow (x={0}, y={1}), color={2}", indexMapX, indexMapY, color));
                    int maskValue = color.R + color.G * 256 + color.B * 256 * 256;
                    indexMap[indexMapY, indexMapX] = new int[] { maskValue };
                }

            // Restore graphics device's old settings.
            gfx.VertexDeclaration = oldVertexDeclaration;
            gfx.DepthStencilBuffer = oldDepthStencilBuffer;
            maskTarget.Dispose();
        }

        #endregion Private methods
    }
}
