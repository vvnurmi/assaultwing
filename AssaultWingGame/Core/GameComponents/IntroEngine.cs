using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Media;
using AW2.Graphics;
using AW2.Helpers;
using AW2.UI;

namespace AW2.Core.GameComponents
{
    /// <summary>
    /// Game intro sequence implementation.
    /// </summary>
    public class IntroEngine : AWGameComponent
    {
        private Control _skipControl;
        private AWVideo _introVideo;
        private SpriteBatch _spriteBatch;

        public new AssaultWing Game { get { return (AssaultWing)base.Game; } }

        public IntroEngine(AssaultWing game)
            : base(game)
        {
        }

        public void BeginIntro()
        {
            _introVideo.Play();
        }

        private void EndIntro()
        {
            _introVideo.Stop();
            Game.ShowMenu();
        }

        public override void Initialize()
        {
            base.Initialize();
            _skipControl = new KeyboardKey(Microsoft.Xna.Framework.Input.Keys.Escape);
            _introVideo = new AWVideo(Game.Content.Load<Video>("aw_intro"));
        }

        public override void LoadContent()
        {
            base.LoadContent();
            _spriteBatch = new SpriteBatch(Game.GraphicsDeviceService.GraphicsDevice);
        }

        public override void UnloadContent()
        {
            if (_spriteBatch != null)
            {
                _spriteBatch.Dispose();
                _spriteBatch = null;
            }
            _introVideo.Dispose();
            base.UnloadContent();
        }

        public override void Update()
        {
            if (_skipControl.Pulse) EndIntro();
            if (_introVideo.IsFinished) EndIntro();
        }

        public override void Draw()
        {
            Game.GraphicsDeviceService.GraphicsDevice.Clear(Color.Black);
            var videoFrame = _introVideo.GetTexture();
            if (videoFrame != null)
            {
                _spriteBatch.Begin();
                int width = videoFrame.Width;
                int height = videoFrame.Height;
                var titleSafeArea = Game.GraphicsDeviceService.GraphicsDevice.Viewport.TitleSafeArea;
                titleSafeArea.Clamp(ref width, ref height);
                var destinationRect = new Rectangle((titleSafeArea.Width - width) / 2, (titleSafeArea.Height - height) / 2, width, height);
                _spriteBatch.Draw(videoFrame, destinationRect, Color.White);
                _spriteBatch.End();
            }
        }
    }
}