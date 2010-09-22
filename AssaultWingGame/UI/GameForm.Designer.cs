﻿namespace AW2.UI
{
    partial class GameForm
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(GameForm));
            this._gameView = new AW2.Core.GraphicsDeviceControl();
            this.SuspendLayout();
            // 
            // _gameView
            // 
            this._gameView.CausesValidation = false;
            this._gameView.Dock = System.Windows.Forms.DockStyle.Fill;
            this._gameView.GraphicsDeviceService = null;
            this._gameView.Location = new System.Drawing.Point(0, 0);
            this._gameView.Margin = new System.Windows.Forms.Padding(0);
            this._gameView.Name = "_gameView";
            this._gameView.Size = new System.Drawing.Size(984, 762);
            this._gameView.TabIndex = 0;
            this._gameView.Text = "game view";
            // 
            // GameForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(984, 762);
            this.Controls.Add(this._gameView);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.MinimumSize = new System.Drawing.Size(1000, 800);
            this.Name = "GameForm";
            this.Text = "Assault Wing";
            this.ResumeLayout(false);

        }

        #endregion

        private AW2.Core.GraphicsDeviceControl _gameView;
    }
}