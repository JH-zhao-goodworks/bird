using BirdHotel.App.Data;
using BirdHotel.App.Models;

namespace BirdHotel.App.Forms;

public class BirdEditForm : Form
{
    private readonly OwnerRepository _ownerRepository;
    private readonly SpeciesRepository _speciesRepository;
    private List<Owner> _owners = new();
    private List<Species> _speciesList = new();

    public Bird Bird { get; }

    private ComboBox _speciesCombo = null!;
    private TextBox _nameBox = null!;
    private CheckBox _birthDateUnknownCheck = null!;
    private DateTimePicker _birthDatePicker = null!;
    private ComboBox _sizeCombo = null!;
    private ComboBox _genderCombo = null!;
    private ComboBox _ownerCombo = null!;
    private CheckBox _isProprietorCheck = null!;
    private bool _suppressProprietorSync;
    private CheckBox _canPairCheck = null!;
    private TextBox _pairNameBox = null!;
    private TextBox _notesBox = null!;

    public BirdEditForm(Bird bird, OwnerRepository ownerRepository, SpeciesRepository speciesRepository)
    {
        _ownerRepository = ownerRepository;
        _speciesRepository = speciesRepository;

        // 編集対象を書き換えないよう複製してから編集する
        Bird = new Bird
        {
            Id = bird.Id,
            Species = bird.Species,
            Name = bird.Name,
            BirthDate = bird.BirthDate,
            Size = bird.Size,
            Gender = bird.Gender,
            OwnerId = bird.OwnerId,
            CanPair = bird.CanPair,
            PairName = bird.PairName,
            Notes = bird.Notes,
        };

        BuildUi();
        LoadOwners();
        LoadSpecies();
        LoadFromBird();
    }

    private void BuildUi()
    {
        Text = Bird.Id == 0 ? "鳥の新規登録" : "鳥の編集";
        Width = 460;
        Height = 680;
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        Font = new Font("Yu Gothic UI", 10F);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            Padding = new Padding(16),
            AutoSize = true,
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        int row = 0;

        AddLabel(layout, row, "種類");
        var speciesPanel = new FlowLayoutPanel { AutoSize = true };
        _speciesCombo = new ComboBox { Width = 190, DropDownStyle = ComboBoxStyle.DropDownList };
        var addSpeciesButton = new Button { Text = "新規種類...", Width = 100, Height = 24, Margin = new Padding(6, 0, 0, 0) };
        addSpeciesButton.Click += (_, _) => AddSpeciesInline();
        speciesPanel.Controls.Add(_speciesCombo);
        speciesPanel.Controls.Add(addSpeciesButton);
        layout.Controls.Add(speciesPanel, 1, row++);

        AddLabel(layout, row, "名前");
        _nameBox = new TextBox { Width = 250 };
        layout.Controls.Add(_nameBox, 1, row++);

        AddLabel(layout, row, "生年月日");
        var birthPanel = new FlowLayoutPanel { AutoSize = true };
        _birthDatePicker = new DateTimePicker { Width = 150, Format = DateTimePickerFormat.Short, Value = DateTime.Today };
        _birthDateUnknownCheck = new CheckBox { Text = "不明", AutoSize = true, Margin = new Padding(10, 4, 0, 0) };
        _birthDateUnknownCheck.CheckedChanged += (_, _) => _birthDatePicker.Enabled = !_birthDateUnknownCheck.Checked;
        birthPanel.Controls.Add(_birthDatePicker);
        birthPanel.Controls.Add(_birthDateUnknownCheck);
        layout.Controls.Add(birthPanel, 1, row++);

        AddLabel(layout, row, "型");
        _sizeCombo = new ComboBox { Width = 150, DropDownStyle = ComboBoxStyle.DropDownList };
        _sizeCombo.Items.AddRange([BirdSize.中小型.ToString(), BirdSize.中大型.ToString()]);
        layout.Controls.Add(_sizeCombo, 1, row++);

        AddLabel(layout, row, "性別");
        _genderCombo = new ComboBox { Width = 150, DropDownStyle = ComboBoxStyle.DropDownList };
        _genderCombo.Items.AddRange([BirdGender.オス.ToString(), BirdGender.メス.ToString(), BirdGender.不明.ToString()]);
        layout.Controls.Add(_genderCombo, 1, row++);

        AddLabel(layout, row, "飼い主");
        var ownerPanel = new FlowLayoutPanel { AutoSize = true };
        _ownerCombo = new ComboBox { Width = 150, DropDownStyle = ComboBoxStyle.DropDownList };
        _ownerCombo.SelectedIndexChanged += (_, _) => SyncProprietorCheckFromOwner();
        _isProprietorCheck = new CheckBox { Text = "経営者本人", AutoSize = true, Margin = new Padding(8, 4, 0, 0) };
        _isProprietorCheck.CheckedChanged += (_, _) => OnProprietorCheckChanged();
        var addOwnerButton = new Button { Text = "新規飼い主...", Width = 100, Height = 24, Margin = new Padding(8, 0, 0, 0) };
        addOwnerButton.Click += (_, _) => AddOwnerInline();
        ownerPanel.Controls.Add(_ownerCombo);
        ownerPanel.Controls.Add(_isProprietorCheck);
        ownerPanel.Controls.Add(addOwnerButton);
        layout.Controls.Add(ownerPanel, 1, row++);

        AddLabel(layout, row, "ペア");
        var pairPanel = new FlowLayoutPanel { AutoSize = true };
        _canPairCheck = new CheckBox { Text = "他の鳥と同じ籠に入れてよい（ペア可）", AutoSize = true, Margin = new Padding(0, 4, 8, 0) };
        _canPairCheck.CheckedChanged += (_, _) => _pairNameBox.Enabled = _canPairCheck.Checked;
        pairPanel.Controls.Add(_canPairCheck);
        layout.Controls.Add(pairPanel, 1, row++);

        AddLabel(layout, row, "ペア名");
        _pairNameBox = new TextBox { Width = 250, Enabled = false };
        var pairNamePanel = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown };
        pairNamePanel.Controls.Add(_pairNameBox);
        pairNamePanel.Controls.Add(new Label { Text = "同じ籠に入れる鳥同士に同じ名前を付けてください（例: セキセイ家族）", AutoSize = true, ForeColor = Color.Gray, MaximumSize = new Size(260, 0) });
        layout.Controls.Add(pairNamePanel, 1, row++);

        AddLabel(layout, row, "備考");
        _notesBox = new TextBox { Width = 250, Height = 60, Multiline = true };
        layout.Controls.Add(_notesBox, 1, row++);

        var buttonPanel = new FlowLayoutPanel { Dock = DockStyle.Bottom, FlowDirection = FlowDirection.RightToLeft, Height = 50, Padding = new Padding(10) };
        var cancelButton = new Button { Text = "キャンセル", Width = 90, Height = 32, DialogResult = DialogResult.Cancel };
        var okButton = new Button { Text = "保存", Width = 90, Height = 32 };
        okButton.Click += (_, _) => TrySave();
        buttonPanel.Controls.Add(cancelButton);
        buttonPanel.Controls.Add(okButton);
        AcceptButton = okButton;
        CancelButton = cancelButton;

        var scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true };
        scroll.Controls.Add(layout);

        Controls.Add(scroll);
        Controls.Add(buttonPanel);
    }

    private static void AddLabel(TableLayoutPanel layout, int row, string text)
    {
        layout.RowCount = row + 1;
        layout.Controls.Add(new Label { Text = text, AutoSize = true, TextAlign = ContentAlignment.MiddleLeft, Margin = new Padding(3, 8, 3, 3) }, 0, row);
    }

    private void LoadOwners()
    {
        _owners = _ownerRepository.GetAll();
        _ownerCombo.Items.Clear();
        foreach (var owner in _owners)
            _ownerCombo.Items.Add(owner);
    }

    private void AddOwnerInline()
    {
        using var ownerForm = new OwnerEditForm(new Owner());
        if (ownerForm.ShowDialog(this) != DialogResult.OK) return;

        var newId = _ownerRepository.Insert(ownerForm.EditedOwner);
        LoadOwners();
        var added = _owners.FirstOrDefault(o => o.Id == newId);
        if (added is not null)
            _ownerCombo.SelectedItem = added;
    }

    private void SyncProprietorCheckFromOwner()
    {
        _suppressProprietorSync = true;
        _isProprietorCheck.Checked = (_ownerCombo.SelectedItem as Owner)?.IsProprietor ?? false;
        _suppressProprietorSync = false;
    }

    // 「経営者本人」は飼い主自体の属性なので、ここで切り替えると選択中の飼い主の全ての鳥に反映される
    private void OnProprietorCheckChanged()
    {
        if (_suppressProprietorSync) return;
        if (_ownerCombo.SelectedItem is not Owner selectedOwner) return;

        selectedOwner.IsProprietor = _isProprietorCheck.Checked;
        _ownerRepository.Update(selectedOwner);

        var ownerId = selectedOwner.Id;
        LoadOwners();
        _ownerCombo.SelectedItem = _owners.FirstOrDefault(o => o.Id == ownerId);
    }

    private void LoadSpecies()
    {
        _speciesList = _speciesRepository.GetAll();
        _speciesCombo.Items.Clear();
        foreach (var species in _speciesList)
            _speciesCombo.Items.Add(species);
    }

    private void AddSpeciesInline()
    {
        using var speciesForm = new SpeciesEditForm(new Species());
        if (speciesForm.ShowDialog(this) != DialogResult.OK) return;

        var newId = _speciesRepository.Insert(speciesForm.EditedSpecies);
        LoadSpecies();
        var added = _speciesList.FirstOrDefault(s => s.Id == newId);
        if (added is not null)
            _speciesCombo.SelectedItem = added;
    }

    private void LoadFromBird()
    {
        _speciesCombo.SelectedItem = _speciesList.FirstOrDefault(s => s.Name == Bird.Species);
        _nameBox.Text = Bird.Name;
        if (Bird.BirthDate is { } birthDate)
        {
            _birthDateUnknownCheck.Checked = false;
            _birthDatePicker.Value = birthDate;
        }
        else
        {
            _birthDateUnknownCheck.Checked = true;
        }
        _sizeCombo.SelectedItem = Bird.Size.ToString();
        _genderCombo.SelectedItem = Bird.Gender.ToString();
        _ownerCombo.SelectedItem = _owners.FirstOrDefault(o => o.Id == Bird.OwnerId);
        _canPairCheck.Checked = Bird.CanPair;
        _pairNameBox.Text = Bird.PairName;
        _pairNameBox.Enabled = Bird.CanPair;
        _notesBox.Text = Bird.Notes;
    }

    private void TrySave()
    {
        if (_speciesCombo.SelectedItem is not Species selectedSpecies)
        {
            MessageBox.Show("種類を選択してください。まだ登録がない場合は「新規種類...」から登録してください。", "入力エラー", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        if (string.IsNullOrWhiteSpace(_nameBox.Text))
        {
            MessageBox.Show("名前を入力してください。", "入力エラー", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        if (_ownerCombo.SelectedItem is not Owner selectedOwner)
        {
            MessageBox.Show("飼い主を選択してください。まだ登録がない場合は「新規飼い主...」から登録してください。", "入力エラー", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        if (_canPairCheck.Checked && string.IsNullOrWhiteSpace(_pairNameBox.Text))
        {
            MessageBox.Show("ペア名を入力してください。同じ籠に入れる鳥同士に同じペア名を付けます。", "入力エラー", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        Bird.Species = selectedSpecies.Name;
        Bird.Name = _nameBox.Text.Trim();
        Bird.BirthDate = _birthDateUnknownCheck.Checked ? null : _birthDatePicker.Value.Date;
        Bird.Size = Enum.Parse<BirdSize>((string)_sizeCombo.SelectedItem!);
        Bird.Gender = Enum.Parse<BirdGender>((string)_genderCombo.SelectedItem!);
        Bird.OwnerId = selectedOwner.Id;
        Bird.CanPair = _canPairCheck.Checked;
        Bird.PairName = _canPairCheck.Checked ? _pairNameBox.Text.Trim() : "";
        Bird.Notes = _notesBox.Text.Trim();

        DialogResult = DialogResult.OK;
        Close();
    }
}
