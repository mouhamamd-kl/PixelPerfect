namespace PixelPerfect
{
    partial class MainEditorForm
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
                components.Dispose();
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();

            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode       = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize          = new System.Drawing.Size(1280, 800);
            this.MinimumSize         = new System.Drawing.Size(960, 640);
            this.Name                = "MainEditorForm";
            this.Text                = "PixelPerfect";
            this.StartPosition       = System.Windows.Forms.FormStartPosition.CenterScreen;

            this.ResumeLayout(false);
        }
    }
}
