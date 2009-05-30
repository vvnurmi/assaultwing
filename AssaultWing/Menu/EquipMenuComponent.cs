using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using AW2.Game;
using AW2.Graphics;
using AW2.Net;
using AW2.Net.Messages;
using AW2.UI;

namespace AW2.Menu
{
    /// <summary>
    /// The equip menu component where players can choose their ships and weapons.
    /// </summary>
    /// The equip menu consists of four panes, one for each player.
    /// Each pane consists of a top that indicates the player and the main body where
    /// the menu content lies.
    /// Each pane, in its main mode, displays the player's selection of equipment.
    /// Each player can control their menu individually, and their current position
    /// in the menu main display is indicated by a cursor and a highlight.
    class EquipMenuComponent : MenuComponent
    {
        /// <summary>
        /// An item in a pane main display in the equip menu.
        /// </summary>
        enum EquipMenuItem
        {
            /// <summary>
            /// Start a local play session.
            /// </summary>
            Ship,

            /// <summary>
            /// Start a network play session.
            /// </summary>
            Weapon1,

            /// <summary>
            /// Set up Assault Wing's technical thingies.
            /// </summary>
            Weapon2,
        }

        Control controlBack, controlDone;
        Vector2 pos; // position of the component's background texture in menu system coordinates
        SpriteFont menuBigFont, menuSmallFont;
        Texture2D backgroundTexture;
        Texture2D cursorMainTexture, highlightMainTexture;
        Texture2D playerPaneTexture, player1PaneTopTexture, player2PaneTopTexture;
        Texture2D statusPaneTexture;

        /// <summary>
        /// Cursor fade curve as a function of time in seconds.
        /// Values range from 0 (transparent) to 255 (opaque).
        /// </summary>
        Curve cursorFade;

        /// <summary>
        /// Time at which the cursor started fading for each player.
        /// </summary>
        TimeSpan[] cursorFadeStartTimes;

        /// <summary>
        /// Index of current item in each player's pane main display.
        /// </summary>
        EquipMenuItem[] currentItems;

        /// <summary>
        /// Equipment selectors for each player and each aspect of the player's equipment.
        /// Indexed as [playerI, aspectI].
        /// </summary>
        EquipmentSelector[,] equipmentSelectors;

        /// <summary>
        /// Does the menu component react to input.
        /// </summary>
        public override bool Active
        {
            set
            {
                base.Active = value;
                if (value)
                {
                    menuEngine.IsProgressBarVisible = false;
                    menuEngine.IsHelpTextVisible = true;
                    CreateSelectors();
                }
            }
        }

        /// <summary>
        /// The center of the menu component in menu system coordinates.
        /// </summary>
        /// This is a good place to center the menu view to when the menu component
        /// is to be seen well on the screen.
        public override Vector2 Center { get { return pos + new Vector2(750, 480); } }

        /// <summary>
        /// Creates an equip menu component for a menu system.
        /// </summary>
        /// <param name="menuEngine">The menu system.</param>
        public EquipMenuComponent(MenuEngineImpl menuEngine)
            : base(menuEngine)
        {
            controlDone = new KeyboardKey(Keys.Enter);
            controlBack = new KeyboardKey(Keys.Escape);
            pos = new Vector2(0, 0);
            currentItems = new EquipMenuItem[4];
            cursorFadeStartTimes = new TimeSpan[4];

            cursorFade = new Curve();
            cursorFade.Keys.Add(new CurveKey(0, 255, 0, 0, CurveContinuity.Step));
            cursorFade.Keys.Add(new CurveKey(0.5f, 0, 0, 0, CurveContinuity.Step));
            cursorFade.Keys.Add(new CurveKey(1, 255, 0, 0, CurveContinuity.Step));
            cursorFade.PreLoop = CurveLoopType.Cycle;
            cursorFade.PostLoop = CurveLoopType.Cycle;
        }

        /// <summary>
        /// Called when graphics resources need to be loaded.
        /// </summary>
        public override void LoadContent()
        {
            var data = AssaultWing.Instance.DataEngine;
            menuBigFont = data.GetFont(FontName.MenuFontBig);
            menuSmallFont = data.GetFont(FontName.MenuFontSmall);
            backgroundTexture = data.GetTexture(TextureName.EquipMenuBackground);
            cursorMainTexture = data.GetTexture(TextureName.EquipMenuCursorMain);
            highlightMainTexture = data.GetTexture(TextureName.EquipMenuHighlightMain);
            playerPaneTexture = data.GetTexture(TextureName.EquipMenuPlayerBackground);
            player1PaneTopTexture = data.GetTexture(TextureName.EquipMenuPlayerTop1);
            player2PaneTopTexture = data.GetTexture(TextureName.EquipMenuPlayerTop2);
            statusPaneTexture = data.GetTexture(TextureName.EquipMenuStatusDisplay);
        }

        /// <summary>
        /// Called when graphics resources need to be unloaded.
        /// </summary>
        public override void UnloadContent()
        {
            // The textures and fonts we reference will be disposed by GraphicsEngine.
        }

        /// <summary>
        /// Updates the menu component.
        /// </summary>
        public override void Update()
        {
            if (Active)
            {
                CheckGeneralControls();
                CheckPlayerControls();
                CheckNetwork();
            }
        }

        /// <summary>
        /// Sets up selectors for each aspect of equipment of each player.
        /// </summary>
        private void CreateSelectors()
        {
            int aspectCount = Enum.GetValues(typeof(EquipMenuItem)).Length;
            equipmentSelectors = new EquipmentSelector[AssaultWing.Instance.DataEngine.Players.Count, aspectCount];

            int playerI = 0;
            foreach (var player in AssaultWing.Instance.DataEngine.Players)
            {
                equipmentSelectors[playerI, (int)EquipMenuItem.Ship] = new ShipSelector(player);
                equipmentSelectors[playerI, (int)EquipMenuItem.Weapon1] = new Weapon1Selector(player);
                equipmentSelectors[playerI, (int)EquipMenuItem.Weapon2] = new Weapon2Selector(player);
                ++playerI;
            }
        }

        private void CheckGeneralControls()
        {
            if (controlBack.Pulse)
                menuEngine.ActivateComponent(MenuComponentType.Main);
            else if (controlDone.Pulse)
            {
                switch (AssaultWing.Instance.NetworkMode)
                {
                    case NetworkMode.Server:
                        // HACK: Server has a fixed arena playlist
                        // Start loading the first arena and display its progress.
                        menuEngine.ProgressBarAction(
                            AssaultWing.Instance.PrepareFirstArena,
                            AssaultWing.Instance.StartArena);

                        // We don't accept input while an arena is loading.
                        Active = false;
                        break;
                    case NetworkMode.Client:
                        // Client advances only when the server says so.
                        break;
                    case NetworkMode.Standalone:
                        menuEngine.ActivateComponent(MenuComponentType.Arena);
                        break;
                    default: throw new Exception("Unexpected network mode " + AssaultWing.Instance.NetworkMode);
                }
            }
        }

        private void CheckPlayerControls()
        {
            int playerI = -1;
            foreach (var player in AssaultWing.Instance.DataEngine.Players)
            {
                if (player.IsRemote) return;
                ++playerI;

                ConditionalPlayerAction(player.Controls.thrust.Pulse, playerI, () =>
                {
                    if (currentItems[playerI] > 0)
                        --currentItems[playerI];
                });
                ConditionalPlayerAction(player.Controls.down.Pulse, playerI, () =>
                {
                    if ((int)currentItems[playerI] < Enum.GetValues(typeof(EquipMenuItem)).Length - 1)
                        ++currentItems[playerI];
                });

                int selectionChange = 0;
                ConditionalPlayerAction(player.Controls.left.Pulse, playerI,
                    () => { selectionChange = -1; });
                ConditionalPlayerAction(player.Controls.fire1.Pulse || player.Controls.right.Pulse, playerI,
                    () => { selectionChange = 1; });
                if (selectionChange != 0)
                {
                    equipmentSelectors[playerI, (int)currentItems[playerI]].CurrentValue += selectionChange;

                    // Send new equipment choices to the game server.
                    if (AssaultWing.Instance.NetworkMode == NetworkMode.Client)
                    {
                        var equipUpdateRequest = new JoinGameRequest();
                        equipUpdateRequest.PlayerInfos = new List<PlayerInfo> { new PlayerInfo(player) };
                        AssaultWing.Instance.NetworkEngine.SendToServer(equipUpdateRequest);
                    }
                }
            }
        }

        /// <summary>
        /// Helper for <seealso cref="CheckPlayerControls"/>
        /// </summary>
        private void ConditionalPlayerAction(bool condition, int playerI, Action action)
        {
            if (!condition) return;
            cursorFadeStartTimes[playerI] = AssaultWing.Instance.GameTime.TotalRealTime;
            action();
        }

        private void CheckNetwork()
        {
            if (AssaultWing.Instance.NetworkMode == NetworkMode.Server)
            {
                // Handle JoinGameRequests from game clients.
                JoinGameRequest message = null;
                while ((message = AssaultWing.Instance.NetworkEngine.ReceiveFromClients<JoinGameRequest>()) != null)
                {
                    // Send player ID changes for new players, if any. A join game request
                    // may also update the chosen equipment of a previously added player.
                    JoinGameReply reply = new JoinGameReply();
                    var playerIdChanges = new List<JoinGameReply.IdChange>();
                    foreach (PlayerInfo info in message.PlayerInfos)
                    {
                        var oldPlayer = AssaultWing.Instance.DataEngine.Players.FirstOrDefault(
                            plr => plr.ConnectionId == message.ConnectionId && plr.Id == info.id);
                        if (oldPlayer != null)
                        {
                            oldPlayer.Name = info.name;
                            oldPlayer.ShipName = info.shipTypeName;
                            oldPlayer.Weapon1Name = info.weapon1TypeName;
                            oldPlayer.Weapon2Name = info.weapon2TypeName;
                        }
                        else
                        {
                            Player player = new Player(info.name, info.shipTypeName, info.weapon1TypeName, info.weapon2TypeName, message.ConnectionId);
                            AssaultWing.Instance.DataEngine.Players.Add(player);
                            playerIdChanges.Add(new JoinGameReply.IdChange { oldId = info.id, newId = player.Id });
                        }
                    }
                    if (playerIdChanges.Count > 0)
                    {
                        reply.CanonicalStrings = AW2.Helpers.CanonicalString.CanonicalForms;
                        reply.PlayerIdChanges = playerIdChanges.ToArray();
                        AssaultWing.Instance.NetworkEngine.SendToClient(message.ConnectionId, reply);
                    }
                }
            }
            if (AssaultWing.Instance.NetworkMode == NetworkMode.Client)
            {
                var message = AssaultWing.Instance.NetworkEngine.ReceiveFromServer<StartGameMessage>();
                if (message != null)
                {
                    for (int i = 0; i < message.PlayerCount; ++i)
                    {
                        Player player = new Player("uninitialised", "uninitialised", "uninitialised", "uninitialised", 0x7ea1eaf);
                        message.Read(player, SerializationModeFlags.All);

                        // Only add the player if it is remote.
                        if (!AssaultWing.Instance.DataEngine.Players.Any(plr => plr.Id == player.Id))
                            AssaultWing.Instance.DataEngine.Players.Add(player);
                    }

                    AssaultWing.Instance.DataEngine.ArenaPlaylist = message.ArenaPlaylist;

                    // Prepare and start playing the game.
                    menuEngine.ProgressBarAction(
                        AssaultWing.Instance.PrepareFirstArena,
                        AssaultWing.Instance.StartArena);

                    // We don't accept input while an arena is loading.
                    Active = false;
                }
            }
        }

        /// <summary>
        /// Draws the menu component.
        /// </summary>
        /// <param name="view">Top left corner of the menu view in menu system coordinates.</param>
        /// <param name="spriteBatch">The sprite batch to use. <c>Begin</c> is assumed
        /// to have been called and <c>End</c> is assumed to be called after this
        /// method returns.</param>
        public override void Draw(Vector2 view, SpriteBatch spriteBatch)
        {
            var data = AssaultWing.Instance.DataEngine;
            spriteBatch.Draw(backgroundTexture, pos - view, Color.White);

            // Draw player panes.
            Vector2 player1PanePos = new Vector2(334, 164);
            Vector2 playerPaneDeltaPos = new Vector2(203, 0);
            Vector2 playerPaneMainDeltaPos = new Vector2(0, player1PaneTopTexture.Height);
            Vector2 playerPaneCursorDeltaPos = playerPaneMainDeltaPos + new Vector2(22, 3);
            Vector2 playerPaneIconDeltaPos = playerPaneMainDeltaPos + new Vector2(21, 1);
            Vector2 playerPaneRowDeltaPos = new Vector2(0, 91);
            Vector2 playerPaneNamePos = new Vector2(104, 30);
            int playerI = -1;
            foreach (var player in data.Players)
            {
                if (player.IsRemote) return;
                ++playerI;

                // Draw pane background.
                Vector2 playerPanePos = pos - view + player1PanePos + playerI * playerPaneDeltaPos;
                Vector2 playerCursorPos = playerPanePos + playerPaneCursorDeltaPos
                    + (int)currentItems[playerI] * playerPaneRowDeltaPos;
                Vector2 playerNamePos = playerPanePos
                    + new Vector2((int)(104 - menuSmallFont.MeasureString(player.Name).X / 2), 30);
                Texture2D playerPaneTopTexture = playerI == 1 ? player2PaneTopTexture : player1PaneTopTexture;
                spriteBatch.Draw(playerPaneTopTexture, playerPanePos, Color.White);
                spriteBatch.Draw(playerPaneTexture, playerPanePos + playerPaneMainDeltaPos, Color.White);
                spriteBatch.DrawString(menuSmallFont, player.Name, playerNamePos, Color.White);

                // Draw icons of selected equipment.
                Game.Gobs.Ship ship = (Game.Gobs.Ship)data.GetTypeTemplate(typeof(Gob), player.ShipName);
                Weapon weapon1 = (Weapon)data.GetTypeTemplate(typeof(Weapon), player.Weapon1Name);
                Weapon weapon2 = (Weapon)data.GetTypeTemplate(typeof(Weapon), player.Weapon2Name);
                Texture2D shipTexture = data.Textures[ship.IconEquipName];
                Texture2D weapon1Texture = data.Textures[weapon1.IconEquipName];
                Texture2D weapon2Texture = data.Textures[weapon2.IconEquipName];
                spriteBatch.Draw(shipTexture, playerPanePos + playerPaneCursorDeltaPos, Color.White);
                spriteBatch.Draw(weapon1Texture, playerPanePos + playerPaneCursorDeltaPos + playerPaneRowDeltaPos, Color.White);
                spriteBatch.Draw(weapon2Texture, playerPanePos + playerPaneCursorDeltaPos + 2 * playerPaneRowDeltaPos, Color.White);

                // Draw cursor and highlight.
                float cursorTime = (float)(AssaultWing.Instance.GameTime.TotalRealTime - cursorFadeStartTimes[playerI]).TotalSeconds;
                spriteBatch.Draw(highlightMainTexture, playerCursorPos, Color.White);
                spriteBatch.Draw(cursorMainTexture, playerCursorPos, new Color(255, 255, 255, (byte)cursorFade.Evaluate(cursorTime)));
            }

            // Draw network game status pane.
            if (AssaultWing.Instance.NetworkMode != NetworkMode.Standalone)
            {
                // Draw pane background.
                Vector2 statusPanePos = pos - view + new Vector2(537, 160);
                spriteBatch.Draw(statusPaneTexture, statusPanePos, Color.White);

                // Draw pane content text.
                Vector2 textPos = statusPanePos + new Vector2(60, 60);
                string textContent = AssaultWing.Instance.NetworkMode == NetworkMode.Client
                    ? "Connected to game server"
                    : "Hosting a game as server";
                textContent += "\n\n" + data.Players.Count + (data.Players.Count == 1 ? " player" : " players");
                textContent += "\n\nArena: " + data.ArenaPlaylist[0];
                spriteBatch.DrawString(menuBigFont, textContent, textPos, Color.White);
            }
        }
    }
}
