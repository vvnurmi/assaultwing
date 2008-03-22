using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using AW2.Helpers;
using AW2.Events;
using AW2.Game;
using AW2.UI;
using Microsoft.Xna.Framework.Input;

namespace AW2.Graphics
{
    class MenuEngineImpl : DrawableGameComponent
    {
        private float timeSinceLastMove = 0; // TODO: Remove timeSinceLastMove
        //private bool active;
        private int currentMenu = 0;
        private int currentSubMenu = 0;
        private int currentMenuLevel = 0; // 0 = main menu, 1 = submenu
        private SpriteBatch spriteBatch;
        private Texture2D menuTexture;
        private Texture2D bigArrow;
        private float bigArrowLocation;
        private float bigArrowTarget;
        private float bigArrowSpeed;
        private bool bigArrowStopped = true;
        private int bigArrowDir = 0;

        /// <summary>
        /// General controls for moving in the menu. These are in addition
        /// to player controls that also may have a function in parts of the menu.
        /// </summary>
        private Control controlUp, controlDown;

        private Rectangle menuScreenRec;
        private Microsoft.Xna.Framework.Graphics.Viewport menuScreenView;

        private AssaultWing game;

        private float aspectX = 1;
        private float aspectY = 1;
        public MenuEngineImpl(Microsoft.Xna.Framework.Game game)
            : base(game)
        {
            controlUp = new KeyboardKey(Keys.Up);
            controlDown = new KeyboardKey(Keys.Down);
        }

        /// <summary>
        /// </summary>
        public override void Initialize()
        {
            game = (AssaultWing)Game;
            WindowResize();
            spriteBatch = new SpriteBatch(game.GraphicsDevice);

            bigArrowLocation = 110 * aspectY;
            bigArrowSpeed = 6f;
            base.Initialize();
        }

        /// <summary>
        /// Draws menu graphics.
        /// </summary>
        /// <param name="gameTime">
        /// Time passed since the last call to Microsoft.Xna.Framework.DrawableGameComponent.Draw(Microsoft.Xna.Framework.GameTime).
        /// </param>
        public override void Draw(GameTime gameTime)
        {
            game.GraphicsDevice.Viewport = menuScreenView;
            spriteBatch.Begin();
            spriteBatch.Draw(menuTexture, menuScreenRec, Color.White);
            spriteBatch.Draw(bigArrow, new Rectangle((int)Math.Round(220 * aspectX), (int)Math.Round(bigArrowLocation), 
                (int)Math.Round(aspectX * 95), (int)Math.Round(aspectY * 101)), Color.White);
            spriteBatch.End();
        }

        /// <summary>
        /// When menu is active it will consume all keyboard events for moving in menus.
        /// </summary>
        /// <param name="gameTime">Time elapsed since the last call to Microsoft.Xna.Framework.GameComponent.Update(Microsoft.Xna.Framework.GameTime)</param>
        public override void Update(GameTime gameTime)
        {
            timeSinceLastMove += (float)gameTime.ElapsedRealTime.TotalMilliseconds;
            EventEngine eventer = (EventEngine)Game.Services.GetService(typeof(EventEngine));
            DataEngine data = (DataEngine)Game.Services.GetService(typeof(DataEngine));

            bigArrowTarget = (110 + currentMenu * 83) * aspectY;
            if (bigArrowLocation > bigArrowTarget + bigArrowSpeed) bigArrowLocation -= bigArrowSpeed;
            else if (bigArrowLocation < bigArrowTarget - bigArrowSpeed) bigArrowLocation += bigArrowSpeed;

            // Check our controls and react to them.
            bool upDone = false;
            bool downDone = false;
            if (controlUp.Pulse && !upDone)
            {
                upDone = true;
                if (currentMenuLevel == 0)
                    bigArrowDir = -1;
            }
            if (controlDown.Pulse && !downDone)
            {
                downDone = true;
                if (currentMenuLevel == 0)
                    bigArrowDir = 1;
            }
            data.ForEachPlayer(delegate(Player player)
            {
                if (player.Controls[PlayerControlType.Thrust].Pulse && !upDone)
                {
                    upDone = true;
                    if (currentMenuLevel == 0)
                        bigArrowDir = -1;
                }
                if (player.Controls[PlayerControlType.Down].Pulse && !downDone)
                {
                    downDone = true;
                    if (currentMenuLevel == 0)
                        bigArrowDir = 1;
                }
            });
            if (timeSinceLastMove >= 0)
            {
                timeSinceLastMove = 0;
                currentMenu += bigArrowDir;
                bigArrowDir = 0;
                if (currentMenu > 5) currentMenu = 5;
                if (currentMenu < 0) currentMenu = 0;
            }
        }

        /// <summary>
        ///     Called when the component needs to load graphics resources. Override this
        ///     method to load any component-specific graphics resources.
        /// </summary>
        protected override void LoadContent()
        {
            AssaultWing game = (AssaultWing)Game;

            Log.Write("Menu engine loading menu graphics.");
            menuTexture = LoadTexture(game, "menubg");
            bigArrow = LoadTexture(game, "mainarrow");
        }

        private Texture2D LoadTexture(AssaultWing game, string name)
        {
            string textureNamePath = System.IO.Path.Combine("textures", name);
            return game.Content.Load<Texture2D>(textureNamePath);
        }

        /// <summary>
        ///     Called when graphics resources should be unloaded. 
        ///     Handle component-specific graphics resources.
        /// </summary>
        protected override void UnloadContent()
        {
            menuTexture.Dispose();
            bigArrow.Dispose();
            base.UnloadContent();
        }

        /// <summary>
        /// Reacts to a system window resize.
        /// </summary>
        /// This method should be called after the window size changes in windowed mode,
        /// or after the screen resolution changes in fullscreen mode,
        /// or after switching between windowed and fullscreen mode.
        public void WindowResize()
        {
            menuScreenRec = new Rectangle(0, 0, game.GraphicsDevice.Viewport.Width, game.GraphicsDevice.Viewport.Height);
            menuScreenView = game.GraphicsDevice.Viewport;
            aspectX = (float)game.GraphicsDevice.Viewport.Width / 1024f;
            aspectY = (float)game.GraphicsDevice.Viewport.Height / 768f;
            Log.Write("Aspect-ratios: " + aspectX + "; " + aspectY);
        }
    }
}
