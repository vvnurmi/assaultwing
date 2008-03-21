using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.GamerServices;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Storage;
using Microsoft.Xna.Framework.Content;
using AW2.UI;
using AW2.Events;

namespace AW2.Graphics
{
    /// <summary>
    /// This is a game component that implements IUpdateable.
    /// </summary>
    public class OverlayDialog : Microsoft.Xna.Framework.DrawableGameComponent
    {
        SpriteFont textWriter;
        SpriteBatch spriteBatch;
        Texture2D dialogTexture;
        string dialogText;
        Action<object> yesAction;
        Action<object> noAction;
        Control dialogYesControl, dialogNoControl;

        /// <summary>
        /// The text to display in the dialog.
        /// </summary>
        public string DialogText { get { return dialogText; } set { dialogText = value; } }

        /// <summary>
        /// The action to perform when the user gives positive input.
        /// </summary>
        public Action<object> YesAction { set { yesAction = value; } }

        /// <summary>
        /// The action to perform when the user gives negative input.
        /// </summary>
        public Action<object> NoAction { set { noAction = value; } }

        /// <summary>
        /// Creates an overlay dialog.
        /// </summary>
        /// <param name="game">The game instance to attach the dialog to.</param>
        public OverlayDialog(Microsoft.Xna.Framework.Game game)
            : base(game)
        {
            dialogText = "Huh?";
            yesAction = delegate(object obj) { };
            noAction = delegate(object obj) { };
            dialogYesControl = new KeyboardKey(Keys.Y);
            dialogNoControl = new KeyboardKey(Keys.N);
        }

        /// <summary>
        /// Allows the game component to perform any initialization it needs to before starting
        /// to run.  This is where it can query for any required services and load content.
        /// </summary>
        public override void Initialize()
        {
            // TODO: Add your initialization code here

            base.Initialize();
        }

        /// <summary>
        /// Allows the game component to update itself.
        /// </summary>
        /// <param name="gameTime">Provides a snapshot of timing values.</param>
        public override void Update(GameTime gameTime)
        {
            // Process player input.
            EventEngine eventer = (EventEngine)Game.Services.GetService(typeof(EventEngine));
            for (PlayerControlEvent eve = eventer.GetEvent<PlayerControlEvent>(); eve != null;
                eve = eventer.GetEvent<PlayerControlEvent>())
            {
                // Positive input.
                if (eve.ControlType == PlayerControlType.Fire1 ||
                    eve.ControlType == PlayerControlType.Fire2)
                {
                    yesAction(null);
                }
                // Negative input.
                else
                {
                    noAction(null);
                }
            }

            base.Update(gameTime);
        }

        /// <summary>
        /// Called when graphics resources need to be loaded. Override this method to
        /// load any component-specific graphics resources.
        /// </summary>
        protected override void LoadContent()
        {           
            textWriter = this.Game.Content.Load<SpriteFont>(System.IO.Path.Combine("fonts", "DotMatrix"));
            dialogTexture = this.Game.Content.Load<Texture2D>(System.IO.Path.Combine("textures", "dialog")); 
            spriteBatch = new SpriteBatch(this.GraphicsDevice);
        }

        /// <summary>
        /// Called when the DrawableGameComponent needs to be drawn. Override this method
        /// with component-specific drawing code.
        /// </summary>
        /// <param name="gameTime">Time passed since the last call to Microsoft.Xna.Framework.DrawableGameComponent.Draw(Microsoft.Xna.Framework.GameTime).</param>
        public override void Draw(GameTime gameTime)
        {
            #region Overlay menu
            spriteBatch.Begin();
            Vector2 dialogTopLeft = new Vector2(0, AssaultWing.Instance.ClientBounds.Height - dialogTexture.Height) / 2;
            Vector2 textCenter = dialogTopLeft + new Vector2(474, 150);
            Vector2 textSize = textWriter.MeasureString(dialogText);
            spriteBatch.Draw(dialogTexture, dialogTopLeft, Color.White);
            spriteBatch.DrawString(textWriter, dialogText, textCenter, Color.White, 0,
                textSize / 2, 1, SpriteEffects.None, 0);
            spriteBatch.End();
            #endregion

        }


    }
}