using BirdHotel.App.Models;

namespace BirdHotel.App.Forms;

public class OwnerEditForm : Form
{
    public Owner EditedOwner { get; }

    private TextBox _nameBox = null!;
    private TextBox _contactBox = null!;
    private CheckBox _isProprietorCheck = null!;
    private TextBox _notesBox = null!;

    public OwnerEditForm(Owner owner)
    {
        EditedOwner = new Owner
        {
            Id = owner.Id,
            Name = owner.Name,
            Contact = owner.Contact,
            IsProprietor = owner.IsProprietor,
            Notes = owner.Notes,
        };
        BuildUi();
        LoadFromOwner();
    }

    private void BuildUi()
    {
        Text = EditedOwner.Id == 0 ? "飼い主の新規登録" : "飼い主の編集";
        Width = 420;
        Height = 340;
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        Font = new Font("Yu Gothic UI", 10F);

        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Padding = new Padding(16) };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        layout.Controls.Add(new Label { Text = "飼い主名", AutoSize = true, Margin = new Padding(3, 8, 3, 3) }, 0, 0);
        _nameBox = new TextBox { Width = 240 };
        layout.Controls.Add(_nameBox, 1, 0);

        layout.Controls.Add(new Label { Text = "連絡先", AutoSize = true, Margin = new Padding(3, 8, 3, 3) }, 0, 1);
        _contactBox = new TextBox { Width = 240 };
        layout.Controls.Add(_contactBox, 1, 1);

        layout.Controls.Add(new Label(), 0, 2);
        _isProprietorCheck = new CheckBox { Text = "経営者本人（この飼い主の鳥は予約時に自動で「期間なし」になる）", AutoSize = true, MaximumSize = new Size(260, 0) };
        layout.Controls.Add(_isProprietorCheck, 1, 2);

        layout.Controls.Add(new Label { Text = "備考", AutoSize = true, Margin = new Padding(3, 8, 3, 3) }, 0, 3);
        _notesBox = new TextBox { Width = 240, Height = 60, Multiline = true };
        layout.Controls.Add(_notesBox, 1, 3);

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

    private void LoadFromOwner()
    {
        _nameBox.Text = EditedOwner.Name;
        _contactBox.Text = EditedOwner.Contact;
        _isProprietorCheck.Checked = EditedOwner.IsProprietor;
        _notesBox.Text = EditedOwner.Notes;
    }

    private void TrySave()
    {
        if (string.IsNullOrWhiteSpace(_nameBox.Text))
        {
            MessageBox.Show("飼い主名を入力してください。", "入力エラー", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        EditedOwner.Name = _nameBox.Text.Trim();
        EditedOwner.Contact = _contactBox.Text.Trim();
        EditedOwner.IsProprietor = _isProprietorCheck.Checked;
        EditedOwner.Notes = _notesBox.Text.Trim();

        DialogResult = DialogResult.OK;
        Close();
    }
}
