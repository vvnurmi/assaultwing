using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using AW2.Game;
using AW2.Events;
using AW2.Net;
using AW2.Net.Messages;

namespace AW2.UI
{
    /// <summary>
    /// Basic user interface implementation.
    /// </summary>
    class UIEngineImpl : GameComponent
    {
        /// <summary>
        /// The state of input controls in the previous frame.
        /// </summary>
        private InputState oldState;

        /// <summary>
        /// True iff mouse input is eaten by the game.
        /// </summary>
        bool eatMouse;

        /// <summary>
        /// If mouse input is being consumed for the purposes of using the mouse
        /// for game controls. Such consumption prevents other programs from using
        /// the mouse in any practical manner. Defaults to <b>false</b>.
        /// </summary>
        public bool MouseControlsEnabled { get { return eatMouse; } set { eatMouse = value; } }

        public UIEngineImpl(Microsoft.Xna.Framework.Game game) : base(game)
        {
            oldState = InputState.GetState();
            eatMouse = false;
        }

        /// <summary>
        /// Reacts to user input.
        /// </summary>
        /// <param name="gameTime">Time elapsed since the last call to Update</param>
        public override void Update(GameTime gameTime)
        {
            EventEngine eventEngine = (EventEngine)Game.Services.GetService(typeof(EventEngine));
            InputState newState = InputState.GetState();

            // Reset mouse cursor to the middle of the game window.
            if (eatMouse)
                Mouse.SetPosition(
                    AssaultWing.Instance.ClientBounds.Width / 2,
                    AssaultWing.Instance.ClientBounds.Height / 2);

            // Update controls.
            Control.ForEachControl(control => control.SetState(ref oldState, ref newState));

            if (AssaultWing.Instance.NetworkMode == NetworkMode.Server)
            {
                // Fetch control messages from game clients.
                PlayerControlsMessage message = null;
                while ((message = AssaultWing.Instance.NetworkEngine.GameClientConnections.Messages.TryDequeue<PlayerControlsMessage>()) != null)
                {
                    var player = AssaultWing.Instance.DataEngine.Spectators.First(plr => plr.Id == message.PlayerId);
                    if (player.ConnectionId != message.ConnectionId)
                    {
                        // A client sent controls for a player that lives on another game instance.
                        // We silently ignore the controls.
                        continue;
                    }
                    SetRemoteControlState((RemoteControl)player.Controls.thrust, message.GetControlState(PlayerControlType.Thrust));
                    SetRemoteControlState((RemoteControl)player.Controls.left, message.GetControlState(PlayerControlType.Left));
                    SetRemoteControlState((RemoteControl)player.Controls.right, message.GetControlState(PlayerControlType.Right));
                    SetRemoteControlState((RemoteControl)player.Controls.down, message.GetControlState(PlayerControlType.Down));
                    SetRemoteControlState((RemoteControl)player.Controls.fire1, message.GetControlState(PlayerControlType.Fire1));
                    SetRemoteControlState((RemoteControl)player.Controls.fire2, message.GetControlState(PlayerControlType.Fire2));
                    SetRemoteControlState((RemoteControl)player.Controls.extra, message.GetControlState(PlayerControlType.Extra));
                }
            }

            oldState = newState;
        }

        private static void SetRemoteControlState(RemoteControl control, PlayerControlsMessage.ControlState state)
        {
            control.SetControlState(state.force, state.pulse);
        }
    }
}
