namespace Test
{
    partial class Form1
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose( bool disposing )
        {
            if ( disposing && ( components != null ) )
            {
                components.Dispose();
            }
            base.Dispose( disposing );
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.documentPreview1 = new MigraDoc.Rendering.Forms.DocumentPreview();
            this.button1 = new System.Windows.Forms.Button();
            this.SuspendLayout();
            // 
            // documentPreview1
            // 
            this.documentPreview1.Ddl = null;
            this.documentPreview1.DesktopColor = System.Drawing.SystemColors.ControlDark;
            this.documentPreview1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.documentPreview1.Document = null;
            this.documentPreview1.Location = new System.Drawing.Point(0, 23);
            this.documentPreview1.Name = "documentPreview1";
            this.documentPreview1.Page = 0;
            this.documentPreview1.PageColor = System.Drawing.Color.GhostWhite;
            this.documentPreview1.PageSize = new System.Drawing.Size(595, 842);
            this.documentPreview1.PrivateFonts = null;
            this.documentPreview1.Size = new System.Drawing.Size(1144, 814);
            this.documentPreview1.TabIndex = 0;
            this.documentPreview1.ZoomPercent = 70;
            // 
            // button1
            // 
            this.button1.Dock = System.Windows.Forms.DockStyle.Top;
            this.button1.Location = new System.Drawing.Point(0, 0);
            this.button1.Name = "button1";
            this.button1.Size = new System.Drawing.Size(1144, 23);
            this.button1.TabIndex = 1;
            this.button1.Text = "button1";
            this.button1.UseVisualStyleBackColor = true;
            this.button1.Click += new System.EventHandler(this.button1_Click);
            // 
            // Form1
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1144, 837);
            this.Controls.Add(this.documentPreview1);
            this.Controls.Add(this.button1);
            this.Name = "Form1";
            this.Text = "Form1";
            this.ResumeLayout(false);

        }

        #endregion

        private MigraDoc.Rendering.Forms.DocumentPreview documentPreview1;
        private System.Windows.Forms.Button button1;
    }
}

