using BirdHotel.App.Models;

namespace BirdHotel.App.Forms;

public class CageEditForm : Form
{
    public Cage Cage { get; }

    private TextBox _nameBox = null!;
    private NumericUpDown _capacityUpDown = null!;
    private TextBox _notesBox = null!;

    public CageEditForm(Cage cage)
    {
        Cage = new Cage { Id = cage.Id, Name = cage.Name, Capacity = cage.Capacity, Notes = cage.Notes };
        BuildUi();
        LoadFromCage();
    }

    private void BuildUi()
    {
        Text = Cage.Id == 0 ? "籠の新規登録" : "籠の編集";
        Width = 380;
        Height = 300;
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        Font = new Font("Yu Gothic UI", 10F);

        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Padding = new Padding(16) };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        layout.Controls.Add(new Label { Text = "籠名", AutoSize = true, Margin = new Padding(3, 8, 3, 3) }, 0, 0);
        _nameBox = new TextBox { Width = 200 };
        layout.Controls.Add(_nameBox, 1, 0);

        layout.Controls.Add(new Label { Text = "定員", AutoSize = true, Margin = new Padding(3, 8, 3, 3) }, 0, 1);
        _capacityUpDown = new NumericUpDown { Width = 80, Minimum = 1, Maximum = 20, Value = 2 };
        var capacityPanel = new FlowLayoutPanel { AutoSize = true };
        capacityPanel.Controls.Add(_capacityUpDown);
        capacityPanel.Controls.Add(new Label { Text = "羽（通常2、特別な場合のみ変更）", AutoSize = true, ForeColor = Color.Gray, Margin = new Padding(6, 8, 3, 3) });
        layout.Controls.Add(capacityPanel, 1, 1);

        layout.Controls.Add(new Label { Text = "備考", AutoSize = true, Margin = new Padding(3, 8, 3, 3) }, 0, 2);
        _notesBox = new TextBox { Width = 200, Height = 60, Multiline = true };
        layout.Controls.Add(_notesBox, 1, 2);

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

    private void LoadFromCage()
    {
        _nameBox.Text = Cage.Name;
        _capacityUpDown.Value = Math.Clamp(Cage.Capacity, _capacityUpDown.Minimum, _capacityUpDown.Maximum);
        _notesBox.Text = Cage.Notes;
    }

    private void TrySave()
    {
        if (string.IsNullOrWhiteSpace(_nameBox.Text))
        {
            MessageBox.Show("籠名を入力してください。", "入力エラー", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        Cage.Name = _nameBox.Text.Trim();
        Cage.Capacity = (int)_capacityUpDown.Value;
        Cage.Notes = _notesBox.Text.Trim();

        DialogResult = DialogResult.OK;
        Close();
    }
}
