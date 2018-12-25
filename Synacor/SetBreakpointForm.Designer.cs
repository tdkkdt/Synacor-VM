namespace Synacor {
    partial class SetBreakpointForm {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing) {
            if(disposing && (components != null)) {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent() {
            this.ipTxb = new System.Windows.Forms.TextBox();
            this.executeChb = new System.Windows.Forms.CheckBox();
            this.writeCkb = new System.Windows.Forms.CheckBox();
            this.readCkb = new System.Windows.Forms.CheckBox();
            this.button1 = new System.Windows.Forms.Button();
            this.button2 = new System.Windows.Forms.Button();
            this.SuspendLayout();
            // 
            // ipTxb
            // 
            this.ipTxb.Location = new System.Drawing.Point(12, 12);
            this.ipTxb.Name = "ipTxb";
            this.ipTxb.Size = new System.Drawing.Size(216, 20);
            this.ipTxb.TabIndex = 0;
            // 
            // executeChb
            // 
            this.executeChb.AutoSize = true;
            this.executeChb.Location = new System.Drawing.Point(12, 38);
            this.executeChb.Name = "executeChb";
            this.executeChb.Size = new System.Drawing.Size(65, 17);
            this.executeChb.TabIndex = 1;
            this.executeChb.Text = "Execute";
            this.executeChb.UseVisualStyleBackColor = true;
            // 
            // writeCkb
            // 
            this.writeCkb.AutoSize = true;
            this.writeCkb.Location = new System.Drawing.Point(83, 38);
            this.writeCkb.Name = "writeCkb";
            this.writeCkb.Size = new System.Drawing.Size(51, 17);
            this.writeCkb.TabIndex = 1;
            this.writeCkb.Text = "Write";
            this.writeCkb.UseVisualStyleBackColor = true;
            // 
            // readCkb
            // 
            this.readCkb.AutoSize = true;
            this.readCkb.Location = new System.Drawing.Point(140, 38);
            this.readCkb.Name = "readCkb";
            this.readCkb.Size = new System.Drawing.Size(52, 17);
            this.readCkb.TabIndex = 2;
            this.readCkb.Text = "Read";
            this.readCkb.UseVisualStyleBackColor = true;
            // 
            // button1
            // 
            this.button1.DialogResult = System.Windows.Forms.DialogResult.OK;
            this.button1.Location = new System.Drawing.Point(72, 61);
            this.button1.Name = "button1";
            this.button1.Size = new System.Drawing.Size(75, 23);
            this.button1.TabIndex = 3;
            this.button1.Text = "OK";
            this.button1.UseVisualStyleBackColor = true;
            // 
            // button2
            // 
            this.button2.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.button2.Location = new System.Drawing.Point(153, 61);
            this.button2.Name = "button2";
            this.button2.Size = new System.Drawing.Size(75, 23);
            this.button2.TabIndex = 3;
            this.button2.Text = "Cancel";
            this.button2.UseVisualStyleBackColor = true;
            // 
            // SetBreakpointForm
            // 
            this.AcceptButton = this.button1;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.button2;
            this.ClientSize = new System.Drawing.Size(239, 90);
            this.Controls.Add(this.button2);
            this.Controls.Add(this.button1);
            this.Controls.Add(this.readCkb);
            this.Controls.Add(this.writeCkb);
            this.Controls.Add(this.executeChb);
            this.Controls.Add(this.ipTxb);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "SetBreakpointForm";
            this.Text = "SetBreakpointForm";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        internal System.Windows.Forms.TextBox ipTxb;
        internal System.Windows.Forms.CheckBox executeChb;
        internal System.Windows.Forms.CheckBox writeCkb;
        internal System.Windows.Forms.CheckBox readCkb;
        private System.Windows.Forms.Button button1;
        private System.Windows.Forms.Button button2;
    }
}