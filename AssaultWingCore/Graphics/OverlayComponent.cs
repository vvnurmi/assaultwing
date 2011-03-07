using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using AW2.Core;
using AW2.Helpers;

namespace AW2.Graphics
{
    /// <summary>
    /// An overlay graphics component, for example in player's screen during play.
    /// </summary>
    /// An overlay component is drawn onto an <see cref="AWViewport"/>.
    /// The component is aligned by setting a point of reference in the viewport.
    /// This point can be top, center, bottom on the vertical axis, and left, center,
    /// right on the horizontal axis. The component can also be stretched to cover
    /// the whole viewport.
    public abstract class OverlayComponent : IDisposable
    {
        /// <summary>
        /// Horizontal alignment of the component in the backbuffer viewport.
        /// </summary>
        public HorizontalAlignment HorizontalAlignment { get; set; }

        /// <summary>
        /// Vertical alignment of the component in the backbuffer viewport.
        /// </summary>
        public VerticalAlignment VerticalAlignment { get; set; }

        /// <summary>
        /// Alignment adjustment; added to the coordinates obtained by the chosen alignment.
        /// Initially set to Vector2.Zero, which gives no adjustment to the chosen alignment.
        /// </summary>
        public Vector2 CustomAlignment { get; set; }

        /// <summary>
        /// Is the component visible.
        /// </summary>
        public bool Visible { get; set; }

        /// <summary>
        /// The dimensions of the component in pixels.
        /// </summary>
        public abstract Point Dimensions { get; }

        /// <summary>
        /// The viewport into which the component is drawn, or null.
        /// </summary>
        protected AWViewport Viewport { get; private set; }

        /// <param name="viewport">The viewport to draw on, or null.</param>
        protected OverlayComponent(AWViewport viewport, HorizontalAlignment horizontal, VerticalAlignment vertical)
        {
            HorizontalAlignment = horizontal;
            VerticalAlignment = vertical;
            Visible = true;
            Viewport = viewport;
            LoadContent();
        }

        /// <summary>
        /// Draws the overlay graphics component, correctly aligned in the
        /// graphics device's current viewport.
        /// </summary>
        /// <param name="spriteBatch">The sprite batch to use. This method
        /// will call <c>Begin</c> and <c>End</c>.</param>
        public void Draw(SpriteBatch spriteBatch)
        {
            if (!Visible) return;
            var gfx = AssaultWingCore.Instance.GraphicsDeviceService.GraphicsDevice;
            var oldViewport = gfx.Viewport;
            var newViewport = oldViewport;
            var dimensions = Dimensions;
            switch (HorizontalAlignment)
            {
                case HorizontalAlignment.Left:
                    break;
                case HorizontalAlignment.Center:
                    newViewport.X += Math.Max(0, (oldViewport.Width - dimensions.X) / 2);
                    break;
                case HorizontalAlignment.Right:
                    newViewport.X += Math.Max(0, oldViewport.Width - dimensions.X);
                    break;
                case HorizontalAlignment.Stretch:
                    dimensions.X = oldViewport.Width;
                    break;
            }
            switch (VerticalAlignment)
            {
                case VerticalAlignment.Top:
                    break;
                case VerticalAlignment.Center:
                    newViewport.Y += Math.Max(0, (oldViewport.Height - dimensions.Y) / 2);
                    break;
                case VerticalAlignment.Bottom:
                    newViewport.Y += Math.Max(0, oldViewport.Height - dimensions.Y);
                    break;
                case VerticalAlignment.Stretch:
                    dimensions.Y = oldViewport.Height;
                    break;
            }
            newViewport.X += (int)CustomAlignment.X;
            newViewport.Y += (int)CustomAlignment.Y;
            newViewport.Width = Math.Min(oldViewport.Width, dimensions.X);
            newViewport.Height = Math.Min(oldViewport.Height, dimensions.Y);
            newViewport = LimitViewport(newViewport);
            gfx.Viewport = newViewport;
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend);
            DrawContent(spriteBatch);
            spriteBatch.End();
            gfx.Viewport = oldViewport;
        }

        private static Viewport LimitViewport(Viewport viewport)
        {
            var clientBounds = AssaultWingCore.Instance.Window.ClientBounds;
            var x_width = LimitViewportAxis(viewport.X, viewport.Width, clientBounds.X, clientBounds.Right);
            viewport.X = x_width.Item1;
            viewport.Width = x_width.Item2;
            var y_height = LimitViewportAxis(viewport.Y, viewport.Height, clientBounds.Y, clientBounds.Bottom);
            viewport.Y = y_height.Item1;
            viewport.Height = y_height.Item2;

            //if (viewport.X < clientBounds.X)
            //{
            //    viewport.Width -= clientBounds.X - viewport.X;
            //    viewport.X = clientBounds.X;
            //}
            //if (viewport.X < clientBounds.Right)
            //    viewport.Width = viewport.Width.Clamp(0, Math.Max(0, clientBounds.Right - viewport.X));
            //else
            //{
            //    viewport.X = clientBounds.Right - 1;
            //    viewport.Width = 1; // 0 is not allowed by GraphicsDevice.Viewport
            //}

            //if (viewport.Y < clientBounds.Y)
            //{
            //    viewport.Height -= clientBounds.Y - viewport.Y;
            //    viewport.Y = clientBounds.Y;
            //}
            //if (viewport.Y < clientBounds.Bottom)
            //    viewport.Height = viewport.Height.Clamp(0, Math.Max(0, clientBounds.Bottom - viewport.Y));
            //else
            //{
            //    viewport.Y = clientBounds.Bottom - 1;
            //    viewport.Height = 1; // 0 is not allowed by GraphicsDevice.Viewport
            //}
            return viewport;
        }

        private static Tuple<int, int> LimitViewportAxis(int start, int size, int min, int max)
        {
            if (start < min)
            {
                size -= min - start;
                start = min;
            }
            if (start < max)
                size = size.Clamp(0, Math.Max(0, max - start));
            else
            {
                start = max;
                size = 1; // 0 is not allowed by GraphicsDevice.Viewport
            }
            return Tuple.Create(start, size);
        }

        /// <summary>
        /// Draws the overlay graphics component using the guarantee that the
        /// graphics device's viewport is set to the exact area needed by the component.
        /// </summary>
        /// <param name="spriteBatch">The sprite batch to use. <c>Begin</c> is assumed
        /// to have been called and <c>End</c> is assumed to be called after this
        /// method returns.</param>
        protected abstract void DrawContent(SpriteBatch spriteBatch);

        /// <summary>
        /// Called when graphics resources need to be loaded.
        /// </summary>
        public virtual void LoadContent() { }

        /// <summary>
        /// Called when graphics resources need to be unloaded.
        /// </summary>
        public virtual void UnloadContent() { }

        /// <summary>
        /// Called when allocated resources should be released.
        /// </summary>
        public virtual void Dispose()
        {
        }
    }
}
