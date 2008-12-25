using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using AW2.Game;
using AW2.UI;

namespace AW2.Graphics
{
    /// <summary>
    /// Content and action data for an overlay dialog displaying standings after
    /// a finished arena while loading another arena.
    /// </summary>
    public class ArenaOverOverlayDialogData : OverlayDialogData
    {
        string arenaWinner;
        string arenaName;
        bool arenaLoaded;

        SpriteFont fontBig, fontSmall;

        /// <summary>
        /// Creates contents for an overlay dialog displaying arena over.
        /// </summary>
        /// <param name="arenaName">Name of the next arena.</param>
        public ArenaOverOverlayDialogData(string arenaName)
            : base()
        {
            DataEngine data = (DataEngine)AssaultWing.Instance.Services.GetService(typeof(DataEngine));
            this.arenaName = arenaName;

            Actions = new TriggeredCallback[] 
            {
                new TriggeredCallback(TriggeredCallback.GetProceedControl(), delegate() 
                {
                    if (arenaLoaded)
                        AssaultWing.Instance.StartArena();
                })
            };

            // Find out the winner.
            arenaWinner = "No-one";
            data.ForEachPlayer(delegate(Player player)
            {
                if (player.Lives > 0)
                    arenaWinner = player.Name;
            });

            // Start loading the next arena.
            data.ProgressBar.HorizontalAlignment = HorizontalAlignment.Center;
            data.ProgressBar.VerticalAlignment = VerticalAlignment.Top;
            data.ProgressBar.CustomAlignment = new Vector2(0, 270);
            data.ProgressBar.Task = AssaultWing.Instance.PrepareNextArena;
            data.ProgressBar.SetSubtaskCount(10); // just something until DataEngine sets the real value
            data.ProgressBar.StartTask();
        }

        /// <summary>
        /// Updates the overlay dialog contents and acts on triggered callbacks.
        /// </summary>
        public override void Update()
        {
            DataEngine data = (DataEngine)AssaultWing.Instance.Services.GetService(typeof(DataEngine));

            // Update our status based on the progress of arena loading.
            if (!arenaLoaded && data.ProgressBar.TaskCompleted)
            {
                data.ProgressBar.FinishTask();
                arenaLoaded = true;
            }

            base.Update();
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
            DataEngine data = (DataEngine)AssaultWing.Instance.Services.GetService(typeof(DataEngine));

            // Draw static text.
            Vector2 textPos = new Vector2(100, 50);
            spriteBatch.DrawString(fontBig, arenaWinner, textPos, Color.White);
            textPos += new Vector2(0, fontBig.LineSpacing);
            spriteBatch.DrawString(fontSmall, "is the winner of the arena", textPos, Color.White);
            textPos += new Vector2(0, fontSmall.LineSpacing + fontBig.LineSpacing);
            spriteBatch.DrawString(fontBig, "Current score:", textPos, Color.White);
            textPos += new Vector2(0, fontBig.LineSpacing);

            // List players and their scores sorted decreasing by score.
            List<Player> players = new List<Player>();
            data.ForEachPlayer(player => players.Add(player));
            var scores =
                from p in players
                let Score = p.Kills - p.Suicides
                orderby Score descending
                select new { p.Name, Score, p.Kills, p.Suicides };
            int line = 0;
            foreach (var entry in scores)
            {
                string scoreText = string.Format("{0} = {1}-{2}", entry.Score, entry.Kills, entry.Suicides);
                spriteBatch.DrawString(fontSmall, (line + 1) + ". " + entry.Name, textPos, Color.White);
                spriteBatch.DrawString(fontSmall, scoreText, textPos + new Vector2(250, 0), Color.White);
                textPos += new Vector2(0, fontSmall.LineSpacing);
                ++line;
            }

            // Draw arena loading text and possibly the progress bar.
            Vector2 loadTextPos = new Vector2(100, 240);
            if (!arenaLoaded)
            {
                spriteBatch.DrawString(fontSmall, "Loading next arena: " + arenaName, loadTextPos, Color.White);
                spriteBatch.End();
                data.ProgressBar.Draw(spriteBatch);
                spriteBatch.Begin();
            }
            else
            {
                spriteBatch.DrawString(fontSmall, "Arena loaded: " + arenaName, loadTextPos, Color.White);
                loadTextPos += new Vector2(0, fontSmall.LineSpacing);
                spriteBatch.DrawString(fontSmall, "Press Enter to begin", loadTextPos, Color.White);
            }
        }

        /// <summary>
        /// Called when graphics resources need to be loaded.
        /// </summary>
        public override void LoadContent()
        {
            DataEngine data = (DataEngine)AssaultWing.Instance.Services.GetService(typeof(DataEngine));
            fontBig = data.GetFont(FontName.MenuFontBig);
            fontSmall = data.GetFont(FontName.MenuFontSmall);
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
