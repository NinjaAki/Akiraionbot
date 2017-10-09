namespace AkiraionbotForm
{
    partial class Akiraionbotapp
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
            this.chatConsole = new System.Windows.Forms.TabPage();
            this.chatDisplay = new System.Windows.Forms.RichTextBox();
            this.chatTabControl = new System.Windows.Forms.TabControl();
            this.chatConsole.SuspendLayout();
            this.chatTabControl.SuspendLayout();
            this.SuspendLayout();
            // 
            // chatConsole
            // 
            this.chatConsole.Controls.Add(this.chatDisplay);
            this.chatConsole.Location = new System.Drawing.Point(4, 22);
            this.chatConsole.Name = "chatConsole";
            this.chatConsole.Padding = new System.Windows.Forms.Padding(3);
            this.chatConsole.Size = new System.Drawing.Size(731, 454);
            this.chatConsole.TabIndex = 0;
            this.chatConsole.Text = "Chat Console";
            this.chatConsole.UseVisualStyleBackColor = true;
            // 
            // chatDisplay (728, 451)
            // 
            this.chatDisplay.BackColor = System.Drawing.Color.Black;
            this.chatDisplay.ForeColor = System.Drawing.Color.White;
            this.chatDisplay.Location = new System.Drawing.Point(3, 3);
            this.chatDisplay.Name = "chatDisplay";
            this.chatDisplay.Size = new System.Drawing.Size(730, 450);
            this.chatDisplay.TabIndex = 0;
            this.chatDisplay.Text = "";
            this.chatDisplay.TextChanged += new System.EventHandler(this.chatDisplay_TextChanged);
            // 
            // chatTabControl
            // 
            this.chatTabControl.Controls.Add(this.chatConsole);
            this.chatTabControl.Location = new System.Drawing.Point(12, 12);
            this.chatTabControl.Name = "chatTabControl";
            this.chatTabControl.SelectedIndex = 0;
            this.chatTabControl.Size = new System.Drawing.Size(740, 480);
            this.chatTabControl.TabIndex = 1;
            // 
            // Akiraionbotapp
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.Color.Gray;
            this.ClientSize = new System.Drawing.Size(765, 505);
            this.Controls.Add(this.chatTabControl);
            this.ForeColor = System.Drawing.SystemColors.ControlText;
            this.Name = "Akiraionbotapp";
            this.Text = "Form1";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.Akiraionbotapp_FormClosing);
            this.Load += new System.EventHandler(this.Akiraionbotapp_Load);
            this.chatConsole.ResumeLayout(false);
            this.chatTabControl.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        public void chatDisplay_TextChanged()
        {
            this.chatDisplay.SelectionStart = this.chatDisplay.Text.Length;
            this.chatDisplay.ScrollToCaret();
        }

        #endregion
        
        private System.Windows.Forms.TabPage chatConsole;
        private System.Windows.Forms.RichTextBox chatDisplay;
        private System.Windows.Forms.TabControl chatTabControl;
    }
}

