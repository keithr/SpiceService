namespace SpiceSharp.Api.Tray;

partial class IDEConfigurationDialog
{
    private System.ComponentModel.IContainer components = null;
    private Label endpointLabel;
    private TextBox endpointTextBox;
    private Label endpointHelpLabel;
    private Label proxyLabel;
    private TextBox proxyTextBox;
    private Label proxyHelpLabel;
    private Label ideSelectionLabel;
    private CheckedListBox ideCheckedListBox;
    private Label modeLabel;
    private RadioButton appendRadioButton;
    private RadioButton overwriteRadioButton;
    private CheckBox backupCheckBox;
    private Button cancelButton;
    private Button applyButton;

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
        this.endpointLabel = new Label();
        this.endpointTextBox = new TextBox();
        this.endpointHelpLabel = new Label();
        this.proxyLabel = new Label();
        this.proxyTextBox = new TextBox();
        this.proxyHelpLabel = new Label();
        this.ideSelectionLabel = new Label();
        this.ideCheckedListBox = new CheckedListBox();
        this.modeLabel = new Label();
        this.appendRadioButton = new RadioButton();
        this.overwriteRadioButton = new RadioButton();
        this.backupCheckBox = new CheckBox();
        this.cancelButton = new Button();
        this.applyButton = new Button();
        this.SuspendLayout();
        // 
        // endpointLabel
        // 
        this.endpointLabel.AutoSize = true;
        this.endpointLabel.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
        this.endpointLabel.Location = new System.Drawing.Point(20, 20);
        this.endpointLabel.Name = "endpointLabel";
        this.endpointLabel.Size = new System.Drawing.Size(90, 15);
        this.endpointLabel.TabIndex = 0;
        this.endpointLabel.Text = "Server Endpoint:";
        // 
        // endpointTextBox
        // 
        this.endpointTextBox.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
        this.endpointTextBox.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
        this.endpointTextBox.Location = new System.Drawing.Point(20, 38);
        this.endpointTextBox.Name = "endpointTextBox";
        this.endpointTextBox.ReadOnly = true;
        this.endpointTextBox.Size = new System.Drawing.Size(560, 23);
        this.endpointTextBox.TabIndex = 1;
        // 
        // endpointHelpLabel
        // 
        this.endpointHelpLabel.AutoSize = true;
        this.endpointHelpLabel.Font = new System.Drawing.Font("Segoe UI", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
        this.endpointHelpLabel.ForeColor = System.Drawing.Color.Gray;
        this.endpointHelpLabel.Location = new System.Drawing.Point(20, 64);
        this.endpointHelpLabel.Name = "endpointHelpLabel";
        this.endpointHelpLabel.Size = new System.Drawing.Size(200, 13);
        this.endpointHelpLabel.TabIndex = 2;
        this.endpointHelpLabel.Text = "(Provided by SpiceService - read-only)";
        // 
        // proxyLabel
        // 
        this.proxyLabel.AutoSize = true;
        this.proxyLabel.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
        this.proxyLabel.Location = new System.Drawing.Point(20, 80);
        this.proxyLabel.Name = "proxyLabel";
        this.proxyLabel.Size = new System.Drawing.Size(90, 15);
        this.proxyLabel.TabIndex = 3;
        this.proxyLabel.Text = "Proxy Executable:";
        // 
        // proxyTextBox
        // 
        this.proxyTextBox.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
        this.proxyTextBox.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
        this.proxyTextBox.Location = new System.Drawing.Point(20, 98);
        this.proxyTextBox.Name = "proxyTextBox";
        this.proxyTextBox.ReadOnly = true;
        this.proxyTextBox.Size = new System.Drawing.Size(560, 23);
        this.proxyTextBox.TabIndex = 4;
        // 
        // proxyHelpLabel
        // 
        this.proxyHelpLabel.AutoSize = true;
        this.proxyHelpLabel.Font = new System.Drawing.Font("Segoe UI", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
        this.proxyHelpLabel.ForeColor = System.Drawing.Color.Gray;
        this.proxyHelpLabel.Location = new System.Drawing.Point(20, 124);
        this.proxyHelpLabel.Name = "proxyHelpLabel";
        this.proxyHelpLabel.Size = new System.Drawing.Size(200, 13);
        this.proxyHelpLabel.TabIndex = 5;
        this.proxyHelpLabel.Text = "(Bundled with SpiceService - no Node.js required)";
        // 
        // ideSelectionLabel
        // 
        this.ideSelectionLabel.AutoSize = true;
        this.ideSelectionLabel.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
        this.ideSelectionLabel.Location = new System.Drawing.Point(20, 150);
        this.ideSelectionLabel.Name = "ideSelectionLabel";
        this.ideSelectionLabel.Size = new System.Drawing.Size(140, 15);
        this.ideSelectionLabel.TabIndex = 6;
        this.ideSelectionLabel.Text = "Select IDEs to Configure:";
        // 
        // ideCheckedListBox
        // 
        this.ideCheckedListBox.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
        this.ideCheckedListBox.CheckOnClick = true;
        this.ideCheckedListBox.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
        this.ideCheckedListBox.FormattingEnabled = true;
        this.ideCheckedListBox.Location = new System.Drawing.Point(20, 168);
        this.ideCheckedListBox.Name = "ideCheckedListBox";
        this.ideCheckedListBox.Size = new System.Drawing.Size(560, 160);
        this.ideCheckedListBox.TabIndex = 7;
        this.ideCheckedListBox.ItemCheck += new System.Windows.Forms.ItemCheckEventHandler(this.IdeCheckedListBox_ItemCheck);
        // 
        // modeLabel
        // 
        this.modeLabel.AutoSize = true;
        this.modeLabel.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
        this.modeLabel.Location = new System.Drawing.Point(20, 340);
        this.modeLabel.Name = "modeLabel";
        this.modeLabel.Size = new System.Drawing.Size(110, 15);
        this.modeLabel.TabIndex = 8;
        this.modeLabel.Text = "Configuration Mode:";
        // 
        // appendRadioButton
        // 
        this.appendRadioButton.AutoSize = true;
        this.appendRadioButton.Checked = true;
        this.appendRadioButton.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
        this.appendRadioButton.Location = new System.Drawing.Point(20, 358);
        this.appendRadioButton.Name = "appendRadioButton";
        this.appendRadioButton.Size = new System.Drawing.Size(250, 19);
        this.appendRadioButton.TabIndex = 9;
        this.appendRadioButton.TabStop = true;
        this.appendRadioButton.Text = "Append to existing config (recommended)";
        this.appendRadioButton.UseVisualStyleBackColor = true;
        // 
        // overwriteRadioButton
        // 
        this.overwriteRadioButton.AutoSize = true;
        this.overwriteRadioButton.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
        this.overwriteRadioButton.Location = new System.Drawing.Point(20, 383);
        this.overwriteRadioButton.Name = "overwriteRadioButton";
        this.overwriteRadioButton.Size = new System.Drawing.Size(200, 19);
        this.overwriteRadioButton.TabIndex = 10;
        this.overwriteRadioButton.Text = "Overwrite entire config file";
        this.overwriteRadioButton.UseVisualStyleBackColor = true;
        // 
        // backupCheckBox
        // 
        this.backupCheckBox.AutoSize = true;
        this.backupCheckBox.Checked = true;
        this.backupCheckBox.CheckState = System.Windows.Forms.CheckState.Checked;
        this.backupCheckBox.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
        this.backupCheckBox.Location = new System.Drawing.Point(20, 410);
        this.backupCheckBox.Name = "backupCheckBox";
        this.backupCheckBox.Size = new System.Drawing.Size(220, 19);
        this.backupCheckBox.TabIndex = 11;
        this.backupCheckBox.Text = "Create backup before modifying files";
        this.backupCheckBox.UseVisualStyleBackColor = true;
        // 
        // cancelButton
        // 
        this.cancelButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
        this.cancelButton.DialogResult = System.Windows.Forms.DialogResult.Cancel;
        this.cancelButton.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
        this.cancelButton.Location = new System.Drawing.Point(425, 510);
        this.cancelButton.Name = "cancelButton";
        this.cancelButton.Size = new System.Drawing.Size(75, 30);
        this.cancelButton.TabIndex = 12;
        this.cancelButton.Text = "Cancel";
        this.cancelButton.UseVisualStyleBackColor = true;
        this.cancelButton.Click += new System.EventHandler(this.CancelButton_Click);
        // 
        // applyButton
        // 
        this.applyButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
        this.applyButton.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
        this.applyButton.Location = new System.Drawing.Point(505, 510);
        this.applyButton.Name = "applyButton";
        this.applyButton.Size = new System.Drawing.Size(75, 30);
        this.applyButton.TabIndex = 13;
        this.applyButton.Text = "Apply";
        this.applyButton.UseVisualStyleBackColor = true;
        this.applyButton.Click += new System.EventHandler(this.ApplyButton_Click);
        // 
        // IDEConfigurationDialog
        // 
        this.AcceptButton = this.applyButton;
        this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
        this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
        this.CancelButton = this.cancelButton;
        this.ClientSize = new System.Drawing.Size(600, 560);
        this.Controls.Add(this.applyButton);
        this.Controls.Add(this.cancelButton);
        this.Controls.Add(this.backupCheckBox);
        this.Controls.Add(this.overwriteRadioButton);
        this.Controls.Add(this.appendRadioButton);
        this.Controls.Add(this.modeLabel);
        this.Controls.Add(this.ideCheckedListBox);
        this.Controls.Add(this.ideSelectionLabel);
        this.Controls.Add(this.proxyHelpLabel);
        this.Controls.Add(this.proxyTextBox);
        this.Controls.Add(this.proxyLabel);
        this.Controls.Add(this.endpointHelpLabel);
        this.Controls.Add(this.endpointTextBox);
        this.Controls.Add(this.endpointLabel);
        this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
        this.MaximizeBox = false;
        this.MinimizeBox = false;
        this.Name = "IDEConfigurationDialog";
        this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
        this.Text = "Configure IDE Integration";
        this.ResumeLayout(false);
        this.PerformLayout();
    }
}

