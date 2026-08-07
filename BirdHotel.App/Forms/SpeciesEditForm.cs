using BirdHotel.App.Models;

namespace BirdHotel.App.Forms;

public class SpeciesEditForm : Form
{
    public Species EditedSpecies { get; }

    private TextBox _nameBox = null!;

    public SpeciesEditForm(Species species)
    {
        EditedSpecies = new Species { Id = species.Id, Name = species.Name };
        BuildUi();
        _nameBox.Text = EditedSpecies.Name;
    }

    private void BuildUi()
    {
        Text = EditedSpecies.Id == 0 ? "種類の新規登録" : "種類の編集";
        Width = 360;
        Height = 170;
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        Font = new Font("Yu Gothic UI", 10F);

        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Padding = new Padding(16) };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 60));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        layout.Controls.Add(new Label { Text = "種類名", AutoSize = true, Margin = new Padding(3, 8, 3, 3) }, 0, 0);
        _nameBox = new TextBox { Width = 220 };
        layout.Controls.Add(_nameBox, 1, 0);

        var buttonPanel = new FlowLayoutPanel { Dock = DockStyle.Bottom, FlowDirection = FlowDirection.RightToLeft, Height = 50, Padding = new Padding(10) };
        var cancelButton = new Button { Text = "キャンセル", Width = 90, Height = 32, DialogResult = DialogResult.Cancel };
        var okButton = new Button { Text = "保存", Width = 90, Height = 32 };
        okButton.Click += (_, _) => TrySave();
        buttonPanel.Controls.Add(cancelButton);
        buttonPanel.Controls.Add(okButton);
        AcceptButton = okButton;
        CancelButton = cancelButton;

        Controls.Add(layout);
        Controls.Add(buttonPanel);
    }

    private void TrySave()
    {
        if (string.IsNullOrWhiteSpace(_nameBox.Text))
        {
            MessageBox.Show("種類名を入力してください。", "入力エラー", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        EditedSpecies.Name = _nameBox.Text.Trim();
        DialogResult = DialogResult.OK;
        Close();
    }
}
