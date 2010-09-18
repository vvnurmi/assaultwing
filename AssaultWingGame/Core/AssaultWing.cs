﻿using System;
using System.Collections.Generic;
using System.Linq;
using AW2.Menu;
using AW2.Graphics;
using AW2.Helpers;
using AW2.Game;
using AW2.UI;
using Microsoft.Xna.Framework.Input;
using AW2.Net.MessageHandling;

namespace AW2.Core
{
    public class AssaultWing : AssaultWingCore
    {
        private GameState _gameState;

        // HACK: Debug keys
        private Control _musicSwitch;
        private Control _arenaReload;
        private Control _frameStepControl;
        private Control _frameRunControl;
        private bool _frameStep;

        public MenuEngineImpl MenuEngine { get; private set; }

        public GameState GameState
        {
            get { return _gameState; }
            private set
            {
                DisableCurrentGameState();
                EnableGameState(value);
                var oldState = _gameState;
                _gameState = value;
                if (GameStateChanged != null && _gameState != oldState)
                    GameStateChanged(_gameState);
            }
        }

        public event Action<GameState> GameStateChanged;

        private IntroEngine IntroEngine { get { return (IntroEngine)Components.First(c => c is IntroEngine); } }
        private LogicEngine LogicEngine { get { return (LogicEngine)Components.First(c => c is LogicEngine); } }
        private OverlayDialog OverlayDialog { get { return (OverlayDialog)Components.First(c => c is OverlayDialog); } }
        private UIEngineImpl UIEngine { get { return (UIEngineImpl)Components.First(c => c is UIEngineImpl); } }

        public AssaultWing()
        {
            MenuEngine = new MenuEngineImpl();
            Components.Add(MenuEngine);
            GameState = GameState.Initializing;
            _musicSwitch = new KeyboardKey(Keys.F5);
            _arenaReload = new KeyboardKey(Keys.F6);
            _frameStepControl = new KeyboardKey(Keys.F8);
            _frameRunControl = new KeyboardKey(Keys.F7);
            _frameStep = false;
        }

        private void EnableGameState(GameState value)
        {
            switch (value)
            {
                case GameState.Initializing:
                    break;
                case GameState.Intro:
                    IntroEngine.Enabled = true;
                    IntroEngine.Visible = true;
                    IntroEngine.BeginIntro();
                    break;
                case GameState.Gameplay:
                    Log.Write("Saving settings to file");
                    Settings.ToFile();
                    LogicEngine.Enabled = DataEngine.Arena.IsForPlaying;
                    GraphicsEngine.Visible = true;
                    break;
                case GameState.Menu:
                    MenuEngine.Enabled = true;
                    MenuEngine.Visible = true;
                    break;
                case GameState.OverlayDialog:
                    OverlayDialog.Enabled = true;
                    OverlayDialog.Visible = true;
                    GraphicsEngine.Visible = true;
                    break;
                default:
                    throw new ApplicationException("Cannot change to unexpected game state " + value);
            }
        }

        private void DisableCurrentGameState()
        {
            switch (_gameState)
            {
                case GameState.Initializing:
                    break;
                case GameState.Intro:
                    IntroEngine.Enabled = false;
                    IntroEngine.Visible = false;
                    break;
                case GameState.Gameplay:
                    LogicEngine.Enabled = false;
                    GraphicsEngine.Visible = false;
                    break;
                case GameState.Menu:
                    MenuEngine.Enabled = false;
                    MenuEngine.Visible = false;
                    break;
                case GameState.OverlayDialog:
                    OverlayDialog.Enabled = false;
                    OverlayDialog.Visible = false;
                    GraphicsEngine.Visible = false;
                    break;
                default:
                    throw new ApplicationException("Cannot change away from unexpected game state " + GameState);
            }
        }

        private void UpdateDebugKeys()
        {
            // Switch music off
            if (_musicSwitch.Pulse && GameState == GameState.Gameplay)
            {
                SoundEngine.StopMusic();
            }

            // Instant arena reload (simple aid for hand-editing an arena)
            if (_arenaReload.Pulse && GameState == GameState.Gameplay && NetworkMode == NetworkMode.Standalone)
            {
                var arenaFilename = DataEngine.ArenaInfos.Single(info => info.Name == DataEngine.ArenaPlaylist.Current).FileName;
                try
                {
                    var arena = Arena.FromFile(arenaFilename);
                    DataEngine.InitializeFromArena(arena, true);
                    StartArena();
                }
                catch (Exception e)
                {
                    Log.Write("Arena reload failed: " + e);
                }
            }

            // Frame stepping (for debugging)
            if (_frameRunControl.Pulse)
            {
                LogicEngine.Enabled = true;
                _frameStep = false;
            }
            if (_frameStep)
            {
                if (_frameStepControl.Pulse)
                    LogicEngine.Enabled = true;
                else
                    LogicEngine.Enabled = false;
            }
            else if (_frameStepControl.Pulse)
            {
                LogicEngine.Enabled = false;
                _frameStep = true;
            }
        }

        public override void Update(Microsoft.Xna.Framework.GameTime gameTime)
        {
            base.Update(gameTime);
            UpdateDebugKeys();
        }

        protected override void StartArenaImpl()
        {
            base.StartArenaImpl();
            GameState = GameState.Gameplay;
        }

        /// <summary>
        /// Resumes playing the current arena, closing the dialog if it's visible.
        /// </summary>
        public override void ResumePlay()
        {
            GameState = GameState.Gameplay;
        }

        /// <summary>
        /// Displays the dialog on top of the game and stops updating the game logic.
        /// </summary>
        /// <param name="dialogData">The contents and actions for the dialog.</param>
        public override void ShowDialog(AW2.Graphics.OverlayComponents.OverlayDialogData dialogData)
        {
            if (!AllowDialogs) return;
            OverlayDialog.Data = dialogData;
            GameState = GameState.OverlayDialog;
            SoundEngine.PlaySound("EscPause");
        }

        /// <summary>
        /// Displays the main menu and stops any ongoing gameplay.
        /// </summary>
        public override void ShowMenu()
        {
            Log.Write("Entering menus");
            if (NetworkMode == NetworkMode.Client) MessageHandlers.DeactivateHandlers(MessageHandlers.GetClientGameplayHandlers());
            if (NetworkMode == NetworkMode.Server) MessageHandlers.DeactivateHandlers(MessageHandlers.GetServerGameplayHandlers());
            DataEngine.ClearGameState();
            MenuEngine.Activate();
            GameState = GameState.Menu;
        }

        /// <summary>
        /// Called after all components are initialized but before the first update in the game loop. 
        /// </summary>
        public override void BeginRun()
        {
            Log.Write("Assault Wing begins to run");

            // Hardcoded for now!!!

            PlayerControls plr1Controls;
            plr1Controls.Thrust = new KeyboardKey(Keys.Up);
            plr1Controls.Left = new KeyboardKey(Keys.Left);
            plr1Controls.Right = new KeyboardKey(Keys.Right);
            plr1Controls.Down = new KeyboardKey(Keys.Down);
            plr1Controls.Fire1 = new KeyboardKey(Keys.RightControl);
            plr1Controls.Fire2 = new KeyboardKey(Keys.RightShift);
            plr1Controls.Extra = new KeyboardKey(Keys.Down);

            PlayerControls plr2Controls;
#if false // mouse control
            //plr2Controls.Thrust = new MouseDirection(MouseDirections.Up, 2, 7, 5);
            plr2Controls.Thrust = new MouseButton(MouseButtons.Left);
            plr2Controls.Left = new MouseDirection(MouseDirections.Left, 2, 9, 5);
            plr2Controls.Right = new MouseDirection(MouseDirections.Right, 2, 9, 5);
            plr2Controls.Down = new MouseDirection(MouseDirections.Down, 2, 12, 5);
            //plr2Controls.Fire1 = new MouseDirection(MouseDirections.Down, 0, 12, 20);
            //plr2Controls.Fire2 = new MouseButton(MouseButtons.Right);
            plr2Controls.Fire1 = new MouseWheelDirection(MouseWheelDirections.Forward, 0, 1, 1);
            plr2Controls.Fire2 = new MouseWheelDirection(MouseWheelDirections.Backward, 0, 1, 1);
            plr2Controls.Extra = new KeyboardKey(Keys.CapsLock);
            _uiEngine.MouseControlsEnabled = true;
#else
            plr2Controls.Thrust = new KeyboardKey(Keys.W);
            plr2Controls.Left = new KeyboardKey(Keys.A);
            plr2Controls.Right = new KeyboardKey(Keys.D);
            plr2Controls.Down = new KeyboardKey(Keys.X);
            plr2Controls.Fire1 = new KeyboardKey(Keys.LeftControl);
            plr2Controls.Fire2 = new KeyboardKey(Keys.LeftShift);
            plr2Controls.Extra = new KeyboardKey(Keys.X);
            UIEngine.MouseControlsEnabled = false;
#endif

            Player player1 = new Player("Newbie", (CanonicalString)"Windlord", (CanonicalString)"rockets", (CanonicalString)"reverse thruster", plr1Controls);
            Player player2 = new Player("Lamer", (CanonicalString)"Bugger", (CanonicalString)"bazooka", (CanonicalString)"reverse thruster", plr2Controls);
            DataEngine.Spectators.Add(player1);
            DataEngine.Spectators.Add(player2);

            DataEngine.GameplayMode = new GameplayMode();
            DataEngine.GameplayMode.ShipTypes = new string[] { "Windlord", "Bugger", "Plissken" };
            DataEngine.GameplayMode.ExtraDeviceTypes = new string[] { "reverse thruster", "blink" };
            DataEngine.GameplayMode.Weapon2Types = new string[] { "bazooka", "rockets", "mines" };

            GameState = GameState.Intro;
            base.BeginRun();
        }
    }
}