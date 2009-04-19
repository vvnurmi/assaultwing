using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using AW2.Game;
using AW2.Helpers;
using AW2.UI;

namespace AW2.Graphics
{
    /// <summary>
    /// Content and action data for an overlay dialog displaying custom data.
    /// </summary>
    public class CustomOverlayDialogData : OverlayDialogData
    {
        string text;

        SpriteFont font;

        /// <summary>
        /// Creates contents for an overlay dialog displaying arena over.
        /// </summary>
        /// <param name="text">The text to display in the dialog.</param>
        /// <param name="actions">The actions to allow in the dialog.</param>
        public CustomOverlayDialogData(string text, params TriggeredCallback[] actions)
            : base(actions)
        {
            this.text = text;
        }

        /// <summary>
        /// Draws the overlay graphics component using the guarantee that the
        /// graphics device's viewport is set to the exact area needed by the component.
        /// </summary>
        /// <param name="spriteBatch">The sprite batch to use. <c>Begin</c> is assumed
        /// to have been called and <c>End</c> is assumed to be called after this
        /// method returns.</param>
        protected override void DrawContent(SpriteBatch spriteBatch)
        {
            GraphicsDevice gfx = AssaultWing.Instance.GraphicsDevice;
            Vector2 textCenter = new Vector2(gfx.Viewport.Width, gfx.Viewport.Height) / 2;
            Vector2 textSize = font.MeasureString(text);
            spriteBatch.DrawStringRounded(font, text, textCenter - textSize / 2, Color.White);
        }

        /// <summary>
        /// Called when graphics resources need to be loaded.
        /// </summary>
        public override void LoadContent()
        {
            DataEngine data = (DataEngine)AssaultWing.Instance.Services.GetService(typeof(DataEngine));
            font = data.GetFont(FontName.MenuFontBig);
        }

        /// <summary>
        /// Called when graphics resources need to be unloaded.
        /// </summary>
        public override void UnloadContent()
        {
            // Our fonts are disposed by the graphics engine.
        }
    }
}
