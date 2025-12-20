namespace SpiceSharp.Api.Tray;

partial class IDEConfigurationSuccessDialog
{
    private System.ComponentModel.IContainer components = null;
    private Label titleLabel;
    private Label messageLabel;
    private Label vsCodeLabel;
    private TextBox vsCodeTextBox;
    private Button copyButton;
    private Button okButton;
    private Panel scrollPanel;

    protected override void Dispose(bool disposing)
    {
        if (disposing && (components != null))
        {
            components.Dispose();
        }
        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        this.titleLabel = new Label();
        this.scrollPanel = new Panel();
        this.messageLabel = new Label();
        this.vsCodeLabel = new Label();
        this.vsCodeTextBox = new TextBox();
        this.copyButton = new Button();
        this.okButton = new Button();
        this.scrollPanel.SuspendLayout();
        this.SuspendLayout();
        // 
        // titleLabel
        // 
        this.titleLabel.AutoSize = true;
        this.titleLabel.Font = new System.Drawing.Font("Segoe UI", 14F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
        this.titleLabel.Location = new System.Drawing.Point(20, 20);
        this.titleLabel.Name = "titleLabel";
        this.titleLabel.Size = new System.Drawing.Size(200, 25);
        this.titleLabel.TabIndex = 0;
        this.titleLabel.Text = "Configuration Successful";
        // 
        // scrollPanel
        // 
        this.scrollPanel.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
        this.scrollPanel.AutoScroll = true;
        this.scrollPanel.Controls.Add(this.vsCodeTextBox);
        this.scrollPanel.Controls.Add(this.vsCodeLabel);
        this.scrollPanel.Controls.Add(this.messageLabel);
        this.scrollPanel.Location = new System.Drawing.Point(20, 55);
        this.scrollPanel.Name = "scrollPanel";
        this.scrollPanel.Size = new System.Drawing.Size(560, 350);
        this.scrollPanel.TabIndex = 1;
        // 
        // messageLabel
        // 
        this.messageLabel.AutoSize = false;
        this.messageLabel.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
        this.messageLabel.Location = new System.Drawing.Point(0, 0);
        this.messageLabel.Name = "messageLabel";
        this.messageLabel.Size = new System.Drawing.Size(540, 200);
        this.messageLabel.TabIndex = 0;
        this.messageLabel.Text = "Message";
        // 
        // vsCodeLabel
        // 
        this.vsCodeLabel.AutoSize = true;
        this.vsCodeLabel.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
        this.vsCodeLabel.Location = new System.Drawing.Point(0, 200);
        this.vsCodeLabel.Name = "vsCodeLabel";
        this.vsCodeLabel.Size = new System.Drawing.Size(200, 15);
        this.vsCodeLabel.TabIndex = 1;
        this.vsCodeLabel.Text = "VS Code Configuration JSON:";
        this.vsCodeLabel.Visible = false;
        // 
        // vsCodeTextBox
        // 
        this.vsCodeTextBox.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
        this.vsCodeTextBox.Font = new System.Drawing.Font("Consolas", 8.5F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
        this.vsCodeTextBox.Location = new System.Drawing.Point(0, 220);
        this.vsCodeTextBox.Multiline = true;
        this.vsCodeTextBox.Name = "vsCodeTextBox";
        this.vsCodeTextBox.ReadOnly = true;
        this.vsCodeTextBox.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
        this.vsCodeTextBox.Size = new System.Drawing.Size(540, 120);
        this.vsCodeTextBox.TabIndex = 2;
        this.vsCodeTextBox.Visible = false;
        // 
        // copyButton
        // 
        this.copyButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
        this.copyButton.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
        this.copyButton.Location = new System.Drawing.Point(425, 415);
        this.copyButton.Name = "copyButton";
        this.copyButton.Size = new System.Drawing.Size(75, 30);
        this.copyButton.TabIndex = 3;
        this.copyButton.Text = "Copy JSON";
        this.copyButton.UseVisualStyleBackColor = true;
        this.copyButton.Visible = false;
        this.copyButton.Click += new System.EventHandler(this.CopyButton_Click);
        // 
        // okButton
        // 
        this.okButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
        this.okButton.DialogResult = System.Windows.Forms.DialogResult.OK;
        this.okButton.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
        this.okButton.Location = new System.Drawing.Point(505, 415);
        this.okButton.Name = "okButton";
        this.okButton.Size = new System.Drawing.Size(75, 30);
        this.okButton.TabIndex = 4;
        this.okButton.Text = "Done";
        this.okButton.UseVisualStyleBackColor = true;
        this.okButton.Click += new System.EventHandler(this.OkButton_Click);
        // 
        // IDEConfigurationSuccessDialog
        // 
        this.AcceptButton = this.okButton;
        this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
        this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
        this.ClientSize = new System.Drawing.Size(600, 460);
        this.Controls.Add(this.okButton);
        this.Controls.Add(this.copyButton);
        this.Controls.Add(this.scrollPanel);
        this.Controls.Add(this.titleLabel);
        this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
        this.MaximizeBox = false;
        this.MinimizeBox = false;
        this.Name = "IDEConfigurationSuccessDialog";
        this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
        this.Text = "Configuration Result";
        this.scrollPanel.ResumeLayout(false);
        this.scrollPanel.PerformLayout();
        this.ResumeLayout(false);
        this.PerformLayout();
    }
}

