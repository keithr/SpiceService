using System.Drawing;
using System.Windows.Forms;

namespace SpiceSharp.Api.Tray;

partial class AboutDialog
{
    private System.ComponentModel.IContainer components = null;
    private Panel headerPanel;
    private TableLayoutPanel headerLayout;
    private PictureBox iconPictureBox;
    private Label titleLabel;
    private Label versionLabel;
    private Panel contentPanel;
    private Panel scrollPanel;
    private Label descLabel;
    private Label licenseLabel;
    private Panel buttonPanel;
    private Button closeButton;

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
        this.headerPanel = new Panel();
        this.headerLayout = new TableLayoutPanel();
        this.iconPictureBox = new PictureBox();
        this.titleLabel = new Label();
        this.versionLabel = new Label();
        this.contentPanel = new Panel();
        this.scrollPanel = new Panel();
        this.descLabel = new Label();
        this.licenseLabel = new Label();
        this.buttonPanel = new Panel();
        this.closeButton = new Button();
        this.headerPanel.SuspendLayout();
        this.headerLayout.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)(this.iconPictureBox)).BeginInit();
        this.contentPanel.SuspendLayout();
        this.scrollPanel.SuspendLayout();
        this.buttonPanel.SuspendLayout();
        this.SuspendLayout();
        // 
        // headerPanel
        // 
        this.headerPanel.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
        this.headerPanel.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(45)))), ((int)(((byte)(45)))), ((int)(((byte)(48)))));
        this.headerPanel.Controls.Add(this.headerLayout);
        this.headerPanel.Location = new System.Drawing.Point(0, 0);
        this.headerPanel.Name = "headerPanel";
        this.headerPanel.Padding = new System.Windows.Forms.Padding(25, 20, 25, 15);
        this.headerPanel.Size = new System.Drawing.Size(600, 130);
        this.headerPanel.TabIndex = 0;
        // 
        // headerLayout
        // 
        this.headerLayout.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
        this.headerLayout.ColumnCount = 2;
        this.headerLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 90F));
        this.headerLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
        this.headerLayout.Controls.Add(this.iconPictureBox, 0, 0);
        this.headerLayout.Controls.Add(this.titleLabel, 1, 0);
        this.headerLayout.Controls.Add(this.versionLabel, 1, 1);
        this.headerLayout.Location = new System.Drawing.Point(25, 20);
        this.headerLayout.Name = "headerLayout";
        this.headerLayout.RowCount = 2;
        this.headerLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
        this.headerLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
        this.headerLayout.Size = new System.Drawing.Size(550, 95);
        this.headerLayout.TabIndex = 0;
        // 
        // iconPictureBox
        // 
        this.iconPictureBox.Location = new System.Drawing.Point(3, 3);
        this.iconPictureBox.Name = "iconPictureBox";
        this.iconPictureBox.Size = new System.Drawing.Size(80, 80);
        this.iconPictureBox.SizeMode = System.Windows.Forms.PictureBoxSizeMode.Zoom;
        this.iconPictureBox.TabIndex = 0;
        this.iconPictureBox.TabStop = false;
        this.headerLayout.SetRowSpan(this.iconPictureBox, 2);
        // 
        // titleLabel
        // 
        this.titleLabel.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
        this.titleLabel.AutoSize = false;
        this.titleLabel.Font = new System.Drawing.Font("Segoe UI", 24F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
        this.titleLabel.ForeColor = System.Drawing.Color.White;
        this.titleLabel.Location = new System.Drawing.Point(93, 0);
        this.titleLabel.Margin = new System.Windows.Forms.Padding(15, 0, 0, 0);
        this.titleLabel.Name = "titleLabel";
        this.titleLabel.Size = new System.Drawing.Size(457, 50);
        this.titleLabel.TabIndex = 1;
        this.titleLabel.Text = "SpiceService";
        this.titleLabel.TextAlign = System.Drawing.ContentAlignment.BottomLeft;
        // 
        // versionLabel
        // 
        this.versionLabel.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
        this.versionLabel.AutoSize = false;
        this.versionLabel.Font = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
        this.versionLabel.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(200)))), ((int)(((byte)(200)))), ((int)(((byte)(200)))));
        this.versionLabel.Location = new System.Drawing.Point(93, 50);
        this.versionLabel.Margin = new System.Windows.Forms.Padding(15, 5, 0, 0);
        this.versionLabel.Name = "versionLabel";
        this.versionLabel.Size = new System.Drawing.Size(457, 23);
        this.versionLabel.TabIndex = 2;
        this.versionLabel.Text = "Version";
        this.versionLabel.TextAlign = System.Drawing.ContentAlignment.TopLeft;
        // 
        // contentPanel
        // 
        this.contentPanel.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
        this.contentPanel.BackColor = System.Drawing.Color.White;
        this.contentPanel.Controls.Add(this.scrollPanel);
        this.contentPanel.Controls.Add(this.buttonPanel);
        this.contentPanel.Location = new System.Drawing.Point(0, 130);
        this.contentPanel.Name = "contentPanel";
        this.contentPanel.Padding = new System.Windows.Forms.Padding(30, 25, 30, 20);
        this.contentPanel.Size = new System.Drawing.Size(600, 370);
        this.contentPanel.TabIndex = 1;
        // 
        // scrollPanel
        // 
        this.scrollPanel.AutoScroll = true;
        this.scrollPanel.BackColor = System.Drawing.Color.White;
        this.scrollPanel.Controls.Add(this.licenseLabel);
        this.scrollPanel.Controls.Add(this.descLabel);
        this.scrollPanel.Dock = System.Windows.Forms.DockStyle.Fill;
        this.scrollPanel.Location = new System.Drawing.Point(30, 25);
        this.scrollPanel.Name = "scrollPanel";
        this.scrollPanel.Padding = new System.Windows.Forms.Padding(0);
        this.scrollPanel.Size = new System.Drawing.Size(540, 290);
        this.scrollPanel.TabIndex = 0;
        // 
        // descLabel
        // 
        this.descLabel.AutoSize = false;
        this.descLabel.Font = new System.Drawing.Font("Segoe UI", 9.5F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
        this.descLabel.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(60)))), ((int)(((byte)(60)))), ((int)(((byte)(60)))));
        this.descLabel.Location = new System.Drawing.Point(0, 0);
        this.descLabel.Name = "descLabel";
        this.descLabel.Size = new System.Drawing.Size(540, 180);
        this.descLabel.TabIndex = 0;
        this.descLabel.Text = "Circuit Simulation API Tray Application\r\n\r\nProvides a system tray interface for the SpiceSharp Circuit Simulation API with MCP server integration.\r\n\r\nFeatures:\r\n• Auto-start on login\r\n• Circuit management and export\r\n• MCP server integration\r\n• Network visibility control\r\n• Component library search with 500+ SPICE library files";
        this.descLabel.TextAlign = System.Drawing.ContentAlignment.TopLeft;
        // 
        // licenseLabel
        // 
        this.licenseLabel.AutoSize = false;
        this.licenseLabel.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
        this.licenseLabel.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(60)))), ((int)(((byte)(60)))), ((int)(((byte)(60)))));
        this.licenseLabel.Location = new System.Drawing.Point(0, 180);
        this.licenseLabel.Name = "licenseLabel";
        this.licenseLabel.Size = new System.Drawing.Size(540, 140);
        this.licenseLabel.TabIndex = 1;
        this.licenseLabel.Text = "Copyright (c) 2025 Keith Rule\r\n\r\nThis software is free for personal, non-commercial use. Commercial use requires a commercial license.\r\n\r\nFor full license terms, see the LICENSE file.\r\n\r\nThird-Party Libraries:\r\n\r\nThis application uses SpiceSharp, a circuit simulation library provided under a liberal open-source license (MIT/BSD-style).\r\nCopyright and license information: https://github.com/SpiceSharp/SpiceSharp\r\n\r\nThis application includes SPICE component libraries from the KiCad Spice Library project as a courtesy.\r\nThese libraries are licensed under the GNU General Public License version 3 (GPL-3.0).\r\nKiCad Spice Library: https://github.com/KiCad/KiCad-Spice-Library";
        this.licenseLabel.TextAlign = System.Drawing.ContentAlignment.TopLeft;
        // 
        // buttonPanel
        // 
        this.buttonPanel.BackColor = System.Drawing.Color.White;
        this.buttonPanel.Controls.Add(this.closeButton);
        this.buttonPanel.Dock = System.Windows.Forms.DockStyle.Bottom;
        this.buttonPanel.Location = new System.Drawing.Point(30, 315);
        this.buttonPanel.Name = "buttonPanel";
        this.buttonPanel.Padding = new System.Windows.Forms.Padding(0, 15, 0, 0);
        this.buttonPanel.Size = new System.Drawing.Size(540, 55);
        this.buttonPanel.TabIndex = 1;
        // 
        // closeButton
        // 
        this.closeButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
        this.closeButton.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(0)))), ((int)(((byte)(120)))), ((int)(((byte)(215)))));
        this.closeButton.Cursor = System.Windows.Forms.Cursors.Hand;
        this.closeButton.DialogResult = System.Windows.Forms.DialogResult.OK;
        this.closeButton.FlatAppearance.BorderSize = 0;
        this.closeButton.FlatAppearance.MouseDownBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(0)))), ((int)(((byte)(90)))), ((int)(((byte)(180)))));
        this.closeButton.FlatAppearance.MouseOverBackColor = System.Drawing.Color.FromArgb(((int)(((byte)(0)))), ((int)(((byte)(102)))), ((int)(((byte)(204)))));
        this.closeButton.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
        this.closeButton.Font = new System.Drawing.Font("Segoe UI", 9.5F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
        this.closeButton.ForeColor = System.Drawing.Color.White;
        this.closeButton.Location = new System.Drawing.Point(430, 15);
        this.closeButton.Name = "closeButton";
        this.closeButton.Size = new System.Drawing.Size(110, 35);
        this.closeButton.TabIndex = 0;
        this.closeButton.Text = "Close";
        this.closeButton.UseVisualStyleBackColor = false;
        // 
        // AboutDialog
        // 
        this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
        this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
        this.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(250)))), ((int)(((byte)(250)))), ((int)(((byte)(250)))));
        this.ClientSize = new System.Drawing.Size(600, 500);
        this.Controls.Add(this.contentPanel);
        this.Controls.Add(this.headerPanel);
        this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
        this.MaximizeBox = false;
        this.MinimizeBox = false;
        this.Name = "AboutDialog";
        this.Padding = new System.Windows.Forms.Padding(0);
        this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
        this.Text = "About SpiceService";
        this.headerPanel.ResumeLayout(false);
        this.headerLayout.ResumeLayout(false);
        ((System.ComponentModel.ISupportInitialize)(this.iconPictureBox)).EndInit();
        this.contentPanel.ResumeLayout(false);
        this.scrollPanel.ResumeLayout(false);
        this.buttonPanel.ResumeLayout(false);
        this.ResumeLayout(false);
    }
}
